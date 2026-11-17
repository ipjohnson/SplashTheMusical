// Upstream grammar: packages/mermaid/src/diagrams/flowchart/parser/flow.jison
using SplashTheMusical.Parsing;

namespace SplashTheMusical.Diagrams.Flowchart;

/// <summary>The terminals of <c>flow.jison</c>, named as the grammar names them.</summary>
internal enum FlowchartTokenKind : byte
{
	/// <summary>The end of the input, jison's <c>$end</c>.</summary>
	End,

	/// <summary>Where no lexer rule matches. No grammar rule accepts it.</summary>
	Error,
	Eof,
	AccTitle,
	AccTitleValue,
	AccDescr,
	AccDescrValue,
	AccDescrMultilineValue,

	/// <summary>The <c>@{</c> that starts a <c>@{ }</c> block, a <c>SHAPE_DATA</c> with empty text.</summary>
	ShapeDataStart,

	/// <summary>A piece of a <c>@{ }</c> block.</summary>
	ShapeData,

	/// <summary>A piece of a quoted string in a <c>@{ }</c> block, where <c>\n\s*</c> reads as <c>&lt;br/&gt;</c>.</summary>
	ShapeDataString,
	CallbackName,
	CallbackArgs,
	MdStr,
	Str,
	Style,
	Default,
	LinkStyle,
	Interpolate,
	ClassDef,
	Class,
	Href,
	Click,
	Graph,
	Subgraph,
	EndKeyword,
	LinkTarget,
	NoDir,
	Dir,
	DirectionTb,
	DirectionBt,
	DirectionRl,
	DirectionLr,
	DirectionTd,
	LinkId,
	Num,
	Brkt,
	StyleSeparator,
	Colon,
	Amp,
	Semi,
	Comma,
	Mult,
	Link,
	StartLink,
	EdgeText,
	EllipseStart,
	EllipseEnd,
	Text,
	StadiumStart,
	StadiumEnd,
	SubroutineStart,
	SubroutineEnd,
	VertexWithPropsStart,
	TagEnd,
	CylinderStart,
	CylinderEnd,
	DoubleCircleStart,
	DoubleCircleEnd,
	TrapStart,
	TrapEnd,
	InvTrapStart,
	InvTrapEnd,
	TagStart,
	Up,
	Sep,
	Down,
	NodeString,
	Minus,
	UnicodeText,
	Pipe,
	Ps,
	Pe,
	Sqs,
	Sqe,
	DiamondStart,
	DiamondStop,
	NewLine,
	Space,
}

internal readonly record struct FlowchartToken(FlowchartTokenKind Kind, int Start, int Length)
{
	public int End => Start + Length;
}

