namespace SplashTheMusical.Diagrams.Flowchart;

/// <summary>
/// A flowchart: what mermaid.js's <c>FlowDB</c> holds after <c>flow.jison</c> has parsed the source. The nodes, edges,
/// subgraphs and classes keep the text the source gave them. Labels keep entity codes such as <c>#quot;</c> as written,
/// and are not sanitized.
/// </summary>
public sealed class FlowchartDocument : MermaidDocument
{
	public FlowchartDocument()
		: this("flowchart-v2") { }

	/// <param name="diagramType">The detector id: <c>flowchart-v2</c>, or <c>flowchart-elk</c> for a source that starts with <c>flowchart-elk</c>.</param>
	public FlowchartDocument(string diagramType)
	{
		ArgumentNullException.ThrowIfNull(diagramType);
		DiagramType = diagramType;
	}

	public override string DiagramType { get; }

	/// <summary>The header keyword: <c>graph</c>, <c>flowchart</c>, <c>flowchart-elk</c> or <c>swimlane-beta</c>.</summary>
	public string Keyword { get; set; } = "flowchart";

	/// <summary>
	/// The direction, as <c>setDirection</c> normalizes it: <c>TB</c>, <c>BT</c>, <c>LR</c> or <c>RL</c>, with <c>TD</c>
	/// and <c>v</c> read as <c>TB</c>, <c>&lt;</c> as <c>RL</c>, <c>&gt;</c> as <c>LR</c> and <c>^</c> as <c>BT</c>.
	/// <c>BR</c> stays <c>BR</c>, as in mermaid.js.
	/// </summary>
	public string Direction { get; set; } = "TB";

	/// <summary>The nodes, in the order the source first names them.</summary>
	public List<FlowchartNode> Nodes { get; init; } = [];

	/// <summary>The edges, in source order. <c>linkStyle</c> numbers them from 0 in this order.</summary>
	public List<FlowchartEdge> Edges { get; init; } = [];

	/// <summary>
	/// The subgraphs, in the order mermaid.js adds them: a subgraph is added at its <c>end</c>, so a nested one comes
	/// before the subgraph that holds it.
	/// </summary>
	public List<FlowchartSubgraph> Subgraphs { get; init; } = [];

	/// <summary>The <c>classDef</c> classes, in the order the source first defines them.</summary>
	public List<FlowchartClass> Classes { get; init; } = [];

	/// <summary>The styles of <c>linkStyle default</c>, or <c>null</c> when the source has none.</summary>
	public List<string>? DefaultEdgeStyles { get; set; }

	/// <summary>The curve of <c>linkStyle default interpolate</c>, or <c>null</c> when the source has none.</summary>
	public string? DefaultInterpolate { get; set; }
}

/// <summary>How mermaid.js reads a label: plain text, a <c>"string"</c>, or a <c>"`Markdown string`"</c>.</summary>
public enum FlowchartLabelType
{
	Text,
	String,
	Markdown,
}

/// <summary>The arrow ends of an edge, as <c>destructLink</c> names them.</summary>
public enum FlowchartEdgeType
{
	/// <summary><c>arrow_open</c>: no arrowhead, as in <c>---</c>.</summary>
	Open,

	/// <summary><c>arrow_point</c>: an arrowhead at the end, as in <c>--&gt;</c>.</summary>
	Point,

	/// <summary><c>arrow_circle</c>, as in <c>--o</c>.</summary>
	Circle,

	/// <summary><c>arrow_cross</c>, as in <c>--x</c>.</summary>
	Cross,

	/// <summary><c>double_arrow_point</c>, as in <c>&lt;--&gt;</c>.</summary>
	DoublePoint,

	/// <summary><c>double_arrow_circle</c>, as in <c>o--o</c>.</summary>
	DoubleCircle,

	/// <summary><c>double_arrow_cross</c>, as in <c>x--x</c>.</summary>
	DoubleCross,

	/// <summary><c>INVALID</c>: the two halves of a <c>-- text --&gt;</c> edge disagree, as in <c>-- text ==&gt;</c>.</summary>
	Invalid,
}

/// <summary>The line of an edge.</summary>
public enum FlowchartStroke
{
	Normal,
	Thick,
	Dotted,
	Invisible,

	/// <summary>The two halves of a <c>-- text --&gt;</c> edge disagree.</summary>
	Invalid,
}

public sealed class FlowchartNode
{
	public FlowchartNode(string id)
	{
		ArgumentNullException.ThrowIfNull(id);
		Id = id;
		Label = id;
	}

	public string Id { get; }

	/// <summary>The label, trimmed, with the quotes of a quoted label taken off. A node the source gives no label is labelled with its id.</summary>
	public string Label { get; set; }

	public FlowchartLabelType LabelType { get; set; }

	/// <summary>
	/// The shape as the source names it: the bracket shape's name, such as <c>square</c>, <c>round</c> or
	/// <c>diamond</c>, or the <c>shape</c> of a <c>@{ }</c> block. <c>null</c> for a node without brackets, which is drawn
	/// as <c>squareRect</c>.
	/// </summary>
	public string? Shape { get; set; }

	/// <summary>The declarations of <c>style</c> statements.</summary>
	public List<string> Styles { get; init; } = [];

	/// <summary>The classes of <c>:::</c>, <c>class</c> and <c>click</c>, which adds <c>clickable</c>.</summary>
	public List<string> Classes { get; init; } = [];

	/// <summary>The field and value of a <c>[|field:value|text]</c> node.</summary>
	public KeyValuePair<string, string>? Property { get; set; }

