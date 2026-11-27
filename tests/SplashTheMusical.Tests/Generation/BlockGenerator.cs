using CsCheck;
using SplashTheMusical.Diagrams.Block;

namespace SplashTheMusical.Tests.Generation;

/// <summary>
/// Random block diagrams in the form <see cref="BlockParser"/> gives them: every statement of the model, blocks of
/// every shape with and without labels and widths, block arrows with directions and the space around them, edges and
/// chains with every stroke and arrowhead, composites nested two deep with and without a header, columns, spaces,
/// classes and styles. Ids hold the characters <c>NODE_ID</c> allows, such as <c>~</c>, <c>,</c> and <c>"</c>, and
/// entity codes.
/// </summary>
/// <remarks>
/// Labels are <see cref="GenText.Label"/> without <c>"</c>, which no label can hold, a <c>`</c> at the start, which
/// starts a Markdown string, <c>%</c>, which could make a comment line, and a <c>=</c> at the end, whose <c>="</c>
/// <c>cleanupText</c> rewrites inside an HTML tag. A link without a label never starts with
/// a circle or a cross, which the lexer would read as an id, there is no <c>columns 0</c>, which mermaid.js fails to
/// lay out, and a <c>style</c> with styles only names a block a statement before it gives.
/// </remarks>
public static class BlockGenerator
{
	private static readonly string[] Ids = ["A", "b1", "node_2", "x.y", "Ünï", "日本", "a,b", "a/b", "~~~", "a\"b", "n#35;1", "x", "o", "1"];

	private static readonly string[] Words = ["A", "b1", "node_2", "c0"];

	private static readonly string[] DirectionWords = ["right", "left", "up", "down", "x", "y"];

	private static readonly string[] StyleDeclarations = ["fill:#f9f", "stroke:#333", "stroke-width:4px", "color:#fff", "fill:#f9f;", "stroke-dasharray: 5 5"];

	private static readonly string[] ClassNames = ["c0", "c1", "c0 c1", ""];

	private static readonly Gen<string> Label = GenText.Label.Select(Clean);

	private static readonly Gen<string> Id = Gen.OneOf(Ids);

	private static readonly Gen<List<string>> Directions =
		Gen.Select(
			Gen.OneOf(DirectionWords).Array[1, 3],
			Gen.OneOf("", " ", "\t"),
			Gen.OneOf("", "", " "),
			(words, leading, trailing) =>
			{
				var list = words.Select(w => w + trailing).ToList();
				list[0] = leading + list[0];
				return list;
			});

	private static readonly Gen<BlockNode> PlainNode =
		Gen.Select(
			Id,
			Gen.Frequency(
				(3, Gen.Const((BlockShape?)null)),
				(5, Gen.OneOf(Enum.GetValues<BlockShape>()).Select(s => (BlockShape?)s))),
			Label,
			Directions,
			(id, shape, label, directions) => shape switch
			{
				null => new BlockNode(id),
				BlockShape.BlockArrow => new BlockNode(id) { Shape = BlockShape.BlockArrow, Label = label, Directions = directions },
				{ } s => new BlockNode(id) { Shape = s, Label = label },
			});

	private static readonly Gen<int> Width = Gen.Frequency((4, Gen.Const(1)), (2, Gen.Int[2, 5]), (1, Gen.Const(0)));

	private static readonly Gen<BlockNode> FirstNode = Gen.Select(PlainNode, Width, (node, width) =>
	{
		node.Width = width;
		return node;
	});

	private static readonly Gen<BlockLink> Link =
		Gen.Select(
			Label.OrNull(),
			Gen.OneOf(Enum.GetValues<BlockArrowHead>()),
			Gen.OneOf(Enum.GetValues<BlockArrowHead>()),
			Gen.OneOf(Enum.GetValues<BlockStroke>()),
			(label, start, end, stroke) => new BlockLink
			{
				Label = label,
				Start = label is null && start is BlockArrowHead.Circle or BlockArrowHead.Cross ? BlockArrowHead.Point : start,
				End = end,
				Stroke = stroke,
			});

	private static readonly Gen<BlockStatement> Edge =
		Gen.Select(
			FirstNode,
			Gen.Select(Link, PlainNode).Array[1, 2],
			(first, rest) => (BlockStatement)new BlockEdge([first, .. rest.Select(r => r.Item2)], [.. rest.Select(r => r.Item1)]));

