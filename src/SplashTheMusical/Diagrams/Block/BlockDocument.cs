namespace SplashTheMusical.Diagrams.Block;

/// <summary>
/// A block diagram: the statements <c>block.jison</c> parses, in source order, with the statements of each composite
/// block inside it. mermaid.js's <c>blockDB</c> places a block where its id first appears and lets a later statement
/// change its shape and label, so the model keeps the statements and <see cref="BlockDb"/> runs them as the db does.
/// Text keeps entity codes such as <c>#quot;</c> as written. A number past <see cref="int.MaxValue"/> is read as
/// <see cref="int.MaxValue"/>.
/// </summary>
public sealed class BlockDocument : MermaidDocument
{
	public override string DiagramType => "block";

	public List<BlockStatement> Statements { get; init; } = [];
}

/// <summary>One statement of a block diagram or of a composite block.</summary>
public abstract class BlockStatement;

/// <summary>
/// The shape of a block: the db's <c>typeStr2Type</c> of the brackets around its label. <see cref="Na"/> is a block
/// without brackets, or with a pair the db has no shape for, such as <c>[/"a"]</c>.
/// </summary>
public enum BlockShape
{
	/// <summary><c>na</c>: no brackets, or a pair with no shape.</summary>
	Na,

	/// <summary><c>["a"]</c>.</summary>
	Square,

	/// <summary><c>("a")</c>.</summary>
	Round,

	/// <summary><c>(("a"))</c>.</summary>
	Circle,

	/// <summary><c>&gt;"a"]</c>.</summary>
	RectLeftInvArrow,

	/// <summary><c>{"a"}</c>.</summary>
	Diamond,

	/// <summary><c>{{"a"}}</c>.</summary>
	Hexagon,

	/// <summary><c>(["a"])</c>.</summary>
	Stadium,

	/// <summary><c>[["a"]]</c>.</summary>
	Subroutine,

	/// <summary><c>[("a")]</c>.</summary>
	Cylinder,

	/// <summary><c>((("a")))</c>.</summary>
	DoubleCircle,

	/// <summary><c>[/"a"/]</c>.</summary>
	LeanRight,

	/// <summary><c>[\"a"\]</c>.</summary>
	LeanLeft,

	/// <summary><c>[/"a"\]</c>.</summary>
	Trapezoid,

	/// <summary><c>[\"a"/]</c>.</summary>
	InvTrapezoid,

	/// <summary><c>&lt;["a"]&gt;(right)</c>, with <see cref="BlockNode.Directions"/>.</summary>
	BlockArrow,
}

/// <summary>
/// <c>id</c>, <c>id["label"]</c> or another shape, and <c>:width</c>: the grammar's <c>node</c>. On its own it is a
/// statement. In a <see cref="BlockEdge"/> or as a composite's <see cref="BlockComposite.Header"/> it is one of the
/// nodes of a <c>nodeStatement</c>.
/// </summary>
public sealed class BlockNode : BlockStatement
{
	public BlockNode(string id)
	{
		ArgumentException.ThrowIfNullOrEmpty(id);
		Id = id;
	}

	public string Id { get; }

	/// <summary>The text between the quotes, or <c>null</c> for a block without brackets.</summary>
	public string? Label { get; set; }

	public BlockShape Shape { get; set; }

	/// <summary>
	/// The directions of a <see cref="BlockShape.BlockArrow"/> as the db holds them: <c>right</c>, <c>left</c>,
	/// <c>up</c>, <c>down</c>, <c>x</c> or <c>y</c>, with the space the lexer leaves around them, such as the
	/// <c>" right"</c> of <c>( right)</c>. <c>null</c> for every other shape.
	/// </summary>
	public List<string>? Directions { get; set; }

	/// <summary>The columns of <c>:width</c>, 1 without it. Only the first node of a statement can have one.</summary>
	public int Width { get; set; } = 1;
}

/// <summary>The arrowhead at one end of a <see cref="BlockLink"/>.</summary>
public enum BlockArrowHead
{
	/// <summary>No arrowhead: <c>arrow_open</c> at the start, nothing at the end.</summary>
	None,

	/// <summary><c>&lt;</c> at the start or <c>&gt;</c> at the end: <c>arrow_point</c>.</summary>
	Point,

	/// <summary><c>o</c>: <c>arrow_circle</c>.</summary>
	Circle,

	/// <summary><c>x</c>: <c>arrow_cross</c>.</summary>
	Cross,
}

/// <summary>The line of a <see cref="BlockLink"/>.</summary>
public enum BlockStroke
{
	/// <summary><c>--</c> or <c>~~~</c>: <c>normal</c> and <c>solid</c>.</summary>
	Normal,

	/// <summary><c>==</c>: <c>thick</c>.</summary>
	Thick,

	/// <summary><c>-.-</c>: <c>dotted</c>.</summary>
	Dotted,
}

