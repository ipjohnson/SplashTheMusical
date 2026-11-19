// Upstream grammar: packages/mermaid/src/diagrams/class/parser/classDiagram.jison
using SplashTheMusical.Parsing;

namespace SplashTheMusical.Diagrams.Class;

/// <summary>The terminals of <c>classDiagram.jison</c>, named as the grammar names them.</summary>
internal enum ClassTokenKind : byte
{
	/// <summary>The end of the input, jison's <c>$end</c>.</summary>
	End,

	/// <summary>Where no lexer rule matches. No grammar rule accepts it.</summary>
	Error,
	Eof,
	Newline,
	ClassDiagram,

	/// <summary><c>direction_tb</c> and the other three. <see cref="ClassToken.Direction"/> says which.</summary>
	Direction,
	AccTitle,
	AccTitleValue,
	AccDescr,
	AccDescrValue,
	AccDescrMultilineValue,
	EdgeState,
	CallbackName,
	CallbackArgs,
	Str,
	Style,
	ClassDef,
	Namespace,
	StructStart,
	StructStop,
	EofInStruct,
	Class,
	OpenInStruct,
	Member,
	CssClass,
	Callback,
	Link,
	Click,
	NoteFor,
	Note,
	AnnotationStart,
	AnnotationEnd,
	Href,
	GenericType,
	BquoteStr,
	LinkTarget,
	Extension,
	Dependency,
	Composition,
	Aggregation,
	Lollipop,
	Line,
	DottedLine,
	Label,
	StyleSeparator,
	Minus,
	Dot,
	Plus,
	Colon,
	Comma,
	Brkt,
	Pct,
	Equals,
	Alpha,
	Sqs,
	Sqe,
	Punctuation,
	UnicodeText,
	Space,
}

/// <summary>A token: its kind and its text in the prepared source, after jison's actions have cut it.</summary>
internal readonly record struct ClassToken(ClassTokenKind Kind, int Start, int Length, byte Direction = 0)
{
	public int End => Start + Length;

	public ReadOnlySpan<char> Text(ReadOnlySpan<char> text) => text.Slice(Start, Length);
}

