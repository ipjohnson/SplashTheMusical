// Upstream grammar: packages/mermaid/src/diagrams/requirement/parser/requirementDiagram.jison
using System.Buffers;
using System.Runtime.CompilerServices;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Diagrams.State;
using SplashTheMusical.Parsing;

namespace SplashTheMusical.Diagrams.Requirement;

/// <summary>The terminals of <c>requirementDiagram.jison</c>, named as the grammar names them.</summary>
internal enum RequirementTokenKind : byte
{
	/// <summary>jison's <c>$end</c>, which the lexer returns after <see cref="Eof"/>, or at the end of an exclusive condition.</summary>
	End,

	/// <summary>Where no lexer rule matches. No grammar rule accepts it.</summary>
	Error,

	/// <summary><c>EOF</c>, which the <c>&lt;&lt;EOF&gt;&gt;</c> rule returns once at the end of the input.</summary>
	Eof,
	Newline,

	/// <summary><c>title</c> and the rest of its line. No grammar rule accepts it.</summary>
	Title,
	AccTitle,
	AccTitleValue,
	AccDescr,
	AccDescrValue,
	AccDescrMultilineValue,

	/// <summary><c>direction_tb</c> and the other three. <see cref="RequirementToken.Value"/> is 0 to 3 for TB, BT, RL and LR.</summary>
	Direction,

	/// <summary><c>RD</c>: <c>requirementDiagram</c>.</summary>
	Rd,
	StructStart,
	StructStop,
	StyleSeparator,
	ColonSep,
	Id,
	Text,
	Risk,
	VerifyMethod,

	/// <summary><c>REQUIREMENT</c> and the other five. <see cref="RequirementToken.Value"/> is the <see cref="RequirementKind"/>.</summary>
	RequirementType,

	/// <summary><c>LOW_RISK</c>, <c>MED_RISK</c> or <c>HIGH_RISK</c>. <see cref="RequirementToken.Value"/> is the <see cref="RequirementRisk"/>.</summary>
	RiskLevel,

	/// <summary><c>VERIFY_ANALYSIS</c> and the other three. <see cref="RequirementToken.Value"/> is the <see cref="RequirementVerifyMethod"/>.</summary>
	VerifyType,
	Element,

	/// <summary><c>CONTAINS</c> and the other six. <see cref="RequirementToken.Value"/> is the <see cref="RequirementRelationshipType"/>.</summary>
	Relationship,
	Type,
	DocRef,
	Style,
	ClassDef,
	Class,
	EndArrowL,
	EndArrowR,
	Line,

	/// <summary>The text between double quotes, without them.</summary>
	QString,

	/// <summary>An unquoted string, without the space at its end, which the rule's action trims.</summary>
	UnqString,
	Alpha,
	Comma,
	Colon,
	Semicolon,

	/// <summary><c>PERCENT</c>, which the grammar names <c>PCT</c>, so no grammar rule accepts it.</summary>
	Percent,
	Minus,
	Brkt,
}

/// <summary>A token: its kind and its text in the prepared source.</summary>
internal readonly record struct RequirementToken(RequirementTokenKind Kind, int Start, int Length, byte Value = 0)
{
	public ReadOnlySpan<char> Text(ReadOnlySpan<char> text) => text.Slice(Start, Length);

	/// <summary>Whether the token is an <c>id</c>, <c>text</c> or any other of the grammar's <c>unqString | qString</c>.</summary>
	public bool IsString => Kind is RequirementTokenKind.UnqString or RequirementTokenKind.QString;
}

