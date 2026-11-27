// Upstream grammar: packages/mermaid/src/diagrams/c4/parser/c4Diagram.jison
using System.Buffers;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Parsing;

namespace SplashTheMusical.Diagrams.C4;

/// <summary>The terminals of <c>c4Diagram.jison</c>, named as the grammar names them.</summary>
internal enum C4TokenKind : byte
{
	/// <summary>jison's <c>$end</c>, which the lexer returns after <see cref="Eof"/>, or at the end of an exclusive condition.</summary>
	End,

	/// <summary>Where no lexer rule matches. No grammar rule accepts it.</summary>
	Error,

	/// <summary><c>EOF</c>, which the <c>&lt;&lt;EOF&gt;&gt;</c> rule of the initial condition returns.</summary>
	Eof,

	/// <summary><c>EOF_IN_STRUCT</c>, the end of the input right after a macro's name. No grammar rule accepts it.</summary>
	EofInStruct,
	Newline,

	/// <summary><c>direction_tb</c> and the other three. Only <c>start: direction</c> accepts one, and never before <c>$end</c>.</summary>
	Direction,

	/// <summary><c>title</c> and the rest of its line up to <c>#</c> or <c>;</c>.</summary>
	Title,

	/// <summary><c>accDescription</c> and the rest of its line up to <c>#</c> or <c>;</c>.</summary>
	AccDescription,
	AccTitle,
	AccTitleValue,
	AccDescr,
	AccDescrValue,
	AccDescrMultilineValue,

	/// <summary><c>C4_CONTEXT</c> and the other four. <see cref="C4Token.Value"/> is the <see cref="C4DiagramKind"/>.</summary>
	Header,

	/// <summary><c>PERSON</c> and the other element macros. <see cref="C4Token.Value"/> is the <see cref="C4ElementKind"/>.</summary>
	Element,

	/// <summary><c>BOUNDARY</c>, <c>NODE</c> and the others. <see cref="C4Token.Value"/> is the <see cref="C4BoundaryKind"/>.</summary>
	Boundary,

	/// <summary><c>REL</c> and the others. <see cref="C4Token.Value"/> is the <see cref="C4RelationKind"/>.</summary>
	Relation,

	/// <summary><c>UPDATE_EL_STYLE</c> and the other two. <see cref="C4Token.Value"/> is the <see cref="C4UpdateKind"/>.</summary>
	Update,
	AttributeEmpty,

	/// <summary>A positional argument, before the grammar trims it.</summary>
	Str,

	/// <summary>The name of a <c>$name="value"</c> argument, before the grammar trims it.</summary>
	StrKey,

	/// <summary>The value of a <c>$name="value"</c> argument, before the grammar trims it.</summary>
	StrValue,
	LBrace,
	RBrace,
}

/// <summary>A token: its kind and its text in the prepared source.</summary>
internal readonly record struct C4Token(C4TokenKind Kind, int Start, int Length, byte Value = 0)
{
	public ReadOnlySpan<char> Text(ReadOnlySpan<char> text) => text.Slice(Start, Length);

	/// <summary>Whether the token is one of the grammar's <c>attribute</c>: <c>STR</c>, <c>STR_KEY</c> or <c>ATTRIBUTE_EMPTY</c>.</summary>
	public bool IsAttribute => Kind is C4TokenKind.Str or C4TokenKind.StrKey or C4TokenKind.AttributeEmpty;
}