/// <summary>
/// The lexer of <c>classDiagram.jison</c> at mermaid 12.3.0. jison tries the rules of the current start condition in the
/// order the grammar lists them, takes the first that matches, and adds <c>\b</c> after a rule that ends in a letter or
/// a digit, such as <c>"note"</c> and <c>\s*o</c>. Every start condition is exclusive, so a rule without one is an
/// <c>INITIAL</c> rule and <c>&lt;*&gt;</c> rules apply everywhere. Each start condition is a branch of
/// <see cref="Rule"/> that checks its rules in that order. The conditions <c>struct</c> and <c>href</c> are never
/// entered. Rules that no condition reaches, such as <c>&lt;*&gt;[0-9]+</c> after <c>&lt;*&gt;\w+</c>, are left out.
/// </summary>
internal ref struct ClassLexer
{
	private const byte Initial = 0;
	private const byte StringCondition = 1;
	private const byte BqString = 2;
	private const byte Generic = 3;
	private const byte CallbackNameCondition = 4;
	private const byte CallbackArgsCondition = 5;
	private const byte AccTitleCondition = 6;
	private const byte AccDescrCondition = 7;
	private const byte AccDescrMultiline = 8;
	private const byte ClassCondition = 9;
	private const byte ClassBody = 10;
	private const byte NamespaceCondition = 11;
	private const byte NamespaceBody = 12;

	private readonly ReadOnlySpan<char> _text;
	private readonly Span<int> _directions;
	private PooledList<byte> _conditions;
	private int _at;
	private bool _done;
	private int _directionLineStart;
	private int _directionLineEnd = -1;

	private ClassLexer(ReadOnlySpan<char> text, Span<int> directions)
	{
		_text = text;
		_directions = directions;
		_conditions = new PooledList<byte>(8);
		_conditions.Add(Initial);
	}

	/// <summary>
	/// Lexes <paramref name="text"/> into <paramref name="tokens"/>, ending with <see cref="ClassTokenKind.End"/>, or
	/// ends them with an <see cref="ClassTokenKind.Error"/> token where lexing stops.
	/// </summary>
	public static void Lex(ReadOnlySpan<char> text, ref PooledList<ClassToken> tokens)
	{
		var lexer = new ClassLexer(text, stackalloc int[8]);
		try
		{
			while (true)
			{
				var token = lexer.Next();
				tokens.Add(token);
				if (token.Kind is ClassTokenKind.End or ClassTokenKind.Error)
				{
					if (token.Kind != ClassTokenKind.End)
					{
						tokens.Add(new ClassToken(ClassTokenKind.End, text.Length, 0));
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
	/// Whether <paramref name="text"/>, on a line of its own, reads as the tokens of a name without a generic:
	/// <c>alphaNumToken</c>s with a <c>DOT</c> between two of them. The writer writes such a name as it is.
	/// </summary>
	public static bool ReadsAsName(string text) => ReadsAs(text, name: true);

	/// <summary>Whether <paramref name="text"/>, on a line of its own, reads as one <c>alphaNumToken</c>.</summary>
	public static bool ReadsAsAlphaNum(string text) => ReadsAs(text, name: false);

	/// <summary>Whether <paramref name="text"/>, on a line of its own, reads as one <c>ALPHA</c>.</summary>
	public static bool ReadsAsAlpha(string text) => text.Length > 0 && ReadsAs(text, name: false) && IsWordChar(text[0]);

	private static bool ReadsAs(string text, bool name)
	{
		if (text.Length == 0)
		{
			return false;
		}

		var line = text + "\n";
		var tokens = new PooledList<ClassToken>(16);
		try
		{
			Lex(line, ref tokens);
			var span = tokens.AsSpan();
			var count = span.Length - 3;
			if (count < 1 || (!name && count != 1) || span[count].Kind != ClassTokenKind.Newline || span[count].Start != text.Length)
			{
				return false;
			}

			var length = 0;
			for (var i = 0; i < count; i++)
			{
				var token = span[i];
				var isDot = token.Kind == ClassTokenKind.Dot;
				if (token.Start != length
					|| (isDot && (i == 0 || i == count - 1 || span[i - 1].Kind == ClassTokenKind.Dot))
					|| (!isDot && token.Kind is not (ClassTokenKind.Alpha or ClassTokenKind.Minus or ClassTokenKind.UnicodeText)))
				{
					return false;
				}

				length = token.End;
			}

			return length == text.Length;
		}
		finally
		{
			tokens.Dispose();
		}
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
	/// rules are tried once more, so a rule that matches the empty string, such as <c>&lt;&lt;EOF&gt;&gt;</c>, fires
	/// once, and then the input is over.
	/// </summary>
	private ClassToken Next()
	{
		while (true)
		{
			if (_done)
			{
				return new ClassToken(ClassTokenKind.End, _text.Length, 0);
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
				return _at >= _text.Length ? new ClassToken(ClassTokenKind.End, _text.Length, 0) : new ClassToken(ClassTokenKind.Error, _at, 0);
			}

			if (token.Kind is not ClassTokenKind.End)
			{
				return token;
			}

			// A skipped empty match that leaves the condition alone would repeat for ever, as it does in jison.
			if (_at == before && Condition == condition && _conditions.Count == depth && !_done)
			{
				return new ClassToken(ClassTokenKind.Error, _at, 0);
			}
		}
	}

	/// <summary>
	/// The first rule of the current condition that matches at the cursor. A rule that returns no token gives
	/// <see cref="ClassTokenKind.End"/> with <paramref name="matched"/> set.
	/// </summary>
	private ClassToken Rule(out bool matched)
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

				return Token(ClassTokenKind.Str, NotAny(rest, '"'));
			case CallbackNameCondition:
				if (rest.Length > 0 && rest[0] == '(')
				{
					var close = 1;
					while (close < rest.Length && IsSpace(rest[close]))
					{
						close++;
					}

					Pop();
					if (close < rest.Length && rest[close] == ')')
					{
						return Skip(close + 1);
					}

					Push(CallbackArgsCondition);
					return Skip(1);
				}

				return Token(ClassTokenKind.CallbackName, NotAny(rest, '('));
			case CallbackArgsCondition:
				if (rest.Length > 0 && rest[0] == ')')
				{
					Pop();
					return Skip(1);
				}

				return Token(ClassTokenKind.CallbackArgs, NotAny(rest, ')'));
			case AccTitleCondition:
				Pop();
				return Token(ClassTokenKind.AccTitleValue, NotAny(rest, '\n'));
			case AccDescrCondition:
				Pop();
				return Token(ClassTokenKind.AccDescrValue, NotAny(rest, '\n'));
			case AccDescrMultiline:
				if (rest.Length > 0 && rest[0] == '}')
				{
					Pop();
					return Skip(1);
				}

				return Token(ClassTokenKind.AccDescrMultilineValue, NotAny(rest, '}'));
			case ClassBody:
				return ClassBodyRule(rest);
			default:
				return StateRule(rest, out matched);
		}
	}

	private ClassToken InitialRule(ReadOnlySpan<char> rest, out bool matched)
	{
		matched = true;
		if (DirectionRule(out var direction, out var length))
		{
			return new ClassToken(ClassTokenKind.Direction, Advance(length), length, direction);
		}

		// \%\%(?!\{)*[^\n]*(\r?\n?)+, where the quantified look-ahead matches nothing.
		if (rest.StartsWith("%%", StringComparison.Ordinal))
		{
			var end = NotAny(rest, '\n');
			while (end < rest.Length && rest[end] is '\r' or '\n')
			{
				end++;
			}

			return Skip(end);
		}

		if (AccessibilityKeyword(rest, "accTitle", ':') is > 0 and var title)
		{
			Push(AccTitleCondition);
			return Token(ClassTokenKind.AccTitle, title);
		}

		if (AccessibilityKeyword(rest, "accDescr", ':') is > 0 and var description)
		{
			Push(AccDescrCondition);
			return Token(ClassTokenKind.AccDescr, description);
		}

		if (AccessibilityKeyword(rest, "accDescr", '{') is > 0 and var multiline)
		{
			Push(AccDescrMultiline);
			return Skip(multiline);
		}

		if (NewlineRule(rest) is > 0 and var newline)
		{
			return Token(ClassTokenKind.Newline, newline);
		}

		if (Spaces(rest) is > 0 and var spaces)
		{
			return Skip(spaces);
		}

		if (Keyword(rest, "classDiagram-v2"))
		{
			return Token(ClassTokenKind.ClassDiagram, 15);
		}

		if (Keyword(rest, "classDiagram"))
		{
			return Token(ClassTokenKind.ClassDiagram, 12);
		}

		if (rest.StartsWith("[*]", StringComparison.Ordinal))
		{
			return Token(ClassTokenKind.EdgeState, 3);
		}

		if (rest.StartsWith("call", StringComparison.Ordinal) && Spaces(rest[4..]) is > 0 and var callSpaces)
		{
			Push(CallbackNameCondition);
			return Skip(4 + callSpaces);
		}

		if (rest.Length > 0 && rest[0] == '"')
		{
			Push(StringCondition);
			return Skip(1);
		}

		if (Keyword(rest, "style"))
		{
			return Token(ClassTokenKind.Style, 5);
		}

		if (Keyword(rest, "classDef"))
		{
			return Token(ClassTokenKind.ClassDef, 8);
		}

		if (Keyword(rest, "namespace"))
		{
			Push(NamespaceCondition);
			return Token(ClassTokenKind.Namespace, 9);
		}

		if (Keyword(rest, "class"))
		{
			Push(ClassCondition);
			return Token(ClassTokenKind.Class, 5);
		}

		return CommonRule(rest, Initial, out matched);
	}

	/// <summary>The rules of <c>class</c>, <c>namespace</c>, <c>namespace-body</c>, <c>generic</c> and <c>bqstring</c>.</summary>
	private ClassToken StateRule(ReadOnlySpan<char> rest, out bool matched)
	{
		matched = true;
		var condition = Condition;
		if (rest.Length > 0 && rest[0] == '"')
		{
			Push(StringCondition);
			return Skip(1);
		}

		switch (condition)
		{
			case NamespaceCondition:
				if (Keyword(rest, "namespace"))
				{
					Push(NamespaceCondition);
					return Token(ClassTokenKind.Namespace, 9);
				}

				if (NewlineRule(rest) is > 0 and var newline)
				{
					Pop();
					return Token(ClassTokenKind.Newline, newline);
				}

				if (Spaces(rest) is > 0 and var spaces)
				{
					return Skip(spaces);
				}

				if (rest.Length > 0 && rest[0] == '{')
				{
					Push(NamespaceBody);
					return Token(ClassTokenKind.StructStart, 1);
				}

				if (rest.Length > 0 && rest[0] == '}')
				{
					// popState and less(0): the brace is read again in the condition around the namespace.
					Pop();
					return Skip(0);
				}

				break;
			case NamespaceBody:
				if (Keyword(rest, "namespace"))
				{
					Push(NamespaceCondition);
					return Token(ClassTokenKind.Namespace, 9);
				}

				if (rest.Length > 0 && rest[0] == '}')
				{
					Pop();
					return Token(ClassTokenKind.StructStop, 1);
				}

				if (rest.Length == 0)
				{
					return Token(ClassTokenKind.EofInStruct, 0);
				}

				if (NewlineRule(rest) is > 0 and var bodyNewline)
				{
					return Token(ClassTokenKind.Newline, bodyNewline);
				}

				if (Spaces(rest) is > 0 and var bodySpaces)
				{
					return Skip(bodySpaces);
				}

				if (rest.StartsWith("[*]", StringComparison.Ordinal))
				{
					return Token(ClassTokenKind.EdgeState, 3);
				}

				if (Keyword(rest, "class"))
				{
					Push(ClassCondition);
					return Token(ClassTokenKind.Class, 5);
				}

				break;
			case ClassCondition:
				if (NewlineRule(rest) is > 0 and var classNewline)
				{
					Pop();
					return Token(ClassTokenKind.Newline, classNewline);
				}

				if (Spaces(rest) is > 0 and var classSpaces)
				{
					return Skip(classSpaces);
				}

				if (rest.Length > 0 && rest[0] == '}')
				{
					Pop();
					Pop();
					return Token(ClassTokenKind.StructStop, 1);
				}

				if (rest.Length > 0 && rest[0] == '{')
				{
					Push(ClassBody);
					return Token(ClassTokenKind.StructStart, 1);
				}

				break;
		}

		return CommonRule(rest, condition, out matched);
	}

	private ClassToken ClassBodyRule(ReadOnlySpan<char> rest)
	{
		if (rest.Length == 0)
		{
			return Token(ClassTokenKind.EofInStruct, 0);
		}

		switch (rest[0])
		{
			case '"':
				Push(StringCondition);
				return Skip(1);
			case '}':
				Pop();
				return Token(ClassTokenKind.StructStop, 1);
			case '{':
				return Token(ClassTokenKind.OpenInStruct, 1);
			case '\n':
				return Skip(1);
		}

		if (rest.StartsWith("[*]", StringComparison.Ordinal))
		{
			return Token(ClassTokenKind.EdgeState, 3);
		}

		var length = rest.IndexOfAny('{', '}', '\n');
		return Token(ClassTokenKind.Member, length < 0 ? rest.Length : length);
	}

	/// <summary>The <c>&lt;*&gt;</c> rules from <c>"cssClass"</c> on, with the <c>INITIAL</c> rules among them.</summary>
	private ClassToken CommonRule(ReadOnlySpan<char> rest, byte condition, out bool matched)
	{
		matched = true;
		if (Keyword(rest, "cssClass"))
		{
			return Token(ClassTokenKind.CssClass, 8);
		}

		if (Keyword(rest, "callback"))
		{
			return Token(ClassTokenKind.Callback, 8);
		}

		if (Keyword(rest, "link"))
		{
			return Token(ClassTokenKind.Link, 4);
		}

		if (Keyword(rest, "click"))
		{
			return Token(ClassTokenKind.Click, 5);
		}

		if (Keyword(rest, "note for"))
		{
			return Token(ClassTokenKind.NoteFor, 8);
		}

		if (Keyword(rest, "note"))
		{
			return Token(ClassTokenKind.Note, 4);
		}

		if (rest.StartsWith("<<", StringComparison.Ordinal))
		{
			return Token(ClassTokenKind.AnnotationStart, 2);
		}

		if (rest.StartsWith(">>", StringComparison.Ordinal))
		{
			return Token(ClassTokenKind.AnnotationEnd, 2);
		}

		if (Keyword(rest, "href"))
		{
			return Token(ClassTokenKind.Href, 4);
		}

		if (condition == Generic)
		{
			if (rest.Length > 0 && rest[0] == '~')
			{
				Pop();
				return Skip(1);
			}

			return Token(ClassTokenKind.GenericType, NotAny(rest, '~'));
		}

		if (rest.Length > 0 && rest[0] == '~')
		{
			Push(Generic);
			return Skip(1);
		}

		if (condition == BqString)
		{
			if (rest.Length > 0 && rest[0] == '`')
			{
				Pop();
				return Skip(1);
			}

			if (NotAny(rest, '`') is > 0 and var quoted)
			{
				return Token(ClassTokenKind.BquoteStr, quoted);
			}
		}

		if (rest.Length > 0 && rest[0] == '`')
		{
			Push(BqString);
			return Skip(1);
		}

		if (Keyword(rest, "_self") || Keyword(rest, "_blank") || Keyword(rest, "_parent") || Keyword(rest, "_top"))
		{
			return Token(ClassTokenKind.LinkTarget, WordLength(rest));
		}

		// The relation rules start with \s*, but every condition that reaches them has skipped the space already.
		if (rest.Length == 0)
		{
			return Token(ClassTokenKind.Eof, 0);
		}

		var next = rest.Length > 1 ? rest[1] : '\0';
		switch (rest[0])
		{
			case '<' when next == '|':
				return Token(ClassTokenKind.Extension, 2);
			case '|' when next == '>':
				return Token(ClassTokenKind.Extension, 2);
			case '>':
				return Token(ClassTokenKind.Dependency, 1);
			case '<':
				return Token(ClassTokenKind.Dependency, 1);
			case '*':
				return Token(ClassTokenKind.Composition, 1);
			case 'o' when rest.Length == 1 || !IsWordChar(next):
				return Token(ClassTokenKind.Aggregation, 1);
			case '(' when next == ')':
				return Token(ClassTokenKind.Lollipop, 2);
			case '-' when next == '-':
				return Token(ClassTokenKind.Line, 2);
			case '.' when next == '.':
				return Token(ClassTokenKind.DottedLine, 2);
			case ':':
				{
					var label = 1;
					while (label < rest.Length && rest[label] is not (':' or '\n' or ';'))
					{
						label++;
					}

					if (label > 1)
					{
						return Token(ClassTokenKind.Label, label);
					}

					if (rest.StartsWith(":::", StringComparison.Ordinal))
					{
						return Token(ClassTokenKind.StyleSeparator, 3);
					}

					if (condition == Initial)
					{
						return Token(ClassTokenKind.Colon, 1);
					}

					break;
				}

			case '-':
				return Token(ClassTokenKind.Minus, 1);
			case '.':
				return Token(ClassTokenKind.Dot, 1);
			case '+':
				return Token(ClassTokenKind.Plus, 1);
			case ',' when condition == Initial:
				return Token(ClassTokenKind.Comma, 1);
			case '#' when condition == Initial:
				return Token(ClassTokenKind.Brkt, 1);
			case '%':
				return Token(ClassTokenKind.Pct, 1);
			case '=':
				return Token(ClassTokenKind.Equals, 1);
			case '[':
				return Token(ClassTokenKind.Sqs, 1);
			case ']':
				return Token(ClassTokenKind.Sqe, 1);
		}

		if (WordLength(rest) is > 0 and var word)
		{
			return Token(ClassTokenKind.Alpha, word);
		}

		switch (rest[0])
		{
			case '!' or '"' or '#' or '$' or '%' or '&' or '\'' or '*' or '+' or ',' or '-' or '.' or '`' or '?' or '\\' or '/':
				return Token(ClassTokenKind.Punctuation, 1);
		}

		if (IsUnicodeText(rest[0]))
		{
			return Token(ClassTokenKind.UnicodeText, 1);
		}

		if (IsSpace(rest[0]))
		{
			return Token(ClassTokenKind.Space, 1);
		}

		matched = false;
		return default;
	}

	/// <summary>
	/// <c>.*direction\s+TB[^\n]*</c> and the rules for <c>BT</c>, <c>RL</c> and <c>LR</c>, in that order. <c>.*</c> is
	/// greedy, so the last <c>direction</c> of the line that a direction follows wins, and the match runs to the end
	/// of the line the direction is on. The rules are tried at every token of a line, so the last match of each rule
	/// in the line is found once, and a rule matches at the cursor when its last match starts at or after it.
	/// </summary>
	private bool DirectionRule(out byte direction, out int length)
	{
		if (_at < _directionLineStart || _at > _directionLineEnd)
		{
			FindDirections();
		}

		for (direction = 0; direction < 4; direction++)
		{
			if (_directions[direction * 2] >= _at)
			{
				length = _directions[(direction * 2) + 1] - _at;
				return true;
			}
		}

		length = 0;
		return false;
	}

	/// <summary>The last start and the end of each direction rule's match in the line from the cursor on.</summary>
	private void FindDirections()
	{
		var lineEnd = _at;
		while (lineEnd < _text.Length && !IsLineTerminator(_text[lineEnd]))
		{
			lineEnd++;
		}

		_directionLineStart = _at;
		_directionLineEnd = lineEnd;
		for (var direction = 0; direction < 4; direction++)
		{
			var name = direction switch
			{
				0 => "TB",
				1 => "BT",
				2 => "RL",
				_ => "LR",
			};
			_directions[direction * 2] = -1;
			var search = _text[_at..lineEnd];
			int at;
			while ((at = search.LastIndexOf("direction", StringComparison.Ordinal)) >= 0)
			{
				var after = _at + at + 9;
				var spaces = Spaces(_text[after..]);
				if (spaces > 0 && _text[(after + spaces)..].StartsWith(name, StringComparison.Ordinal))
				{
					var end = after + spaces + 2;
					_directions[direction * 2] = _at + at;
					_directions[(direction * 2) + 1] = end + NotAny(_text[end..], '\n');
					break;
				}

				search = search[..at];
			}
		}
	}

	/// <summary><c>keyword\s*":"\s*</c> or <c>keyword\s*"{"\s*</c>: the length of the match, or 0.</summary>
	private static int AccessibilityKeyword(ReadOnlySpan<char> rest, string keyword, char mark)
	{
		if (!rest.StartsWith(keyword, StringComparison.Ordinal))
		{
			return 0;
		}

		var at = keyword.Length + Spaces(rest[keyword.Length..]);
		if (at >= rest.Length || rest[at] != mark)
		{
			return 0;
		}

		at++;
		return at + Spaces(rest[at..]);
	}

	/// <summary><c>\s*(\r?\n)+</c>: the space up to its last line feed, or 0 when the space holds none.</summary>
	private static int NewlineRule(ReadOnlySpan<char> rest)
	{
		var spaces = Spaces(rest);
		var lastFeed = rest[..spaces].LastIndexOf('\n');
		return lastFeed < 0 ? 0 : lastFeed + 1;
	}

	/// <summary>A keyword followed by jison's <c>\b</c>: the next character is not a word character.</summary>
	private static bool Keyword(ReadOnlySpan<char> rest, string keyword) =>
		rest.StartsWith(keyword, StringComparison.Ordinal) && (rest.Length == keyword.Length || !IsWordChar(rest[keyword.Length]));

	private static int Spaces(ReadOnlySpan<char> text)
	{
		var at = 0;
		while (at < text.Length && IsSpace(text[at]))
		{
			at++;
		}

		return at;
	}

	private static int WordLength(ReadOnlySpan<char> text)
	{
		var at = 0;
		while (at < text.Length && IsWordChar(text[at]))
		{
			at++;
		}

		return at;
	}

	private static int NotAny(ReadOnlySpan<char> text, char value)
	{
		var at = text.IndexOf(value);
		return at < 0 ? text.Length : at;
	}

	private int Advance(int length)
	{
		var start = _at;
		_at += length;
		return start;
	}

	private ClassToken Token(ClassTokenKind kind, int length) => new(kind, Advance(length), length);

	private ClassToken Skip(int length)
	{
		_at += length;
		return new ClassToken(ClassTokenKind.End, _at, 0);
	}

	/// <summary>JavaScript's <c>.</c> does not match these.</summary>
	private static bool IsLineTerminator(char c) => c is '\n' or '\r' or (char)0x2028 or (char)0x2029;

	/// <summary>JavaScript's <c>\s</c>.</summary>
	internal static bool IsSpace(char c) => PreambleParser.IsSpace(c);

	/// <summary>JavaScript's <c>\w</c>.</summary>
	internal static bool IsWordChar(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';

	/// <summary>The grammar's <c>UNICODE_TEXT</c> class, a letter outside ASCII.</summary>
	internal static bool IsUnicodeText(char c)
	{
		var low = 0;
		var high = (UnicodeRanges.Length / 2) - 1;
		while (low <= high)
		{
			var middle = (low + high) / 2;
			if (c < UnicodeRanges[middle * 2])
			{
				high = middle - 1;
			}
			else if (c > UnicodeRanges[(middle * 2) + 1])
			{
				low = middle + 1;
			}
			else
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>JavaScript's <c>trim</c>, which removes the characters of <c>\s</c>.</summary>
	internal static ReadOnlySpan<char> Trim(ReadOnlySpan<char> text)
	{
		var start = 0;
		while (start < text.Length && IsSpace(text[start]))
		{
			start++;
		}

		var end = text.Length;
		while (end > start && IsSpace(text[end - 1]))
		{
			end--;
		}

		return text[start..end];
	}

	/// <summary>The first and last character of each range of <c>UNICODE_TEXT</c>, in order.</summary>
	private const string UnicodeRanges =
		"ªªµµººÀÖØöøˁˆˑˠˤ" +
		"ˬˬˮˮͰʹͶͶͷͷͺͽΆΆΈΊ" +
		"ΌΌΎΡΣϵϷҁҊԧԱՖՙՙաև" +
		"אתװײؠيٮٮٯٯٱۓەەۥۥ" +
		"ۦۦۮۮۯۯۺۼۿۿܐܐܒܯݍޥ" +
		"ޱޱߊߪߴߴߵߵߺߺࠀࠕࠚࠚࠤࠤ" +
		"ࠨࠨࡀࡘࢠࢠࢢࢬऄहऽऽॐॐक़ॡ" +
		"ॱॷॹॿঅঌএএঐঐওনপরলল" +
		"শহঽঽৎৎড়ড়ঢ়ঢ়য়ৡৰৰৱৱ" +
		"ਅਊਏਏਐਐਓਨਪਰਲਲਲ਼ਲ਼ਵਵ" +
		"ਸ਼ਸ਼ਸਸਹਹਖ਼ੜਫ਼ਫ਼ੲੴઅઍએઑ" +
		"ઓનપરલલળળવહઽઽૐૐૠૠ" +
		"ૡૡଅଌଏଏଐଐଓନପରଲଲଳଳ" +
		"ଵହଽଽଡ଼ଡ଼ଢ଼ଢ଼ୟୡୱୱஃஃஅஊ" +
		"எஐஒகஙஙசசஜஜஞஞடடணண" +
		"ததநபமஹௐௐఅఌఎఐఒనపళ" +
		"వహఽఽౘౘౙౙౠౠౡౡಅಌಎಐ" +
		"ಒನಪಳವಹಽಽೞೞೠೠೡೡೱೱ" +
		"ೲೲഅഌഎഐഒഺഽഽൎൎൠൠൡൡ" +
		"ൺൿඅඖකනඳරලලවෆกะาา" +
		"ำำเๆກກຂຂຄຄງງຈຈຊຊ" +
		"ຍຍດທນຟມຣລລວວສສຫຫ" +
		"ອະາາຳຳຽຽເໄໆໆໜໟༀༀ" +
		"ཀཇཉཬྈྌကဪဿဿၐၕၚၝၡၡ" +
		"ၥၥၦၦၮၰၵႁႎႎႠჅჇჇჍჍ" +
		"აჺჼቈቊቍቐቖቘቘቚቝበኈኊኍ" +
		"ነኰኲኵኸኾዀዀዂዅወዖዘጐጒጕ" +
		"ጘፚᎀᎏᎠᏴᐁᙬᙯᙿᚁᚚᚠᛪᜀᜌ" +
		"ᜎᜑᜠᜱᝀᝑᝠᝬᝮᝰកឳៗៗៜៜ" +
		"ᠠᡷᢀᢨᢪᢪᢰᣵᤀᤜᥐᥭᥰᥴᦀᦫ" +
		"ᧁᧇᨀᨖᨠᩔᪧᪧᬅᬳᭅᭋᮃᮠᮮᮮ" +
		"ᮯᮯᮺᯥᰀᰣᱍᱏᱚᱽᳩᳬᳮᳱᳵᳵ" +
		"ᳶᳶᴀᶿḀἕἘἝἠὅὈὍὐὗὙὙ" +
		"ὛὛὝὝὟώᾀᾴᾶᾼιιῂῄῆῌ" +
		"ῐΐῖΊῠῬῲῴῶῼⁱⁱⁿⁿₐₜ" +
		"ℂℂℇℇℊℓℕℕℙℝℤℤΩΩℨℨ" +
		"KℭℯℹℼℿⅅⅉⅎⅎↃↃↄↄⰀⰮ" +
		"ⰰⱞⱠⳤⳫⳮⳲⳲⳳⳳⴀⴥⴧⴧⴭⴭ" +
		"ⴰⵧⵯⵯⶀⶖⶠⶦⶨⶮⶰⶶⶸⶾⷀⷆ" +
		"ⷈⷎⷐⷖⷘⷞⸯⸯ々々〆〆〱〵〻〻" +
		"〼〼ぁゖゝゟァヺーヿㄅㄭㄱㆎㆠㆺ" +
		"ㇰㇿ㐀䶵一鿌ꀀꒌꓐꓽꔀꘌꘐꘟꘪꘪ" +
		"ꘫꘫꙀꙮꙿꚗꚠꛥꜗꜟꜢꞈꞋꞎꞐꞓ" +
		"ꞠꞪꟸꠁꠃꠅꠇꠊꠌꠢꡀꡳꢂꢳꣲꣷ" +
		"ꣻꣻꤊꤥꤰꥆꥠꥼꦄꦲꧏꧏꨀꨨꩀꩂ" +
		"ꩄꩋꩠꩶꩺꩺꪀꪯꪱꪱꪵꪵꪶꪶꪹꪽ" +
		"ꫀꫀꫂꫂꫛꫝꫠꫪꫲꫴꬁꬆꬉꬎꬑꬖ" +
		"ꬠꬦꬨꬮꯀꯢ가힣ힰퟆퟋퟻ豈舘並龎" +
		"ﬀﬆﬓﬗיִיִײַﬨשׁזּטּלּמּמּנּנּ" +
		"סּסּףּףּפּפּצּﮱﯓﴽﵐﶏﶒﷇﷰﷻ" +
		"ﹰﹴﹶﻼＡＺａｚｦﾾￂￇￊￏￒￗ" +
		"ￚￜ";
}
