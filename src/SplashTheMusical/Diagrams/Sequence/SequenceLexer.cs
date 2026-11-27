// Upstream grammar: packages/mermaid/src/diagrams/sequence/parser/sequenceDiagram.jison
using SplashTheMusical.Diagrams.State;
using SplashTheMusical.Parsing;

namespace SplashTheMusical.Diagrams.Sequence;

/// <summary>The terminals of <c>sequenceDiagram.jison</c>, named as the grammar names them.</summary>
internal enum SequenceTokenKind : byte
{
	/// <summary>jison's <c>$end</c>, after the input or where a condition has no rule for its end.</summary>
	End,

	/// <summary>Where no lexer rule matches. No grammar rule accepts it.</summary>
	Error,

	/// <summary>A rule whose action returns no token. The lexer never hands it out.</summary>
	Skip,

	/// <summary><c>accDescr {</c>, which only begins its condition. The lexer never hands it out.</summary>
	AccDescrMultilineStart,

	Newline,
	Invalid,

	/// <summary><c>SD</c>: <c>sequenceDiagram</c>.</summary>
	Sd,
	Participant,

	/// <summary><c>participant_actor</c>: <c>actor</c>.</summary>
	ParticipantActor,
	Create,
	Destroy,
	Box,
	As,
	Loop,
	Rect,
	Opt,
	Alt,
	Else,
	Par,
	ParOver,
	And,
	Critical,
	Option,
	Break,

	/// <summary><c>end</c>.</summary>
	EndKeyword,
	RestOfLine,
	LeftOf,
	RightOf,
	Links,
	Link,
	Properties,
	Details,
	Over,
	Note,
	Activate,
	Deactivate,

	/// <summary><c>title</c>, a space and the rest of the line.</summary>
	Title,

	/// <summary><c>title:</c>, a space and the rest of the line.</summary>
	LegacyTitle,
	AccTitle,
	AccTitleValue,
	AccDescr,
	AccDescrValue,
	AccDescrMultilineValue,
	Autonumber,
	Off,
	Comma,

	/// <summary><c>ACTOR</c>, without the space around it, which the rules' actions trim.</summary>
	Actor,

	/// <summary><c>NUM</c>.</summary>
	Num,
	ConfigStart,
	ConfigContent,
	ConfigEnd,

	/// <summary>One of the arrow terminals. <see cref="SequenceToken.Value"/> is the <see cref="SequenceArrow"/>.</summary>
	Arrow,

	/// <summary><c>TXT</c>: <c>:</c> and the rest of the line up to <c>#</c> or <c>;</c>.</summary>
	Txt,
	Plus,
	Minus,

	/// <summary><c>()</c>.</summary>
	Central,
}

/// <summary>A token: its kind and its text in the prepared source.</summary>
internal readonly record struct SequenceToken(SequenceTokenKind Kind, int Start, int Length, byte Value = 0)
{
	public ReadOnlySpan<char> Text(ReadOnlySpan<char> text) => text.Slice(Start, Length);
}