/// <summary>
/// The lexer of <c>requirementDiagram.jison</c> at mermaid 12.3.0. jison tries the rules of the current start condition
/// in the order the grammar lists them, takes the first that matches, and adds <c>\b</c> after a rule that ends in a
/// letter. The grammar is <c>case-insensitive</c>. Each start condition is a method here that checks its rules in that
/// order, and the rules of <c>&lt;*&gt;</c> come last in each. Every condition but the first is exclusive, so only the
/// first has the <c>&lt;&lt;EOF&gt;&gt;</c> rule, and the end of the input in another gives <c>$end</c>.
/// </summary>
/// <remarks>
/// In the initial condition <c>[\w][^:,\r\n\{\&lt;\&gt;\-\=]*</c> matches wherever <c>\w+</c> and <c>[0-9]+</c> would,
/// so they never fire there, and in <c>style</c> <c>\w+</c> comes first, so the lexer never returns <c>NUM</c>. Every
/// keyword is made of word characters, so a keyword with its <c>\b</c> matches exactly when the run of word characters at
/// the cursor is the keyword.
/// </remarks>
internal ref struct RequirementLexer
{
	private const byte Initial = 0;
	private const byte StringCondition = 1;
	private const byte StyleCondition = 2;
	private const byte AccTitleCondition = 3;
	private const byte AccDescrCondition = 4;
	private const byte AccDescrMultiline = 5;

	/// <summary>What <c>.</c> does not match.</summary>
	private static readonly SearchValues<char> LineEnds = SearchValues.Create(['\n', '\r', (char)0x2028, (char)0x2029]);

	/// <summary>What ends <c>[\w][^:,\r\n\{\&lt;\&gt;\-\=]*</c>.</summary>
	private static readonly SearchValues<char> UnquotedEnds = SearchValues.Create(":,\r\n{<>-=");

	/// <summary>What ends <c>[^#\n;]+</c>.</summary>
	private static readonly SearchValues<char> TitleEnds = SearchValues.Create("#\n;");

	private readonly ReadOnlySpan<char> _text;
	private PooledList<byte> _conditions;
	private int _at;
	private bool _done;

	// The direction rules' results for the line that ends at _directionLineEnd: for each direction, the last start on
	// the line where it matches, or -1, and the end of that match.
	private int _directionLineEnd;
	private DirectionMatches _directions;

	private RequirementLexer(ReadOnlySpan<char> text)
	{
		_text = text;
		_conditions = new PooledList<byte>(8);
		_conditions.Add(Initial);
		_directionLineEnd = -1;
	}

	[InlineArray(8)]
	private struct DirectionMatches
	{
		private int _element;
	}

	/// <summary>
	/// Lexes <paramref name="text"/> into <paramref name="tokens"/>, ending with <see cref="RequirementTokenKind.End"/>, or
	/// ends them with an <see cref="RequirementTokenKind.Error"/> token and <see cref="RequirementTokenKind.End"/> where
	/// lexing stops.
	/// </summary>
	public static void Lex(ReadOnlySpan<char> text, ref PooledList<RequirementToken> tokens)
	{
		var lexer = new RequirementLexer(text);
		try
		{
			while (true)
			{
				var token = lexer.Next();
				tokens.Add(token);
				if (token.Kind is RequirementTokenKind.End or RequirementTokenKind.Error)
				{
					if (token.Kind != RequirementTokenKind.End)
					{
						tokens.Add(new RequirementToken(RequirementTokenKind.End, text.Length, 0));
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
	/// The keyword the run of word characters <paramref name="word"/> is, matched without case, or
	/// <see cref="RequirementTokenKind.End"/> when it is none.
	/// </summary>
	public static RequirementTokenKind Keyword(ReadOnlySpan<char> word, out byte value)
	{
		value = 0;
		switch (word.Length)
		{
			case 2 when Is(word, "id"):
				return RequirementTokenKind.Id;
			case 3 when Is(word, "low"):
				return Value(RequirementTokenKind.RiskLevel, (byte)RequirementRisk.Low, out value);
			case 4 when Is(word, "text"):
				return RequirementTokenKind.Text;
			case 4 when Is(word, "risk"):
				return RequirementTokenKind.Risk;
			case 4 when Is(word, "high"):
				return Value(RequirementTokenKind.RiskLevel, (byte)RequirementRisk.High, out value);
			case 4 when Is(word, "test"):
				return Value(RequirementTokenKind.VerifyType, (byte)RequirementVerifyMethod.Test, out value);
			case 4 when Is(word, "type"):
				return RequirementTokenKind.Type;
			case 5 when Is(word, "style"):
				return RequirementTokenKind.Style;
			case 5 when Is(word, "class"):
				return RequirementTokenKind.Class;
			case 6 when Is(word, "medium"):
				return Value(RequirementTokenKind.RiskLevel, (byte)RequirementRisk.Medium, out value);
			case 6 when Is(word, "copies"):
				return Value(RequirementTokenKind.Relationship, (byte)RequirementRelationshipType.Copies, out value);
			case 6 when Is(word, "docref"):
				return RequirementTokenKind.DocRef;
			case 6 when Is(word, "traces"):
				return Value(RequirementTokenKind.Relationship, (byte)RequirementRelationshipType.Traces, out value);
			case 7 when Is(word, "element"):
				return RequirementTokenKind.Element;
			case 7 when Is(word, "derives"):
				return Value(RequirementTokenKind.Relationship, (byte)RequirementRelationshipType.Derives, out value);
			case 7 when Is(word, "refines"):
				return Value(RequirementTokenKind.Relationship, (byte)RequirementRelationshipType.Refines, out value);
			case 8 when Is(word, "analysis"):
				return Value(RequirementTokenKind.VerifyType, (byte)RequirementVerifyMethod.Analysis, out value);
			case 8 when Is(word, "contains"):
				return Value(RequirementTokenKind.Relationship, (byte)RequirementRelationshipType.Contains, out value);
			case 8 when Is(word, "verifies"):
				return Value(RequirementTokenKind.Relationship, (byte)RequirementRelationshipType.Verifies, out value);
			case 8 when Is(word, "classDef"):
				return RequirementTokenKind.ClassDef;
			case 9 when Is(word, "satisfies"):
				return Value(RequirementTokenKind.Relationship, (byte)RequirementRelationshipType.Satisfies, out value);
			case 10 when Is(word, "inspection"):
				return Value(RequirementTokenKind.VerifyType, (byte)RequirementVerifyMethod.Inspection, out value);
			case 11 when Is(word, "requirement"):
				return Value(RequirementTokenKind.RequirementType, (byte)RequirementKind.Requirement, out value);
			case 12 when Is(word, "verifyMethod"):
				return RequirementTokenKind.VerifyMethod;
			case 13 when Is(word, "demonstration"):
				return Value(RequirementTokenKind.VerifyType, (byte)RequirementVerifyMethod.Demonstration, out value);
			case 16 when Is(word, "designConstraint"):
				return Value(RequirementTokenKind.RequirementType, (byte)RequirementKind.DesignConstraint, out value);
			case 18 when Is(word, "requirementDiagram"):
				return RequirementTokenKind.Rd;
			case 19 when Is(word, "physicalRequirement"):
				return Value(RequirementTokenKind.RequirementType, (byte)RequirementKind.PhysicalRequirement, out value);
			case 20 when Is(word, "interfaceRequirement"):
				return Value(RequirementTokenKind.RequirementType, (byte)RequirementKind.InterfaceRequirement, out value);
			case 21 when Is(word, "functionalRequirement"):
				return Value(RequirementTokenKind.RequirementType, (byte)RequirementKind.FunctionalRequirement, out value);
			case 22 when Is(word, "performanceRequirement"):
				return Value(RequirementTokenKind.RequirementType, (byte)RequirementKind.PerformanceRequirement, out value);
			default:
				return RequirementTokenKind.End;
		}
	}

	/// <summary>
	/// Whether <paramref name="text"/> lexes in the initial condition, before a line end, a space or <c>{</c>, as one
	/// <c>unqString</c> that is all of it. The rules for <c>accTitle</c> and <c>accDescr</c> need a <c>:</c> or a
	/// <c>{</c>, which such text has none of, and the writer checks the rule for <c>direction</c> on all its text.
	/// </summary>
	public static bool IsUnquoted(ReadOnlySpan<char> text) =>
		!text.IsEmpty
		&& FlowchartText.IsWordChar(text[0])
		&& !text.ContainsAny(UnquotedEnds)
		&& !text.ContainsAny(LineEnds)
		&& StateLexer.Trim(text).Length == text.Length
		&& Keyword(text[..WordLength(text)], out _) == RequirementTokenKind.End
		&& TitleRule(text) == 0;

	/// <summary>Whether <paramref name="text"/> lexes after <c>style</c>, <c>classDef</c> or <c>class</c> as one <c>ALPHA</c>.</summary>
	public static bool IsAlpha(ReadOnlySpan<char> text) => !text.IsEmpty && WordLength(text) == text.Length;

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
	/// rules are tried once more, so a rule that matches the empty string, such as <c>&lt;&lt;EOF&gt;&gt;</c>, fires once,
	/// and then the input is over.
	/// </summary>
	private RequirementToken Next()
	{
		while (true)
		{
			if (_done)
			{
				return new RequirementToken(RequirementTokenKind.End, _text.Length, 0);
			}

			if (_at >= _text.Length)
			{
				_done = true;
			}

			var before = _at;
			var condition = Condition;
			var depth = _conditions.Count;
			var token = Rule(out var matched);
			if (!matched)
			{
				return _at >= _text.Length
					? new RequirementToken(RequirementTokenKind.End, _text.Length, 0)
					: new RequirementToken(RequirementTokenKind.Error, _at, 0);
			}

			if (token.Kind is not RequirementTokenKind.End)
			{
				return token;
			}

			// A skipped empty match that leaves the condition alone would repeat for ever, as it does in jison.
			if (_at == before && Condition == condition && _conditions.Count == depth && !_done)
			{
				return new RequirementToken(RequirementTokenKind.Error, _at, 0);
			}
		}
	}

	/// <summary>
	/// The first rule of the current condition that matches at the cursor. A rule that returns no token gives
	/// <see cref="RequirementTokenKind.End"/> with <paramref name="matched"/> set.
	/// </summary>
	private RequirementToken Rule(out bool matched)
	{
		matched = true;
		var rest = _text[_at..];
		switch (Condition)
		{
			case Initial:
				return InitialRule(rest, out matched);
			case StringCondition:
				if (rest.Length > 0 && rest[0] == '"')
				{
					Pop();
					return Skip(1);
				}

				return Token(RequirementTokenKind.QString, NotAny(rest, '"'));
			case StyleCondition:
				return StyleRule(rest, out matched);
			case AccTitleCondition:
				Pop();
				return Token(RequirementTokenKind.AccTitleValue, NotAny(rest, '\n'));
			case AccDescrCondition:
				Pop();
				return Token(RequirementTokenKind.AccDescrValue, NotAny(rest, '\n'));
			case AccDescrMultiline:
				if (rest.Length > 0 && rest[0] == '}')
				{
					Pop();
					return Skip(1);
				}

				return Token(RequirementTokenKind.AccDescrMultilineValue, NotAny(rest, '}'));
		}

		matched = false;
		return default;
	}

	private RequirementToken InitialRule(ReadOnlySpan<char> rest, out bool matched)
	{
		matched = true;
		if (rest.IsEmpty)
		{
			return Token(RequirementTokenKind.Eof, 0);
		}

		if (TitleRule(rest) is > 0 and var title)
		{
			return Token(RequirementTokenKind.Title, title);
		}

		if ((rest[0] | 0x20) == 'a' && AccessibilityRule(rest, out var accessibility) is > 0 and var accessibilityLength)
		{
			return accessibility == RequirementTokenKind.End ? Skip(accessibilityLength) : Token(accessibility, accessibilityLength);
		}

		if (DirectionRule(out var direction) is > 0 and var directionLength)
		{
			return Token(RequirementTokenKind.Direction, directionLength, direction);
		}

		var c = rest[0];
		if (c == '\n')
		{
			var length = 1;
			while (length < rest.Length && rest[length] == '\n')
			{
				length++;
			}

			return Token(RequirementTokenKind.Newline, length);
		}

		if (Spaces(rest) is > 0 and var space)
		{
			return Skip(space);
		}

		if (c == '#' || (c == '%' && rest.Length > 1 && rest[1] == '%'))
		{
			return Skip(NotAny(rest, '\n'));
		}

		var word = WordLength(rest);
		if (word > 0 && Keyword(rest[..word], out var value) is not RequirementTokenKind.End and var keyword)
		{
			if (keyword is RequirementTokenKind.Style or RequirementTokenKind.ClassDef or RequirementTokenKind.Class)
			{
				Push(StyleCondition);
			}

			return Token(keyword, word, value);
		}

		switch (c)
		{
			case '{':
				return Token(RequirementTokenKind.StructStart, 1);
			case '}':
				return Token(RequirementTokenKind.StructStop, 1);
			case ':':
				return rest.StartsWith(":::") ? Token(RequirementTokenKind.StyleSeparator, 3) : Token(RequirementTokenKind.ColonSep, 1);
			case '<' when rest.Length > 1 && rest[1] == '-':
				return Token(RequirementTokenKind.EndArrowL, 2);
			case '-':
				return rest.Length > 1 && rest[1] == '>' ? Token(RequirementTokenKind.EndArrowR, 2) : Token(RequirementTokenKind.Line, 1);
			case '"':
				Push(StringCondition);
				return Skip(1);
			case ',':
				return Token(RequirementTokenKind.Comma, 1);
		}

		if (word > 0)
		{
			var end = rest[1..].IndexOfAny(UnquotedEnds);
			var length = end < 0 ? rest.Length : end + 1;
			var trimmed = StateLexer.Trim(rest[..length]).Length;
			var token = new RequirementToken(RequirementTokenKind.UnqString, _at, trimmed);
			_at += length;
			return token;
		}

		matched = false;
		return default;
	}

	/// <summary>
	/// <c>style</c>: <c>\w+</c>, <c>:</c>, <c>;</c>, <c>%</c>, <c>-</c> and <c>#</c>, a space, which is skipped, a double
	/// quote, which starts a string, and a line end, which ends the condition and returns no token. Then
	/// <c>&lt;*&gt;</c>'s <c>,</c>.
	/// </summary>
	private RequirementToken StyleRule(ReadOnlySpan<char> rest, out bool matched)
	{
		matched = true;
		if (rest.IsEmpty)
		{
			matched = false;
			return default;
		}

		if (WordLength(rest) is > 0 and var word)
		{
			return Token(RequirementTokenKind.Alpha, word);
		}

		switch (rest[0])
		{
			case ':':
				return Token(RequirementTokenKind.Colon, 1);
			case ';':
				return Token(RequirementTokenKind.Semicolon, 1);
			case '%':
				return Token(RequirementTokenKind.Percent, 1);
			case '-':
				return Token(RequirementTokenKind.Minus, 1);
			case '#':
				return Token(RequirementTokenKind.Brkt, 1);
			case ' ':
				return Skip(1);
			case '"':
				Push(StringCondition);
				return Skip(1);
			case '\n':
				Pop();
				return Skip(1);
			case ',':
				return Token(RequirementTokenKind.Comma, 1);
		}

		matched = false;
		return default;
	}

	/// <summary><c>"title"\s[^#\n;]+</c>. Returns the length, or 0.</summary>
	private static int TitleRule(ReadOnlySpan<char> rest)
	{
		if (rest.Length < 7 || !StartsWith(rest, "title") || !PreambleParser.IsSpace(rest[5]))
		{
			return 0;
		}

		var end = rest[6..].IndexOfAny(TitleEnds);
		var length = end < 0 ? rest.Length - 6 : end;
		return length > 0 ? 6 + length : 0;
	}

	/// <summary>
	/// <c>.*direction\s+TB[^\n]*</c>, then <c>BT</c>, <c>RL</c> and <c>LR</c>: a <c>direction</c> on the rest of the line,
	/// then space, which may cross lines, then the direction and the rest of its line. <c>.*</c> takes the last such
	/// <c>direction</c>. The matches of a line are found once, when the lexer first reaches it. Returns the length and
	/// which direction, 0 to 3 in that order.
	/// </summary>
	private int DirectionRule(out byte direction)
	{
		if (_at > _directionLineEnd)
		{
			FindDirections();
		}

		for (direction = 0; direction < 4; direction++)
		{
			if (_directions[direction * 2] >= _at)
			{
				return _directions[(direction * 2) + 1] - _at;
			}
		}

		direction = 0;
		return 0;
	}

	private void FindDirections()
	{
		var rest = _text[_at..];
		var lineLength = rest.IndexOfAny(LineEnds);
		lineLength = lineLength < 0 ? rest.Length : lineLength;
		_directionLineEnd = _at + lineLength;
		var line = rest[..lineLength];
		ReadOnlySpan<string> names = ["TB", "BT", "RL", "LR"];
		for (var direction = 0; direction < names.Length; direction++)
		{
			_directions[direction * 2] = -1;
			for (var at = LastIndexOfDirection(line); at >= 0; at = LastIndexOfDirection(line[..at]))
			{
				var after = at + 9;
				var space = Spaces(rest[after..]);
				if (space > 0 && StartsWith(rest[(after + space)..], names[direction]))
				{
					var end = after + space + 2;
					_directions[direction * 2] = _at + at;
					_directions[(direction * 2) + 1] = _at + end + NotAny(rest[end..], '\n');
					break;
				}
			}
		}
	}

	/// <summary>The last <c>direction</c> in <paramref name="line"/>, matched without case, as JavaScript's <c>i</c> flag does.</summary>
	private static int LastIndexOfDirection(ReadOnlySpan<char> line)
	{
		for (var at = line.Length - 9; at >= 0; at--)
		{
			if (StartsWith(line[at..], "direction"))
			{
				return at;
			}
		}

		return -1;
	}

	/// <summary>The rules <c>accTitle\s*":"\s*</c>, <c>accDescr\s*":"\s*</c> and <c>accDescr\s*"{"\s*</c>, which returns no token.</summary>
	private int AccessibilityRule(ReadOnlySpan<char> rest, out RequirementTokenKind kind)
	{
		kind = RequirementTokenKind.End;
		var isTitle = StartsWith(rest, "accTitle");
		if (!isTitle && !StartsWith(rest, "accDescr"))
		{
			return 0;
		}

		var at = 8 + Spaces(rest[8..]);
		if (at >= rest.Length)
		{
			return 0;
		}

		if (rest[at] == ':')
		{
			at++;
			at += Spaces(rest[at..]);
			kind = isTitle ? RequirementTokenKind.AccTitle : RequirementTokenKind.AccDescr;
			Push(isTitle ? AccTitleCondition : AccDescrCondition);
			return at;
		}

		if (!isTitle && rest[at] == '{')
		{
			at++;
			at += Spaces(rest[at..]);
			Push(AccDescrMultiline);
			return at;
		}

		return 0;
	}

	private RequirementToken Token(RequirementTokenKind kind, int length, byte value = 0)
	{
		var token = new RequirementToken(kind, _at, length, value);
		_at += length;
		return token;
	}

	private RequirementToken Skip(int length)
	{
		_at += length;
		return default;
	}

	private static RequirementTokenKind Value(RequirementTokenKind kind, byte value, out byte result)
	{
		result = value;
		return kind;
	}

	/// <summary>Whether the word <paramref name="word"/> is <paramref name="keyword"/> without case.</summary>
	private static bool Is(ReadOnlySpan<char> word, string keyword) => StartsWith(word, keyword);

	/// <summary>
	/// Whether <paramref name="rest"/> starts with the letters of <paramref name="text"/> in either case. JavaScript's
	/// <c>i</c> flag matches no other letter with an ASCII one.
	/// </summary>
	private static bool StartsWith(ReadOnlySpan<char> rest, string text)
	{
		if (rest.Length < text.Length)
		{
			return false;
		}

		for (var i = 0; i < text.Length; i++)
		{
			var c = rest[i];
			if (c >= '\u0080' || (c | 0x20) != (text[i] | 0x20))
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>The length of the run of JavaScript's <c>\w</c> at the start of <paramref name="rest"/>.</summary>
	private static int WordLength(ReadOnlySpan<char> rest)
	{
		var at = 0;
		while (at < rest.Length && FlowchartText.IsWordChar(rest[at]))
		{
			at++;
		}

		return at;
	}

	private static int NotAny(ReadOnlySpan<char> rest, char stop)
	{
		var at = rest.IndexOf(stop);
		return at < 0 ? rest.Length : at;
	}

	private static int Spaces(ReadOnlySpan<char> rest)
	{
		var at = 0;
		while (at < rest.Length && PreambleParser.IsSpace(rest[at]))
		{
			at++;
		}

		return at;
	}
}
