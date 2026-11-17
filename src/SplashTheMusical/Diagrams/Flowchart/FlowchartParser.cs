// Upstream grammar: packages/mermaid/src/diagrams/flowchart/parser/flow.jison
using System.Globalization;
using SplashTheMusical.Parsing;
using Kind = SplashTheMusical.Diagrams.Flowchart.FlowchartTokenKind;

namespace SplashTheMusical.Diagrams.Flowchart;

/// <summary>
/// The parser of <c>flow.jison</c> at mermaid 12.3.0, with the actions of <c>flowDb.ts</c>. It reads the tokens of
/// <see cref="FlowchartLexer"/> by recursive descent over the grammar:
/// <code>
/// start: graphConfig document
/// graphConfig: (SPACE | NEWLINE)* GRAPH (NODIR | DIR FirstStmtSeparator)
/// line: statement | SEMI | NEWLINE | SPACE | EOF
/// statement: vertexStatement separator | styleStatement separator | linkStyleStatement separator
///     | classDefStatement separator | classStatement separator | clickStatement separator
///     | subgraph SPACE textNoTags SQS text SQE separator document end
///     | subgraph SPACE textNoTags separator document end | subgraph separator document end
///     | direction | acc_title acc_title_value | acc_descr acc_descr_value | acc_descr_multiline_value
/// vertexStatement: node shapeData? spaceList? (link node shapeData? spaceList?)*
/// node: styledVertex (shapeData? spaceList AMP spaceList styledVertex)*
/// </code>
/// The actions run in the order jison reduces the rules, so a node's <c>@{ }</c> block applies after the vertex it
/// follows, and an edge is added after the nodes at both ends. The actions that throw in mermaid.js, such as an unknown
/// shape or a <c>linkStyle</c> index past the last edge, refuse the source.
/// </summary>
internal ref struct FlowchartParser
{
	private const string Clickable = "clickable";

	private readonly ReadOnlySpan<char> _text;
	private readonly ReadOnlySpan<FlowchartToken> _tokens;
	private readonly DiagramParseContext _context;
	private readonly FlowchartDocument _document;
	private ResourceGuard _guard;
	private SourceCursor _cursor;
	private int _at;
	private string? _error;
	private int _errorAt;

	// Every id the parse meets, with what it names: a node, the first edge with that id, a subgraph, the subgraph that
	// holds it, and the stamp of the last subgraph member list it was seen in.
	private PooledStringTable _ids;
	private PooledList<int> _nodeOf;
	private PooledList<int> _edgeOf;
	private PooledList<int> _subgraphOf;
	private PooledList<int> _memberOf;
	private PooledList<int> _seenIn;
	private PooledList<int> _firstEdgeFrom;
	private PooledList<int> _nextEdgeFrom;
	private PooledList<int> _edgeStartIds;
	private PooledList<int> _edgeEndIds;

	private PooledList<FlowchartNode> _nodes;
	private PooledList<FlowchartEdge> _edges;
	private PooledList<FlowchartSubgraph> _subgraphs;
	private PooledList<FlowchartClass> _classes;
	private PooledStringTable _classIds;
	private PooledList<KeyValuePair<string, string>> _tooltips;

	// The member lists of the open subgraphs, one frame per subgraph, and each frame's last direction statement.
	private PooledList<string> _members;
	private PooledList<int> _frames;
	private PooledList<string?> _frameDirections;

	// The node ids of the vertex statement being read, and where each of its node groups starts.
	private PooledList<string> _statementIds;
	private PooledList<int> _groups;

	private PooledList<char> _scratch;
	private int _subgraphCount;
	private int _stamp;

	private FlowchartParser(ReadOnlySpan<char> text, ReadOnlySpan<FlowchartToken> tokens, DiagramParseContext context, FlowchartDocument document)
	{
		_text = text;
		_tokens = tokens;
		_context = context;
		_document = document;
		_guard = ResourceGuard.Start(text, context.Limits);
		_cursor = new SourceCursor(text);
	}

	public static FlowchartDocument? Parse(ReadOnlySpan<char> source, DiagramParseContext context)
	{
		var body = source[context.BodyStart..];
		var text = new PooledList<char>(body.Length + 16);
		var tokens = new PooledList<FlowchartToken>(64);
		try
		{
			FlowchartText.Prepare(body, ref text);
			var lexError = FlowchartLexer.Lex(text.AsSpan(), ref tokens);
			if (lexError >= 0)
			{
				// jison lexes as the parser asks, so a parse error before the bad text is reported first. No rule accepts
				// Error, and the End after it keeps a lookahead inside the tokens.
				tokens.Add(new FlowchartToken(Kind.Error, lexError, 0));
				tokens.Add(new FlowchartToken(Kind.End, text.Count, 0));
			}

			var document = new FlowchartDocument(context.DiagramType == "flowchart-elk" ? "flowchart-elk" : "flowchart-v2");
			var parser = new FlowchartParser(text.AsSpan(), tokens.AsSpan(), context, document);
			try
			{
				if (parser.ParseStart() && parser.Finish())
				{
					return document;
				}

				parser.Report(source, context.BodyStart);
				return null;
			}
			finally
			{
				parser.Dispose();
			}
		}
		finally
		{
			text.Dispose();
			tokens.Dispose();
		}
	}

	private void Dispose()
	{
		_ids.Dispose();
		_nodeOf.Dispose();
		_edgeOf.Dispose();
		_subgraphOf.Dispose();
		_memberOf.Dispose();
		_seenIn.Dispose();
		_firstEdgeFrom.Dispose();
		_nextEdgeFrom.Dispose();
		_edgeStartIds.Dispose();
		_edgeEndIds.Dispose();
		_nodes.Dispose();
		_edges.Dispose();
		_subgraphs.Dispose();
		_classes.Dispose();
		_classIds.Dispose();
		_tooltips.Dispose();
		_members.Dispose();
		_frames.Dispose();
		_frameDirections.Dispose();
		_statementIds.Dispose();
		_groups.Dispose();
		_scratch.Dispose();
	}

	private readonly FlowchartToken Token => _tokens[_at];

	private readonly Kind Peek => _tokens[_at].Kind;

	private readonly ReadOnlySpan<char> TextOf(FlowchartToken token) => _text.Slice(token.Start, Math.Max(token.Length, 0));

	// ---- Grammar ----

	private bool ParseStart()
	{
		while (Peek is Kind.Space or Kind.NewLine)
		{
			_at++;
		}

		if (Peek != Kind.Graph)
		{
			return Fail("'GRAPH'");
		}

		_document.Keyword = TextOf(Token).ToString();
		_at++;
		if (Peek == Kind.NoDir)
		{
			_at++;
			_document.Direction = "TB";
		}
		else if (Peek == Kind.Dir)
		{
			var direction = TextOf(Token);
			_at++;
			if (Peek is Kind.Semi or Kind.NewLine)
			{
				_at++;
			}
			else if (Peek == Kind.Space)
			{
				SkipSpaces();
				if (Peek != Kind.NewLine)
				{
					return Fail("'NEWLINE'");
				}

				_at++;
			}
			else
			{
				return Fail("'SEMI', 'NEWLINE', 'SPACE'");
			}

			_document.Direction = NormalizeDirection(direction);
		}
		else
		{
			return Fail("'NODIR', 'DIR'");
		}

		if (!ParseDocument(0))
		{
			return false;
		}

		return Peek == Kind.End || Fail("'EOF'");
	}

	/// <summary>Lines up to the end of the input, or up to <c>end</c> inside a subgraph.</summary>
	private bool ParseDocument(int depth)
	{
		while (true)
		{
			switch (Peek)
			{
				case Kind.End:
					return depth == 0 || Fail("'end'");
				case Kind.EndKeyword:
					return depth > 0 || Fail("a statement");
				case Kind.Semi:
					AddMember(";", depth);
					_at++;
					continue;
				case Kind.NewLine or Kind.Space or Kind.Eof:
					_at++;
					continue;
			}

			if (!ParseStatement(depth))
			{
				return false;
			}

			_cursor.Advance(Math.Min(Token.Start, _text.Length) - _cursor.Position);
			_guard.Statement(_cursor);
		}
	}

	private bool ParseStatement(int depth)
	{
		switch (Peek)
		{
			case Kind.Style:
				return ParseStyle() && Separator();
			case Kind.LinkStyle:
				return ParseLinkStyle() && Separator();
			case Kind.ClassDef:
				return ParseClassDef() && Separator();
			case Kind.Class:
				return ParseClass() && Separator();
			case Kind.Click:
				return ParseClick() && Separator();
			case Kind.Subgraph:
				return ParseSubgraph(depth);
			case Kind.DirectionTb or Kind.DirectionBt or Kind.DirectionRl or Kind.DirectionLr or Kind.DirectionTd:
				if (depth > 0)
				{
					_frameDirections[_frameDirections.Count - 1] = Peek switch
					{
						Kind.DirectionTb => "TB",
						Kind.DirectionBt => "BT",
						Kind.DirectionRl => "RL",
						Kind.DirectionLr => "LR",
						_ => "TD",
					};
				}

				_at++;
				return true;
			case Kind.AccTitle:
				_at++;
				if (Peek != Kind.AccTitleValue)
				{
					return Fail("'acc_title_value'");
				}

				_document.AccessibilityTitle = MakeString(TrimJs(TextOf(Token)));
				AddMember(_document.AccessibilityTitle, depth);
				_at++;
				return true;
			case Kind.AccDescr:
				_at++;
				if (Peek != Kind.AccDescrValue)
				{
					return Fail("'acc_descr_value'");
				}

				_document.AccessibilityDescription = MakeString(TrimJs(TextOf(Token)));
				AddMember(_document.AccessibilityDescription, depth);
				_at++;
				return true;
			case Kind.AccDescrMultilineValue:
				_document.AccessibilityDescription = MakeString(TrimJs(TextOf(Token)));
				AddMember(_document.AccessibilityDescription, depth);
				_at++;
				return true;
			default:
				return ParseVertexStatement(depth) && Separator();
		}
	}

	private bool Separator()
	{
		if (Peek is Kind.NewLine or Kind.Semi or Kind.Eof)
		{
			_at++;
			return true;
		}

		return Fail("'NEWLINE', 'SEMI', 'EOF'");
	}

	private void SkipSpaces()
	{
		while (Peek == Kind.Space)
		{
			_at++;
		}
	}

	private static bool IsShapeData(Kind kind) => kind is Kind.ShapeDataStart or Kind.ShapeData or Kind.ShapeDataString;

	private static bool IsLinkStart(Kind kind) => kind is Kind.Link or Kind.StartLink or Kind.LinkId;

	private bool ParseVertexStatement(int depth)
	{
		_statementIds.Truncate(0);
		_groups.Truncate(0);
		if (!ParseNode())
		{
			return false;
		}

		if (IsShapeData(Peek) && !ApplyShapeData(_statementIds[_statementIds.Count - 1], ReadShapeDataRange()))
		{
			return false;
		}

		SkipSpaces();
		while (IsLinkStart(Peek))
		{
			if (!ParseLink(out var link))
			{
				return false;
			}

			var previous = _groups[_groups.Count - 1];
			if (!ParseNode())
			{
				return false;
			}

			if (IsShapeData(Peek) && !ApplyShapeData(_statementIds[_statementIds.Count - 1], ReadShapeDataRange()))
			{
				return false;
			}

			var current = _groups[_groups.Count - 1];
			if (!AddLink(previous, current, _statementIds.Count, link))
			{
				return false;
			}

			SkipSpaces();
		}

		if (depth > 0)
		{
			// The statement's value is its last node group, then the groups before it, newest first.
			var end = _statementIds.Count;
			for (var group = _groups.Count - 1; group >= 0; group--)
			{
				var start = _groups[group];
				for (var i = start; i < end; i++)
				{
					AddMember(_statementIds[i], depth);
				}

				end = start;
			}
		}

		return true;
	}

	/// <summary><c>node</c>: one group of nodes joined by <c>&amp;</c>, appended to the statement's ids.</summary>
	private bool ParseNode()
	{
		_groups.Add(_statementIds.Count);
		if (!ParseStyledVertex(out var id))
		{
			return false;
		}

		_statementIds.Add(id);
		while (true)
		{
			var save = _at;
			var shapeData = (Start: -1, End: -1);
			if (IsShapeData(Peek))
			{
				shapeData = ReadShapeDataRange();
			}

			if (Peek != Kind.Space)
			{
				_at = save;
				return true;
			}

			SkipSpaces();
			if (Peek != Kind.Amp)
			{
				_at = save;
				return true;
			}

			_at++;
			if (Peek != Kind.Space)
			{
				return Fail("'SPACE'");
			}

			SkipSpaces();
			if (!ParseStyledVertex(out var next))
			{
				return false;
			}

			if (shapeData.Start >= 0 && !ApplyShapeData(_statementIds[_statementIds.Count - 1], shapeData))
			{
				return false;
			}

			_statementIds.Add(next);
		}
	}

	private bool ParseStyledVertex(out string id)
	{
		if (!ParseVertex(out id))
		{
			return false;
		}

		if (Peek == Kind.StyleSeparator)
		{
			_at++;
			if (!ParseIdString(out var className))
			{
				return false;
			}

			SetClass(id, className);
		}

		return true;
	}

	private bool ParseVertex(out string id)
	{
		if (!ParseIdString(out id))
		{
			return false;
		}

		string shape;
		Kind close;
		KeyValuePair<string, string>? property = null;
		switch (Peek)
		{
			case Kind.Sqs:
				(shape, close) = ("square", Kind.Sqe);
				break;
			case Kind.DoubleCircleStart:
				(shape, close) = ("doublecircle", Kind.DoubleCircleEnd);
				break;
			case Kind.Ps when _tokens[_at + 1].Kind == Kind.Ps:
				_at++;
				(shape, close) = ("circle", Kind.Pe);
				break;
			case Kind.Ps:
				(shape, close) = ("round", Kind.Pe);
				break;
			case Kind.EllipseStart:
				(shape, close) = ("ellipse", Kind.EllipseEnd);
				break;
			case Kind.StadiumStart:
				(shape, close) = ("stadium", Kind.StadiumEnd);
				break;
			case Kind.SubroutineStart:
				(shape, close) = ("subroutine", Kind.SubroutineEnd);
				break;
			case Kind.CylinderStart:
				(shape, close) = ("cylinder", Kind.CylinderEnd);
				break;
			case Kind.DiamondStart when _tokens[_at + 1].Kind == Kind.DiamondStart:
				_at++;
				(shape, close) = ("hexagon", Kind.DiamondStop);
				break;
			case Kind.DiamondStart:
				(shape, close) = ("diamond", Kind.DiamondStop);
				break;
			case Kind.TagEnd:
				(shape, close) = ("odd", Kind.Sqe);
				break;
			case Kind.TrapStart:
				(shape, close) = ("trapezoid", Kind.TrapEnd);
				break;
			case Kind.InvTrapStart:
				(shape, close) = ("inv_trapezoid", Kind.InvTrapEnd);
				break;
			case Kind.VertexWithPropsStart:
				_at++;
				if (Peek != Kind.NodeString)
				{
					return Fail("'NODE_STRING'");
				}

				var field = TextOf(Token).ToString();
				_at++;
				if (Peek != Kind.Colon)
				{
					return Fail("'COLON'");
				}

				_at++;
				if (Peek != Kind.NodeString)
				{
					return Fail("'NODE_STRING'");
				}

				property = new KeyValuePair<string, string>(field, TextOf(Token).ToString());
				_at++;
				if (Peek != Kind.Pipe)
				{
					return Fail("'PIPE'");
				}

				(shape, close) = ("rect", Kind.Sqe);
				break;
			default:
				AddVertex(id);
				return true;
		}

		_at++;
		if (!ParseText(out var label, out var labelType))
		{
			return false;
		}

		if (shape is "trapezoid" or "inv_trapezoid")
		{
			// [/text\] and [\text/] close with the other slash.
			if (Peek == (shape == "trapezoid" ? Kind.InvTrapEnd : Kind.TrapEnd))
			{
				(shape, close) = shape == "trapezoid" ? ("lean_right", Kind.InvTrapEnd) : ("lean_left", Kind.TrapEnd);
			}
		}

		if (Peek != close)
		{
			return Fail("'" + Name(close) + "'");
		}

		_at++;
		if (shape is "circle" or "hexagon")
		{
			if (Peek != close)
			{
				return Fail("'" + Name(close) + "'");
			}

			_at++;
		}

		AddVertex(id, label, labelType, shape, property);
		return true;
	}

	private static bool IsIdStringToken(Kind kind) =>
		kind is Kind.Num or Kind.NodeString or Kind.Down or Kind.Minus or Kind.Default or Kind.Comma or Kind.Colon or Kind.Amp
			or Kind.Brkt or Kind.Mult or Kind.UnicodeText;

	private static bool IsAlphaNumToken(Kind kind) =>
		kind is Kind.Num or Kind.UnicodeText or Kind.NodeString or Kind.Dir or Kind.Down or Kind.Minus or Kind.Comma or Kind.Colon
			or Kind.Amp or Kind.Brkt or Kind.Mult;

	private static bool IsTextToken(Kind kind) => kind is Kind.Text or Kind.TagStart or Kind.TagEnd or Kind.UnicodeText;

	private static bool IsEdgeTextToken(Kind kind) => kind is Kind.EdgeText or Kind.UnicodeText;

	private static bool IsTextNoTagsToken(Kind kind) =>
		kind is Kind.Num or Kind.NodeString or Kind.Space or Kind.Minus or Kind.Amp or Kind.UnicodeText or Kind.Colon or Kind.Mult
			or Kind.Brkt or Kind.Style or Kind.LinkStyle or Kind.ClassDef or Kind.Class or Kind.Click or Kind.Graph or Kind.Dir
			or Kind.Subgraph or Kind.EndKeyword or Kind.Down or Kind.Up or Kind.StartLink;

	/// <summary><c>idString</c>: adjacent id tokens, which the lexer leaves touching, so the id is one slice of the text.</summary>
	private bool ParseIdString(out string id)
	{
		id = "";
		if (!IsIdStringToken(Peek))
		{
			return Fail("an id");
		}

		var start = Token.Start;
		while (IsIdStringToken(Peek))
		{
			_at++;
		}

		id = Intern(_text[start.._tokens[_at - 1].End]);
		return true;
	}

	/// <summary><c>alphaNum</c>, as a slice of the text.</summary>
	private bool ParseAlphaNum(out string value)
	{
		value = "";
		if (!IsAlphaNumToken(Peek))
		{
			return Fail("'ALPHA_NUM'");
		}

		var start = Token.Start;
		while (IsAlphaNumToken(Peek))
		{
			_at++;
		}

		value = _text[start.._tokens[_at - 1].End].ToString();
		return true;
	}

	/// <summary>
	/// <c>text</c>, <c>edgeText</c> or <c>textNoTags</c>: a string, a Markdown string or a token the rule allows, then
	/// more tokens it allows. The pieces are joined into the scratch buffer.
	/// </summary>
	private bool ReadText(Func<Kind, bool> allowed, FlowchartLabelType stringType, out FlowchartLabelType type)
	{
		_scratch.Truncate(0);
		switch (Peek)
		{
			case Kind.Str:
				type = stringType;
				break;
			case Kind.MdStr:
				type = FlowchartLabelType.Markdown;
				break;
			case var kind when allowed(kind):
				type = FlowchartLabelType.Text;
				break;
			default:
				type = FlowchartLabelType.Text;
				return Fail("text");
		}

		_scratch.AddRange(TextOf(Token));
		_at++;
		while (allowed(Peek))
		{
			_scratch.AddRange(TextOf(Token));
			_at++;
		}

		return true;
	}

	private bool ParseText(out string text, out FlowchartLabelType type)
	{
		text = "";
		if (!ReadText(IsTextToken, FlowchartLabelType.String, out type))
		{
			return false;
		}

		text = Label(_scratch.AsSpan());
		return true;
	}

	private bool ParseLink(out LinkInfo link)
	{
		link = default;
		string? id = null;
		if (Peek == Kind.LinkId)
		{
			id = TextOf(Token).ToString();
			_at++;
		}

		if (Peek == Kind.Link)
		{
			link = DestructLink(TextOf(Token), default) with { Id = id };
			_at++;
			if (Peek == Kind.Pipe)
			{
				_at++;
				if (!ParseText(out var label, out var type))
				{
					return false;
				}

				if (Peek != Kind.Pipe)
				{
					return Fail("'PIPE'");
				}

				_at++;
				link = link with { Label = label, LabelType = type };
				if (Peek == Kind.Space)
				{
					_at++;
				}
			}

			return true;
		}

		if (Peek != Kind.StartLink)
		{
			return Fail("'LINK', 'START_LINK'");
		}

		var start = TextOf(Token);
		_at++;
		if (!ReadText(IsEdgeTextToken, FlowchartLabelType.String, out var edgeType))
		{
			return false;
		}

		var edgeLabel = Label(_scratch.AsSpan());
		if (Peek != Kind.Link)
		{
			return Fail("'LINK'");
		}

		link = DestructLink(TextOf(Token), start) with { Id = id, Label = edgeLabel, LabelType = edgeType };
		_at++;
		return true;
	}

	private (int Start, int End) ReadShapeDataRange()
	{
		var start = _at;
		while (IsShapeData(Peek))
		{
			_at++;
		}

		return (start, _at);
	}

	private bool ParseStyle()
	{
		_at++;
		if (Peek != Kind.Space)
		{
			return Fail("'SPACE'");
		}

		_at++;
		if (!ParseIdString(out var id))
		{
			return false;
		}

		if (Peek != Kind.Space)
		{
			return Fail("'SPACE'");
		}

		_at++;
		var styles = new PooledList<string>(4);
		try
		{
			if (!ParseStyles(ref styles))
			{
				return false;
			}

			AddVertexStyles(id, styles.AsSpan());
			return true;
		}
		finally
		{
			styles.Dispose();
		}
	}

	private static bool IsStyleComponent(Kind kind) =>
		kind is Kind.Num or Kind.NodeString or Kind.Colon or Kind.Space or Kind.Brkt or Kind.Style;

	/// <summary><c>stylesOpt</c>: styles separated by commas, each a run of style components.</summary>
	private bool ParseStyles(ref PooledList<string> styles)
	{
		while (true)
		{
			if (!IsStyleComponent(Peek))
			{
				return Fail("a style");
			}

			var start = Token.Start;
			while (IsStyleComponent(Peek))
			{
				_at++;
			}

			styles.Add(_text[start.._tokens[_at - 1].End].ToString());
			if (Peek != Kind.Comma)
			{
				return true;
			}

			_at++;
		}
	}

	private bool ParseLinkStyle()
	{
		_at++;
		if (Peek != Kind.Space)
		{
			return Fail("'SPACE'");
		}

		_at++;
		var positions = new PooledList<int>(4);
		var styles = new PooledList<string>(4);
		try
		{
			// -1 stands for `default`, and -2 for a number that is not an index of the edges array.
			if (Peek == Kind.Default)
			{
				positions.Add(-1);
				_at++;
			}
			else
			{
				while (true)
				{
					if (Peek != Kind.Num)
					{
						return Fail("'NUM'");
					}

					positions.Add(EdgeIndex(TextOf(Token)));
					_at++;
					if (Peek != Kind.Comma)
					{
						break;
					}

					_at++;
				}
			}

			if (Peek != Kind.Space)
			{
				return Fail("'SPACE'");
			}

			_at++;
			if (Peek == Kind.Interpolate)
			{
				_at++;
				if (Peek != Kind.Space)
				{
					return Fail("'SPACE'");
				}

				_at++;
				if (!ParseAlphaNum(out var curve))
				{
					return false;
				}

				if (!UpdateInterpolate(positions.AsSpan(), curve))
				{
					return false;
				}

				if (Peek != Kind.Space)
				{
					return true;
				}

				_at++;
			}

			return ParseStyles(ref styles) && UpdateEdgeStyles(positions.AsSpan(), styles.AsSpan());
		}
		finally
		{
			positions.Dispose();
			styles.Dispose();
		}
	}

	private bool ParseClassDef()
	{
		_at++;
		if (Peek != Kind.Space)
		{
			return Fail("'SPACE'");
		}

		_at++;
		if (!ParseIdString(out var ids))
		{
			return false;
		}

		if (Peek != Kind.Space)
		{
			return Fail("'SPACE'");
		}

		_at++;
		var styles = new PooledList<string>(4);
		try
		{
			if (!ParseStyles(ref styles))
			{
				return false;
			}

			AddClass(ids, styles.AsSpan());
			return true;
		}
		finally
		{
			styles.Dispose();
		}
	}

	private bool ParseClass()
	{
		_at++;
		if (Peek != Kind.Space)
		{
			return Fail("'SPACE'");
		}

		_at++;
		if (!ParseIdString(out var ids))
		{
			return false;
		}

		if (Peek != Kind.Space)
		{
			return Fail("'SPACE'");
		}

		_at++;
		if (!ParseIdString(out var className))
		{
			return false;
		}

		SetClass(ids, className);
		return true;
	}

	private bool ParseClick()
	{
		var ids = TextOf(Token);
		_at++;
		string? tooltip = null;
		switch (Peek)
		{
			case Kind.CallbackName:
				var callback = TextOf(Token).ToString();
				_at++;
				string? arguments = null;
				if (Peek == Kind.CallbackArgs)
				{
					arguments = TextOf(Token).ToString();
					_at++;
				}

				if (Peek == Kind.Space && _tokens[_at + 1].Kind == Kind.Str)
				{
					tooltip = TextOf(_tokens[_at + 1]).ToString();
					_at += 2;
				}

				SetClickEvent(ids, callback, arguments);
				break;
			case Kind.Href or Kind.Str:
				if (Peek == Kind.Href)
				{
					_at++;
					if (Peek != Kind.Str)
					{
						return Fail("'STR'");
					}
				}

				var link = TextOf(Token).ToString();
				_at++;
				string? target = null;
				if (Peek == Kind.Space && _tokens[_at + 1].Kind == Kind.Str)
				{
					tooltip = TextOf(_tokens[_at + 1]).ToString();
					_at += 2;
				}

				if (Peek == Kind.Space && _tokens[_at + 1].Kind == Kind.LinkTarget)
				{
					target = TextOf(_tokens[_at + 1]).ToString();
					_at += 2;
				}

				SetLink(ids, link, target);
				break;
			default:
				if (!ParseAlphaNum(out var function))
				{
					return false;
				}

				if (Peek == Kind.Space && _tokens[_at + 1].Kind == Kind.Str)
				{
					tooltip = TextOf(_tokens[_at + 1]).ToString();
					_at += 2;
				}

				SetClickEvent(ids, function, null);
				break;
		}

		if (tooltip is not null)
		{
			foreach (var range in ids.Split(','))
			{
				_tooltips.Add(new KeyValuePair<string, string>(Intern(ids[range]), tooltip));
			}
		}

		return true;
	}

	private bool ParseSubgraph(int depth)
	{
		_at++;
		string? id;
		string title;
		FlowchartLabelType titleType;
		if (Peek == Kind.Space)
		{
			_at++;
			if (!ReadText(IsTextNoTagsToken, FlowchartLabelType.Text, out var idType))
			{
				return false;
			}

			var idText = _scratch.AsSpan();
			if (Peek == Kind.Sqs)
			{
				id = MakeString(TrimJs(idText));
				_at++;
				if (!ReadText(IsTextToken, FlowchartLabelType.String, out titleType))
				{
					return false;
				}

				title = MakeString(TrimJs(_scratch.AsSpan()));
				if (Peek != Kind.Sqe)
				{
					return Fail("'SQE'");
				}

				_at++;
			}
			else
			{
				// The id and the title are one object, and /\s/ on the untrimmed title clears the id.
				titleType = idType;
				id = ContainsJsSpace(idText) ? null : MakeString(TrimJs(idText));
				title = MakeString(TrimJs(idText));
			}
		}
		else
		{
			if (Peek is not (Kind.NewLine or Kind.Semi or Kind.Eof))
			{
				return Fail("'SPACE', 'NEWLINE', 'SEMI', 'EOF'");
			}

			// addSubGraph reads `_id.text` of an undefined id, which throws.
			return Fail(null, "Cannot read properties of undefined (reading 'text')");
		}

		if (!Separator())
		{
			return false;
		}

		_cursor.Advance(Math.Min(Token.Start, _text.Length) - _cursor.Position);
		_guard.Depth(depth + 1, _cursor);
		_frames.Add(_members.Count);
		_frameDirections.Add(null);
		if (!ParseDocument(depth + 1))
		{
			return false;
		}

		if (Peek != Kind.EndKeyword)
		{
			return Fail("'end'");
		}

		_at++;
		var frame = _frames[_frames.Count - 1];
		var direction = _frameDirections[_frameDirections.Count - 1];
		var subgraphId = AddSubgraph(id, title, titleType, frame, direction);
		_members.Truncate(frame);
		_frames.Truncate(_frames.Count - 1);
		_frameDirections.Truncate(_frameDirections.Count - 1);
		AddMember(subgraphId, depth);
		return true;
	}

	// ---- flowDb actions ----

	private record struct LinkInfo(FlowchartEdgeType Type, FlowchartStroke Stroke, int? Length, string? Id, string? Label, FlowchartLabelType LabelType);

	/// <summary><c>destructLink</c>: the arrow type, stroke and length of a link, and of the start of a <c>-- text --&gt;</c> link.</summary>
	private static LinkInfo DestructLink(ReadOnlySpan<char> end, ReadOnlySpan<char> start)
	{
		var info = DestructEndLink(end);
		if (start.IsEmpty)
		{
			return info;
		}

		var startText = TrimJs(start);
		var startType = startText[0] switch
		{
			'<' => FlowchartEdgeType.Point,
			'x' => FlowchartEdgeType.Cross,
			'o' => FlowchartEdgeType.Circle,
			_ => FlowchartEdgeType.Open,
		};
		var startStroke = startText.Contains('.') ? FlowchartStroke.Dotted : startText.Contains('=') ? FlowchartStroke.Thick : FlowchartStroke.Normal;
		if (startStroke != info.Stroke)
		{
			return new LinkInfo(FlowchartEdgeType.Invalid, FlowchartStroke.Invalid, null, null, null, default);
		}

		if (startType == FlowchartEdgeType.Open)
		{
			return info;
		}

		if (startType != info.Type)
		{
			return new LinkInfo(FlowchartEdgeType.Invalid, FlowchartStroke.Invalid, null, null, null, default);
		}

		return info with
		{
			Type = startType switch
			{
				FlowchartEdgeType.Point => FlowchartEdgeType.DoublePoint,
				FlowchartEdgeType.Cross => FlowchartEdgeType.DoubleCross,
				_ => FlowchartEdgeType.DoubleCircle,
			},
		};
	}

	private static LinkInfo DestructEndLink(ReadOnlySpan<char> link)
	{
		var text = TrimJs(link);
		var line = text[..^1];
		var type = FlowchartEdgeType.Open;
		switch (text[^1])
		{
			case 'x':
				type = FlowchartEdgeType.Cross;
				if (text[0] == 'x')
				{
					type = FlowchartEdgeType.DoubleCross;
					line = line[1..];
				}

				break;
			case '>':
				type = FlowchartEdgeType.Point;
				if (text[0] == '<')
				{
					type = FlowchartEdgeType.DoublePoint;
					line = line[1..];
				}

				break;
			case 'o':
				type = FlowchartEdgeType.Circle;
				if (text[0] == 'o')
				{
					type = FlowchartEdgeType.DoubleCircle;
					line = line[1..];
				}

				break;
		}

		var stroke = FlowchartStroke.Normal;
		var length = line.Length - 1;
		if (!line.IsEmpty && line[0] == '=')
		{
			stroke = FlowchartStroke.Thick;
		}

		if (!line.IsEmpty && line[0] == '~')
		{
			stroke = FlowchartStroke.Invisible;
		}

		var dots = line.Count('.');
		if (dots > 0)
		{
			stroke = FlowchartStroke.Dotted;
			length = dots;
		}

		return new LinkInfo(type, stroke, length, null, null, default);
	}

	private int IdIndex(string id)
	{
		var index = _ids.GetOrAdd(id);
		while (_nodeOf.Count < _ids.Count)
		{
			_nodeOf.Add(-1);
			_edgeOf.Add(-1);
			_subgraphOf.Add(-1);
			_memberOf.Add(-1);
			_seenIn.Add(0);
			_firstEdgeFrom.Add(-1);
		}

		return index;
	}

	private string Intern(ReadOnlySpan<char> id)
	{
		var index = _ids.GetOrAdd(id);
		IdIndex(_ids[index]);
		return _ids[index];
	}

	private readonly int Lookup(ReadOnlySpan<char> id, ref readonly PooledList<int> table) =>
		_ids.TryGetIndex(id, out var index) ? table[index] : -1;

	/// <summary><c>addVertex</c> of a node the statement names without a label: the node, unless the id names an edge.</summary>
	private int AddVertex(string id)
	{
		if (IsBlank(id) || Lookup(id, ref _edgeOf) >= 0)
		{
			return -1;
		}

		var index = IdIndex(id);
		if (_nodeOf[index] < 0)
		{
			_nodeOf[index] = _nodes.Count;
			_nodes.Add(new FlowchartNode(id));
			CheckElements();
		}

		return _nodeOf[index];
	}

	private void AddVertex(string id, string label, FlowchartLabelType labelType, string shape, KeyValuePair<string, string>? property)
	{
		var index = AddVertex(id);
		if (index < 0)
		{
			return;
		}

		var node = _nodes[index];
		node.Label = label;
		node.LabelType = labelType;
		node.Shape = shape;
		if (property is not null)
		{
			node.Property = property;
		}
	}

	private void AddVertexStyles(string id, ReadOnlySpan<string> styles)
	{
		var index = AddVertex(id);
		if (index >= 0)
		{
			foreach (var style in styles)
			{
				_nodes[index].Styles.Add(Restored(style));
			}
		}
	}

	/// <summary>
	/// <c>addVertex</c> with a <c>@{ }</c> block, read as js-yaml's <c>JSON_SCHEMA</c> reads it: as a flow mapping when
	/// it is one line, and as a block document when it holds a line break. It goes to the subgraph or the edge of that id
	/// when there is one.
	/// </summary>
	private bool ApplyShapeData(string id, (int Start, int End) tokens)
	{
		if (IsBlank(id))
		{
			return true;
		}

		_scratch.Truncate(0);
		var multiline = false;
		for (var i = tokens.Start; i < tokens.End; i++)
		{
			var token = _tokens[i];
			if (token.Kind == Kind.ShapeDataStart)
			{
				continue;
			}

			var piece = TextOf(token);
			if (token.Kind == Kind.ShapeDataString)
			{
				AppendWithBreaks(piece);
			}
			else
			{
				multiline |= piece.Contains('\n');
				_scratch.AddRange(piece);
			}
		}

		// The YAML reads the text with mermaid.js's entity placeholders in it, so the # of an entity is not a comment.
		var data = _scratch.AsSpan();
		var yaml = new PooledList<char>(data.Length + 4);
		MermaidValue? value;
		try
		{
			if (multiline)
			{
				yaml.AddRange(data);
				yaml.Add('\n');
			}
			else
			{
				yaml.AddRange("{\n");
				yaml.AddRange(data);
				yaml.AddRange("\n}");
			}

			if (!ReadYaml(yaml.AsSpan(), out value))
			{
				return false;
			}
		}
		finally
		{
			yaml.Dispose();
		}

		value = WithoutInternalKeys(value);
		var subgraph = Lookup(id, ref _subgraphOf);
		if (subgraph >= 0 && value is not null && IsTruthyDocument(value))
		{
			if (value.Kind == MermaidValueKind.Mapping && value.Entries.Count > 0)
			{
				var target = _subgraphs[subgraph];
				target.Metadata = Merge(target.Metadata, value);
			}

			return true;
		}

		var edge = Lookup(id, ref _edgeOf);
		if (edge >= 0)
		{
			ApplyEdgeData(_edges[edge], value);
			return true;
		}

		var index = AddVertex(id);
		if (index < 0 || value is null)
		{
			return true;
		}

		if (value.Kind == MermaidValueKind.Null)
		{
			return Fail(null, "Cannot read properties of null (reading 'shape')");
		}

		return value.Kind != MermaidValueKind.Mapping || ApplyNodeData(_nodes[index], id, value);
	}

	/// <summary>The <c>&lt;shapeDataStr&gt;</c> rule's <c>yytext.replace(/\n\s*/g, "&lt;br/&gt;")</c>.</summary>
	private void AppendWithBreaks(ReadOnlySpan<char> piece)
	{
		int at;
		while ((at = piece.IndexOf('\n')) >= 0)
		{
			_scratch.AddRange(piece[..at]);
			_scratch.AddRange("<br/>");
			var next = at + 1;
			while (next < piece.Length && PreambleParser.IsSpace(piece[next]))
			{
				next++;
			}

			piece = piece[next..];
		}

		_scratch.AddRange(piece);
	}

	private bool ReadYaml(ReadOnlySpan<char> yaml, out MermaidValue? value)
	{
		value = null;
		if (IsEmptyYaml(yaml))
		{
			return true;
		}

		var diagnostics = new List<MermaidDiagnostic>();
		value = YamlReader.Read(yaml, diagnostics, _context.Limits);
		if (value is null)
		{
			var message = diagnostics.Count > 0 ? diagnostics[0].Message : "The @{ } block is not valid YAML.";
			return Fail(null, message);
		}

		return true;
	}

	/// <summary>Whether js-yaml loads the text as no document, which <c>yaml.load</c> returns as <c>undefined</c>.</summary>
	private static bool IsEmptyYaml(ReadOnlySpan<char> yaml)
	{
		foreach (var range in yaml.Split('\n'))
		{
			var line = TrimJs(yaml[range]);
			if (!line.IsEmpty && line[0] != '#')
			{
				return false;
			}
		}

		return true;
	}

	private static MermaidValue? WithoutInternalKeys(MermaidValue? value)
	{
		if (value is not { Kind: MermaidValueKind.Mapping }
			|| (value["laneRole"] is null && value["laneIndex"] is null && value["drawnExtent"] is null))
		{
			return value;
		}

		var kept = new OrderedDictionary<string, MermaidValue>(StringComparer.Ordinal);
		foreach (var (key, entry) in value.Entries)
		{
			if (key is not "laneRole" and not "laneIndex" and not "drawnExtent")
			{
				kept.Add(key, entry);
			}
		}

		return MermaidValue.FromEntries(kept);
	}

	private static bool IsTruthyDocument(MermaidValue value) =>
		value.Kind is MermaidValueKind.Mapping or MermaidValueKind.Sequence || PreambleParser.IsTruthy(value);

	private static MermaidValue Merge(MermaidValue? existing, MermaidValue added)
	{
		if (existing is not { Kind: MermaidValueKind.Mapping })
		{
			return added;
		}

		var merged = new OrderedDictionary<string, MermaidValue>(StringComparer.Ordinal);
		foreach (var (key, entry) in existing.Entries)
		{
			merged[key] = entry;
		}

		foreach (var (key, entry) in added.Entries)
		{
			merged[key] = entry;
		}

		return MermaidValue.FromEntries(merged);
	}

	/// <summary>A <c>@{ }</c> value as JavaScript's <c>String</c> gives it, with the entity placeholders turned back into the source's text.</summary>
	private string DataString(MermaidValue value) => Restored(PreambleParser.ToJsString(value));

	private string Restored(string text) => FlowchartText.HasPlaceholder(text) ? MakeString(text) : text;

	private void ApplyEdgeData(FlowchartEdge edge, MermaidValue? data)
	{
		if (data is not { Kind: MermaidValueKind.Mapping })
		{
			return;
		}

		if (data["animate"] is { } animate)
		{
			edge.Animate = PreambleParser.IsTruthy(animate);
		}

		if (data["animation"] is { } animation)
		{
			edge.Animation = DataString(animation);
		}

		if (data["curve"] is { } curve)
		{
			edge.Interpolate = DataString(curve);
		}

		if (data["step"] is { Kind: MermaidValueKind.Number } step && step.Number > 0 && step.Number == Math.Floor(step.Number) && step.Number <= int.MaxValue)
		{
			edge.Step = (int)step.Number;
		}
	}

	private bool ApplyNodeData(FlowchartNode node, string id, MermaidValue data)
	{
		if (data["shape"] is { } shapeValue && PreambleParser.IsTruthy(shapeValue))
		{
			if (shapeValue.Kind != MermaidValueKind.String)
			{
				return Fail(null, "doc.shape.toLowerCase is not a function");
			}

			var shape = shapeValue.String;
			if (!shape.Equals(shape.ToLowerInvariant(), StringComparison.Ordinal) || shape.Contains('_'))
			{
				return Fail(null, $"No such shape: {shape}. Shape names should be lowercase.");
			}

			if (!FlowchartShapes.IsValid(shape))
			{
				return Fail(null, $"No such shape: {shape}.");
			}

			node.Shape = shape;
		}

		var label = data["label"];
		if (label is not null && PreambleParser.IsTruthy(label))
		{
			node.Label = DataString(label);
			node.LabelType = data["labelType"] is { Kind: MermaidValueKind.String } labelType
				? labelType.String switch
				{
					"string" => FlowchartLabelType.String,
					"text" => FlowchartLabelType.Text,
					_ => FlowchartLabelType.Markdown,
				}
				: FlowchartLabelType.Markdown;
		}

		var hasLabel = label is { Kind: MermaidValueKind.String } && !TrimJs(label.String).IsEmpty;
		if (data["icon"] is { } icon && PreambleParser.IsTruthy(icon))
		{
			node.Icon = DataString(icon);
			if (!hasLabel && ReferenceEquals(node.Label, id))
			{
				node.Label = "";
			}
		}

		if (data["form"] is { } form && PreambleParser.IsTruthy(form))
		{
			node.Form = DataString(form);
		}

		if (data["pos"] is { } position && PreambleParser.IsTruthy(position))
		{
			node.LabelPosition = DataString(position);
		}

		if (data["img"] is { } image && PreambleParser.IsTruthy(image))
		{
			node.Image = DataString(image);
			if (!hasLabel && ReferenceEquals(node.Label, id))
			{
				node.Label = "";
			}
		}

		if (data["constraint"] is { } constraint && PreambleParser.IsTruthy(constraint))
		{
			node.Constraint = DataString(constraint);
		}

		if (data["w"] is { } width && PreambleParser.IsTruthy(width))
		{
			node.AssetWidth = JsNumberOf(width);
		}

		if (data["h"] is { } height && PreambleParser.IsTruthy(height))
		{
			node.AssetHeight = JsNumberOf(height);
		}

		return true;
	}

	private static double JsNumberOf(MermaidValue value) => value.Kind switch
	{
		MermaidValueKind.Number => value.Number,
		MermaidValueKind.Boolean => value.Boolean ? 1 : 0,
		MermaidValueKind.String => double.TryParse(value.String.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : double.NaN,
		_ => double.NaN,
	};

	/// <summary><c>addLink</c>: an edge from every node of one group to every node of the next.</summary>
	private bool AddLink(int startGroup, int endGroup, int endGroupEnd, LinkInfo link)
	{
		var id = link.Id;
		if (id is not null)
		{
			var at = id.IndexOf('@', StringComparison.Ordinal);
			id = at < 0 ? id : string.Concat(id.AsSpan(0, at), id.AsSpan(at + 1));
		}

		var lastStart = _statementIds[endGroup - 1];
		var firstEnd = _statementIds[endGroup];
		for (var s = startGroup; s < endGroup; s++)
		{
			for (var e = endGroup; e < endGroupEnd; e++)
			{
				var start = _statementIds[s];
				var end = _statementIds[e];
				AddSingleLink(start, end, link, ReferenceEquals(start, lastStart) && ReferenceEquals(end, firstEnd) ? id : null);
			}
		}

		return true;
	}

	private void AddSingleLink(string start, string end, LinkInfo link, string? id)
	{
		var startIndex = IdIndex(start);
		var endIndex = IdIndex(end);
		string edgeId;
		var userDefined = false;
		if (!string.IsNullOrEmpty(id) && Lookup(id, ref _edgeOf) < 0)
		{
			edgeId = Intern(id);
			userDefined = true;
		}
		else
		{
			var existing = 0;
			for (var edge = _firstEdgeFrom[startIndex]; edge >= 0; edge = _nextEdgeFrom[edge])
			{
				if (_edgeEndIds[edge] == endIndex)
				{
					existing++;
				}
			}

			edgeId = Intern(string.Create(CultureInfo.InvariantCulture, $"L_{start}_{end}_{(existing == 0 ? 0 : existing + 1)}"));
		}

		var flowEdge = new FlowchartEdge(start, end, edgeId)
		{
			IsUserDefinedId = userDefined,
			Type = link.Type,
			Stroke = link.Stroke,
			Length = link.Length is { } length ? Math.Min(length, 10) : null,
			Interpolate = _document.DefaultInterpolate,
		};
		if (link.Label is not null)
		{
			flowEdge.Label = link.Label;
			flowEdge.LabelType = link.LabelType;
		}

		var index = _edges.Count;
		_edges.Add(flowEdge);
		_edgeStartIds.Add(startIndex);
		_edgeEndIds.Add(endIndex);
		_nextEdgeFrom.Add(_firstEdgeFrom[startIndex]);
		_firstEdgeFrom[startIndex] = index;
		var idIndex = IdIndex(edgeId);
		if (_edgeOf[idIndex] < 0)
		{
			_edgeOf[idIndex] = index;
		}

		CheckElements();
	}

	/// <summary>
	/// A <c>linkStyle</c> index as JavaScript reads <c>edges[pos]</c> with the index's text: an array index only when it
	/// has no leading zero. -2 when it names no edge.
	/// </summary>
	private static int EdgeIndex(ReadOnlySpan<char> text) =>
		(text.Length == 1 || text[0] != '0') && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var index) ? index : -2;

	private bool UpdateInterpolate(ReadOnlySpan<int> positions, string curve)
	{
		foreach (var position in positions)
		{
			if (position == -1)
			{
				_document.DefaultInterpolate = curve;
			}
			else if (position < 0 || position >= _edges.Count)
			{
				return Fail(null, "Cannot set properties of undefined (setting 'interpolate')");
			}
			else
			{
				_edges[position].Interpolate = curve;
			}
		}

		return true;
	}

	private bool UpdateEdgeStyles(ReadOnlySpan<int> positions, ReadOnlySpan<string> styles)
	{
		foreach (var position in positions)
		{
			if (position == -1)
			{
				_document.DefaultEdgeStyles = new List<string>(styles.Length);
				foreach (var style in styles)
				{
					_document.DefaultEdgeStyles.Add(Restored(style));
				}

				continue;
			}

			if (position < 0 || position >= _edges.Count)
			{
				return Fail(null, "Cannot set properties of undefined (setting 'style')");
			}

			var edgeStyles = new List<string>(styles.Length + 1);
			var hasFill = false;
			foreach (var style in styles)
			{
				edgeStyles.Add(Restored(style));
				hasFill |= style.StartsWith("fill", StringComparison.Ordinal);
			}

			if (!hasFill && styles.Length > 0)
			{
				edgeStyles.Add("fill:none");
			}

			_edges[position].Styles = edgeStyles;
		}

		return true;
	}

	/// <summary>
	/// <c>addClass</c>: <c>_style.join().replace(/\\,/g, '§§§').replace(/,/g, ';').replace(/§§§/g, ',').split(';')</c>.
	/// The styles are joined with commas, and the text is split at each <c>,</c> and <c>;</c> that is not <c>\,</c>. A
	/// style that mentions <c>color</c> is also a text style, with its first <c>fill</c> renamed <c>bgFill</c>.
	/// </summary>
	private void AddClass(string ids, ReadOnlySpan<string> styles)
	{
		var pieces = new PooledList<string>(styles.Length + 2);
		try
		{
			SplitClassStyles(styles, ref pieces);
			foreach (var range in ids.AsSpan().Split(','))
			{
				var classId = ids.AsSpan()[range];
				FlowchartClass target;
				if (_classIds.TryGetIndex(classId, out var index))
				{
					target = _classes[index];
				}
				else
				{
					target = new FlowchartClass(_classIds[_classIds.GetOrAdd(classId)]);
					_classes.Add(target);
				}

				foreach (var style in pieces.AsSpan())
				{
					if (style.Contains("color", StringComparison.Ordinal))
					{
						var fill = style.IndexOf("fill", StringComparison.Ordinal);
						target.TextStyles.Add(fill < 0 ? style : string.Concat(style.AsSpan(0, fill), "bgFill", style.AsSpan(fill + 4)));
					}

					target.Styles.Add(style);
				}
			}
		}
		finally
		{
			pieces.Dispose();
		}
	}

	private void SplitClassStyles(ReadOnlySpan<string> styles, ref PooledList<string> pieces)
	{
		const string Mark = "§§§";
		var joined = new PooledList<char>(64);
		var escaped = new PooledList<char>(64);
		try
		{
			for (var i = 0; i < styles.Length; i++)
			{
				if (i > 0)
				{
					joined.Add(',');
				}

				joined.AddRange(styles[i]);
			}

			var rest = joined.AsSpan();
			int found;
			while ((found = rest.IndexOf("\\,", StringComparison.Ordinal)) >= 0)
			{
				escaped.AddRange(rest[..found]);
				escaped.AddRange(Mark);
				rest = rest[(found + 2)..];
			}

			escaped.AddRange(rest);
			_scratch.Truncate(0);
			var text = escaped.AsSpan();
			for (var at = 0; at < text.Length; at++)
			{
				if (text[at..].StartsWith(Mark, StringComparison.Ordinal))
				{
					_scratch.Add(',');
					at += Mark.Length - 1;
				}
				else if (text[at] is ',' or ';')
				{
					pieces.Add(MakeString(_scratch.AsSpan()));
					_scratch.Truncate(0);
				}
				else
				{
					_scratch.Add(text[at]);
				}
			}

			pieces.Add(MakeString(_scratch.AsSpan()));
		}
		finally
		{
			joined.Dispose();
			escaped.Dispose();
		}
	}

	/// <summary><c>setClass</c>: the class on each node, edge and subgraph the comma-separated ids name.</summary>
	private void SetClass(string ids, string className)
	{
		foreach (var range in ids.AsSpan().Split(','))
		{
			var id = ids.AsSpan()[range];
			if (Lookup(id, ref _nodeOf) is var node and >= 0)
			{
				_nodes[node].Classes.Add(className);
			}

			if (Lookup(id, ref _edgeOf) is var edge and >= 0)
			{
				_edges[edge].Classes.Add(className);
			}

			if (Lookup(id, ref _subgraphOf) is var subgraph and >= 0)
			{
				_subgraphs[subgraph].Classes.Add(className);
			}
		}
	}

	private void SetClickEvent(ReadOnlySpan<char> ids, string callback, string? arguments)
	{
		foreach (var range in ids.Split(','))
		{
			if (Lookup(ids[range], ref _nodeOf) is var node and >= 0)
			{
				_nodes[node].Callback = callback;
				_nodes[node].CallbackArguments = arguments;
			}
		}

		SetClass(ids.ToString(), Clickable);
	}

	private void SetLink(ReadOnlySpan<char> ids, string link, string? target)
	{
		foreach (var range in ids.Split(','))
		{
			if (Lookup(ids[range], ref _nodeOf) is var node and >= 0)
			{
				_nodes[node].Link = link;
				_nodes[node].LinkTarget = target;
			}
		}

		SetClass(ids.ToString(), Clickable);
	}

	private void AddMember(string member, int depth)
	{
		if (depth > 0 && !IsBlank(member))
		{
			_members.Add(member);
		}
	}

	/// <summary>
	/// <c>addSubGraph</c>: the members of the subgraph's document without repeats, without the nodes an earlier
	/// subgraph holds and without the subgraph itself. A subgraph whose id was used before adds its members to the
	/// first.
	/// </summary>
	private string AddSubgraph(string? id, string title, FlowchartLabelType titleType, int frame, string? direction)
	{
		id ??= Intern(string.Create(CultureInfo.InvariantCulture, $"subGraph{_subgraphCount}"));
		_subgraphCount++;
		var idIndex = IdIndex(id);
		var existing = _subgraphOf[idIndex];
		var subgraph = existing >= 0 ? _subgraphs[existing] : new FlowchartSubgraph(id, title) { LabelType = titleType, Direction = direction };
		var target = existing >= 0 ? existing : _subgraphs.Count;
		_stamp++;
		var members = _members.AsSpan()[frame..];
		for (var i = 0; i < members.Length; i++)
		{
			var member = IdIndex(members[i]);
			members[i] = _ids[member];
			if (_seenIn[member] == _stamp)
			{
				continue;
			}

			_seenIn[member] = _stamp;
			if (_memberOf[member] >= 0 || member == idIndex)
			{
				continue;
			}

			subgraph.Nodes.Add(_ids[member]);
		}

		foreach (var member in subgraph.Nodes)
		{
			var index = IdIndex(member);
			if (_memberOf[index] < 0)
			{
				_memberOf[index] = target;
			}
		}

		if (existing < 0)
		{
			_subgraphs.Add(subgraph);
			_subgraphOf[idIndex] = target;
		}

		return id;
	}

	private void CheckElements()
	{
		_cursor.Advance(Math.Min(Token.Start, _text.Length) - _cursor.Position);
		_guard.Elements(_nodes.Count + _edges.Count, _cursor);
	}

	/// <summary>
	/// Copies the collections into the document, gives each node its tooltip, and refuses a node mermaid.js cannot draw:
	/// <c>(-text-)</c> makes an <c>ellipse</c>, which its shape table lacks.
	/// </summary>
	private bool Finish()
	{
		foreach (var (id, tooltip) in _tooltips.AsSpan())
		{
			if (Lookup(id, ref _nodeOf) is var node and >= 0)
			{
				_nodes[node].Tooltip = tooltip;
			}
		}

		foreach (var node in _nodes.AsSpan())
		{
			if (node.Shape == "ellipse" && node.Icon is null && node.Image is null && Lookup(node.Id, ref _subgraphOf) < 0)
			{
				return Fail(null, "No such shape: ellipse. Please check your syntax.");
			}
		}

		_document.Nodes.AddRange(_nodes.AsSpan());
		_document.Edges.AddRange(_edges.AsSpan());
		_document.Subgraphs.AddRange(_subgraphs.AsSpan());
		_document.Classes.AddRange(_classes.AsSpan());
		return true;
	}

	// ---- Text ----

	/// <summary>A label as <c>addVertex</c> and <c>addSingleLink</c> keep it: trimmed, and without the quotes of a quoted label.</summary>
	private string Label(ReadOnlySpan<char> text)
	{
		text = TrimJs(text);
		if (text.Length >= 2 && text[0] == '"' && text[^1] == '"')
		{
			text = text[1..^1];
		}

		return MakeString(text);
	}

	/// <summary>The text as a string, with mermaid.js's entity placeholders turned back into the source's text.</summary>
	private string MakeString(ReadOnlySpan<char> text)
	{
		if (!FlowchartText.HasPlaceholder(text))
		{
			return text.ToString();
		}

		var restored = new PooledList<char>(text.Length);
		try
		{
			FlowchartText.Restore(text, ref restored);
			return restored.AsSpan().ToString();
		}
		finally
		{
			restored.Dispose();
		}
	}

	private static ReadOnlySpan<char> TrimJs(ReadOnlySpan<char> text)
	{
		var start = 0;
		while (start < text.Length && PreambleParser.IsSpace(text[start]))
		{
			start++;
		}

		var end = text.Length;
		while (end > start && PreambleParser.IsSpace(text[end - 1]))
		{
			end--;
		}

		return text[start..end];
	}

	private static bool IsBlank(ReadOnlySpan<char> text) => TrimJs(text).IsEmpty;

	private static bool ContainsJsSpace(ReadOnlySpan<char> text)
	{
		foreach (var c in text)
		{
			if (PreambleParser.IsSpace(c))
			{
				return true;
			}
		}

		return false;
	}

	private static string NormalizeDirection(ReadOnlySpan<char> direction)
	{
		var text = TrimJs(direction);
		return text switch
		{
			"TB" or "TD" => "TB",
			"BT" => "BT",
			"LR" => "LR",
			"RL" => "RL",
			"<" => "RL",
			"^" => "BT",
			">" => "LR",
			"v" => "TB",
			_ => text.ToString(),
		};
	}

	// ---- Errors ----

	private bool Fail(string? expected, string? message = null)
	{
		if (_error is null)
		{
			_errorAt = _at;
			_error = message ?? expected;
			_expected = message is null;
		}

		return false;
	}

	private bool _expected;

	private readonly void Report(ReadOnlySpan<char> source, int bodyStart)
	{
		var token = _tokens[Math.Min(_errorAt, _tokens.Length - 1)];
		var offset = Math.Min(token.Start, _text.Length);
		var (line, column) = TextPosition.Of(_text, offset);
		var (bodyLine, _) = TextPosition.Of(source, bodyStart);
		var sourceLine = line + bodyLine - 1;
		string message;
		if (token.Kind == Kind.Error)
		{
			message = string.Create(CultureInfo.InvariantCulture, $"Lexical error on line {line}. Unrecognized text.\n{Excerpt(offset)}");
		}
		else if (_expected)
		{
			message = string.Create(
				CultureInfo.InvariantCulture,
				$"Parse error on line {line}:\n{Excerpt(offset)}\nExpecting {_error}, got '{Name(token.Kind)}'");
		}
		else
		{
			message = _error ?? "The flowchart did not parse.";
		}

		_context.AddError(message, sourceLine, column);
	}

	/// <summary>jison's <c>showPosition</c>: the line up to the error and a caret under it.</summary>
	private readonly string Excerpt(int offset)
	{
		var lineStart = _text[..offset].LastIndexOf('\n') + 1;
		var lineEnd = _text[offset..].IndexOf('\n');
		var line = _text[lineStart..(lineEnd < 0 ? _text.Length : offset + lineEnd)];
		return string.Concat(line, "\n", new string('-', offset - lineStart), "^");
	}

	private static string Name(Kind kind) => kind switch
	{
		Kind.End => "EOF",
		Kind.Eof => "EOF",
		Kind.AccTitle => "acc_title",
		Kind.AccTitleValue => "acc_title_value",
		Kind.AccDescr => "acc_descr",
		Kind.AccDescrValue => "acc_descr_value",
		Kind.AccDescrMultilineValue => "acc_descr_multiline_value",
		Kind.ShapeDataStart or Kind.ShapeData or Kind.ShapeDataString => "SHAPE_DATA",
		Kind.CallbackName => "CALLBACKNAME",
		Kind.CallbackArgs => "CALLBACKARGS",
		Kind.MdStr => "MD_STR",
		Kind.Str => "STR",
		Kind.Style => "STYLE",
		Kind.Default => "DEFAULT",
		Kind.LinkStyle => "LINKSTYLE",
		Kind.Interpolate => "INTERPOLATE",
		Kind.ClassDef => "CLASSDEF",
		Kind.Class => "CLASS",
		Kind.Href => "HREF",
		Kind.Click => "CLICK",
		Kind.Graph => "GRAPH",
		Kind.Subgraph => "subgraph",
		Kind.EndKeyword => "end",
		Kind.LinkTarget => "LINK_TARGET",
		Kind.NoDir => "NODIR",
		Kind.Dir => "DIR",
		Kind.DirectionTb => "direction_tb",
		Kind.DirectionBt => "direction_bt",
		Kind.DirectionRl => "direction_rl",
		Kind.DirectionLr => "direction_lr",
		Kind.DirectionTd => "direction_td",
		Kind.LinkId => "LINK_ID",
		Kind.Num => "NUM",
		Kind.Brkt => "BRKT",
		Kind.StyleSeparator => "STYLE_SEPARATOR",
		Kind.Colon => "COLON",
		Kind.Amp => "AMP",
		Kind.Semi => "SEMI",
		Kind.Comma => "COMMA",
		Kind.Mult => "MULT",
		Kind.Link => "LINK",
		Kind.StartLink => "START_LINK",
		Kind.EdgeText => "EDGE_TEXT",
		Kind.EllipseStart => "(-",
		Kind.EllipseEnd => "-)",
		Kind.Text => "TEXT",
		Kind.StadiumStart => "STADIUMSTART",
		Kind.StadiumEnd => "STADIUMEND",
		Kind.SubroutineStart => "SUBROUTINESTART",
		Kind.SubroutineEnd => "SUBROUTINEEND",
		Kind.VertexWithPropsStart => "VERTEX_WITH_PROPS_START",
		Kind.TagEnd => "TAGEND",
		Kind.CylinderStart => "CYLINDERSTART",
		Kind.CylinderEnd => "CYLINDEREND",
		Kind.DoubleCircleStart => "DOUBLECIRCLESTART",
		Kind.DoubleCircleEnd => "DOUBLECIRCLEEND",
		Kind.TrapStart => "TRAPSTART",
		Kind.TrapEnd => "TRAPEND",
		Kind.InvTrapStart => "INVTRAPSTART",
		Kind.InvTrapEnd => "INVTRAPEND",
		Kind.TagStart => "TAGSTART",
		Kind.Up => "UP",
		Kind.Sep => "SEP",
		Kind.Down => "DOWN",
		Kind.NodeString => "NODE_STRING",
		Kind.Minus => "MINUS",
		Kind.UnicodeText => "UNICODE_TEXT",
		Kind.Pipe => "PIPE",
		Kind.Ps => "PS",
		Kind.Pe => "PE",
		Kind.Sqs => "SQS",
		Kind.Sqe => "SQE",
		Kind.DiamondStart => "DIAMOND_START",
		Kind.DiamondStop => "DIAMOND_STOP",
		Kind.NewLine => "NEWLINE",
		_ => "SPACE",
	};
}