	/// <summary>The URL of a <c>click</c> link, as written.</summary>
	public string? Link { get; set; }

	/// <summary>The target of a <c>click</c> link, such as <c>_blank</c>.</summary>
	public string? LinkTarget { get; set; }

	/// <summary>The function a <c>click</c> statement calls. mermaid.js binds it only with <c>securityLevel: loose</c>.</summary>
	public string? Callback { get; set; }

	/// <summary>The arguments of a <c>click ... call f(args)</c>, as written.</summary>
	public string? CallbackArguments { get; set; }

	/// <summary>The tooltip of a <c>click</c> statement.</summary>
	public string? Tooltip { get; set; }

	/// <summary>The <c>icon</c> of a <c>@{ }</c> block, such as <c>fa:user</c>.</summary>
	public string? Icon { get; set; }

	/// <summary>The <c>form</c> of an icon node: <c>square</c>, <c>circle</c> or <c>rounded</c>.</summary>
	public string? Form { get; set; }

	/// <summary>Where an icon or image node puts its label: <c>t</c> or <c>b</c>.</summary>
	public string? LabelPosition { get; set; }

	/// <summary>The <c>img</c> URL of a <c>@{ }</c> block.</summary>
	public string? Image { get; set; }

	/// <summary>The <c>constraint</c> of an image node: <c>on</c> keeps the image's aspect ratio.</summary>
	public string? Constraint { get; set; }

	/// <summary>The <c>w</c> of an icon or image node, in pixels.</summary>
	public double? AssetWidth { get; set; }

	/// <summary>The <c>h</c> of an icon or image node, in pixels.</summary>
	public double? AssetHeight { get; set; }
}

public sealed class FlowchartEdge
{
	public FlowchartEdge(string start, string end, string id)
	{
		ArgumentNullException.ThrowIfNull(start);
		ArgumentNullException.ThrowIfNull(end);
		ArgumentNullException.ThrowIfNull(id);
		Start = start;
		End = end;
		Id = id;
	}

	public string Start { get; }

	public string End { get; }

	/// <summary>The id the source gives with <c>id@--&gt;</c>, or mermaid.js's <c>L_start_end_n</c>.</summary>
	public string Id { get; }

	/// <summary>Whether the source gave <see cref="Id"/>.</summary>
	public bool IsUserDefinedId { get; set; }

	public FlowchartEdgeType Type { get; set; } = FlowchartEdgeType.Point;

	public FlowchartStroke Stroke { get; set; }

	/// <summary>The extra length of the edge from its count of dashes, dots or equals signs, at most 10. <c>null</c> for an <see cref="FlowchartEdgeType.Invalid"/> edge.</summary>
	public int? Length { get; set; } = 1;

	/// <summary>The label, trimmed, or empty.</summary>
	public string Label { get; set; } = "";

	public FlowchartLabelType LabelType { get; set; }

	/// <summary>The curve of <c>linkStyle ... interpolate</c> or of <c>curve</c> in a <c>@{ }</c> block.</summary>
	public string? Interpolate { get; set; }

	/// <summary>The declarations of <c>linkStyle</c>, with <c>fill:none</c> added when they set no fill. <c>null</c> when none applies.</summary>
	public List<string>? Styles { get; set; }

	public List<string> Classes { get; init; } = [];

	/// <summary>The <c>animate</c> of a <c>@{ }</c> block.</summary>
	public bool? Animate { get; set; }

	/// <summary>The <c>animation</c> of a <c>@{ }</c> block: <c>fast</c> or <c>slow</c>.</summary>
	public string? Animation { get; set; }

	/// <summary>The <c>step</c> of a <c>@{ }</c> block, a positive integer.</summary>
	public int? Step { get; set; }
}

public sealed class FlowchartSubgraph
{
	public FlowchartSubgraph(string id, string title)
	{
		ArgumentNullException.ThrowIfNull(id);
		ArgumentNullException.ThrowIfNull(title);
		Id = id;
		Title = title;
	}

	/// <summary>The id the source gives, or <c>subGraph</c> and a counter for a title with spaces and no id.</summary>
	public string Id { get; }

	public string Title { get; set; }

	public FlowchartLabelType LabelType { get; set; }

	/// <summary>
	/// The ids of the members, nodes and subgraphs, in mermaid.js's order. A node that an earlier subgraph already holds
	/// is left out.
	/// </summary>
	public List<string> Nodes { get; init; } = [];

	/// <summary>The direction of a <c>direction</c> statement in the subgraph, as written, or <c>null</c>.</summary>
	public string? Direction { get; set; }

	public List<string> Classes { get; init; } = [];

	/// <summary>The <c>@{ }</c> block of a <c>id@{ }</c> statement after the subgraph, such as <c>view: collapsed</c>.</summary>
	public MermaidValue? Metadata { get; set; }
}

/// <summary>A <c>classDef</c> class.</summary>
public sealed class FlowchartClass
{
	public FlowchartClass(string id)
	{
		ArgumentNullException.ThrowIfNull(id);
		Id = id;
	}

	public string Id { get; }

	/// <summary>The declarations, split at commas that are not escaped as <c>\,</c>.</summary>
	public List<string> Styles { get; init; } = [];

	/// <summary>The declarations that mention <c>color</c>, with a <c>fill</c> renamed <c>bgFill</c>, which mermaid.js applies to the label.</summary>
	public List<string> TextStyles { get; init; } = [];
}
