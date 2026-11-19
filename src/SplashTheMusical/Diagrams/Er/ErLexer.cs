// Upstream grammar: packages/mermaid/src/diagrams/er/parser/erDiagram.jison
using System.Buffers;
using System.Runtime.CompilerServices;
using SplashTheMusical.Parsing;

namespace SplashTheMusical.Diagrams.Er;

/// <summary>The terminals of <c>erDiagram.jison</c>, named as the grammar names them.</summary>
internal enum ErTokenKind : byte
{
	/// <summary>jison's <c>$end</c>, which the lexer returns after <see cref="Eof"/>, or at the end of an exclusive condition.</summary>
	End,

	/// <summary>Where no lexer rule matches. No grammar rule accepts it.</summary>
	Error,

	/// <summary><c>EOF</c>, which the <c>&lt;&lt;EOF&gt;&gt;</c> rule returns once at the end of the input.</summary>
	Eof,
	Newline,
	EntityName,
	Word,
	ErDiagram,
	BlockStart,
	BlockStop,
	Brkt,
	Comma,
	StyleSeparator,
	Colon,
	Sqs,
	Sqe,
	Style,
	ClassDef,
	Class,
	Subgraph,

	/// <summary><c>END</c>: <c>end</c> and the space after it.</summary>
	EndKeyword,

	/// <summary>A cardinality. <see cref="ErToken.Value"/> is its <see cref="ErCardinality"/>.</summary>
	Cardinality,

	/// <summary><c>IDENTIFYING</c> or <c>NON_IDENTIFYING</c>. <see cref="ErToken.Value"/> is 1 for identifying.</summary>
	Identification,
	DecimalNum,
	EntityOne,
	Num,
	UnicodeText,
	AttributeKey,
	AttributeWord,
	Comment,
	StyleText,
	Semi,

	/// <summary>The one character of the <c>.</c> rule, which the grammar names as itself, such as <c>'?'</c>.</summary>
	Char,
	AccTitle,
	AccTitleValue,
	AccDescr,
	AccDescrValue,
	AccDescrMultilineValue,

	/// <summary><c>direction_tb</c> and the other three. <see cref="ErToken.Value"/> is 0 to 3 for TB, BT, RL and LR.</summary>
	Direction,
}

/// <summary>A token: its kind and its text in the prepared source.</summary>
internal readonly record struct ErToken(ErTokenKind Kind, int Start, int Length, byte Value = 0)
{
	public ReadOnlySpan<char> Text(ReadOnlySpan<char> text) => text.Slice(Start, Length);

	public bool IsEntityName => Kind is ErTokenKind.EntityName or ErTokenKind.UnicodeText or ErTokenKind.Num or ErTokenKind.DecimalNum or ErTokenKind.EntityOne;
}

