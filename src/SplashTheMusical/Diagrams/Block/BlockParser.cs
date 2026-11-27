// Upstream grammar: packages/mermaid/src/diagrams/block/parser/block.jison
using System.Globalization;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Diagrams.State;
using SplashTheMusical.Parsing;
using Kind = SplashTheMusical.Diagrams.Block.BlockTokenKind;

namespace SplashTheMusical.Diagrams.Block;

/// <summary>
/// The parser of <c>block.jison</c> at mermaid 12.3.0. It reads the tokens of <see cref="BlockLexer"/> by recursive
/// descent over the grammar:
/// <code>
/// start: BLOCK_DIAGRAM_KEY document EOF
/// document: statement | statement document
/// statement: nodeStatement | columnsStatement | SPACE_BLOCK | blockStatement | classDefStatement
///     | cssClassStatement | styleStatement
/// nodeStatement: nodeStatement link node | node SIZE | node
/// link: LINK | START_LINK LINK_LABEL STR LINK
/// columnsStatement: COLUMNS
/// blockStatement: id-block nodeStatement document end | BLOCK_DIAGRAM_KEY document end
/// node: NODE_ID | NODE_ID nodeShapeNLabel
/// nodeShapeNLabel: NODE_DSTART STR NODE_DEND | BLOCK_ARROW_START STR dirList BLOCK_ARROW_END
/// dirList: DIR | DIR dirList
/// classDefStatement: classDef CLASSDEF_ID CLASSDEF_STYLEOPTS | classDef DEFAULT CLASSDEF_STYLEOPTS
/// cssClassStatement: class CLASSENTITY_IDS STYLECLASS
/// styleStatement: style STYLE_ENTITY_IDS STYLE_DEFINITION_DATA
/// </code>
/// The lexer never returns <c>DEFAULT</c> after <c>classDef</c>, so the second <c>classDefStatement</c> never
/// matches. The text is prepared as mermaid.js prepares it for every jison diagram, by
/// <see cref="FlowchartText.Prepare"/>. The lexer does not depend on the parser, so the whole text is lexed first.
/// After the parse, <see cref="BlockDb.FindUndefinedStyle"/> refuses a <c>style</c> statement that
/// <c>addStyle2Node</c> fails on, as mermaid.js does.
/// </summary>
internal ref struct BlockParser
{
	private readonly ReadOnlySpan<char> _text;
	private readonly ReadOnlySpan<BlockToken> _tokens;
	private ResourceGuard _guard;
	private SourceCursor _cursor;
	private int _at;
	private string? _expected;
	private int _errorAt;
	private int _elements;

	private PooledStringTable _ids;
	private PooledList<BlockStatement> _statements;
	private PooledList<BlockNode> _nodes;
	private PooledList<BlockLink> _links;
	private PooledList<string> _list;
	private PooledList<char> _scratch;

	private BlockParser(ReadOnlySpan<char> text, ReadOnlySpan<BlockToken> tokens, DiagramParseContext context)
	{
		_text = text;
		_tokens = tokens;
		_guard = ResourceGuard.Start(text, context.Limits);
		_cursor = new SourceCursor(text);
	}

	public static BlockDocument? Parse(ReadOnlySpan<char> source, DiagramParseContext context)
	{
		var body = source[context.BodyStart..];
		var text = new PooledList<char>(body.Length + 16);
		var tokens = new PooledList<BlockToken>(64);
		try
		{
			FlowchartText.Prepare(body, ref text, foldSpaceAfterBraces: false);
			BlockLexer.Lex(text.AsSpan(), ref tokens);
			var parser = new BlockParser(text.AsSpan(), tokens.AsSpan(), context);
			try
			{
				if (!parser.ParseStart())
				{
					parser.Report(source, context);
					return null;
				}

				var statements = parser._statements.AsSpan();
				if (BlockDb.FindUndefinedStyle(statements) is not null)
				{
					var (bodyLine, _) = TextPosition.Of(source, context.BodyStart);
					context.AddError(UndefinedStyleMessage, bodyLine, 1);
					return null;
				}

				var document = new BlockDocument();
				document.Statements.AddRange(statements);
				return document;
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

	/// <summary>What mermaid.js throws when <c>addStyle2Node</c> styles an id that has no block.</summary>
	private const string UndefinedStyleMessage = "Cannot set properties of undefined (setting 'styles')";

	private void Dispose()
	{
		_ids.Dispose();
		_statements.Dispose();
		_nodes.Dispose();
		_links.Dispose();
		_list.Dispose();
		_scratch.Dispose();
	}

	private readonly BlockToken Current => _tokens[Math.Min(_at, _tokens.Length - 1)];

	private readonly Kind Peek => Current.Kind;

	private BlockToken Take()
	{
		var token = Current;
		_at = Math.Min(_at + 1, _tokens.Length - 1);
		return token;
	}

	private bool ParseStart()
	{
		if (Peek != Kind.BlockDiagramKey)
		{
			return Fail("'BLOCK_DIAGRAM_KEY'");
		}

		_ = Take();
		if (!ParseDocument(Kind.Eof, 0))
		{
			return false;
		}

		_ = Take();
		return Peek == Kind.End || Fail("'$end'");
	}

	/// <summary>
	/// <c>document</c>: statements until <paramref name="end"/>, which it leaves, added to <see cref="_statements"/>.
	/// It needs one statement.
	/// </summary>
	private bool ParseDocument(Kind end, int depth)
	{
		_guard.Depth(depth, _cursor);
		var count = 0;
		while (Peek != end || count == 0)
		{
			if (!ParseStatement(depth))
			{
				return false;
			}

			count++;
			_cursor.Advance(Math.Min(Current.Start, _text.Length) - _cursor.Position);
			_guard.Statement(_cursor);
		}

		return true;
	}

	private bool ParseStatement(int depth)
	{
		var token = Current;
		switch (token.Kind)
		{
			case Kind.NodeId:
				if (!ParseNodeStatement(out var statement))
				{
					return false;
				}

				_statements.Add(statement);
				return true;
			case Kind.Columns:
				_ = Take();
				_statements.Add(new BlockColumns(Number(token.Text(_text))));
				return true;
			case Kind.ColumnsAuto:
				_ = Take();
				_statements.Add(new BlockColumns(null));
				return true;
			case Kind.SpaceBlock:
				_ = Take();
				Count(1);
				_statements.Add(new BlockSpace(token.Length == 0 ? 1 : Number(token.Text(_text))));
				return true;
			case Kind.IdBlock or Kind.BlockDiagramKey:
				return ParseComposite(depth);
			case Kind.ClassDef:
				return ParseClassDefinition();
			case Kind.Class:
				return ParseClassAssignment();
			case Kind.Style:
				return ParseStyle();
			default:
				return Fail("'SPACE_BLOCK', 'BLOCK_DIAGRAM_KEY', 'id-block', 'COLUMNS', 'classDef', 'class', 'style', 'NODE_ID'");
		}
	}

	/// <summary><c>blockStatement</c>: <c>block:</c> and a <c>nodeStatement</c>, or <c>block</c>, then the statements and <c>end</c>.</summary>
	private bool ParseComposite(int depth)
	{
		BlockStatement? header = null;
		if (Take().Kind == Kind.IdBlock)
		{
			if (Peek != Kind.NodeId)
			{
				return Fail("'NODE_ID'");
			}

			if (!ParseNodeStatement(out header))
			{
				return false;
			}
		}

		Count(1);
		var from = _statements.Count;
		if (!ParseDocument(Kind.EndKeyword, depth + 1))
		{
			return false;
		}

		_ = Take();
		var children = _statements.AsSpan()[from..];
		var list = new List<BlockStatement>(children.Length);
		list.AddRange(children);
		_statements.Truncate(from);
		_statements.Add(new BlockComposite(list) { Header = header });
		return true;
	}

	/// <summary><c>nodeStatement</c>: a node, its <c>SIZE</c>, and the links and nodes after it.</summary>
	private bool ParseNodeStatement(out BlockStatement statement)
	{
		statement = null!;
		if (!ParseNode(out var first))
		{
			return false;
		}

		if (Peek == Kind.Size)
		{
			first.Width = Number(Take().Text(_text));
		}

		if (Peek is not (Kind.Link or Kind.StartLink))
		{
			statement = first;
			return true;
		}

		var nodesFrom = _nodes.Count;
		var linksFrom = _links.Count;
		_nodes.Add(first);
		while (Peek is Kind.Link or Kind.StartLink)
		{
			if (!ParseLink(out var link))
			{
				return false;
			}

			if (Peek != Kind.NodeId)
			{
				return Fail("'NODE_ID'");
			}

			if (!ParseNode(out var node))
			{
				return false;
			}

			_links.Add(link);
			_nodes.Add(node);
			Count(1);
		}

		var nodes = new List<BlockNode>(_nodes.Count - nodesFrom);
		nodes.AddRange(_nodes.AsSpan()[nodesFrom..]);
		var links = new List<BlockLink>(_links.Count - linksFrom);
		links.AddRange(_links.AsSpan()[linksFrom..]);
		_nodes.Truncate(nodesFrom);
		_links.Truncate(linksFrom);
		statement = new BlockEdge(nodes, links);
		return true;
	}

	/// <summary><c>node</c>: <c>NODE_ID</c> and its brackets and label.</summary>
	private bool ParseNode(out BlockNode node)
	{
		node = null!;
		var id = Id(Take().Text(_text));
		Count(1);
		switch (Peek)
		{
			case Kind.NodeDstart:
				{
					var start = Take().Text(_text);
					if (Peek != Kind.Str)
					{
						return Fail("'STR'");
					}

					var label = Plain(Take().Text(_text));
					if (Peek != Kind.NodeDend)
					{
						return Fail("'NODE_DEND'");
					}

					node = new BlockNode(id) { Label = label, Shape = ShapeOf(start, Take().Text(_text)) };
					return true;
				}

			case Kind.BlockArrowStart:
				{
					_ = Take();
					if (Peek != Kind.Str)
					{
						return Fail("'STR'");
					}

					var label = Plain(Take().Text(_text));
					if (Peek != Kind.Dir)
					{
						return Fail("'DIR'");
					}

					var from = _list.Count;
					while (Peek == Kind.Dir)
					{
						_list.Add(Direction(Take().Text(_text)));
					}

					if (Peek != Kind.BlockArrowEnd)
					{
						_list.Truncate(from);
						return Fail("'DIR', 'BLOCK_ARROW_END'");
					}

					_ = Take();
					node = new BlockNode(id) { Label = label, Shape = BlockShape.BlockArrow, Directions = List(from) };
					return true;
				}

			default:
				node = new BlockNode(id);
				return true;
		}
	}

	/// <summary><c>link</c>: the db's <c>edgeStrTo…</c> functions over the last <c>LINK</c>.</summary>
	private bool ParseLink(out BlockLink link)
	{
		link = null!;
		string? label = null;
		var token = Take();
		if (token.Kind == Kind.StartLink)
		{
			if (Peek != Kind.LinkLabel)
			{
				return Fail("'LINK_LABEL'");
			}

			_ = Take();
			if (Peek != Kind.Str)
			{
				return Fail("'STR'");
			}

			label = Plain(Take().Text(_text));
			if (Peek != Kind.Link)
			{
				return Fail("'LINK'");
			}

			token = Take();
		}

		var text = token.Text(_text);
		var trimmed = StateLexer.Trim(text);
		link = new BlockLink
		{
			Label = label,
			Start = trimmed[0] switch
			{
				'x' => BlockArrowHead.Cross,
				'o' => BlockArrowHead.Circle,
				'<' => BlockArrowHead.Point,
				_ => BlockArrowHead.None,
			},
			End = trimmed[^1] switch
			{
				'x' => BlockArrowHead.Cross,
				'o' => BlockArrowHead.Circle,
				'>' => BlockArrowHead.Point,
				_ => BlockArrowHead.None,
			},
			Stroke = text.Contains("==", StringComparison.Ordinal) ? BlockStroke.Thick
				: text.Contains(".-", StringComparison.Ordinal) ? BlockStroke.Dotted
				: BlockStroke.Normal,
		};
		return true;
	}

	/// <summary><c>classDef CLASSDEF_ID CLASSDEF_STYLEOPTS</c>, with both trimmed.</summary>
	private bool ParseClassDefinition()
	{
		_ = Take();
		if (Peek != Kind.ClassDefId)
		{
			return Fail("'CLASSDEF_ID', 'DEFAULT'");
		}

		var name = Id(StateLexer.Trim(Take().Text(_text)));
		if (Peek != Kind.ClassDefStyleOpts)
		{
			return Fail("'CLASSDEF_STYLEOPTS'");
		}

		_statements.Add(new BlockClassDefinition(name, Styles(Take().Text(_text))));
		return true;
	}

	/// <summary><c>class CLASSENTITY_IDS STYLECLASS</c>, with the ids split at each <c>,</c> as <c>setCssClass</c> splits them.</summary>
	private bool ParseClassAssignment()
	{
		_ = Take();
		if (Peek != Kind.ClassEntityIds)
		{
			return Fail("'CLASSENTITY_IDS'");
		}

		var ids = Take().Text(_text);
		if (Peek != Kind.StyleClass)
		{
			return Fail("'STYLECLASS'");
		}

		var from = _list.Count;
		foreach (var range in ids.Split(','))
		{
			_list.Add(Id(ids[range]));
		}

		var list = List(from);
		_statements.Add(new BlockClassAssignment(list, Plain(StateLexer.Trim(Take().Text(_text)))));
		return true;
	}

	/// <summary><c>style STYLE_ENTITY_IDS STYLE_DEFINITION_DATA</c>.</summary>
	private bool ParseStyle()
	{
		_ = Take();
		if (Peek != Kind.StyleEntityIds)
		{
			return Fail("'STYLE_ENTITY_IDS'");
		}

		var id = Id(Take().Text(_text));
		if (Peek != Kind.StyleDefinitionData)
		{
			return Fail("'STYLE_DEFINITION_DATA'");
		}

		_statements.Add(new BlockStyle(id, Styles(Take().Text(_text))));
		return true;
	}

	/// <summary>The text trimmed and split at each <c>,</c>.</summary>
	private List<string> Styles(ReadOnlySpan<char> text)
	{
		var trimmed = StateLexer.Trim(text);
		var from = _list.Count;
		foreach (var range in trimmed.Split(','))
		{
			_list.Add(Plain(trimmed[range]));
		}

		return List(from);
	}

	/// <summary>The db's <c>typeStr2Type</c> of the brackets.</summary>
	private static BlockShape ShapeOf(ReadOnlySpan<char> start, ReadOnlySpan<char> end) => start switch
	{
		"[" when end is "]" => BlockShape.Square,
		"(" when end is ")" => BlockShape.Round,
		"((" when end is "))" => BlockShape.Circle,
		">" when end is "]" => BlockShape.RectLeftInvArrow,
		"{" when end is "}" => BlockShape.Diamond,
		"{{" when end is "}}" => BlockShape.Hexagon,
		"([" when end is "])" => BlockShape.Stadium,
		"[[" when end is "]]" => BlockShape.Subroutine,
		"[(" when end is ")]" => BlockShape.Cylinder,
		"(((" when end is ")))" => BlockShape.DoubleCircle,
		"[/" when end is "/]" => BlockShape.LeanRight,
		"[\\" when end is "\\]" => BlockShape.LeanLeft,
		"[/" when end is "\\]" => BlockShape.Trapezoid,
		"[\\" when end is "/]" => BlockShape.InvTrapezoid,
		_ => BlockShape.Na,
	};

	/// <summary>A direction as the db holds it. The six words without space are the same strings each time.</summary>
	private string Direction(ReadOnlySpan<char> text) => text switch
	{
		"right" => "right",
		"left" => "left",
		"x" => "x",
		"y" => "y",
		"up" => "up",
		"down" => "down",
		_ => text.ToString(),
	};

	/// <summary><c>parseInt</c> of the digits, up to <see cref="int.MaxValue"/>.</summary>
	private static int Number(ReadOnlySpan<char> digits) =>
		int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : int.MaxValue;

	private void Count(int count)
	{
		_elements += count;
		_guard.Elements(_elements, _cursor);
	}

	/// <summary>The strings of <see cref="_list"/> from <paramref name="from"/> on as a list, taken off it.</summary>
	private List<string> List(int from)
	{
		var items = _list.AsSpan()[from..];
		var list = new List<string>(items.Length);
		list.AddRange(items);
		_list.Truncate(from);
		return list;
	}

	/// <summary>An id, interned, with mermaid.js's entity placeholders turned back into the source's text.</summary>
	private string Id(ReadOnlySpan<char> text)
	{
		if (!FlowchartText.HasPlaceholder(text))
		{
			return _ids[_ids.GetOrAdd(text)];
		}

		_scratch.Truncate(0);
		FlowchartText.Restore(text, ref _scratch);
		return _ids[_ids.GetOrAdd(_scratch.AsSpan())];
	}

	/// <summary>Text, with mermaid.js's entity placeholders turned back into the source's text.</summary>
	private string Plain(ReadOnlySpan<char> text)
	{
		if (!FlowchartText.HasPlaceholder(text))
		{
			return text.ToString();
		}

		_scratch.Truncate(0);
		FlowchartText.Restore(text, ref _scratch);
		return _scratch.AsSpan().ToString();
	}

	private bool Fail(string? expected)
	{
		if (_expected is null)
		{
			_errorAt = _at;
			_expected = expected ?? "";
		}

		return false;
	}

	private readonly void Report(ReadOnlySpan<char> source, DiagramParseContext context)
	{
		var token = _tokens[Math.Min(_errorAt, _tokens.Length - 1)];
		var offset = Math.Min(token.Start, _text.Length);
		var (line, column) = TextPosition.Of(_text, offset);
		var (bodyLine, _) = TextPosition.Of(source, context.BodyStart);
		var message = token.Kind == Kind.Error
			? string.Create(CultureInfo.InvariantCulture, $"Lexical error on line {line}. Unrecognized text.\n{Excerpt(offset)}")
			: string.Create(
				CultureInfo.InvariantCulture,
				$"Parse error on line {line}:\n{Excerpt(offset)}\nExpecting {(_expected is { Length: > 0 } ? _expected : "a statement")}, got '{token.Kind}'");
		context.AddError(message, line + bodyLine - 1, column);
	}

	/// <summary>jison's <c>showPosition</c>: the line up to the error and a caret under it.</summary>
	private readonly string Excerpt(int offset)
	{
		var lineStart = _text[..offset].LastIndexOf('\n') + 1;
		var lineEnd = _text[offset..].IndexOf('\n');
		var line = _text[lineStart..(lineEnd < 0 ? _text.Length : offset + lineEnd)];
		return string.Concat(line, "\n", new string('-', offset - lineStart), "^");
	}
}