/// <summary>
/// The lexer of <c>sequenceDiagram.jison</c> at mermaid 12.3.0. jison tries the rules of the current start condition
/// in the order the grammar lists them, takes the first that matches, and adds <c>\b</c> after a rule that ends in a
/// letter. The grammar is <c>case-insensitive</c>. Each start condition is a method here that checks its rules in that
/// order. Every condition but the first is exclusive, so only the first has the <c>&lt;&lt;EOF&gt;&gt;</c> rule, which
/// returns <c>NEWLINE</c>, and the end of the input in another gives <c>$end</c>.
/// </summary>
/// <remarks>
/// The keywords are words of ASCII letters and <c>_</c>, and jison's <c>\b</c> is ASCII's, so a keyword matches exactly
/// when the run of ASCII word characters at the cursor is the keyword. The first <c>ACTOR</c> rule is
/// <code>
/// [^\/\\\+\()\+&lt;\-&gt;\-&gt;:\n,;]+((?!(\-x|\-\-x|\-\)|\-\-\)|\-\|\\|\-\\|\-\/|\-\/\/|\-\|\/|\/\|\-|\\\|\-|\/\/\-|\\\\\-|\/\|\-|\-\-\|\\|\-\-|\(\)))[\-]*[^\+&lt;\-&gt;\-&gt;:\n,;]+)*
/// </code>
/// Nothing follows its group, so the greedy match never backtracks into it: <see cref="ActorLength"/> takes the first
/// run and then each repetition until one fails.
/// </remarks>
internal ref struct SequenceLexer
{
	private const byte Initial = 0;
	private const byte IdCondition = 1;
	private const byte AliasCondition = 2;
	private const byte LineCondition = 3;
	private const byte ConfigCondition = 4;
	private const byte AccTitleCondition = 5;
	private const byte AccDescrCondition = 6;
	private const byte AccDescrMultiline = 7;

	private readonly ReadOnlySpan<char> _text;
	private PooledList<byte> _conditions;
	private int _at;
	private bool _done;

	private SequenceLexer(ReadOnlySpan<char> text)
	{
		_text = text;
		_conditions = new PooledList<byte>(8);
		_conditions.Add(Initial);
	}

	/// <summary>
	/// Lexes <paramref name="text"/> into <paramref name="tokens"/>, ending with <see cref="SequenceTokenKind.End"/>, or
	/// ends them with an <see cref="SequenceTokenKind.Error"/> token and <see cref="SequenceTokenKind.End"/> where lexing
	/// stops.
	/// </summary>
	public static void Lex(ReadOnlySpan<char> text, ref PooledList<SequenceToken> tokens)
	{
		var lexer = new SequenceLexer(text);
		try
		{
			while (true)
			{
				var token = lexer.Next();
				tokens.Add(token);
				if (token.Kind is SequenceTokenKind.End or SequenceTokenKind.Error)
				{
					if (token.Kind != SequenceTokenKind.End)
					{
						tokens.Add(new SequenceToken(SequenceTokenKind.End, text.Length, 0));
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
	private SequenceToken Next()
	{
		while (true)
		{
			if (_done)
			{
				return new SequenceToken(SequenceTokenKind.End, _text.Length, 0);
			}

			if (_at >= _text.Length)
			{
				_done = true;
			}

			var token = Condition switch
			{
				Initial => InitialRule(),
				IdCondition => IdRule(),
				AliasCondition => AliasRule(),
				LineCondition => LineRule(),
				ConfigCondition => ConfigRule(),
				AccTitleCondition or AccDescrCondition => AccValueRule(),
				_ => AccDescrMultilineRule(),
			};
			if (token.Kind == SequenceTokenKind.End)
			{
				return _at >= _text.Length
					? new SequenceToken(SequenceTokenKind.End, _text.Length, 0)
					: new SequenceToken(SequenceTokenKind.Error, _at, 0);
			}

			if (token.Kind is not (SequenceTokenKind.Skip or SequenceTokenKind.AccDescrMultilineStart))
			{
				return token;
			}
		}
	}

	private SequenceToken Take(SequenceTokenKind kind, int length, byte value = 0)
	{
		var token = new SequenceToken(kind, _at, length, value);
		_at += length;
		return token;
	}

	private SequenceToken InitialRule()
	{
		var kind = MatchInitial(_text, _at, out var length, out var value);
		switch (kind)
		{
			case SequenceTokenKind.Box or SequenceTokenKind.Loop or SequenceTokenKind.Rect or SequenceTokenKind.Opt
				or SequenceTokenKind.Alt or SequenceTokenKind.Else or SequenceTokenKind.Par or SequenceTokenKind.ParOver
				or SequenceTokenKind.And or SequenceTokenKind.Critical or SequenceTokenKind.Option or SequenceTokenKind.Break:
				Push(LineCondition);
				break;
			case SequenceTokenKind.Participant or SequenceTokenKind.ParticipantActor or SequenceTokenKind.Destroy
				or SequenceTokenKind.Activate or SequenceTokenKind.Deactivate:
				Push(IdCondition);
				break;
			case SequenceTokenKind.AccTitle:
				Push(AccTitleCondition);
				break;
			case SequenceTokenKind.AccDescr:
				Push(AccDescrCondition);
				break;
			case SequenceTokenKind.AccDescrMultilineStart:
				Push(AccDescrMultiline);
				break;
			case SequenceTokenKind.Actor:
				{
					var start = _at;
					_at += length;
					var trimmed = StateLexer.Trim(_text.Slice(start, length));
					var lead = StateLexer.TrimStart(_text.Slice(start, length)).Length;
					return new SequenceToken(SequenceTokenKind.Actor, start + (length - lead), trimmed.Length);
				}
		}

		return Take(kind, length, value);
	}

	/// <summary>
	/// The first rule of the initial condition that matches at <paramref name="at"/>: its token, or
	/// <see cref="SequenceTokenKind.Skip"/> for a rule that returns none, and the length it matches. An <c>ACTOR</c>'s
	/// length is its match before the trim. At the end of the text it is the <c>&lt;&lt;EOF&gt;&gt;</c> rule's
	/// <c>NEWLINE</c>.
	/// </summary>
	public static SequenceTokenKind MatchInitial(ReadOnlySpan<char> text, int at, out int length, out byte value)
	{
		value = 0;
		length = 0;
		if (at >= text.Length)
		{
			return SequenceTokenKind.Newline;
		}

		var c = text[at];
		if (c == '\n')
		{
			length = RunOf(text, at, '\n');
			return SequenceTokenKind.Newline;
		}

		if (PreambleParser.IsSpace(c))
		{
			var end = at;
			while (end < text.Length && PreambleParser.IsSpace(text[end]))
			{
				end++;
			}

			length = end - at;
			return SequenceTokenKind.Skip;
		}

		// \#[^\n]*, \%%(?!\{)[^\n]* and [^\}]\%\%[^\n]*
		if (c == '#'
			|| (c == '%' && At(text, at + 1) == '%' && At(text, at + 2) != '{')
			|| (c != '}' && At(text, at + 1) == '%' && At(text, at + 2) == '%'))
		{
			length = LineLength(text, at);
			return SequenceTokenKind.Skip;
		}

		length = NumLength(text, at);
		if (length > 0)
		{
			return SequenceTokenKind.Num;
		}

		var keyword = KeywordAt(text, at, out length);
		if (keyword != SequenceTokenKind.End)
		{
			return keyword;
		}

		switch (c)
		{
			case ',':
				length = 1;
				return SequenceTokenKind.Comma;
			case ';':
				length = 1;
				return SequenceTokenKind.Newline;
		}

		length = ActorLength(text, at);
		if (length > 0)
		{
			return SequenceTokenKind.Actor;
		}

		length = ArrowLength(text, at, out var arrow);
		if (length > 0)
		{
			value = (byte)arrow;
			return SequenceTokenKind.Arrow;
		}

		switch (c)
		{
			case ':':
				var end = at + 1;
				while (end < text.Length && text[end] is not ('#' or '\n' or ';'))
				{
					end++;
				}

				length = end - at;
				return SequenceTokenKind.Txt;
			case '+':
				length = 1;
				return SequenceTokenKind.Plus;
			case '-':
				length = 1;
				return SequenceTokenKind.Minus;
			case '(' when At(text, at + 1) == ')':
				length = 2;
				return SequenceTokenKind.Central;
		}

		length = 1;
		return SequenceTokenKind.Invalid;
	}

	/// <summary>
	/// The keyword rule that matches at <paramref name="at"/>, from <c>"box"</c> to <c>"off"</c>, or
	/// <see cref="SequenceTokenKind.End"/>. The rules with a lookahead or a trailing pattern, such as <c>"links"</c> and
	/// <c>"title"\s[^#\n;]+</c>, are checked with it.
	/// </summary>
	private static SequenceTokenKind KeywordAt(ReadOnlySpan<char> text, int at, out int length)
	{
		var word = text[at..];
		var wordLength = 0;
		while (wordLength < word.Length && IsKeywordChar(word[wordLength]))
		{
			wordLength++;
		}

		length = wordLength;
		word = word[..wordLength];
		switch (wordLength)
		{
			case 3 when Is(word, "box"):
				return SequenceTokenKind.Box;
			case 3 when Is(word, "opt"):
				return SequenceTokenKind.Opt;
			case 3 when Is(word, "alt"):
				return SequenceTokenKind.Alt;
			case 3 when Is(word, "par"):
				return SequenceTokenKind.Par;
			case 3 when Is(word, "and"):
				return SequenceTokenKind.And;
			case 3 when Is(word, "end"):
				return SequenceTokenKind.EndKeyword;
			case 3 when Is(word, "off"):
				return SequenceTokenKind.Off;
			case 4 when Is(word, "loop"):
				return SequenceTokenKind.Loop;
			case 4 when Is(word, "rect"):
				return SequenceTokenKind.Rect;
			case 4 when Is(word, "else"):
				return SequenceTokenKind.Else;
			case 4 when Is(word, "over"):
				return SequenceTokenKind.Over;
			case 4 when Is(word, "note"):
				return SequenceTokenKind.Note;
			case 4 when Is(word, "left") && Of(text, at + 4):
				length = 7;
				return SequenceTokenKind.LeftOf;
			case 4 when Is(word, "link") && LinkFollows(text, at + 4):
				return SequenceTokenKind.Link;
			case 5 when Is(word, "actor"):
				return SequenceTokenKind.ParticipantActor;
			case 5 when Is(word, "break"):
				return SequenceTokenKind.Break;
			case 5 when Is(word, "right") && Of(text, at + 5):
				length = 8;
				return SequenceTokenKind.RightOf;
			case 5 when Is(word, "links") && LinkFollows(text, at + 5):
				return SequenceTokenKind.Links;
			case 5 when Is(word, "title"):
				return TitleRule(text, at, out length);
			case 6 when Is(word, "create"):
				return SequenceTokenKind.Create;
			case 6 when Is(word, "option"):
				return SequenceTokenKind.Option;
			case 7 when Is(word, "destroy"):
				return SequenceTokenKind.Destroy;
			case 7 when Is(word, "details") && LinkFollows(text, at + 7):
				return SequenceTokenKind.Details;
			case 8 when Is(word, "par_over"):
				return SequenceTokenKind.ParOver;
			case 8 when Is(word, "critical"):
				return SequenceTokenKind.Critical;
			case 8 when Is(word, "activate"):
				return SequenceTokenKind.Activate;
			case 8 when Is(word, "acctitle"):
				return AccRule(text, at + 8, SequenceTokenKind.AccTitle, at, out length);
			case 8 when Is(word, "accdescr"):
				return AccRule(text, at + 8, SequenceTokenKind.AccDescr, at, out length);
			case 10 when Is(word, "deactivate"):
				return SequenceTokenKind.Deactivate;
			case 10 when Is(word, "properties") && LinkFollows(text, at + 10):
				return SequenceTokenKind.Properties;
			case 10 when Is(word, "autonumber"):
				return SequenceTokenKind.Autonumber;
			case 11 when Is(word, "participant"):
				return SequenceTokenKind.Participant;
			case 15 when Is(word, "sequencediagram"):
				return SequenceTokenKind.Sd;
		}

		length = 0;
		return SequenceTokenKind.End;
	}

	/// <summary><c>" of"</c> after <c>left</c> or <c>right</c>, with jison's <c>\b</c> after it.</summary>
	private static bool Of(ReadOnlySpan<char> text, int at) =>
		At(text, at) == ' ' && Lower(At(text, at + 1)) == 'o' && Lower(At(text, at + 2)) == 'f' && !IsKeywordChar(At(text, at + 3));

	/// <summary>The lookahead <c>(?=[ \t]+[^\/\\\+\(\)&lt;&gt;:\n,;\s\-])</c> of <c>links</c>, <c>link</c>, <c>properties</c> and <c>details</c>.</summary>
	private static bool LinkFollows(ReadOnlySpan<char> text, int at)
	{
		var end = at;
		while (end < text.Length && text[end] is ' ' or '\t')
		{
			end++;
		}

		return end > at && end < text.Length && text[end] is not ('/' or '\\' or '+' or '(' or ')' or '<' or '>' or ':' or '\n' or ',' or ';' or '-')
			&& !PreambleParser.IsSpace(text[end]);
	}

	/// <summary><c>"title"\s[^#\n;]+</c> and <c>"title:"\s[^#\n;]+</c>.</summary>
	private static SequenceTokenKind TitleRule(ReadOnlySpan<char> text, int at, out int length)
	{
		length = 0;
		var kind = SequenceTokenKind.Title;
		var after = at + 5;
		if (At(text, after) == ':')
		{
			kind = SequenceTokenKind.LegacyTitle;
			after++;
		}

		if (after >= text.Length || !PreambleParser.IsSpace(text[after]))
		{
			return SequenceTokenKind.End;
		}

		var end = after + 1;
		while (end < text.Length && text[end] is not ('#' or '\n' or ';'))
		{
			end++;
		}

		if (end == after + 1)
		{
			return SequenceTokenKind.End;
		}

		length = end - at;
		return kind;
	}

	/// <summary><c>accTitle\s*":"\s*</c>, <c>accDescr\s*":"\s*</c> and <c>accDescr\s*"{"\s*</c>.</summary>
	private static SequenceTokenKind AccRule(ReadOnlySpan<char> text, int after, SequenceTokenKind kind, int at, out int length)
	{
		length = 0;
		var end = SkipSpace(text, after);
		var c = At(text, end);
		if (c == ':')
		{
			length = SkipSpace(text, end + 1) - at;
			return kind;
		}

		if (c == '{' && kind == SequenceTokenKind.AccDescr)
		{
			length = SkipSpace(text, end + 1) - at;
			return SequenceTokenKind.AccDescrMultilineStart;
		}

		return SequenceTokenKind.End;
	}

	/// <summary>
	/// <c>([0-9]+(\.[0-9]{1,2})?|\.[0-9]{1,2})(?=[ \n]+)</c>: the length of the number at <paramref name="at"/>, or 0.
	/// A shorter run of digits is followed by a digit, so only the whole run can match.
	/// </summary>
	public static int NumLength(ReadOnlySpan<char> text, int at)
	{
		var digits = 0;
		while (at + digits < text.Length && char.IsAsciiDigit(text[at + digits]))
		{
			digits++;
		}

		if (At(text, at + digits) == '.')
		{
			var fraction = 0;
			while (fraction < 2 && at + digits + 1 + fraction < text.Length && char.IsAsciiDigit(text[at + digits + 1 + fraction]))
			{
				fraction++;
			}

			for (var n = fraction; n >= 1; n--)
			{
				if (NumberEnds(text, at + digits + 1 + n))
				{
					return digits + 1 + n;
				}
			}
		}

		return digits > 0 && NumberEnds(text, at + digits) ? digits : 0;
	}

	private static bool NumberEnds(ReadOnlySpan<char> text, int at) => at < text.Length && text[at] is ' ' or '\n';

	/// <summary>
	/// The length of the first <c>ACTOR</c> rule's match at <paramref name="at"/>, before the trim, or 0. The remarks of
	/// <see cref="SequenceLexer"/> give the rule.
	/// </summary>
	public static int ActorLength(ReadOnlySpan<char> text, int at)
	{
		var end = at;
		while (end < text.Length && IsActorStart(text[end]))
		{
			end++;
		}

		if (end == at)
		{
			return 0;
		}

		while (!EndsActor(text, end))
		{
			var run = end < text.Length && text[end] == '-' ? end + 1 : end;
			var stop = run;
			while (stop < text.Length && IsActorRest(text[stop]))
			{
				stop++;
			}

			if (stop == run)
			{
				break;
			}

			end = stop;
		}

		return end - at;
	}

	/// <summary><c>[^\/\\\+\()\+&lt;\-&gt;\-&gt;:\n,;]</c>.</summary>
	private static bool IsActorStart(char c) => c is not ('/' or '\\' or '+' or '(' or ')' or '<' or '-' or '>' or ':' or '\n' or ',' or ';');

	/// <summary><c>[^\+&lt;\-&gt;\-&gt;:\n,;]</c>.</summary>
	private static bool IsActorRest(char c) => c is not ('+' or '<' or '-' or '>' or ':' or '\n' or ',' or ';');

	/// <summary>
	/// The negative lookahead of the <c>ACTOR</c> rule's group, without case: <c>-x</c>, <c>--x</c>, <c>-)</c>,
	/// <c>--)</c>, <c>-|\</c>, <c>-\</c>, <c>-/</c>, <c>-//</c>, <c>-|/</c>, <c>/|-</c>, <c>\|-</c>, <c>//-</c>,
	/// <c>\\-</c>, <c>--|\</c>, <c>--</c> and <c>()</c>.
	/// </summary>
	private static bool EndsActor(ReadOnlySpan<char> text, int at)
	{
		var a = At(text, at);
		var b = At(text, at + 1);
		var c = At(text, at + 2);
		return a switch
		{
			'-' => b is 'x' or 'X' or '-' or ')' or '\\' or '/' || (b == '|' && c is '\\' or '/'),
			'/' => (b == '|' && c == '-') || (b == '/' && c == '-'),
			'\\' => (b == '|' && c == '-') || (b == '\\' && c == '-'),
			'(' => b == ')',
			_ => false,
		};
	}

	/// <summary>The arrow rules, from <c>"-&gt;&gt;"</c> to <c>\\\\\-</c>, in the grammar's order.</summary>
	public static int ArrowLength(ReadOnlySpan<char> text, int at, out SequenceArrow arrow)
	{
		var rest = text[at..];
		foreach (var (pattern, value) in Arrows)
		{
			if (StartsWithFolded(rest, pattern))
			{
				arrow = value;
				return pattern.Length;
			}
		}

		arrow = default;
		return 0;
	}

	/// <summary>The arrow terminals in the grammar's order, each with the text the writer writes for it.</summary>
	public static readonly (string Pattern, SequenceArrow Arrow)[] Arrows =
	[
		("->>", SequenceArrow.Solid),
		("<<->>", SequenceArrow.BidirectionalSolid),
		("-->>", SequenceArrow.Dotted),
		("<<-->>", SequenceArrow.BidirectionalDotted),
		("->", SequenceArrow.SolidOpen),
		("-->", SequenceArrow.DottedOpen),
		("-x", SequenceArrow.SolidCross),
		("--x", SequenceArrow.DottedCross),
		("-)", SequenceArrow.SolidPoint),
		("--)", SequenceArrow.DottedPoint),
		("--|\\", SequenceArrow.SolidTopDotted),
		("--|/", SequenceArrow.SolidBottomDotted),
		("--\\\\", SequenceArrow.StickTopDotted),
		("--//", SequenceArrow.StickBottomDotted),
		("/|--", SequenceArrow.SolidArrowTopReverseDotted),
		("\\|--", SequenceArrow.SolidArrowBottomReverseDotted),
		("//--", SequenceArrow.StickArrowTopReverseDotted),
		("\\\\--", SequenceArrow.StickArrowBottomReverseDotted),
		("-|\\", SequenceArrow.SolidTop),
		("-|/", SequenceArrow.SolidBottom),
		("-\\\\", SequenceArrow.StickTop),
		("-//", SequenceArrow.StickBottom),
		("/|-", SequenceArrow.SolidArrowTopReverse),
		("\\|-", SequenceArrow.SolidArrowBottomReverse),
		("//-", SequenceArrow.StickArrowTopReverse),
		("\\\\-", SequenceArrow.StickArrowBottomReverse),
	];

	/// <summary>
	/// The rules of the <c>ID</c> condition, after <c>participant</c>, <c>actor</c>, <c>destroy</c>, <c>activate</c> and
	/// <c>deactivate</c>.
	/// </summary>
	private SequenceToken IdRule()
	{
		var kind = MatchId(_text, _at, out var length, out var next);
		switch (next)
		{
			case IdNext.Config:
				Push(ConfigCondition);
				break;
			case IdNext.Alias:
				Push(AliasCondition);
				break;
			case IdNext.Pop:
				Pop();
				break;
		}

		if (kind is SequenceTokenKind.Actor or SequenceTokenKind.Invalid)
		{
			var start = _at;
			_at += length;
			var match = _text.Slice(start, length);
			var lead = match.Length - StateLexer.TrimStart(match).Length;
			return new SequenceToken(kind, start + lead, StateLexer.Trim(match).Length);
		}

		return Take(kind, length);
	}

	/// <summary>What an <c>ID</c> rule does to the conditions after it matches.</summary>
	public enum IdNext : byte
	{
		Stay,
		Config,
		Alias,
		Pop,
	}

	/// <summary>
	/// The first rule of the <c>ID</c> condition that matches at <paramref name="at"/>, or
	/// <see cref="SequenceTokenKind.End"/> when none does:
	/// <code>
	/// ((?!\n)\s)+                                  skip
	/// \#[^\n]*                                     skip
	/// \@\{                                         CONFIG_START, begin CONFIG
	/// [^&lt;&gt;:\n,;@\s]+(?=\s*\@\{)                    ACTOR
	/// [^&lt;&gt;:\n,;@\s]+(?=\s+as\s)                    ACTOR, begin ALIAS
	/// [^&lt;&gt;:\n,;@]+(?=\s*[\n;#]|$)                  ACTOR, popState
	/// [^&lt;&gt;:\n,;@]*\&lt;[^\n]*                          INVALID, popState
	/// [^\n]+                                       INVALID, popState
	/// </code>
	/// </summary>
	public static SequenceTokenKind MatchId(ReadOnlySpan<char> text, int at, out int length, out IdNext next)
	{
		next = IdNext.Stay;
		length = 0;
		if (at >= text.Length)
		{
			return SequenceTokenKind.End;
		}

		var c = text[at];
		if (c != '\n' && PreambleParser.IsSpace(c))
		{
			length = SameLineSpace(text, at) - at;
			return SequenceTokenKind.Skip;
		}

		if (c == '#')
		{
			length = LineLength(text, at);
			return SequenceTokenKind.Skip;
		}

		if (c == '@' && At(text, at + 1) == '{')
		{
			length = 2;
			next = IdNext.Config;
			return SequenceTokenKind.ConfigStart;
		}

		var word = at;
		while (word < text.Length && IsIdChar(text[word]) && !PreambleParser.IsSpace(text[word]))
		{
			word++;
		}

		if (word > at)
		{
			var space = SkipSpace(text, word);
			if (At(text, space) == '@' && At(text, space + 1) == '{')
			{
				length = word - at;
				return SequenceTokenKind.Actor;
			}

			if (space > word && AsFollows(text, space))
			{
				length = word - at;
				next = IdNext.Alias;
				return SequenceTokenKind.Actor;
			}
		}

		var run = at;
		while (run < text.Length && IsIdChar(text[run]))
		{
			run++;
		}

		// (?=\s*[\n;#]|$) at each end from the longest: a space defers to what follows it.
		if (run > at)
		{
			var follows = run >= text.Length || text[run] is '\n' or ';';
			if (run >= text.Length)
			{
				length = run - at;
				next = IdNext.Pop;
				return SequenceTokenKind.Actor;
			}

			for (var end = run; end > at; end--)
			{
				if (end < run)
				{
					var d = text[end];
					follows = d == '#' || (PreambleParser.IsSpace(d) && follows);
				}

				if (follows)
				{
					length = end - at;
					next = IdNext.Pop;
					return SequenceTokenKind.Actor;
				}
			}
		}

		next = IdNext.Pop;
		if (At(text, run) == '<')
		{
			length = LineLength(text, at);
			return SequenceTokenKind.Invalid;
		}

		length = LineLength(text, at);
		if (length == 0)
		{
			next = IdNext.Stay;
			return SequenceTokenKind.End;
		}

		return SequenceTokenKind.Invalid;
	}

	/// <summary><c>[^&lt;&gt;:\n,;@]</c>.</summary>
	private static bool IsIdChar(char c) => c is not ('<' or '>' or ':' or '\n' or ',' or ';' or '@');

	/// <summary><c>as\s</c> at <paramref name="at"/>, without case.</summary>
	private static bool AsFollows(ReadOnlySpan<char> text, int at) =>
		Lower(At(text, at)) == 'a' && Lower(At(text, at + 1)) == 's' && at + 2 < text.Length && PreambleParser.IsSpace(text[at + 2]);

	/// <summary>The rules of the <c>ALIAS</c> condition: space, a comment, <c>as</c>, or the empty match that ends it.</summary>
	private SequenceToken AliasRule()
	{
		var c = At(_text, _at);
		if (c != '\n' && _at < _text.Length && PreambleParser.IsSpace(c))
		{
			return Take(SequenceTokenKind.Skip, SameLineSpace(_text, _at) - _at);
		}

		if (c == '#')
		{
			return Take(SequenceTokenKind.Skip, LineLength(_text, _at));
		}

		Pop();
		Pop();
		if (Lower(c) == 'a' && Lower(At(_text, _at + 1)) == 's' && !IsKeywordChar(At(_text, _at + 2)))
		{
			Push(LineCondition);
			return Take(SequenceTokenKind.As, 2);
		}

		return Take(SequenceTokenKind.Newline, 0);
	}

	/// <summary>The rules of the <c>LINE</c> condition: space, a comment, or <c>restOfLine</c>, <c>[^#\n;]*</c>.</summary>
	private SequenceToken LineRule()
	{
		var c = At(_text, _at);
		if (c != '\n' && _at < _text.Length && PreambleParser.IsSpace(c))
		{
			return Take(SequenceTokenKind.Skip, SameLineSpace(_text, _at) - _at);
		}

		if (c == '#')
		{
			return Take(SequenceTokenKind.Skip, LineLength(_text, _at));
		}

		var end = _at;
		while (end < _text.Length && _text[end] is not ('#' or '\n' or ';'))
		{
			end++;
		}

		Pop();
		return Take(SequenceTokenKind.RestOfLine, end - _at);
	}

	/// <summary>The rules of the <c>CONFIG</c> condition: <c>[^\}]+</c>, then <c>}</c> before <c>as</c> or alone.</summary>
	private SequenceToken ConfigRule()
	{
		if (_at >= _text.Length)
		{
			return new SequenceToken(SequenceTokenKind.End, _at, 0);
		}

		if (_text[_at] != '}')
		{
			var end = _text[_at..].IndexOf('}');
			return Take(SequenceTokenKind.ConfigContent, end < 0 ? _text.Length - _at : end);
		}

		Pop();
		var space = SkipSpace(_text, _at + 1);
		if (space > _at + 1 && AsFollows(_text, space))
		{
			Push(AliasCondition);
		}
		else
		{
			Pop();
		}

		return Take(SequenceTokenKind.ConfigEnd, 1);
	}

	/// <summary>
	/// <c>(?!\n|;|#)*[^\n]*</c> of <c>acc_title</c> and <c>acc_descr</c>. The quantified lookahead matches empty, so it
	/// is the rest of the line.
	/// </summary>
	private SequenceToken AccValueRule()
	{
		var kind = Condition == AccTitleCondition ? SequenceTokenKind.AccTitleValue : SequenceTokenKind.AccDescrValue;
		Pop();
		return Take(kind, LineLength(_text, _at));
	}

	/// <summary><c>[\}]</c>, which ends the condition, then <c>[^\}]*</c>.</summary>
	private SequenceToken AccDescrMultilineRule()
	{
		if (At(_text, _at) == '}')
		{
			Pop();
			return Take(SequenceTokenKind.Skip, 1);
		}

		var end = _text[_at..].IndexOf('}');
		return Take(SequenceTokenKind.AccDescrMultilineValue, end < 0 ? _text.Length - _at : end);
	}

	/// <summary>The character at <paramref name="at"/>, or <c>\0</c> past the end.</summary>
	private static char At(ReadOnlySpan<char> text, int at) => at < text.Length ? text[at] : '\0';

	private static int RunOf(ReadOnlySpan<char> text, int at, char c)
	{
		var end = at;
		while (end < text.Length && text[end] == c)
		{
			end++;
		}

		return end - at;
	}

	/// <summary>The length of <c>[^\n]*</c> at <paramref name="at"/>.</summary>
	private static int LineLength(ReadOnlySpan<char> text, int at)
	{
		var end = text[at..].IndexOf('\n');
		return end < 0 ? text.Length - at : end;
	}

	private static int SkipSpace(ReadOnlySpan<char> text, int at)
	{
		while (at < text.Length && PreambleParser.IsSpace(text[at]))
		{
			at++;
		}

		return at;
	}

	private static int SameLineSpace(ReadOnlySpan<char> text, int at)
	{
		while (at < text.Length && text[at] != '\n' && PreambleParser.IsSpace(text[at]))
		{
			at++;
		}

		return at;
	}

	/// <summary>jison's <c>\b</c> is JavaScript's, whose word characters are ASCII letters, digits and <c>_</c>.</summary>
	private static bool IsKeywordChar(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';

	private static char Lower(char c) => char.IsAsciiLetterUpper(c) ? (char)(c + 32) : c;

	/// <summary>Whether <paramref name="word"/> is <paramref name="keyword"/>, in lower case, without ASCII case.</summary>
	private static bool Is(ReadOnlySpan<char> word, string keyword)
	{
		for (var i = 0; i < keyword.Length; i++)
		{
			if (Lower(word[i]) != keyword[i])
			{
				return false;
			}
		}

		return true;
	}

	private static bool StartsWithFolded(ReadOnlySpan<char> text, string pattern)
	{
		if (text.Length < pattern.Length)
		{
			return false;
		}

		for (var i = 0; i < pattern.Length; i++)
		{
			if (Lower(text[i]) != pattern[i])
			{
				return false;
			}
		}

		return true;
	}
}