/// <summary>
/// A link between two nodes: <c>--&gt;</c>, or <c>-- "label" --&gt;</c>. The db reads the arrowheads and the line from
/// the last link token, so the start of <c>&lt;-- "a" --&gt;</c> has no arrowhead.
/// </summary>
public sealed class BlockLink
{
	/// <summary>The text between the quotes, or <c>null</c> for a link without a label.</summary>
	public string? Label { get; set; }

	public BlockArrowHead Start { get; set; }

	public BlockArrowHead End { get; set; } = BlockArrowHead.Point;

	public BlockStroke Stroke { get; set; }
}

/// <summary>
/// <c>a --&gt; b</c>, or a chain such as <c>a --&gt; b --&gt; c</c>: <see cref="Nodes"/> with one of
/// <see cref="Links"/> between each two. The grammar's action reads the id of the chain before the last link from the
/// list the chain gives, which has none, so of a chain of three nodes or more the db keeps a block without an id, the
/// last link from it and the last node.
/// </summary>
public sealed class BlockEdge : BlockStatement
{
	public BlockEdge(BlockNode start, BlockLink link, BlockNode end)
		: this([start, end], [link]) { }

	public BlockEdge(List<BlockNode> nodes, List<BlockLink> links)
	{
		ArgumentNullException.ThrowIfNull(nodes);
		ArgumentNullException.ThrowIfNull(links);
		Nodes = nodes;
		Links = links;
	}

	public List<BlockNode> Nodes { get; }

	public List<BlockLink> Links { get; }
}

/// <summary>
/// <c>block … end</c>, or <c>block:id … end</c>: a block that lays out <see cref="Statements"/> inside it. A nested
/// <c>block-beta … end</c> is the same as <c>block … end</c>.
/// </summary>
public sealed class BlockComposite : BlockStatement
{
	public BlockComposite(List<BlockStatement> statements)
	{
		ArgumentNullException.ThrowIfNull(statements);
		Statements = statements;
	}

	/// <summary>
	/// The <c>nodeStatement</c> after <c>block:</c>, a <see cref="BlockNode"/> whose id, label and width the composite
	/// takes, or a <see cref="BlockEdge"/>, of which the db keeps nothing. <c>null</c> for <c>block</c>, whose id the db
	/// generates.
	/// </summary>
	public BlockStatement? Header { get; set; }

	public List<BlockStatement> Statements { get; }
}

/// <summary><c>columns 3</c>, or <c>columns auto</c>. The last one of a block sets its columns.</summary>
public sealed class BlockColumns : BlockStatement
{
	/// <param name="columns">The number, or <c>null</c> for <c>auto</c>.</param>
	public BlockColumns(int? columns) => Columns = columns;

	public int? Columns { get; }
}

/// <summary><c>space</c>, or <c>space:3</c>: empty cells, each <see cref="Width"/> columns wide.</summary>
public sealed class BlockSpace : BlockStatement
{
	public BlockSpace(int width = 1) => Width = width;

	/// <summary>The number after <c>space:</c>, 1 without one. The db adds this many spaces, each this wide.</summary>
	public int Width { get; }
}

/// <summary><c>classDef name styles</c>.</summary>
public sealed class BlockClassDefinition : BlockStatement
{
	public BlockClassDefinition(string name, List<string> styles)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);
		ArgumentNullException.ThrowIfNull(styles);
		Name = name;
		Styles = styles;
	}

	public string Name { get; }

	/// <summary>The rest of the line, trimmed and split at each <c>,</c>, as <c>addStyleClass</c> splits it.</summary>
	public List<string> Styles { get; }
}

/// <summary>
/// <c>class a,b name</c>. <c>setCssClass</c> splits the ids at each <c>,</c> and looks each up as it is, so
/// <see cref="Ids"/> keeps the space after a comma.
/// </summary>
public sealed class BlockClassAssignment : BlockStatement
{
	public BlockClassAssignment(List<string> ids, string className)
	{
		ArgumentNullException.ThrowIfNull(ids);
		ArgumentNullException.ThrowIfNull(className);
		Ids = ids;
		ClassName = className;
	}

	public List<string> Ids { get; }

	/// <summary>The rest of the line, trimmed.</summary>
	public string ClassName { get; }
}

/// <summary>
/// <c>style id styles</c>. <c>addStyle2Node</c> looks <see cref="Id"/> up whole, so <c>style a,b fill:red</c> styles a
/// block named <c>a,b</c>, and mermaid.js fails on a style for an id no statement before it gives a block.
/// </summary>
public sealed class BlockStyle : BlockStatement
{
	public BlockStyle(string id, List<string> styles)
	{
		ArgumentException.ThrowIfNullOrEmpty(id);
		ArgumentNullException.ThrowIfNull(styles);
		Id = id;
		Styles = styles;
	}

	public string Id { get; }

	/// <summary>The rest of the line, trimmed and split at each <c>,</c>, as <c>addStyle2Node</c> splits it.</summary>
	public List<string> Styles { get; }
}