/// <summary>
/// The lexer of <c>c4Diagram.jison</c> at mermaid 12.3.0. jison tries the rules of the current start condition in the
/// order the grammar lists them, takes the first that matches, and adds <c>\b</c> after a rule that ends in a letter.
/// Each start condition is a method here that checks its rules in that order. Every condition but the first is
/// exclusive, so the initial condition's rules, its <c>&lt;&lt;EOF&gt;&gt;</c> among them, apply in no other, and the
/// end of the input in another gives <c>$end</c>.
/// </summary>
/// <remarks>
/// <para>
/// The grammar gives each macro a condition of its own, such as <c>person</c> or <c>rel_u</c>, and lists the same three
/// rules for all of them, so one condition stands for them all here:
/// <code>
/// &lt;&lt;EOF&gt;&gt;  EOF_IN_STRUCT
/// [(][ ]*[,]  begin attribute, ATTRIBUTE_EMPTY
/// [(]         begin attribute
/// [)]         popState, popState
/// </code>
/// The arguments are read in <c>attribute</c>:
/// <code>
/// [)]        popState, popState
/// ",,"       ATTRIBUTE_EMPTY
/// ","
/// [ ]*["]["] ATTRIBUTE_EMPTY
/// [ ]*["]    begin string     (string: ["] popState; [^"]* STR)
/// [ ]*[\$]   begin string_kv  (string_kv: [^=]* begin string_kv_key, STR_KEY)
///                             (string_kv_key: [=][ ]*["] popState, begin string_kv_value)
///                             (string_kv_value: [^"]+ STR_VALUE; ["] popState, popState)
/// [^,]+      STR
/// </code>
/// <c>[^,]+</c> runs over a <c>)</c> and line ends up to the next comma, so an argument that is not quoted ends only
/// at a comma.
/// </para>
/// <para>
/// Every keyword is made of word characters, so a keyword with its <c>\b</c> matches exactly when the run of word
/// characters at the cursor is the keyword. The grammar is case-sensitive.
/// </para>
/// </remarks>
internal ref struct C4Lexer
{
	private const byte Initial = 0;
	private const byte Macro = 1;
	private const byte Attribute = 2;
	private const byte StringCondition = 3;
	private const byte StringKv = 4;
	private const byte StringKvKey = 5;
	private const byte StringKvValue = 6;
	private const byte AccTitleCondition = 7;
	private const byte AccDescrCondition = 8;
	private const byte AccDescrMultiline = 9;

	/// <summary>What <c>.</c> does not match.</summary>
	private static readonly SearchValues<char> LineEnds = SearchValues.Create(['\n', '\r', (char)0x2028, (char)0x2029]);

	/// <summary>What ends <c>[^#\n;]+</c>.</summary>
	private static readonly SearchValues<char> TitleEnds = SearchValues.Create("#\n;");

	private readonly ReadOnlySpan<char> _text;
	private readonly bool _hasDirection;
	private PooledList<byte> _conditions;
	private int _at;
	private bool _done;

	// The direction rules' result for the line that ends at _directionLineEnd: the last start on the line where one of
	// them matches, or -1.
	private int _directionLineEnd;
	private int _lastDirection;

	private C4Lexer(ReadOnlySpan<char> text)
	{
		_text = text;
		_hasDirection = text.Contains("direction", StringComparison.Ordinal);
		_conditions = new PooledList<byte>(8);
		_conditions.Add(Initial);
		_directionLineEnd = -1;
	}

	/// <summary>
	/// Lexes <paramref name="text"/> into <paramref name="tokens"/>, ending with <see cref="C4TokenKind.End"/>, or ends
	/// them with an <see cref="C4TokenKind.Error"/> or <see cref="C4TokenKind.Direction"/> token and
	/// <see cref="C4TokenKind.End"/> where lexing stops. No grammar rule reads past either.
	/// </summary>
	public static void Lex(ReadOnlySpan<char> text, ref PooledList<C4Token> tokens)
	{
		var lexer = new C4Lexer(text);
		try
		{
			while (true)
			{
				var token = lexer.Next();
				tokens.Add(token);
				if (token.Kind is C4TokenKind.End or C4TokenKind.Error or C4TokenKind.Direction)
				{
					if (token.Kind != C4TokenKind.End)
					{
						tokens.Add(new C4Token(C4TokenKind.End, text.Length, 0));
					}

					return;
				}
			}
		}
		finally
		{
			lexer._conditions.Dispose();
		}
	}

	/// <summary>
	/// The keyword the run of word characters <paramref name="word"/> is, or <see cref="C4TokenKind.End"/> when it is
	/// none. <paramref name="value"/> is the kind of the header or macro.
	/// </summary>
	public static C4TokenKind Keyword(ReadOnlySpan<char> word, out byte value)
	{
		value = 0;
		return word switch
		{
			"C4Context" => Value(C4TokenKind.Header, (byte)C4DiagramKind.Context, out value),
			"C4Container" => Value(C4TokenKind.Header, (byte)C4DiagramKind.Container, out value),
			"C4Component" => Value(C4TokenKind.Header, (byte)C4DiagramKind.Component, out value),
			"C4Dynamic" => Value(C4TokenKind.Header, (byte)C4DiagramKind.Dynamic, out value),
			"C4Deployment" => Value(C4TokenKind.Header, (byte)C4DiagramKind.Deployment, out value),
			"Person_Ext" => Value(C4TokenKind.Element, (byte)C4ElementKind.PersonExt, out value),
			"Person" => Value(C4TokenKind.Element, (byte)C4ElementKind.Person, out value),
			"SystemQueue_Ext" => Value(C4TokenKind.Element, (byte)C4ElementKind.SystemQueueExt, out value),
			"SystemDb_Ext" => Value(C4TokenKind.Element, (byte)C4ElementKind.SystemDbExt, out value),
			"System_Ext" => Value(C4TokenKind.Element, (byte)C4ElementKind.SystemExt, out value),
			"SystemQueue" => Value(C4TokenKind.Element, (byte)C4ElementKind.SystemQueue, out value),
			"SystemDb" => Value(C4TokenKind.Element, (byte)C4ElementKind.SystemDb, out value),
			"System" => Value(C4TokenKind.Element, (byte)C4ElementKind.System, out value),
			"Boundary" => Value(C4TokenKind.Boundary, (byte)C4BoundaryKind.Boundary, out value),
			"Enterprise_Boundary" => Value(C4TokenKind.Boundary, (byte)C4BoundaryKind.EnterpriseBoundary, out value),
			"System_Boundary" => Value(C4TokenKind.Boundary, (byte)C4BoundaryKind.SystemBoundary, out value),
			"ContainerQueue_Ext" => Value(C4TokenKind.Element, (byte)C4ElementKind.ContainerQueueExt, out value),
			"ContainerDb_Ext" => Value(C4TokenKind.Element, (byte)C4ElementKind.ContainerDbExt, out value),
			"Container_Ext" => Value(C4TokenKind.Element, (byte)C4ElementKind.ContainerExt, out value),
			"ContainerQueue" => Value(C4TokenKind.Element, (byte)C4ElementKind.ContainerQueue, out value),
			"ContainerDb" => Value(C4TokenKind.Element, (byte)C4ElementKind.ContainerDb, out value),
			"Container" => Value(C4TokenKind.Element, (byte)C4ElementKind.Container, out value),
			"Container_Boundary" => Value(C4TokenKind.Boundary, (byte)C4BoundaryKind.ContainerBoundary, out value),
			"ComponentQueue_Ext" => Value(C4TokenKind.Element, (byte)C4ElementKind.ComponentQueueExt, out value),
			"ComponentDb_Ext" => Value(C4TokenKind.Element, (byte)C4ElementKind.ComponentDbExt, out value),
			"Component_Ext" => Value(C4TokenKind.Element, (byte)C4ElementKind.ComponentExt, out value),
			"ComponentQueue" => Value(C4TokenKind.Element, (byte)C4ElementKind.ComponentQueue, out value),
			"ComponentDb" => Value(C4TokenKind.Element, (byte)C4ElementKind.ComponentDb, out value),
			"Component" => Value(C4TokenKind.Element, (byte)C4ElementKind.Component, out value),
			"Deployment_Node" => Value(C4TokenKind.Boundary, (byte)C4BoundaryKind.DeploymentNode, out value),
			"Node" => Value(C4TokenKind.Boundary, (byte)C4BoundaryKind.Node, out value),
			"Node_L" => Value(C4TokenKind.Boundary, (byte)C4BoundaryKind.NodeL, out value),
			"Node_R" => Value(C4TokenKind.Boundary, (byte)C4BoundaryKind.NodeR, out value),
			"Rel" => Value(C4TokenKind.Relation, (byte)C4RelationKind.Rel, out value),
			"BiRel" => Value(C4TokenKind.Relation, (byte)C4RelationKind.BiRel, out value),
			"Rel_Up" => Value(C4TokenKind.Relation, (byte)C4RelationKind.RelUp, out value),
			"Rel_U" => Value(C4TokenKind.Relation, (byte)C4RelationKind.RelU, out value),
			"Rel_Down" => Value(C4TokenKind.Relation, (byte)C4RelationKind.RelDown, out value),
			"Rel_D" => Value(C4TokenKind.Relation, (byte)C4RelationKind.RelD, out value),
			"Rel_Left" => Value(C4TokenKind.Relation, (byte)C4RelationKind.RelLeft, out value),
			"Rel_L" => Value(C4TokenKind.Relation, (byte)C4RelationKind.RelL, out value),
			"Rel_Right" => Value(C4TokenKind.Relation, (byte)C4RelationKind.RelRight, out value),
			"Rel_R" => Value(C4TokenKind.Relation, (byte)C4RelationKind.RelR, out value),
			"Rel_Back" => Value(C4TokenKind.Relation, (byte)C4RelationKind.RelBack, out value),
			"RelIndex" => Value(C4TokenKind.Relation, (byte)C4RelationKind.RelIndex, out value),
			"UpdateElementStyle" => Value(C4TokenKind.Update, (byte)C4UpdateKind.ElementStyle, out value),
			"UpdateRelStyle" => Value(C4TokenKind.Update, (byte)C4UpdateKind.RelStyle, out value),
			"UpdateLayoutConfig" => Value(C4TokenKind.Update, (byte)C4UpdateKind.LayoutConfig, out value),
			_ => C4TokenKind.End,
		};
	}

	/// <summary>
	/// The start of the last match on the line of <paramref name="at"/> of one of the rules
	/// <c>.*direction\s+TB[^\n]*</c>, <c>BT</c>, <c>RL</c> and <c>LR</c>, which run before every other rule of the
	/// initial condition and match from any earlier point of the line. Their <c>\s+</c> may run into the next line.
	/// <paramref name="lineEnd"/> is the end of the line. The result is <c>-1</c> when none matches.
	/// </summary>
	public static int LastDirection(ReadOnlySpan<char> text, int at, out int lineEnd)
	{
		var lineStart = at;
		while (lineStart > 0 && !LineEnds.Contains(text[lineStart - 1]))
		{
			lineStart--;
		}

		var length = text[lineStart..].IndexOfAny(LineEnds);
		lineEnd = length < 0 ? text.Length : lineStart + length;
		var line = text[lineStart..lineEnd];
		for (var start = line.LastIndexOf("direction", StringComparison.Ordinal); start >= 0; start = start == 0 ? -1 : line[..(start + 8)].LastIndexOf("direction", StringComparison.Ordinal))
		{
			var space = lineStart + start + 9;
			var next = space;
			while (next < text.Length && PreambleParser.IsSpace(text[next]))
			{
				next++;
			}

			if (next > space && next + 2 <= text.Length && text.Slice(next, 2) is "TB" or "BT" or "RL" or "LR")
			{
				return lineStart + start;
			}
		}

		return -1;
	}

	private static C4TokenKind Value(C4TokenKind kind, byte value, out byte result)
	{
		result = value;
		return kind;
	}

	private readonly byte Condition => _conditions[_conditions.Count - 1];

	private void Push(byte condition) => _conditions.Add(condition);

	// jison's popState leaves the first condition in place.
	private void Pop()
	{
		if (_conditions.Count > 1)
		{
			_conditions.Truncate(_conditions.Count - 1);
		}
	}

	/// <summary>
	/// jison's <c>next</c> and <c>lex</c>: rules whose action returns no token are skipped. At the end of the input the
	/// rules are tried once more, so a rule that matches the empty string, such as <c>&lt;&lt;EOF&gt;&gt;</c> or
	/// <c>[^"]*</c>, fires once, and then the input is over.
	/// </summary>
	private C4Token Next()
	{
		while (true)
		{
			if (_done)
			{
				return new C4Token(C4TokenKind.End, _text.Length, 0);
			}

			if (_at >= _text.Length)
			{
				_done = true;
			}

			var token = Rule(out var matched);
			if (!matched)
			{
				return _at >= _text.Length
					? new C4Token(C4TokenKind.End, _text.Length, 0)
					: new C4Token(C4TokenKind.Error, _at, 0);
			}

			if (token.Kind is not C4TokenKind.End)
			{
				return token;
			}
		}
	}

	/// <summary>
	/// The first rule of the current condition that matches at the cursor. A rule that returns no token gives
	/// <see cref="C4TokenKind.End"/> with <paramref name="matched"/> set. No rule that returns no token matches the empty
	/// string, so each one moves the cursor.
	/// </summary>
	private C4Token Rule(out bool matched)
	{
		matched = true;
		var rest = _text[_at..];
		switch (Condition)
		{
			case Macro:
				return MacroRule(rest, out matched);
			case Attribute:
				return AttributeRule(rest, out matched);
			case StringCondition:
				if (rest.StartsWith('"'))
				{
					Pop();
					return Skip(1);
				}

				return Take(C4TokenKind.Str, IndexOrEnd(rest, '"'));
			case StringKv:
				{
					var key = Take(C4TokenKind.StrKey, IndexOrEnd(rest, '='));
					Push(StringKvKey);
					return key;
				}

			case StringKvKey:
				{
					var spaces = 1;
					while (spaces < rest.Length && rest[spaces] == ' ')
					{
						spaces++;
					}

					if (rest.StartsWith('=') && spaces < rest.Length && rest[spaces] == '"')
					{
						Pop();
						Push(StringKvValue);
						return Skip(spaces + 1);
					}

					matched = false;
					return default;
				}

			case StringKvValue:
				if (rest.StartsWith('"'))
				{
					Pop();
					Pop();
					return Skip(1);
				}

				if (!rest.IsEmpty)
				{
					return Take(C4TokenKind.StrValue, IndexOrEnd(rest, '"'));
				}

				matched = false;
				return default;
			case AccTitleCondition or AccDescrCondition:
				{
					var kind = Condition == AccTitleCondition ? C4TokenKind.AccTitleValue : C4TokenKind.AccDescrValue;
					Pop();
					return Take(kind, IndexOrEnd(rest, '\n'));
				}

			case AccDescrMultiline:
				if (rest.StartsWith('}'))
				{
					Pop();
					return Skip(1);
				}

				return Take(C4TokenKind.AccDescrMultilineValue, IndexOrEnd(rest, '}'));
			default:
				return InitialRule(rest, out matched);
		}
	}

	private C4Token InitialRule(ReadOnlySpan<char> rest, out bool matched)
	{
		matched = true;
		if (_hasDirection && DirectionAt())
		{
			return Take(C4TokenKind.Direction, 0);
		}

		if (TextRule(rest, "title") is > 0 and var title)
		{
			return Take(C4TokenKind.Title, title);
		}

		if (TextRule(rest, "accDescription") is > 0 and var description)
		{
			return Take(C4TokenKind.AccDescription, description);
		}

		if (AccRule(rest, "accTitle", ':') is > 0 and var accTitle)
		{
			Push(AccTitleCondition);
			return Take(C4TokenKind.AccTitle, accTitle);
		}

		if (AccRule(rest, "accDescr", ':') is > 0 and var accDescr)
		{
			Push(AccDescrCondition);
			return Take(C4TokenKind.AccDescr, accDescr);
		}

		if (AccRule(rest, "accDescr", '{') is > 0 and var multiline)
		{
			Push(AccDescrMultiline);
			return Skip(multiline);
		}

		if (rest.StartsWith("%%", StringComparison.Ordinal))
		{
			var end = IndexOrEnd(rest, '\n');
			while (end < rest.Length && rest[end] is '\n' or '\r')
			{
				end++;
			}

			return Skip(end);
		}

		if (!rest.IsEmpty && PreambleParser.IsSpace(rest[0]))
		{
			var spaces = 0;
			var newline = -1;
			while (spaces < rest.Length && PreambleParser.IsSpace(rest[spaces]))
			{
				if (rest[spaces] == '\n')
				{
					newline = spaces;
				}

				spaces++;
			}

			return newline >= 0 ? Take(C4TokenKind.Newline, newline + 1) : Skip(spaces);
		}

		var word = WordLength(rest);
		if (word > 0)
		{
			var kind = Keyword(rest[..word], out var value);
			if (kind == C4TokenKind.End)
			{
				matched = false;
				return default;
			}

			if (kind != C4TokenKind.Header)
			{
				Push(Macro);
			}

			var token = new C4Token(kind, _at, word, value);
			_at += word;
			return token;
		}

		if (rest.StartsWith('{'))
		{
			return Take(C4TokenKind.LBrace, 1);
		}

		if (rest.StartsWith('}'))
		{
			return Take(C4TokenKind.RBrace, 1);
		}

		if (rest.IsEmpty)
		{
			return Take(C4TokenKind.Eof, 0);
		}

		matched = false;
		return default;
	}

	private C4Token MacroRule(ReadOnlySpan<char> rest, out bool matched)
	{
		matched = true;
		if (rest.IsEmpty)
		{
			return Take(C4TokenKind.EofInStruct, 0);
		}

		if (rest[0] == '(')
		{
			Push(Attribute);
			var spaces = 1;
			while (spaces < rest.Length && rest[spaces] == ' ')
			{
				spaces++;
			}

			return spaces < rest.Length && rest[spaces] == ',' ? Take(C4TokenKind.AttributeEmpty, spaces + 1) : Skip(1);
		}

		if (rest[0] == ')')
		{
			Pop();
			Pop();
			return Skip(1);
		}

		matched = false;
		return default;
	}

	private C4Token AttributeRule(ReadOnlySpan<char> rest, out bool matched)
	{
		matched = true;
		if (rest.IsEmpty)
		{
			matched = false;
			return default;
		}

		switch (rest[0])
		{
			case ')':
				Pop();
				Pop();
				return Skip(1);
			case ',':
				return rest.StartsWith(",,", StringComparison.Ordinal) ? Take(C4TokenKind.AttributeEmpty, 2) : Skip(1);
		}

		var spaces = 0;
		while (spaces < rest.Length && rest[spaces] == ' ')
		{
			spaces++;
		}

		if (spaces < rest.Length)
		{
			if (rest[spaces..].StartsWith("\"\"", StringComparison.Ordinal))
			{
				return Take(C4TokenKind.AttributeEmpty, spaces + 2);
			}

			if (rest[spaces] == '"')
			{
				Push(StringCondition);
				return Skip(spaces + 1);
			}

			if (rest[spaces] == '$')
			{
				Push(StringKv);
				return Skip(spaces + 1);
			}
		}

		return Take(C4TokenKind.Str, IndexOrEnd(rest, ','));
	}

	/// <summary>Whether a direction rule matches at the cursor, from the cached result for its line.</summary>
	private bool DirectionAt()
	{
		if (_at > _directionLineEnd || _directionLineEnd < 0)
		{
			_lastDirection = LastDirection(_text, _at, out _directionLineEnd);
		}

		return _lastDirection >= _at;
	}

	/// <summary>
	/// The length of <c>"keyword"\s[^#\n;]+</c> at the start of <paramref name="rest"/>, or 0 when it does not match.
	/// </summary>
	private static int TextRule(ReadOnlySpan<char> rest, string keyword)
	{
		if (!rest.StartsWith(keyword, StringComparison.Ordinal) || rest.Length <= keyword.Length + 1 || !PreambleParser.IsSpace(rest[keyword.Length]))
		{
			return 0;
		}

		var text = rest[(keyword.Length + 1)..];
		var length = text.IndexOfAny(TitleEnds);
		length = length < 0 ? text.Length : length;
		return length == 0 ? 0 : keyword.Length + 1 + length;
	}

	/// <summary>The length of <c>keyword\s*"mark"\s*</c> at the start of <paramref name="rest"/>, or 0 when it does not match.</summary>
	private static int AccRule(ReadOnlySpan<char> rest, string keyword, char mark)
	{
		if (!rest.StartsWith(keyword, StringComparison.Ordinal))
		{
			return 0;
		}

		var at = keyword.Length;
		while (at < rest.Length && PreambleParser.IsSpace(rest[at]))
		{
			at++;
		}

		if (at >= rest.Length || rest[at] != mark)
		{
			return 0;
		}

		at++;
		while (at < rest.Length && PreambleParser.IsSpace(rest[at]))
		{
			at++;
		}

		return at;
	}

	private static int IndexOrEnd(ReadOnlySpan<char> rest, char c)
	{
		var at = rest.IndexOf(c);
		return at < 0 ? rest.Length : at;
	}

	private static int WordLength(ReadOnlySpan<char> text)
	{
		var length = 0;
		while (length < text.Length && FlowchartText.IsWordChar(text[length]))
		{
			length++;
		}

		return length;
	}

	private C4Token Take(C4TokenKind kind, int length)
	{
		var token = new C4Token(kind, _at, length);
		_at += length;
		return token;
	}

	private C4Token Skip(int length)
	{
		_at += length;
		return default;
	}
}