	private static readonly Gen<BlockStatement> Columns =
		Gen.Int[1, 5].Select(c => (int?)c).OrNullValue().Select(c => (BlockStatement)new BlockColumns(c));

	private static readonly Gen<BlockStatement> Space = Gen.Int[0, 4].Select(w => (BlockStatement)new BlockSpace(w));

	private static readonly Gen<List<string>> Styles =
		Gen.OneOf(StyleDeclarations).Array[1, 3].Select(styles => new List<string>(styles));

	private static readonly Gen<BlockStatement> ClassDefinition =
		Gen.Select(Gen.OneOf("c0", "c1", "default"), Styles, (name, styles) => (BlockStatement)new BlockClassDefinition(name, styles));

	private static readonly Gen<BlockStatement> ClassAssignment =
		Gen.Select(
			Gen.OneOf(Words),
			Gen.Select(Gen.OneOf("", " ", "  "), Gen.OneOf(Words), (space, word) => space + word).Array[0, 2],
			Gen.OneOf(ClassNames),
			(first, rest, name) => (BlockStatement)new BlockClassAssignment([first, .. rest], name));

	private static readonly Gen<BlockStatement> Style =
		Gen.Select(
			Gen.OneOf(Words),
			Gen.Frequency((1, Gen.Const(new List<string> { "" })), (3, Styles)),
			(id, styles) => (BlockStatement)new BlockStyle(id, styles));

	private static readonly Gen<BlockStatement> Leaf = Gen.Frequency(
		(6, FirstNode.Select(n => (BlockStatement)n)),
		(4, Edge),
		(1, Columns),
		(2, Space),
		(1, ClassDefinition),
		(1, ClassAssignment),
		(1, Style));

	private static readonly Gen<BlockStatement> Inner = Composite(Leaf);

	private static readonly Gen<BlockStatement> Statement = Gen.Frequency((10, Leaf), (2, Composite(Gen.Frequency((10, Leaf), (1, Inner)))));

	public static Gen<BlockDocument> Document { get; } =
		Statement.List[1, 8].Select(statements => new BlockDocument { Statements = WithDefinedStyles(statements) });

	/// <summary><c>block … end</c> or <c>block:node … end</c>, with one to four of <paramref name="statement"/> inside.</summary>
	private static Gen<BlockStatement> Composite(Gen<BlockStatement> statement) =>
		Gen.Select(
			Gen.Frequency(
				(2, Gen.Const((BlockStatement?)null)),
				(4, FirstNode.Select(n => (BlockStatement?)n)),
				(1, Edge.Select(e => (BlockStatement?)e))),
			statement.List[1, 4],
			(header, statements) => (BlockStatement)new BlockComposite(statements) { Header = header });

	/// <summary>
	/// The statements with no styles in each style mermaid.js fails on, which names no block a statement before it
	/// gives.
	/// </summary>
	private static List<BlockStatement> WithDefinedStyles(List<BlockStatement> statements)
	{
		while (BlockDb.FindUndefinedStyle(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(statements)) is { } style)
		{
			Replace(statements, style, new BlockStyle(style.Id, [""]));
		}

		return statements;
	}

	private static bool Replace(List<BlockStatement> statements, BlockStyle style, BlockStyle empty)
	{
		var at = statements.IndexOf(style);
		if (at >= 0)
		{
			statements[at] = empty;
			return true;
		}

		return statements.OfType<BlockComposite>().Any(c => Replace(c.Statements, style, empty));
	}

	/// <summary>A label a <c>STR</c> holds as it is.</summary>
	private static string Clean(string label)
	{
		var clean = label.Replace('"', '\'').Replace("%", "", StringComparison.Ordinal);
		if (clean.Length == 0)
		{
			return "a";
		}

		clean = clean[0] == '`' ? "a" + clean : clean;
		return clean[^1] == '=' ? clean + "a" : clean;
	}

	/// <summary>The value or <c>null</c>, one time in three.</summary>
	private static Gen<T?> OrNullValue<T>(this Gen<T?> value)
		where T : struct =>
		Gen.Frequency((1, Gen.Const((T?)null)), (2, value));
}