/// <summary>
/// The lexer of <c>erDiagram.jison</c> at mermaid 12.3.0. jison tries the rules of the current start condition in the
/// order the grammar lists them, takes the first that matches, and adds <c>\b</c> after a rule that ends in a letter or
/// digit. The grammar is <c>case-insensitive</c>. Each start condition is a method here that checks its rules in that
/// order. Every condition but the first is exclusive, so only the first has the <c>&lt;&lt;EOF&gt;&gt;</c> rule, and the
/// end of the input in another gives <c>$end</c>.
/// </summary>
internal ref struct ErLexer
{
	private const byte Initial = 0;
	private const byte Block = 1;
	private const byte BlockBackquote = 2;
	private const byte AccTitleCondition = 3;
	private const byte AccDescrCondition = 4;
	private const byte AccDescrMultiline = 5;
	private const byte StyleCondition = 6;

	private static readonly SearchValues<char> LineEnds = SearchValues.Create(['\n', '\r', (char)0x2028, (char)0x2029]);

	private readonly ReadOnlySpan<char> _text;
	private PooledList<byte> _conditions;
	private int _at;
	private bool _done;

	// The direction rules' results for the line that ends at _directionLineEnd: for each direction, the last start on
	// the line where it matches, or -1, and the end of that match.
	private int _directionLineEnd;
	private DirectionMatches _directions;

	private ErLexer(ReadOnlySpan<char> text)
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
	/// Lexes <paramref name="text"/> into <paramref name="tokens"/>, ending with <see cref="ErTokenKind.End"/>, or ends
	/// them with an <see cref="ErTokenKind.Error"/> token and <see cref="ErTokenKind.End"/> where lexing stops.
	/// </summary>
	public static void Lex(ReadOnlySpan<char> text, ref PooledList<ErToken> tokens)
	{
		var lexer = new ErLexer(text);
		try
		{
			while (true)
			{
				var token = lexer.Next();
				tokens.Add(token);
				if (token.Kind is ErTokenKind.End or ErTokenKind.Error)
				{
					if (token.Kind != ErTokenKind.End)
					{
						tokens.Add(new ErToken(ErTokenKind.End, text.Length, 0));
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
	/// Whether <paramref name="text"/> lexes, at the start of a line, as one token of an <c>entityName</c> that covers it
	/// and that a space ends. The writer writes such a name without quotes.
	/// </summary>
	public static bool IsBareEntityName(ReadOnlySpan<char> text)
	{
		if (text.IsEmpty || !IsUnicodeTextChar(text[0]) || char.IsAsciiDigit(text[0]))
		{
			return false;
		}

		foreach (var c in text)
		{
			if (!IsUnicodeTextChar(c))
			{
				return false;
			}
		}

		var lexer = new ErLexer(text);
		try
		{
			var token = lexer.InitialRule(text, out var matched);
			return matched && token.Kind == ErTokenKind.UnicodeText && token.Length == text.Length;
		}
		finally
		{
			lexer._conditions.Dispose();
		}
	}

	/// <summary>Whether <paramref name="text"/> lexes inside an entity's braces as one <c>ATTRIBUTE_WORD</c> of rule 37.</summary>
	public static bool IsBareAttributeWord(ReadOnlySpan<char> text)
	{
		if (text.IsEmpty || !IsAttributeWordStart(text[0]) || text.Contains('~'))
		{
			return false;
		}

		foreach (var c in text[1..])
		{
			if (!IsAttributeWordChar(c))
			{
				return false;
			}
		}

		return KeyLength(text, 0) == 0;
	}

	/// <summary>Whether <paramref name="text"/>, alone on a line inside an entity's braces, lexes as one <c>ATTRIBUTE_WORD</c> of the <c>~</c> rule.</summary>
	public static bool IsTildeWord(ReadOnlySpan<char> text) =>
		!text.ContainsAny(LineEnds) && KeyLength(text, 0) == 0 && TildeWord(text) == text.Length;

	/// <summary>Whether the <c>~</c> rule matches at the start of <paramref name="text"/>, before the rule for a backquote.</summary>
	public static bool StartsTildeWord(ReadOnlySpan<char> text) => TildeWord(text) > 0;

	/// <summary>Whether <paramref name="text"/> between double quotes lexes as one <c>ENTITY_NAME</c>.</summary>
	public static bool IsQuotedEntityName(ReadOnlySpan<char> text) => !text.IsEmpty && !text.ContainsAny(NotInEntityName);

	/// <summary>Whether <paramref name="text"/> lexes after <c>style</c> or <c>classDef</c> as one <c>STYLE_TEXT</c>.</summary>
	public static bool IsStyleText(ReadOnlySpan<char> text)
	{
		foreach (var c in text)
		{
			if (!IsStyleTextChar(c))
			{
				return false;
			}
		}

		return !text.IsEmpty;
	}

	/// <summary>Whether <paramref name="text"/> lexes after <c>style</c> or <c>classDef</c> as the components of one style.</summary>
	public static bool IsStyle(ReadOnlySpan<char> text)
	{
		foreach (var c in text)
		{
			if (!IsStyleTextChar(c) && c is not (':' or '#'))
			{
				return false;
			}
		}

		return !text.IsEmpty;
	}

	/// <summary>Whether <paramref name="text"/> lexes inside an entity's braces as one <c>ATTRIBUTE_KEY</c>.</summary>
	public static bool IsAttributeKey(ReadOnlySpan<char> text) => text.Length == 2 && KeyLength(text, 0) == 2;

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
	private ErToken Next()
	{
		while (true)
		{
			if (_done)
			{
				return new ErToken(ErTokenKind.End, _text.Length, 0);
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
				return _at >= _text.Length ? new ErToken(ErTokenKind.End, _text.Length, 0) : new ErToken(ErTokenKind.Error, _at, 0);
			}

			if (token.Kind is not ErTokenKind.End)
			{
				return token;
			}

			// A skipped empty match that leaves the condition alone would repeat for ever, as it does in jison.
			if (_at == before && Condition == condition && _conditions.Count == depth && !_done)
			{
				return new ErToken(ErTokenKind.Error, _at, 0);
			}
		}
	}

	/// <summary>
	/// The first rule of the current condition that matches at the cursor. A rule that returns no token gives
	/// <see cref="ErTokenKind.End"/> with <paramref name="matched"/> set.
	/// </summary>
	private ErToken Rule(out bool matched)
	{
		matched = true;
		var rest = _text[_at..];
		switch (Condition)
		{
			case Initial:
				return InitialRule(rest, out matched);
			case Block:
				return BlockRule(rest, out matched);
			case BlockBackquote:
				if (rest.Length > 0 && rest[0] == '`')
				{
					Pop();
					return Skip(1);
				}

				if (NotAny(rest, '`') is > 0 and var quoted)
				{
					return Token(ErTokenKind.AttributeWord, quoted);
				}

				break;
			case AccTitleCondition:
				Pop();
				return Token(ErTokenKind.AccTitleValue, NotAny(rest, '\n'));
			case AccDescrCondition:
				Pop();
				return Token(ErTokenKind.AccDescrValue, NotAny(rest, '\n'));
			case AccDescrMultiline:
				if (rest.Length > 0 && rest[0] == '}')
				{
					Pop();
					return Skip(1);
				}

				return Token(ErTokenKind.AccDescrMultilineValue, NotAny(rest, '}'));
			case StyleCondition:
				return StyleRule(rest, out matched);
		}

		matched = false;
		return default;
	}

	private ErToken InitialRule(ReadOnlySpan<char> rest, out bool matched)
	{
		matched = true;
		if (rest.IsEmpty)
		{
			return Token(ErTokenKind.Eof, 0);
		}

		if ((rest[0] | 0x20) == 'a' && AccessibilityRule(rest, out var accessibility) is > 0 and var accessibilityLength)
		{
			return accessibility == ErTokenKind.End ? Skip(accessibilityLength) : Token(accessibility, accessibilityLength);
		}

		if (DirectionRule(rest, out var direction) is > 0 and var directionLength)
		{
			return Token(ErTokenKind.Direction, directionLength, direction);
		}

		var c = rest[0];
		switch (c)
		{
			case ' ' or '\t' or '\r':
				{
					var length = 1;
					while (length < rest.Length && rest[length] is ' ' or '\t' or '\r')
					{
						length++;
					}

					return Skip(length);
				}

			case '\n':
				{
					var length = 1;
					while (length < rest.Length && rest[length] == '\n')
					{
						length++;
					}

					return Token(ErTokenKind.Newline, length);
				}

			case '"':
				{
					var close = rest[1..].IndexOf('"');
					if (close < 0)
					{
						break;
					}

					var inner = rest.Slice(1, close);
					return Token(inner.Length > 0 && !inner.ContainsAny(NotInEntityName) ? ErTokenKind.EntityName : ErTokenKind.Word, close + 2);
				}

			case '{':
				Push(Block);
				return Token(ErTokenKind.BlockStart, 1);
			case '#':
				return Token(ErTokenKind.Brkt, 1);
			case ',':
				return Token(ErTokenKind.Comma, 1);
			case ':':
				return rest.StartsWith(":::") ? Token(ErTokenKind.StyleSeparator, 3) : Token(ErTokenKind.Colon, 1);
			case '[':
				return Token(ErTokenKind.Sqs, 1);
			case ']':
				return Token(ErTokenKind.Sqe, 1);
		}

		if (Keyword(rest, "erDiagram"))
		{
			return Token(ErTokenKind.ErDiagram, 9);
		}

		if (Keyword(rest, "style"))
		{
			Push(StyleCondition);
			return Token(ErTokenKind.Style, 5);
		}

		if (Keyword(rest, "classDef"))
		{
			Push(StyleCondition);
			return Token(ErTokenKind.ClassDef, 8);
		}

		if (Keyword(rest, "class"))
		{
			return Token(ErTokenKind.Class, 5);
		}

		if (Keyword(rest, "subgraph"))
		{
			return Token(ErTokenKind.Subgraph, 8);
		}

		if (Keyword(rest, "end"))
		{
			return Token(ErTokenKind.EndKeyword, 3 + Spaces(rest[3..]));
		}

		if (CardinalityRule(rest) is { Kind: not ErTokenKind.End } token)
		{
			return token;
		}

		var text = UnicodeText(rest);
		return text > 0 ? Token(ErTokenKind.UnicodeText, text) : Token(ErTokenKind.Char, 1);
	}

	/// <summary>
	/// The rules from <c>"one or zero"</c> to <c>\-\.</c>, in the grammar's order. Returns a token with
	/// <see cref="ErTokenKind.End"/> when none matches.
	/// </summary>
	private ErToken CardinalityRule(ReadOnlySpan<char> rest)
	{
		var c = rest[0];
		var next = rest.Length > 1 ? rest[1] : '\0';
		switch (c | 0x20)
		{
			case 'o':
				if (Keyword(rest, "one or zero"))
				{
					return Cardinality(ErCardinality.ZeroOrOne, 11);
				}

				if (Keyword(rest, "one or more") || Keyword(rest, "one or many"))
				{
					return Cardinality(ErCardinality.OneOrMore, 11);
				}

				break;
			case '1' when next == '+':
				return Cardinality(ErCardinality.OneOrMore, 2);
			case '|' when Keyword(rest, "|o"):
				return Cardinality(ErCardinality.ZeroOrOne, 2);
			case 'z':
				if (Keyword(rest, "zero or one"))
				{
					return Cardinality(ErCardinality.ZeroOrOne, 11);
				}

				if (Keyword(rest, "zero or more") || Keyword(rest, "zero or many"))
				{
					return Cardinality(ErCardinality.ZeroOrMore, 12);
				}

				break;
			case '0' when next == '+':
				return Cardinality(ErCardinality.ZeroOrMore, 2);
			case '}' when Keyword(rest, "}o"):
				return Cardinality(ErCardinality.ZeroOrMore, 2);
		}

		if ((c | 0x20) == 'm')
		{
			if (StartsWith(rest, "many(0)"))
			{
				return Cardinality(ErCardinality.ZeroOrMore, 7);
			}

			if (StartsWith(rest, "many(1)"))
			{
				return Cardinality(ErCardinality.OneOrMore, 7);
			}

			if (Keyword(rest, "many"))
			{
				return Cardinality(ErCardinality.ZeroOrMore, 4);
			}
		}

		if (c == '}' && next == '|')
		{
			return Cardinality(ErCardinality.OneOrMore, 2);
		}

		if ((c | 0x20) == 'o')
		{
			if (Keyword(rest, "one"))
			{
				return Cardinality(ErCardinality.OnlyOne, 3);
			}

			if (Keyword(rest, "only one"))
			{
				return Cardinality(ErCardinality.OnlyOne, 8);
			}
		}

		if (char.IsAsciiDigit(c))
		{
			var digits = Digits(rest);
			if (digits < rest.Length && rest[digits] == '.' && Digits(rest[(digits + 1)..]) is > 0 and var fraction)
			{
				return Token(ErTokenKind.DecimalNum, digits + 1 + fraction);
			}

			if (c == '1')
			{
				var after = rest[1..];
				var space = Spaces(after);
				if (space > 0 && space < after.Length && (char.IsAsciiLetter(after[space]) || after[space] is '_' or '"' or '\'' || char.IsAsciiDigit(after[space])))
				{
					return Cardinality(ErCardinality.OnlyOne, 1);
				}

				if (after.StartsWith("--") || after.StartsWith("..") || after.StartsWith(".-") || after.StartsWith("-."))
				{
					return Cardinality(ErCardinality.OnlyOne, 1);
				}

				if (after.IsEmpty || !IsWordChar(after[0]))
				{
					return Token(ErTokenKind.EntityOne, 1);
				}
			}

			return Token(ErTokenKind.Num, digits);
		}

		switch (c)
		{
			case '|' when next == '|':
				return Cardinality(ErCardinality.OnlyOne, 2);
			case 'o' or 'O' when next == '|':
				return Cardinality(ErCardinality.ZeroOrOne, 2);
			case 'o' or 'O' when next == '{':
				return Cardinality(ErCardinality.ZeroOrMore, 2);
			case '|' when next == '{':
				return Cardinality(ErCardinality.OneOrMore, 2);
			case 'u' or 'U' when next is '.' or '-' or '|':
				return Cardinality(ErCardinality.MdParent, 1);
			case '.' when next == '.':
				return Identification(false, 2);
			case '-' when next == '-':
				return Identification(true, 2);
		}

		if (Keyword(rest, "to"))
		{
			return Identification(true, 2);
		}

		if (Keyword(rest, "optionally to"))
		{
			return Identification(false, 13);
		}

		if ((c == '.' && next == '-') || (c == '-' && next == '.'))
		{
			return Identification(false, 2);
		}

		return default;
	}

	private ErToken BlockRule(ReadOnlySpan<char> rest, out bool matched)
	{
		matched = true;
		if (rest.IsEmpty)
		{
			matched = false;
			return default;
		}

		if (Spaces(rest) is > 0 and var space)
		{
			return Skip(space);
		}

		if (KeyLength(_text, _at) is > 0 and var key)
		{
			return Token(ErTokenKind.AttributeKey, key);
		}

		if (TildeWord(rest) is > 0 and var tilde)
		{
			return Token(ErTokenKind.AttributeWord, tilde);
		}

		if (IsAttributeWordStart(rest[0]))
		{
			var length = 1;
			while (length < rest.Length && IsAttributeWordChar(rest[length]))
			{
				length++;
			}

			return Token(ErTokenKind.AttributeWord, length);
		}

		switch (rest[0])
		{
			case '`':
				Push(BlockBackquote);
				return Skip(1);
			case '"':
				{
					var close = rest[1..].IndexOf('"');
					if (close >= 0)
					{
						return Token(ErTokenKind.Comment, close + 2);
					}

					break;
				}

			case '}':
				Pop();
				return Token(ErTokenKind.BlockStop, 1);
		}

		return Token(ErTokenKind.Char, 1);
	}

	private ErToken StyleRule(ReadOnlySpan<char> rest, out bool matched)
	{
		matched = true;
		if (rest.IsEmpty)
		{
			matched = false;
			return default;
		}

		var c = rest[0];
		if (c == '\n')
		{
			var length = 1;
			while (length < rest.Length && rest[length] == '\n')
			{
				length++;
			}

			Pop();
			return Token(ErTokenKind.Newline, length);
		}

		if (Spaces(rest) is > 0 and var space)
		{
			return Skip(space);
		}

		switch (c)
		{
			case ':':
				return Token(ErTokenKind.Colon, 1);
			case ',':
				return Token(ErTokenKind.Comma, 1);
			case '#':
				return Token(ErTokenKind.Brkt, 1);
		}

		var text = 0;
		while (text < rest.Length && IsStyleTextChar(rest[text]))
		{
			text++;
		}

		if (text > 0)
		{
			return Token(ErTokenKind.StyleText, text);
		}

		if (c == ';')
		{
			return Token(ErTokenKind.Semi, 1);
		}

		matched = false;
		return default;
	}

	/// <summary>
	/// <c>.*direction\s+TB[^\n]*</c>, then <c>BT</c>, <c>RL</c> and <c>LR</c>: a <c>direction</c> on the rest of the line,
	/// then space, which may cross lines, then the direction and the rest of its line. <c>.*</c> takes the last such
	/// <c>direction</c>. The matches of a line are found once, when the lexer first reaches it. Returns the length and
	/// which direction, 0 to 3 in that order.
	/// </summary>
	private int DirectionRule(ReadOnlySpan<char> rest, out byte direction)
	{
		direction = 0;
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
			for (var at = line.LastIndexOf("direction", StringComparison.OrdinalIgnoreCase); at >= 0; at = line[..at].LastIndexOf("direction", StringComparison.OrdinalIgnoreCase))
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

	/// <summary>The rules <c>accTitle\s*":"\s*</c>, <c>accDescr\s*":"\s*</c> and <c>accDescr\s*"{"\s*</c>, which returns no token.</summary>
	private int AccessibilityRule(ReadOnlySpan<char> rest, out ErTokenKind kind)
	{
		kind = ErTokenKind.End;
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
			kind = isTitle ? ErTokenKind.AccTitle : ErTokenKind.AccDescr;
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

	/// <summary><c>\b((?:PK)|(?:FK)|(?:UK))\b</c> at <paramref name="at"/> of <paramref name="text"/>.</summary>
	private static int KeyLength(ReadOnlySpan<char> text, int at)
	{
		if (at > 0 && IsWordChar(text[at - 1]))
		{
			return 0;
		}

		var rest = text[at..];
		if (rest.Length < 2 || (rest[1] | 0x20) != 'k' || (rest[0] | 0x20) is not ('p' or 'f' or 'u'))
		{
			return 0;
		}

		return rest.Length == 2 || !IsWordChar(rest[2]) ? 2 : 0;
	}

	/// <summary>
	/// <c>([^\s]*)[~].*[~]([^\s]*)</c>: a <c>~</c> in the run without space at the start, the last <c>~</c> of the line
	/// after it, and the run without space after that.
	/// </summary>
	private static int TildeWord(ReadOnlySpan<char> rest)
	{
		var lineLength = rest.IndexOfAny(LineEnds);
		var line = lineLength < 0 ? rest : rest[..lineLength];
		var last = line.LastIndexOf('~');
		if (last <= 0)
		{
			return 0;
		}

		var word = 0;
		while (word < rest.Length && !PreambleParser.IsSpace(rest[word]))
		{
			word++;
		}

		if (rest[..Math.Min(word, last)].IndexOf('~') < 0)
		{
			return 0;
		}

		var end = last + 1;
		while (end < rest.Length && !PreambleParser.IsSpace(rest[end]))
		{
			end++;
		}

		return end;
	}

	private ErToken Cardinality(ErCardinality cardinality, int length) => Token(ErTokenKind.Cardinality, length, (byte)cardinality);

	private ErToken Identification(bool identifying, int length) => Token(ErTokenKind.Identification, length, identifying ? (byte)1 : (byte)0);

	private ErToken Token(ErTokenKind kind, int length, byte value = 0)
	{
		var token = new ErToken(kind, _at, length, value);
		_at += length;
		return token;
	}

	private ErToken Skip(int length)
	{
		_at += length;
		return default;
	}

	/// <summary><c>"[^"%\r\n\v\b\\]+"</c>: inside a class, <c>\b</c> is a backspace.</summary>
	private static readonly SearchValues<char> NotInEntityName = SearchValues.Create("\"%\r\n\v\b\\");

	/// <summary>A literal and, as jison adds after a rule that ends in a letter, a word boundary.</summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool Keyword(ReadOnlySpan<char> rest, string keyword) =>
		StartsWith(rest, keyword) && (rest.Length == keyword.Length || !IsWordChar(rest[keyword.Length]));

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool StartsWith(ReadOnlySpan<char> rest, string text) =>
		rest.Length >= text.Length
		&& (rest[0] >= '\u0080' || (rest[0] | 0x20) == (text[0] | 0x20))
		&& rest.StartsWith(text, StringComparison.OrdinalIgnoreCase);

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

	private static int Digits(ReadOnlySpan<char> rest)
	{
		var at = 0;
		while (at < rest.Length && char.IsAsciiDigit(rest[at]))
		{
			at++;
		}

		return at;
	}

	private static int UnicodeText(ReadOnlySpan<char> rest)
	{
		var at = 0;
		while (at < rest.Length && IsUnicodeTextChar(rest[at]))
		{
			at++;
		}

		return at;
	}

	/// <summary>JavaScript's <c>\w</c>.</summary>
	private static bool IsWordChar(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';

	/// <summary><c>[^\x00-\x7F]|\w|\-|\*|\.</c>.</summary>
	private static bool IsUnicodeTextChar(char c) => c >= '\u0080' || IsWordChar(c) || c is '-' or '*' or '.';

	/// <summary><c>[^\x00-\x7F]|\w|\-|\*</c>.</summary>
	private static bool IsStyleTextChar(char c) => c >= '\u0080' || IsWordChar(c) || c is '-' or '*';

	/// <summary><c>[\*A-Za-z_À-￿]</c>.</summary>
	private static bool IsAttributeWordStart(char c) => c >= 'À' || char.IsAsciiLetter(c) || c is '*' or '_';

	/// <summary><c>[A-Za-z0-9\-\_\[\]\(\)\.,À-￿\*]</c>.</summary>
	private static bool IsAttributeWordChar(char c) =>
		c >= 'À' || char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '[' or ']' or '(' or ')' or '.' or ',' or '*';
}