/// <summary>
/// The lexer of <c>flow.jison</c>. jison tries the rules of the current start condition in the order the grammar lists
/// them and takes the first that matches, so each state's method checks its rules in that order. jison adds <c>\b</c> to
/// a rule that ends in a letter or digit, so <c>style</c> does not match the start of <c>styles</c>. The states are
/// exclusive: a rule without a condition applies only in <c>INITIAL</c>, and a <c>&lt;*&gt;</c> rule applies in every
/// state. <c>begin</c> pushes a state and <c>popState</c> pops one but never the last.
/// </summary>
/// <remarks>
/// The tokens do not depend on the parser, so the whole input is lexed before parsing starts. A source is accepted only
/// when every token lexes and the tokens parse, so the order of a lexical and a parse error does not change the outcome.
/// </remarks>
internal ref struct FlowchartLexer
{
	private enum State : byte
	{
		Initial,
		String,
		MdString,
		AccTitle,
		AccDescr,
		AccDescrMultiline,
		Dir,
		Text,
		EllipseText,
		TrapText,
		EdgeText,
		ThickEdgeText,
		DottedEdgeText,
		Click,
		CallbackName,
		CallbackArgs,
		ShapeData,
		ShapeDataString,
	}

	private readonly ReadOnlySpan<char> _text;
	private PooledList<State> _states;
	private int _position;
	private bool _firstGraph;
	private bool _done;

	// The line the direction rules last looked at, and where "direction" last starts on it, or -1.
	private int _directionLineEnd;
	private int _lastDirection;

	private FlowchartLexer(ReadOnlySpan<char> text)
	{
		_text = text;
		_states = new PooledList<State>(8);
		_states.Add(State.Initial);
		_firstGraph = true;
		_directionLineEnd = -1;
	}

	/// <summary>
	/// Lexes <paramref name="text"/> into <paramref name="tokens"/>, which ends with <see cref="FlowchartTokenKind.End"/>.
	/// Returns the offset of the first character no rule matches, or -1.
	/// </summary>
	public static int Lex(ReadOnlySpan<char> text, ref PooledList<FlowchartToken> tokens)
	{
		var lexer = new FlowchartLexer(text);
		try
		{
			while (true)
			{
				if (!lexer.Next(out var token))
				{
					return lexer._position;
				}

				tokens.Add(token);
				if (token.Kind == FlowchartTokenKind.End)
				{
					return -1;
				}
			}
		}
		finally
		{
			lexer._states.Dispose();
		}
	}

	private readonly State Current => _states[_states.Count - 1];

	private readonly ReadOnlySpan<char> Rest => _text[_position..];

	/// <summary>jison's <c>lex</c>: the next token, after the rules that only change the state. False on a lexical error.</summary>
	private bool Next(out FlowchartToken token)
	{
		while (true)
		{
			if (_done)
			{
				token = new FlowchartToken(FlowchartTokenKind.End, _text.Length, 0);
				return true;
			}

			if (_position >= _text.Length)
			{
				_done = true;
			}

			var start = _position;
			var matched = Current switch
			{
				State.Initial => MatchInitial(out token),
				State.String => MatchString(out token),
				State.MdString => MatchMdString(out token),
				State.AccTitle => MatchLineValue(FlowchartTokenKind.AccTitleValue, out token),
				State.AccDescr => MatchLineValue(FlowchartTokenKind.AccDescrValue, out token),
				State.AccDescrMultiline => MatchAccDescrMultiline(out token),
				State.Dir => MatchDir(out token),
				State.Text => MatchText(out token),
				State.EllipseText => MatchEllipseText(out token),
				State.TrapText => MatchTrapText(out token),
				State.EdgeText => MatchEdgeText('-', out token),
				State.ThickEdgeText => MatchEdgeText('=', out token),
				State.DottedEdgeText => MatchEdgeText('.', out token),
				State.Click => MatchClick(out token),
				State.CallbackName => MatchCallbackName(out token),
				State.CallbackArgs => MatchCallbackArgs(out token),
				State.ShapeData => MatchShapeData(out token),
				_ => MatchShapeDataString(out token),
			};

			if (!matched)
			{
				if (start >= _text.Length)
				{
					token = new FlowchartToken(FlowchartTokenKind.End, _text.Length, 0);
					return true;
				}

				token = default;
				return false;
			}

			if (token.Kind != FlowchartTokenKind.End)
			{
				return true;
			}
		}
	}

	// A match that changes the state and returns no token reports End, which Next does not return.
	private readonly bool NoToken(out FlowchartToken token)
	{
		token = new FlowchartToken(FlowchartTokenKind.End, _position, 0);
		return true;
	}

	private bool Emit(FlowchartTokenKind kind, int length, out FlowchartToken token)
	{
		token = new FlowchartToken(kind, _position, length);
		_position += length;
		return true;
	}

	private void Push(State state) => _states.Add(state);

	private void Pop()
	{
		if (_states.Count > 1)
		{
			_states.Truncate(_states.Count - 1);
		}
	}

	private bool MatchInitial(out FlowchartToken token)
	{
		var rest = Rest;
		if (rest.IsEmpty)
		{
			return Emit(FlowchartTokenKind.Eof, 0, out token);
		}

		int length;
		if ((length = AccStatement(rest, "accTitle", ':')) > 0)
		{
			Push(State.AccTitle);
			return Emit(FlowchartTokenKind.AccTitle, length, out token);
		}

		if ((length = AccStatement(rest, "accDescr", ':')) > 0)
		{
			Push(State.AccDescr);
			return Emit(FlowchartTokenKind.AccDescr, length, out token);
		}

		if ((length = AccStatement(rest, "accDescr", '{')) > 0)
		{
			_position += length;
			Push(State.AccDescrMultiline);
			return NoToken(out token);
		}

		if (rest.StartsWith("@{", StringComparison.Ordinal))
		{
			Push(State.ShapeData);
			return Emit(FlowchartTokenKind.ShapeDataStart, 2, out token);
		}

		if (rest.StartsWith("call", StringComparison.Ordinal) && (length = SpaceRun(rest, 4)) > 4)
		{
			_position += length;
			Push(State.CallbackName);
			return NoToken(out token);
		}

		if (MatchQuote(rest, out token))
		{
			return true;
		}

		var keyword = rest[0] switch
		{
			's' when Keyword(rest, "style") => FlowchartTokenKind.Style,
			'd' when Keyword(rest, "default") => FlowchartTokenKind.Default,
			'l' when Keyword(rest, "linkStyle") => FlowchartTokenKind.LinkStyle,
			'i' when Keyword(rest, "interpolate") => FlowchartTokenKind.Interpolate,
			'c' when Keyword(rest, "classDef") => FlowchartTokenKind.ClassDef,
			'c' when Keyword(rest, "class") => FlowchartTokenKind.Class,
			_ => FlowchartTokenKind.End,
		};
		if (keyword != FlowchartTokenKind.End)
		{
			return Emit(keyword, KeywordLength(keyword), out token);
		}

		if (rest.StartsWith("href", StringComparison.Ordinal) && rest.Length > 4 && PreambleParser.IsSpace(rest[4]))
		{
			return Emit(FlowchartTokenKind.Href, 5, out token);
		}

		if (rest.StartsWith("click", StringComparison.Ordinal) && (length = SpaceRun(rest, 5)) > 5)
		{
			_position += length;
			Push(State.Click);
			return NoToken(out token);
		}

		if ((length = GraphKeyword(rest)) > 0)
		{
			if (_firstGraph)
			{
				_firstGraph = false;
				Push(State.Dir);
			}

			return Emit(FlowchartTokenKind.Graph, length, out token);
		}

		if (Keyword(rest, "subgraph"))
		{
			return Emit(FlowchartTokenKind.Subgraph, "subgraph".Length, out token);
		}

		if (Keyword(rest, "end"))
		{
			return Emit(FlowchartTokenKind.EndKeyword, SpaceRun(rest, 3), out token);
		}

		foreach (var target in (ReadOnlySpan<string>)["_self", "_blank", "_parent", "_top"])
		{
			if (Keyword(rest, target))
			{
				return Emit(FlowchartTokenKind.LinkTarget, target.Length, out token);
			}
		}

		if (MatchDirection(out var directionKind, out length))
		{
			return Emit(directionKind, length, out token);
		}

		if ((length = LinkId(rest)) > 0)
		{
			return Emit(FlowchartTokenKind.LinkId, length, out token);
		}

		if (char.IsAsciiDigit(rest[0]))
		{
			length = rest.IndexOfAnyExceptInRange('0', '9');
			return Emit(FlowchartTokenKind.Num, length < 0 ? rest.Length : length, out token);
		}

		switch (rest[0])
		{
			case '#':
				return Emit(FlowchartTokenKind.Brkt, 1, out token);
			case ':':
				return Emit(rest.StartsWith(":::", StringComparison.Ordinal) ? FlowchartTokenKind.StyleSeparator : FlowchartTokenKind.Colon, rest.StartsWith(":::", StringComparison.Ordinal) ? 3 : 1, out token);
			case '&':
				return Emit(FlowchartTokenKind.Amp, 1, out token);
			case ';':
				return Emit(FlowchartTokenKind.Semi, 1, out token);
			case ',':
				return Emit(FlowchartTokenKind.Comma, 1, out token);
			case '*':
				return Emit(FlowchartTokenKind.Mult, 1, out token);
		}

		if ((length = Link(rest, '-')) > 0)
		{
			return Emit(FlowchartTokenKind.Link, length, out token);
		}

		if ((length = StartLink(rest, '-')) > 0)
		{
			Push(State.EdgeText);
			return Emit(FlowchartTokenKind.StartLink, length, out token);
		}

		if ((length = Link(rest, '=')) > 0)
		{
			return Emit(FlowchartTokenKind.Link, length, out token);
		}

		if ((length = StartLink(rest, '=')) > 0)
		{
			Push(State.ThickEdgeText);
			return Emit(FlowchartTokenKind.StartLink, length, out token);
		}

		if ((length = Link(rest, '.')) > 0)
		{
			return Emit(FlowchartTokenKind.Link, length, out token);
		}

		if ((length = StartLink(rest, '.')) > 0)
		{
			Push(State.DottedEdgeText);
			return Emit(FlowchartTokenKind.StartLink, length, out token);
		}

		if ((length = InvisibleLink(rest)) > 0)
		{
			return Emit(FlowchartTokenKind.Link, length, out token);
		}

		if (rest.StartsWith("(-", StringComparison.Ordinal))
		{
			Push(State.EllipseText);
			return Emit(FlowchartTokenKind.EllipseStart, 2, out token);
		}

		if (rest.StartsWith("([", StringComparison.Ordinal))
		{
			return PushText(FlowchartTokenKind.StadiumStart, 2, out token);
		}

		if (rest.StartsWith("[[", StringComparison.Ordinal))
		{
			return PushText(FlowchartTokenKind.SubroutineStart, 2, out token);
		}

		if (rest.StartsWith("[|", StringComparison.Ordinal))
		{
			return Emit(FlowchartTokenKind.VertexWithPropsStart, 2, out token);
		}

		if (rest[0] == '>')
		{
			return PushText(FlowchartTokenKind.TagEnd, 1, out token);
		}

		if (MatchOpeningBrackets(rest, out token))
		{
			return true;
		}

		switch (rest[0])
		{
			case '<':
				return Emit(FlowchartTokenKind.TagStart, 1, out token);
			case '^':
				return Emit(FlowchartTokenKind.Up, 1, out token);
			case '\\' when rest.Length > 1 && rest[1] == '|':
				return Emit(FlowchartTokenKind.Sep, 2, out token);
			case 'v' when Keyword(rest, "v"):
				return Emit(FlowchartTokenKind.Down, 1, out token);
		}

		if ((length = NodeString(rest)) > 0)
		{
			return Emit(FlowchartTokenKind.NodeString, length, out token);
		}

		if (rest[0] == '-')
		{
			return Emit(FlowchartTokenKind.Minus, 1, out token);
		}

		if (IsUnicodeText(rest[0]))
		{
			return Emit(FlowchartTokenKind.UnicodeText, 1, out token);
		}

		switch (rest[0])
		{
			case '|':
				return PushText(FlowchartTokenKind.Pipe, 1, out token);
			case '(':
				return PushText(FlowchartTokenKind.Ps, 1, out token);
			case '[':
				return PushText(FlowchartTokenKind.Sqs, 1, out token);
			case '{':
				return PushText(FlowchartTokenKind.DiamondStart, 1, out token);
			case '\n':
				return Emit(FlowchartTokenKind.NewLine, NewLineRun(rest), out token);
		}

		length = 0;
		while (length < rest.Length && rest[length] is not '\n' and not '\r' && PreambleParser.IsSpace(rest[length]))
		{
			length++;
		}

		if (length > 0)
		{
			return Emit(FlowchartTokenKind.Space, length, out token);
		}

		token = default;
		return false;
	}

	/// <summary>The <c>&lt;*&gt;</c> rules for a quote: <c>"`</c> starts a Markdown string and <c>"</c> a string.</summary>
	private bool MatchQuote(ReadOnlySpan<char> rest, out FlowchartToken token)
	{
		if (!rest.IsEmpty && rest[0] == '"')
		{
			_position += rest.Length > 1 && rest[1] == '`' ? 2 : 1;
			Push(rest.Length > 1 && rest[1] == '`' ? State.MdString : State.String);
			return NoToken(out token);
		}

		token = default;
		return false;
	}

	/// <summary>The <c>&lt;*&gt;</c> rules that open a bracket shape, in the grammar's order, except <c>|</c>, <c>(</c>, <c>[</c> and <c>{</c>.</summary>
	private bool MatchOpeningBrackets(ReadOnlySpan<char> rest, out FlowchartToken token)
	{
		if (rest.StartsWith("[(", StringComparison.Ordinal))
		{
			return PushText(FlowchartTokenKind.CylinderStart, 2, out token);
		}

		if (rest.StartsWith("(((", StringComparison.Ordinal))
		{
			return PushText(FlowchartTokenKind.DoubleCircleStart, 3, out token);
		}

		if (rest.StartsWith("[/", StringComparison.Ordinal))
		{
			Push(State.TrapText);
			return Emit(FlowchartTokenKind.TrapStart, 2, out token);
		}

		if (rest.StartsWith("[\\", StringComparison.Ordinal))
		{
			Push(State.TrapText);
			return Emit(FlowchartTokenKind.InvTrapStart, 2, out token);
		}

		token = default;
		return false;
	}

	/// <summary>
	/// The <c>&lt;*&gt;</c> rules every state but <c>string</c>, <c>md_string</c> and the edge text states reaches after its
	/// own: <c>~~~</c>, <c>(-</c>, <c>([</c>, <c>[[</c>, <c>[(</c>, <c>(((</c>, <c>[/</c>, <c>[\</c>, <c>|</c>, <c>(</c>,
	/// <c>[</c> and <c>{</c>. <paramref name="closing"/> holds the state's own closing rules, which the grammar lists
	/// between them.
	/// </summary>
	private bool MatchSharedRules(ReadOnlySpan<char> rest, bool text, out FlowchartToken token)
	{
		int length;
		if ((length = InvisibleLink(rest)) > 0)
		{
			return Emit(FlowchartTokenKind.Link, length, out token);
		}

		if (rest.StartsWith("(-", StringComparison.Ordinal))
		{
			Push(State.EllipseText);
			return Emit(FlowchartTokenKind.EllipseStart, 2, out token);
		}

		if (text && rest.StartsWith("])", StringComparison.Ordinal))
		{
			Pop();
			return Emit(FlowchartTokenKind.StadiumEnd, 2, out token);
		}

		if (rest.StartsWith("([", StringComparison.Ordinal))
		{
			return PushText(FlowchartTokenKind.StadiumStart, 2, out token);
		}

		if (text && rest.StartsWith("]]", StringComparison.Ordinal))
		{
			Pop();
			return Emit(FlowchartTokenKind.SubroutineEnd, 2, out token);
		}

		if (rest.StartsWith("[[", StringComparison.Ordinal))
		{
			return PushText(FlowchartTokenKind.SubroutineStart, 2, out token);
		}

		if (text && rest.StartsWith(")]", StringComparison.Ordinal))
		{
			Pop();
			return Emit(FlowchartTokenKind.CylinderEnd, 2, out token);
		}

		if (rest.StartsWith("[(", StringComparison.Ordinal))
		{
			return PushText(FlowchartTokenKind.CylinderStart, 2, out token);
		}

		if (text && rest.StartsWith(")))", StringComparison.Ordinal))
		{
			Pop();
			return Emit(FlowchartTokenKind.DoubleCircleEnd, 3, out token);
		}

		if (rest.StartsWith("(((", StringComparison.Ordinal))
		{
			return PushText(FlowchartTokenKind.DoubleCircleStart, 3, out token);
		}

		token = default;
		return false;
	}

	/// <summary>The <c>&lt;*&gt;</c> rules after the trapezoid ones: <c>[/</c>, <c>[\</c>, then <c>|</c>, <c>(</c>, <c>[</c> and <c>{</c> with the <c>text</c> state's closing rules between them.</summary>
	private bool MatchLastSharedRules(ReadOnlySpan<char> rest, bool text, out FlowchartToken token)
	{
		if (rest.StartsWith("[/", StringComparison.Ordinal))
		{
			Push(State.TrapText);
			return Emit(FlowchartTokenKind.TrapStart, 2, out token);
		}

		if (rest.StartsWith("[\\", StringComparison.Ordinal))
		{
			Push(State.TrapText);
			return Emit(FlowchartTokenKind.InvTrapStart, 2, out token);
		}

		switch (rest[0])
		{
			case '|' when text:
				Pop();
				return Emit(FlowchartTokenKind.Pipe, 1, out token);
			case '|':
				return PushText(FlowchartTokenKind.Pipe, 1, out token);
			case ')' when text:
				Pop();
				return Emit(FlowchartTokenKind.Pe, 1, out token);
			case '(':
				return PushText(FlowchartTokenKind.Ps, 1, out token);
			case ']' when text:
				Pop();
				return Emit(FlowchartTokenKind.Sqe, 1, out token);
			case '[':
				return PushText(FlowchartTokenKind.Sqs, 1, out token);
			case '}' when text:
				Pop();
				return Emit(FlowchartTokenKind.DiamondStop, 1, out token);
			case '{':
				return PushText(FlowchartTokenKind.DiamondStart, 1, out token);
		}

		token = default;
		return false;
	}

	private bool PushText(FlowchartTokenKind kind, int length, out FlowchartToken token)
	{
		Push(State.Text);
		return Emit(kind, length, out token);
	}

	/// <summary>
	/// <c>&lt;string&gt;</c>: <c>"`</c> starts a Markdown string, <c>[^"]+</c> is <c>STR</c> and <c>"</c> ends the
	/// string.
	/// </summary>
	private bool MatchString(out FlowchartToken token)
	{
		var rest = Rest;
		if (rest.IsEmpty)
		{
			token = default;
			return false;
		}

		if (rest[0] != '"')
		{
			var length = rest.IndexOf('"');
			return Emit(FlowchartTokenKind.Str, length < 0 ? rest.Length : length, out token);
		}

		if (rest.Length > 1 && rest[1] == '`')
		{
			_position += 2;
			Push(State.MdString);
			return NoToken(out token);
		}

		_position++;
		Pop();
		return NoToken(out token);
	}

	/// <summary><c>&lt;md_string&gt;</c>: <c>[^`"]+</c> is <c>MD_STR</c>, <c>`"</c> ends the string, and a quote starts another.</summary>
	private bool MatchMdString(out FlowchartToken token)
	{
		var rest = Rest;
		if (rest.IsEmpty)
		{
			token = default;
			return false;
		}

		var length = rest.IndexOfAny('`', '"');
		if (length != 0)
		{
			return Emit(FlowchartTokenKind.MdStr, length < 0 ? rest.Length : length, out token);
		}

		if (rest.StartsWith("`\"", StringComparison.Ordinal))
		{
			_position += 2;
			Pop();
			return NoToken(out token);
		}

		if (MatchQuote(rest, out token))
		{
			return true;
		}

		// A backtick that does not end the string matches no rule.
		token = default;
		return false;
	}

	/// <summary><c>(?!\n|;|#)*[^\n]*</c>: the rest of the line, which may be empty.</summary>
	private bool MatchLineValue(FlowchartTokenKind kind, out FlowchartToken token)
	{
		var length = Rest.IndexOf('\n');
		Pop();
		return Emit(kind, length < 0 ? Rest.Length : length, out token);
	}

	private bool MatchAccDescrMultiline(out FlowchartToken token)
	{
		var rest = Rest;
		if (!rest.IsEmpty && rest[0] == '}')
		{
			_position++;
			Pop();
			return NoToken(out token);
		}

		var length = rest.IndexOf('}');
		return Emit(FlowchartTokenKind.AccDescrMultilineValue, length < 0 ? rest.Length : length, out token);
	}

	/// <summary>
	/// <c>&lt;dir&gt;</c>: <c>(\r?\n)*\s*\n</c> is <c>NODIR</c>, and a direction after optional space is <c>DIR</c>. Any
	/// other token after the graph keyword fails to parse, so it is reported as a lexical error here.
	/// </summary>
	private bool MatchDir(out FlowchartToken token)
	{
		var rest = Rest;
		var space = 0;
		var lastBreak = -1;
		while (space < rest.Length && PreambleParser.IsSpace(rest[space]))
		{
			if (rest[space] == '\n')
			{
				lastBreak = space;
			}

			space++;
		}

		if (lastBreak >= 0)
		{
			Pop();
			return Emit(FlowchartTokenKind.NoDir, lastBreak + 1, out token);
		}

		var direction = rest[space..];
		var length = direction switch
		{
			_ when Keyword(direction, "LR") || Keyword(direction, "RL") || Keyword(direction, "TB") || Keyword(direction, "BT") || Keyword(direction, "TD") || Keyword(direction, "BR") => 2,
			['<' or '>' or '^', ..] => 1,
			_ when Keyword(direction, "v") => 1,
			_ => 0,
		};
		if (length > 0)
		{
			Pop();
			return Emit(FlowchartTokenKind.Dir, space + length, out token);
		}

		token = default;
		return false;
	}

	private bool MatchText(out FlowchartToken token)
	{
		var rest = Rest;
		if (rest.IsEmpty)
		{
			token = default;
			return false;
		}

		if (MatchQuote(rest, out token) || MatchSharedRules(rest, text: true, out token) || MatchLastSharedRules(rest, text: true, out token))
		{
			return true;
		}

		var length = rest.IndexOfAny(TextStops);
		if (length == 0)
		{
			return false;
		}

		return Emit(FlowchartTokenKind.Text, length < 0 ? rest.Length : length, out token);
	}

	/// <summary>
	/// <c>&lt;ellipseText&gt;</c>: <c>[-/\)][\)]</c> ends the shape, and any one character but a bracket is <c>TEXT</c>.
	/// </summary>
	private bool MatchEllipseText(out FlowchartToken token)
	{
		var rest = Rest;
		if (rest.IsEmpty)
		{
			token = default;
			return false;
		}

		if (MatchQuote(rest, out token))
		{
			return true;
		}

		var link = InvisibleLink(rest);
		if (link > 0)
		{
			return Emit(FlowchartTokenKind.Link, link, out token);
		}

		if (rest.Length > 1 && rest[0] is '-' or '/' or ')' && rest[1] == ')')
		{
			Pop();
			return Emit(FlowchartTokenKind.EllipseEnd, 2, out token);
		}

		if (rest[0] is not '(' and not ')' and not '[' and not ']' and not '{' and not '}')
		{
			return Emit(FlowchartTokenKind.Text, 1, out token);
		}

		return MatchSharedRules(rest, text: false, out token) || MatchLastSharedRules(rest, text: false, out token);
	}

	/// <summary>
	/// <c>&lt;trapText&gt;</c>: one of <c>\(?=])</c> before <c>]</c> is <c>TRAPEND</c>, <c>/]</c> is <c>INVTRAPEND</c>, and
	/// <c>/</c> or <c>\</c> not before <c>]</c>, or a run without brackets and slashes, is <c>TEXT</c>.
	/// </summary>
	private bool MatchTrapText(out FlowchartToken token)
	{
		var rest = Rest;
		if (rest.IsEmpty)
		{
			token = default;
			return false;
		}

		if (MatchQuote(rest, out token) || MatchSharedRules(rest, text: false, out token))
		{
			return true;
		}

		if (rest.Length > 1 && rest[0] is '\\' or '(' or '?' or '=' or ']' or ')' && rest[1] == ']')
		{
			Pop();
			return Emit(FlowchartTokenKind.TrapEnd, 2, out token);
		}

		if (rest.StartsWith("/]", StringComparison.Ordinal))
		{
			Pop();
			return Emit(FlowchartTokenKind.InvTrapEnd, 2, out token);
		}

		if (rest[0] is '/' or '\\')
		{
			return Emit(FlowchartTokenKind.Text, 1, out token);
		}

		var run = rest.IndexOfAny(TrapTextStops);
		if (run != 0)
		{
			return Emit(FlowchartTokenKind.Text, run < 0 ? rest.Length : run, out token);
		}

		return MatchLastSharedRules(rest, text: false, out token);
	}

	/// <summary>
	/// The edge text states. The closing link of the kind pops the state, and every other character is <c>EDGE_TEXT</c>,
	/// except one that starts the closing run of <paramref name="line"/> characters. Consecutive characters make one
	/// token, which the grammar would concatenate.
	/// </summary>
	private bool MatchEdgeText(char line, out FlowchartToken token)
	{
		var rest = Rest;
		if (rest.IsEmpty)
		{
			token = default;
			return false;
		}

		if (MatchQuote(rest, out token))
		{
			return true;
		}

		var link = Link(rest, line);
		if (link > 0)
		{
			Pop();
			return Emit(FlowchartTokenKind.Link, link, out token);
		}

		var end = 0;
		while (end < rest.Length && rest[end] != '"' && (end == 0 || Link(rest[end..], line) == 0))
		{
			if (!IsEdgeTextChar(rest, end, line))
			{
				break;
			}

			end++;
		}

		if (end == 0)
		{
			token = default;
			return false;
		}

		return Emit(FlowchartTokenKind.EdgeText, end, out token);
	}

	/// <summary><c>[^-]|\-(?!\-)</c>, <c>[^=]|\=(?!=)</c> or <c>[^\.]|\.(?!-)</c>.</summary>
	private static bool IsEdgeTextChar(ReadOnlySpan<char> rest, int at, char line)
	{
		if (rest[at] != line)
		{
			return true;
		}

		var follower = line == '.' ? '-' : line;
		return at + 1 >= rest.Length || rest[at + 1] != follower;
	}

	/// <summary><c>&lt;click&gt;</c>: one space ends the state, and a run of other characters is <c>CLICK</c>.</summary>
	private bool MatchClick(out FlowchartToken token)
	{
		var rest = Rest;
		if (!rest.IsEmpty && PreambleParser.IsSpace(rest[0]))
		{
			_position++;
			Pop();
			return NoToken(out token);
		}

		var length = 0;
		while (length < rest.Length && !PreambleParser.IsSpace(rest[length]))
		{
			length++;
		}

		return Emit(FlowchartTokenKind.Click, length, out token);
	}

	private bool MatchCallbackName(out FlowchartToken token)
	{
		var rest = Rest;
		if (!rest.IsEmpty && rest[0] == '(')
		{
			var close = 1;
			while (close < rest.Length && PreambleParser.IsSpace(rest[close]))
			{
				close++;
			}

			Pop();
			if (close < rest.Length && rest[close] == ')')
			{
				_position += close + 1;
				return NoToken(out token);
			}

			_position++;
			Push(State.CallbackArgs);
			return NoToken(out token);
		}

		var length = rest.IndexOf('(');
		return Emit(FlowchartTokenKind.CallbackName, length < 0 ? rest.Length : length, out token);
	}

	private bool MatchCallbackArgs(out FlowchartToken token)
	{
		var rest = Rest;
		if (!rest.IsEmpty && rest[0] == ')')
		{
			_position++;
			Pop();
			return NoToken(out token);
		}

		var length = rest.IndexOf(')');
		return Emit(FlowchartTokenKind.CallbackArgs, length < 0 ? rest.Length : length, out token);
	}

	/// <summary><c>&lt;shapeData&gt;</c>: <c>"</c> starts a string, <c>[^}^"]+</c> is data and <c>}</c> ends the block. <c>^</c> matches no rule.</summary>
	private bool MatchShapeData(out FlowchartToken token)
	{
		var rest = Rest;
		if (rest.IsEmpty)
		{
			token = default;
			return false;
		}

		switch (rest[0])
		{
			case '"':
				Push(State.ShapeDataString);
				return Emit(FlowchartTokenKind.ShapeData, 1, out token);
			case '}':
				_position++;
				Pop();
				return NoToken(out token);
			case '^':
				token = default;
				return false;
		}

		var length = rest.IndexOfAny('}', '^', '"');
		return Emit(FlowchartTokenKind.ShapeData, length < 0 ? rest.Length : length, out token);
	}

	private bool MatchShapeDataString(out FlowchartToken token)
	{
		var rest = Rest;
		if (rest.IsEmpty)
		{
			token = default;
			return false;
		}

		if (rest[0] == '"')
		{
			Pop();
			return Emit(FlowchartTokenKind.ShapeData, 1, out token);
		}

		var length = rest.IndexOf('"');
		return Emit(FlowchartTokenKind.ShapeDataString, length < 0 ? rest.Length : length, out token);
	}

	/// <summary>
	/// <c>.*direction\s+XX[^\n]*</c> for <c>TB</c>, <c>BT</c>, <c>RL</c>, <c>LR</c> and <c>TD</c>, in that order. The rule
	/// matches from the token's start when <c>direction</c> occurs anywhere later on its line.
	/// </summary>
	private bool MatchDirection(out FlowchartTokenKind kind, out int length)
	{
		kind = FlowchartTokenKind.End;
		length = 0;
		if (_position > _directionLineEnd)
		{
			var lineEnd = Rest.IndexOfAny(LineTerminators);
			_directionLineEnd = lineEnd < 0 ? _text.Length : _position + lineEnd;
			var last = _text[_position.._directionLineEnd].LastIndexOf("direction", StringComparison.Ordinal);
			_lastDirection = last < 0 ? -1 : _position + last;
		}

		if (_lastDirection < _position)
		{
			return false;
		}

		ReadOnlySpan<FlowchartTokenKind> kinds = [FlowchartTokenKind.DirectionTb, FlowchartTokenKind.DirectionBt, FlowchartTokenKind.DirectionRl, FlowchartTokenKind.DirectionLr, FlowchartTokenKind.DirectionTd];
		ReadOnlySpan<string> names = ["TB", "BT", "RL", "LR", "TD"];
		var line = _text[_position.._directionLineEnd];
		for (var rule = 0; rule < kinds.Length; rule++)
		{
			for (var at = line.LastIndexOf("direction", StringComparison.Ordinal); at >= 0; at = line[..at].LastIndexOf("direction", StringComparison.Ordinal))
			{
				var after = _position + at + "direction".Length;
				var space = after;
				while (space < _text.Length && PreambleParser.IsSpace(_text[space]))
				{
					space++;
				}

				if (space > after && _text[space..].StartsWith(names[rule], StringComparison.Ordinal))
				{
					var end = _text[space..].IndexOf('\n');
					kind = kinds[rule];
					length = (end < 0 ? _text.Length : space + end) - _position;
					return true;
				}
			}
		}

		return false;
	}

	/// <summary><c>accTitle\s*:\s*</c>, <c>accDescr\s*:\s*</c> or <c>accDescr\s*{\s*</c>.</summary>
	private static int AccStatement(ReadOnlySpan<char> rest, string name, char mark)
	{
		if (!rest.StartsWith(name, StringComparison.Ordinal))
		{
			return 0;
		}

		var at = SpaceRun(rest, name.Length);
		return at < rest.Length && rest[at] == mark ? SpaceRun(rest, at + 1) : 0;
	}

	/// <summary>The offset after the run of <c>\s</c> from <paramref name="at"/>.</summary>
	private static int SpaceRun(ReadOnlySpan<char> text, int at)
	{
		while (at < text.Length && PreambleParser.IsSpace(text[at]))
		{
			at++;
		}

		return at;
	}

	/// <summary>A keyword followed by <c>\b</c>.</summary>
	private static bool Keyword(ReadOnlySpan<char> rest, string keyword) =>
		rest.StartsWith(keyword, StringComparison.Ordinal)
		&& (rest.Length == keyword.Length || !FlowchartText.IsWordChar(rest[keyword.Length]) || !FlowchartText.IsWordChar(keyword[^1]));

	private static int KeywordLength(FlowchartTokenKind kind) => kind switch
	{
		FlowchartTokenKind.Style => "style".Length,
		FlowchartTokenKind.Default => "default".Length,
		FlowchartTokenKind.LinkStyle => "linkStyle".Length,
		FlowchartTokenKind.Interpolate => "interpolate".Length,
		FlowchartTokenKind.ClassDef => "classDef".Length,
		_ => "class".Length,
	};

	private static int GraphKeyword(ReadOnlySpan<char> rest)
	{
		foreach (var keyword in (ReadOnlySpan<string>)["flowchart-elk", "swimlane-beta", "graph", "flowchart"])
		{
			if (Keyword(rest, keyword))
			{
				return keyword.Length;
			}
		}

		return 0;
	}

	/// <summary>
	/// <c>[^\s\"\[\]\(\)\{\}\|]+\@(?=[^\{\"])</c>: the longest run of those characters that ends with an <c>@</c> before a
	/// character other than <c>{</c> and <c>"</c>.
	/// </summary>
	private static int LinkId(ReadOnlySpan<char> rest)
	{
		var run = 0;
		while (run < rest.Length && !PreambleParser.IsSpace(rest[run]) && rest[run] is not '"' and not '[' and not ']' and not '(' and not ')' and not '{' and not '}' and not '|')
		{
			run++;
		}

		for (var at = run - 1; at >= 1; at--)
		{
			if (rest[at] == '@' && at + 1 < rest.Length && rest[at + 1] is not '{' and not '"')
			{
				return at + 1;
			}
		}

		return 0;
	}

	/// <summary>
	/// The links that end the edge text states: <c>\s*[xo&lt;]?\-\-+[-xo&gt;]\s*</c>, <c>\s*[xo&lt;]?\=\=+[=xo&gt;]\s*</c>
	/// and <c>\s*[xo&lt;]?\-?\.+\-[xo&gt;]?\s*</c>.
	/// </summary>
	private static int Link(ReadOnlySpan<char> rest, char line)
	{
		var at = SpaceRun(rest, 0);
		if (at < rest.Length && rest[at] is 'x' or 'o' or '<')
		{
			var length = LinkBody(rest, at + 1, line);
			if (length > 0)
			{
				return length;
			}
		}

		return LinkBody(rest, at, line);
	}

	private static int LinkBody(ReadOnlySpan<char> rest, int at, char line)
	{
		int end;
		if (line == '.')
		{
			var dots = at < rest.Length && rest[at] == '-' && at + 1 < rest.Length && rest[at + 1] == '.' ? at + 1 : at;
			end = dots;
			while (end < rest.Length && rest[end] == '.')
			{
				end++;
			}

			if (end == dots || end >= rest.Length || rest[end] != '-')
			{
				return 0;
			}

			end++;
			if (end < rest.Length && rest[end] is 'x' or 'o' or '>')
			{
				end++;
			}

			return SpaceRun(rest, end);
		}

		end = at;
		while (end < rest.Length && rest[end] == line)
		{
			end++;
		}

		var run = end - at;
		if (run >= 2 && end < rest.Length && rest[end] is 'x' or 'o' or '>')
		{
			return SpaceRun(rest, end + 1);
		}

		return run >= 3 ? SpaceRun(rest, end) : 0;
	}

	/// <summary><c>\s*[xo&lt;]?\-\-\s*</c>, <c>\s*[xo&lt;]?\=\=\s*</c> or <c>\s*[xo&lt;]?\-\.\s*</c>.</summary>
	private static int StartLink(ReadOnlySpan<char> rest, char line)
	{
		var at = SpaceRun(rest, 0);
		if (at < rest.Length && rest[at] is 'x' or 'o' or '<')
		{
			var length = StartLinkBody(rest, at + 1, line);
			if (length > 0)
			{
				return length;
			}
		}

		return StartLinkBody(rest, at, line);
	}

	private static int StartLinkBody(ReadOnlySpan<char> rest, int at, char line)
	{
		var second = line == '.' ? '.' : line;
		var first = line == '.' ? '-' : line;
		return at + 1 < rest.Length && rest[at] == first && rest[at + 1] == second ? SpaceRun(rest, at + 2) : 0;
	}

	/// <summary><c>\s*\~\~[\~]+\s*</c>.</summary>
	private static int InvisibleLink(ReadOnlySpan<char> rest)
	{
		var at = SpaceRun(rest, 0);
		var end = at;
		while (end < rest.Length && rest[end] == '~')
		{
			end++;
		}

		return end - at >= 3 ? SpaceRun(rest, end) : 0;
	}

	/// <summary><c>([A-Za-z0-9!"\#$%&amp;'*+\.`?\\_\/]|\-(?=[^\&gt;\-\.])|=(?!=))+</c>.</summary>
	private static int NodeString(ReadOnlySpan<char> rest)
	{
		var length = 0;
		while (length < rest.Length)
		{
			var c = rest[length];
			if (char.IsAsciiLetterOrDigit(c) || NodeStringChars.Contains(c))
			{
				length++;
			}
			else if (c == '-' && length + 1 < rest.Length && rest[length + 1] is not '>' and not '-' and not '.')
			{
				length++;
			}
			else if (c == '=' && (length + 1 >= rest.Length || rest[length + 1] != '='))
			{
				length++;
			}
			else
			{
				break;
			}
		}

		return length;
	}

	private static int NewLineRun(ReadOnlySpan<char> rest)
	{
		var length = rest.IndexOfAnyExcept('\n');
		return length < 0 ? rest.Length : length;
	}

	/// <summary>The <c>UNICODE_TEXT</c> class of <c>flow.jison</c>: letters outside ASCII, one character per token.</summary>
	public static bool IsUnicodeText(char c)
	{
		if (c < '\u00AA')
		{
			return false;
		}

		var ranges = UnicodeTextRanges;
		int low = 0, high = (ranges.Length / 2) - 1;
		while (low <= high)
		{
			var middle = (low + high) / 2;
			if (c < ranges[middle * 2])
			{
				high = middle - 1;
			}
			else if (c > ranges[(middle * 2) + 1])
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

	private static ReadOnlySpan<char> NodeStringChars => "!\"#$%&'*+.`?\\_/";

	private static ReadOnlySpan<char> TextStops => "[](){}|\"";

	private static ReadOnlySpan<char> TrapTextStops => "\\[](){}/";

	private static ReadOnlySpan<char> LineTerminators => "\n\r\u2028\u2029";

	// Pairs of first and last character, sorted, from the UNICODE_TEXT rule.
	private static ReadOnlySpan<char> UnicodeTextRanges =>
		"\u00AA\u00AA\u00B5\u00B5\u00BA\u00BA\u00C0\u00D6\u00D8\u00F6\u00F8\u02C1\u02C6\u02D1\u02E0\u02E4\u02EC\u02EC" +
		"\u02EE\u02EE\u0370\u0374\u0376\u0377\u037A\u037D\u0386\u0386\u0388\u038A\u038C\u038C\u038E\u03A1\u03A3\u03F5" +
		"\u03F7\u0481\u048A\u0527\u0531\u0556\u0559\u0559\u0561\u0587\u05D0\u05EA\u05F0\u05F2\u0620\u064A\u066E\u066F" +
		"\u0671\u06D3\u06D5\u06D5\u06E5\u06E6\u06EE\u06EF\u06FA\u06FC\u06FF\u06FF\u0710\u0710\u0712\u072F\u074D\u07A5" +
		"\u07B1\u07B1\u07CA\u07EA\u07F4\u07F5\u07FA\u07FA\u0800\u0815\u081A\u081A\u0824\u0824\u0828\u0828\u0840\u0858" +
		"\u08A0\u08A0\u08A2\u08AC\u0904\u0939\u093D\u093D\u0950\u0950\u0958\u0961\u0971\u0977\u0979\u097F\u0985\u098C" +
		"\u098F\u0990\u0993\u09A8\u09AA\u09B0\u09B2\u09B2\u09B6\u09B9\u09BD\u09BD\u09CE\u09CE\u09DC\u09DD\u09DF\u09E1" +
		"\u09F0\u09F1\u0A05\u0A0A\u0A0F\u0A10\u0A13\u0A28\u0A2A\u0A30\u0A32\u0A33\u0A35\u0A36\u0A38\u0A39\u0A59\u0A5C" +
		"\u0A5E\u0A5E\u0A72\u0A74\u0A85\u0A8D\u0A8F\u0A91\u0A93\u0AA8\u0AAA\u0AB0\u0AB2\u0AB3\u0AB5\u0AB9\u0ABD\u0ABD" +
		"\u0AD0\u0AD0\u0AE0\u0AE1\u0B05\u0B0C\u0B0F\u0B10\u0B13\u0B28\u0B2A\u0B30\u0B32\u0B33\u0B35\u0B39\u0B3D\u0B3D" +
		"\u0B5C\u0B5D\u0B5F\u0B61\u0B71\u0B71\u0B83\u0B83\u0B85\u0B8A\u0B8E\u0B90\u0B92\u0B95\u0B99\u0B9A\u0B9C\u0B9C" +
		"\u0B9E\u0B9F\u0BA3\u0BA4\u0BA8\u0BAA\u0BAE\u0BB9\u0BD0\u0BD0\u0C05\u0C0C\u0C0E\u0C10\u0C12\u0C28\u0C2A\u0C33" +
		"\u0C35\u0C39\u0C3D\u0C3D\u0C58\u0C59\u0C60\u0C61\u0C85\u0C8C\u0C8E\u0C90\u0C92\u0CA8\u0CAA\u0CB3\u0CB5\u0CB9" +
		"\u0CBD\u0CBD\u0CDE\u0CDE\u0CE0\u0CE1\u0CF1\u0CF2\u0D05\u0D0C\u0D0E\u0D10\u0D12\u0D3A\u0D3D\u0D3D\u0D4E\u0D4E" +
		"\u0D60\u0D61\u0D7A\u0D7F\u0D85\u0D96\u0D9A\u0DB1\u0DB3\u0DBB\u0DBD\u0DBD\u0DC0\u0DC6\u0E01\u0E30\u0E32\u0E33" +
		"\u0E40\u0E46\u0E81\u0E82\u0E84\u0E84\u0E87\u0E88\u0E8A\u0E8A\u0E8D\u0E8D\u0E94\u0E97\u0E99\u0E9F\u0EA1\u0EA3" +
		"\u0EA5\u0EA5\u0EA7\u0EA7\u0EAA\u0EAB\u0EAD\u0EB0\u0EB2\u0EB3\u0EBD\u0EBD\u0EC0\u0EC4\u0EC6\u0EC6\u0EDC\u0EDF" +
		"\u0F00\u0F00\u0F40\u0F47\u0F49\u0F6C\u0F88\u0F8C\u1000\u102A\u103F\u103F\u1050\u1055\u105A\u105D\u1061\u1061" +
		"\u1065\u1066\u106E\u1070\u1075\u1081\u108E\u108E\u10A0\u10C5\u10C7\u10C7\u10CD\u10CD\u10D0\u10FA\u10FC\u1248" +
		"\u124A\u124D\u1250\u1256\u1258\u1258\u125A\u125D\u1260\u1288\u128A\u128D\u1290\u12B0\u12B2\u12B5\u12B8\u12BE" +
		"\u12C0\u12C0\u12C2\u12C5\u12C8\u12D6\u12D8\u1310\u1312\u1315\u1318\u135A\u1380\u138F\u13A0\u13F4\u1401\u166C" +
		"\u166F\u167F\u1681\u169A\u16A0\u16EA\u1700\u170C\u170E\u1711\u1720\u1731\u1740\u1751\u1760\u176C\u176E\u1770" +
		"\u1780\u17B3\u17D7\u17D7\u17DC\u17DC\u1820\u1877\u1880\u18A8\u18AA\u18AA\u18B0\u18F5\u1900\u191C\u1950\u196D" +
		"\u1970\u1974\u1980\u19AB\u19C1\u19C7\u1A00\u1A16\u1A20\u1A54\u1AA7\u1AA7\u1B05\u1B33\u1B45\u1B4B\u1B83\u1BA0" +
		"\u1BAE\u1BAF\u1BBA\u1BE5\u1C00\u1C23\u1C4D\u1C4F\u1C5A\u1C7D\u1CE9\u1CEC\u1CEE\u1CF1\u1CF5\u1CF6\u1D00\u1DBF" +
		"\u1E00\u1F15\u1F18\u1F1D\u1F20\u1F45\u1F48\u1F4D\u1F50\u1F57\u1F59\u1F59\u1F5B\u1F5B\u1F5D\u1F5D\u1F5F\u1F7D" +
		"\u1F80\u1FB4\u1FB6\u1FBC\u1FBE\u1FBE\u1FC2\u1FC4\u1FC6\u1FCC\u1FD0\u1FD3\u1FD6\u1FDB\u1FE0\u1FEC\u1FF2\u1FF4" +
		"\u1FF6\u1FFC\u2071\u2071\u207F\u207F\u2090\u209C\u2102\u2102\u2107\u2107\u210A\u2113\u2115\u2115\u2119\u211D" +
		"\u2124\u2124\u2126\u2126\u2128\u2128\u212A\u212D\u212F\u2139\u213C\u213F\u2145\u2149\u214E\u214E\u2183\u2184" +
		"\u2C00\u2C2E\u2C30\u2C5E\u2C60\u2CE4\u2CEB\u2CEE\u2CF2\u2CF3\u2D00\u2D25\u2D27\u2D27\u2D2D\u2D2D\u2D30\u2D67" +
		"\u2D6F\u2D6F\u2D80\u2D96\u2DA0\u2DA6\u2DA8\u2DAE\u2DB0\u2DB6\u2DB8\u2DBE\u2DC0\u2DC6\u2DC8\u2DCE\u2DD0\u2DD6" +
		"\u2DD8\u2DDE\u2E2F\u2E2F\u3005\u3006\u3031\u3035\u303B\u303C\u3041\u3096\u309D\u309F\u30A1\u30FA\u30FC\u30FF" +
		"\u3105\u312D\u3131\u318E\u31A0\u31BA\u31F0\u31FF\u3400\u4DB5\u4E00\u9FCC\uA000\uA48C\uA4D0\uA4FD\uA500\uA60C" +
		"\uA610\uA61F\uA62A\uA62B\uA640\uA66E\uA67F\uA697\uA6A0\uA6E5\uA717\uA71F\uA722\uA788\uA78B\uA78E\uA790\uA793" +
		"\uA7A0\uA7AA\uA7F8\uA801\uA803\uA805\uA807\uA80A\uA80C\uA822\uA840\uA873\uA882\uA8B3\uA8F2\uA8F7\uA8FB\uA8FB" +
		"\uA90A\uA925\uA930\uA946\uA960\uA97C\uA984\uA9B2\uA9CF\uA9CF\uAA00\uAA28\uAA40\uAA42\uAA44\uAA4B\uAA60\uAA76" +
		"\uAA7A\uAA7A\uAA80\uAAAF\uAAB1\uAAB1\uAAB5\uAAB6\uAAB9\uAABD\uAAC0\uAAC0\uAAC2\uAAC2\uAADB\uAADD\uAAE0\uAAEA" +
		"\uAAF2\uAAF4\uAB01\uAB06\uAB09\uAB0E\uAB11\uAB16\uAB20\uAB26\uAB28\uAB2E\uABC0\uABE2\uAC00\uD7A3\uD7B0\uD7C6" +
		"\uD7CB\uD7FB\uF900\uFA6D\uFA70\uFAD9\uFB00\uFB06\uFB13\uFB17\uFB1D\uFB1D\uFB1F\uFB28\uFB2A\uFB36\uFB38\uFB3C" +
		"\uFB3E\uFB3E\uFB40\uFB41\uFB43\uFB44\uFB46\uFBB1\uFBD3\uFD3D\uFD50\uFD8F\uFD92\uFDC7\uFDF0\uFDFB\uFE70\uFE74" +
		"\uFE76\uFEFC\uFF21\uFF3A\uFF41\uFF5A\uFF66\uFFBE\uFFC2\uFFC7\uFFCA\uFFCF\uFFD2\uFFD7\uFFDA\uFFDC";
}
