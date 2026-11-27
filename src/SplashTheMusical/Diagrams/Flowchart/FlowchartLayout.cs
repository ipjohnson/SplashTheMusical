using SplashTheMusical.Layout.Dagre;
using SplashTheMusical.Rendering;
using SplashTheMusical.Rendering.Shapes;

namespace SplashTheMusical.Diagrams.Flowchart;

/// <summary>
/// A laid out flowchart: the size of the root <c>svg</c>, its viewBox, and the drawing mermaid.js's dagre renderer
/// leaves, a <c>g.root</c> of clusters, edges, edge labels and nodes in which an extracted subgraph is a node holding a
/// <c>g.root</c> of its own.
/// </summary>
public sealed class FlowchartLayout : DiagramLayout
{
	internal FlowchartLayout(
		double width,
		double? height,
		SvgViewBox viewBox,
		FlowchartDrawing root,
		FlowchartTitle? title,
		FlowchartStyle style,
		IReadOnlyList<DagreInput> dagreInputs,
		IReadOnlyList<CssRule> diagramRules,
		IReadOnlyList<StyleClass> classes)
		: base(width, height)
	{
		ViewBox = viewBox;
		Root = root;
		Title = title;
		Style = style;
		DagreInputs = dagreInputs;
		DiagramRules = diagramRules;
		Classes = classes;
		ElementRules = FlowchartStyles.ElementRules(root);
	}

	internal override IReadOnlyList<CssRule> DiagramRules { get; }

	internal override IReadOnlyList<StyleClass> Classes { get; }

	internal override IReadOnlyList<CssRule> ElementRules { get; }

	internal FlowchartDrawing Root { get; }

	internal FlowchartTitle? Title { get; }

	internal FlowchartStyle Style { get; }

	/// <summary>The graph of each call to dagre's <c>layout</c>, in call order: each extracted subgraph before the graph that holds it.</summary>
	internal IReadOnlyList<DagreInput> DagreInputs { get; }

	/// <summary>
	/// The box of each node, subgraph, edge and edge label in the viewBox's space, named as the oracle's flowchart
	/// extractor names them: a node or subgraph by its id, an edge and its label by <c>start --&gt; end</c>, with
	/// <c> #2</c> and on for a name used again, in the order of the document's edges.
	/// </summary>
	internal IReadOnlyList<FlowchartBox> Boxes() => BoxesOf(Root);

	/// <inheritdoc cref="Boxes"/>
	internal static IReadOnlyList<FlowchartBox> BoxesOf(FlowchartDrawing root)
	{
		var boxes = new List<FlowchartBox>();
		var edges = new List<(FlowchartEdgeDrawing Edge, double Dx, double Dy)>();
		Collect(root, 0, 0, boxes, edges);
		var used = new Dictionary<string, int>(StringComparer.Ordinal);
		foreach (var (edge, dx, dy) in edges.OrderBy(e => e.Edge.Index))
		{
			var name = edge.Start + " --> " + edge.End;
			var count = used[name] = used.GetValueOrDefault(name) + 1;
			var unique = count > 1 ? $"{name} #{count}" : name;
			if (!edge.PathBounds.IsEmpty)
			{
				boxes.Add(new FlowchartBox("edge", unique, edge.PathBounds.MinX + dx, edge.PathBounds.MinY + dy, edge.PathBounds.Width, edge.PathBounds.Height));
			}

			if (edge.Label is { HasText: true } label)
			{
				boxes.Add(new FlowchartBox("edge-label", unique, edge.LabelX - (label.Width / 2) + dx, edge.LabelY - (label.Height / 2) + dy, label.Width, label.Height));
			}
		}

		return boxes;
	}

	private static void Collect(FlowchartDrawing drawing, double dx, double dy, List<FlowchartBox> boxes, List<(FlowchartEdgeDrawing, double, double)> edges)
	{
		foreach (var cluster in drawing.Clusters)
		{
			boxes.Add(new FlowchartBox("group", cluster.Id, cluster.X + dx, cluster.Y + dy, cluster.Width, cluster.Height));
		}

		foreach (var edge in drawing.Edges)
		{
			edges.Add((edge, dx, dy));
		}

		foreach (var node in drawing.Nodes)
		{
			if (node.Subgraph is { } subgraph)
			{
				Collect(subgraph, dx + node.X, dy + node.Y, boxes, edges);
			}
			else if (node.Geometry is { } geometry && !node.IsDummy && !geometry.Bounds.IsEmpty)
			{
				var bounds = geometry.Bounds;
				boxes.Add(new FlowchartBox(node.IsGroup ? "group" : "node", node.Id, bounds.MinX + node.X + dx, bounds.MinY + node.Y + dy, bounds.Width, bounds.Height));
			}
		}
	}
}

/// <summary>A box of <see cref="FlowchartLayout.Boxes"/>: its kind, its name and its rectangle.</summary>
internal readonly record struct FlowchartBox(string Kind, string Name, double X, double Y, double Width, double Height);

/// <summary>A copy of the marker <paramref name="BaseId"/>, as <paramref name="Id"/>, with its paths stroked, and filled when <paramref name="Fill"/>, in <paramref name="Color"/>.</summary>
internal sealed record ColoredMarker(string BaseId, string Id, string Color, bool Fill);

/// <summary>The theme values the renderer writes as presentation attributes, which the flowchart stylesheet overrides.</summary>
internal sealed record FlowchartStyle(string DiagramType, string Look, string? NodeFill, string? NodeStroke, string? ClusterFill, string? ClusterStroke, string? LineColor, string? TextColor, double FontSize)
{
	/// <summary>The theme's name, whose <c>dark</c> darkens <c>insertLookDefs</c>'s shadows.</summary>
	public string? Theme { get; init; }

	/// <summary>The <c>gradientStart</c> and <c>gradientStop</c> of a theme whose <c>useGradient</c> is truthy, or <c>null</c>.</summary>
	public (string? Start, string? Stop)? Gradient { get; init; }

	/// <summary>The marker sets <c>insertMarkers</c> writes: the diagram's <c>data4Layout.markers</c>.</summary>
	public Layout.Markers.MarkerDiagram Markers { get; init; } = Layout.Markers.MarkerDiagram.Flowchart;

	/// <summary>
	/// The paint of a <c>rect</c> behind each edge label, as the requirement diagram's <c>.labelBkg</c> rule fills the
	/// label's box, or <c>null</c> for no background.
	/// </summary>
	public string? EdgeLabelBackground { get; init; }
}

/// <summary><c>insertTitle</c>'s text: the front matter title, centred over the drawing, with the diagram's title class.</summary>
internal sealed record FlowchartTitle(string Text, double X, double Y, string Class = "flowchartTitleText");

/// <summary>One <c>g.root</c>: its clusters, edges and nodes, in its own space.</summary>
internal sealed class FlowchartDrawing
{
	public List<FlowchartClusterDrawing> Clusters { get; } = [];

	public List<FlowchartEdgeDrawing> Edges { get; } = [];

	public List<FlowchartNodeDrawing> Nodes { get; } = [];

	/// <summary>The <c>getBBox</c> of the <c>g.root</c>: what it draws, without the edges' markers and empty labels.</summary>
	public ShapeBounds Bounds()
	{
		var bounds = ShapeBounds.None;
		foreach (var cluster in Clusters)
		{
			bounds = bounds.Union(new ShapeBounds(cluster.X, cluster.Y, cluster.X + cluster.Width, cluster.Y + cluster.Height));
			if (cluster.Label.HasText)
			{
				bounds = bounds.Union(new ShapeBounds(cluster.LabelX, cluster.LabelY, cluster.LabelX + cluster.Label.Width, cluster.LabelY + cluster.Label.Height));
			}
		}

		foreach (var edge in Edges)
		{
			bounds = bounds.Union(edge.PathBounds);
			if (edge.Label is { HasText: true } label)
			{
				var x = edge.LabelX - (label.Width / 2);
				var y = edge.LabelY - (label.Height / 2);
				bounds = bounds.Union(new ShapeBounds(x, y, x + label.Width, y + label.Height));
			}
		}

		foreach (var node in Nodes)
		{
			var inner = node.Subgraph is { } subgraph ? subgraph.Bounds() : node.Geometry?.Bounds ?? ShapeBounds.None;
			bounds = bounds.Union(inner.Offset(new ShapePoint(node.X, node.Y)));
		}

		return bounds;
	}
}

/// <summary>A subgraph's frame, as <c>insertCluster</c>'s <c>rect</c> draws it, and its title.</summary>
internal sealed class FlowchartClusterDrawing
{
	public required string Id { get; init; }

	/// <summary>The id of the cluster's <c>g</c>, after the SVG's id.</summary>
	public required string DomId { get; init; }

	/// <summary>The cluster shape: <c>rect</c>, or a state's <c>roundedWithTitle</c>, <c>divider</c> or <c>noteGroup</c>.</summary>
	public string Shape { get; init; } = "rect";

	/// <summary>The top and height of a <c>roundedWithTitle</c> frame's inner rectangle, below its title.</summary>
	public (double Y, double Height)? Inner { get; init; }

	public required string Classes { get; init; }

	public double X { get; init; }

	public double Y { get; init; }

	public double Width { get; init; }

	public double Height { get; init; }

	public required FlowchartLabelBox Label { get; init; }

	/// <inheritdoc cref="FlowchartNodeDrawing.NodeStyles"/>
	public string NodeStyles { get; init; } = "";

	/// <inheritdoc cref="FlowchartNodeDrawing.LabelStyles"/>
	public string LabelStyles { get; init; } = "";

	/// <inheritdoc cref="FlowchartNodeDrawing.ColorId"/>
	public string? ColorId { get; init; }

	/// <inheritdoc cref="FlowchartNodeDrawing.StyleClass"/>
	public string? StyleClass { get; set; }

	/// <summary>The translate of the title's <c>g</c>, its top left corner.</summary>
	public double LabelX { get; init; }

	/// <inheritdoc cref="LabelX"/>
	public double LabelY { get; init; }
}

/// <summary>A node, at <see cref="X"/> and <see cref="Y"/>, or an extracted subgraph's <c>g.root</c> moved there.</summary>
internal sealed class FlowchartNodeDrawing
{
	public required string Id { get; init; }

	public required string DomId { get; init; }

	public string Classes { get; init; } = "node default";

	/// <summary>The translate of the node's <c>g</c>: the node's centre, or a subgraph's corner.</summary>
	public double X { get; init; }

	/// <inheritdoc cref="X"/>
	public double Y { get; init; }

	public ShapeGeometry? Geometry { get; init; }

	public FlowchartLabelBox? Label { get; init; }

	/// <summary>The labels of a shape that draws more than one, such as a state's <c>rectWithTitle</c>, each at its top left corner.</summary>
	public IReadOnlyList<(FlowchartLabelBox Box, double X, double Y)>? Labels { get; init; }

	/// <summary>The <c>nodeStyles</c> of <c>styles2String</c>, which mermaid.js writes on the shape's outline.</summary>
	public string NodeStyles { get; init; } = "";

	/// <summary>The <c>labelStyles</c> of <c>styles2String</c>, which mermaid.js writes on the label.</summary>
	public string LabelStyles { get; init; } = "";

	/// <summary>The <c>data-color-id</c> of a colour theme's colour slot, or <c>null</c> for none.</summary>
	public string? ColorId { get; init; }

	/// <summary>The class that keys the node's <see cref="FlowchartLayout.ElementRules"/>, or <c>null</c> when it has none.</summary>
	public string? StyleClass { get; set; }

	/// <summary>The <c>icon</c> an icon shape draws in its <see cref="ShapeGeometry.Picture"/>, such as <c>fa:user</c>.</summary>
	public string? Icon { get; init; }

	/// <summary>The <c>stroke</c> of the node's styles, which colours its icon instead of the theme's <c>nodeBorder</c>.</summary>
	public string? IconColor { get; init; }

	/// <summary>The <c>img</c> URL an image shape draws in its <see cref="ShapeGeometry.Picture"/>.</summary>
	public string? Image { get; init; }

	/// <summary>A subgraph without members, which the dagre renderer draws as a node.</summary>
	public bool IsGroup { get; init; }

	/// <summary>One of the two nodes a self loop is laid out through, which draw nothing.</summary>
	public bool IsDummy { get; init; }

	public FlowchartDrawing? Subgraph { get; init; }
}

/// <summary>An edge's path and its label.</summary>
internal sealed class FlowchartEdgeDrawing
{
	public required string Id { get; init; }

	/// <summary>The edge's place in the document, which orders the names of its boxes.</summary>
	public int Index { get; init; }

	/// <summary>The start the document gives the edge, before it is moved off a subgraph.</summary>
	public required string Start { get; init; }

	/// <inheritdoc cref="Start"/>
	public required string End { get; init; }

	public required string PathData { get; init; }

	public ShapeBounds PathBounds { get; init; }

	public required string Classes { get; init; }

	public string? MarkerStart { get; init; }

	public string? MarkerEnd { get; init; }

	/// <summary>The copies of markers this edge's stroke colour paints, which <c>addEdgeMarker</c> adds.</summary>
	public IReadOnlyList<ColoredMarker> ColoredMarkers { get; init; } = [];

	/// <summary>The <c>style</c> <c>insertEdge</c> writes on the path in the classic look.</summary>
	public string PathStyle { get; init; } = "";

	/// <summary>The edge's <c>labelStyle</c>, which mermaid.js writes on the label.</summary>
	public string LabelStyles { get; init; } = "";

	/// <summary>The class that keys the path's <see cref="FlowchartLayout.ElementRules"/>, or <c>null</c> when it has none.</summary>
	public string? StyleClass { get; set; }

	/// <summary>The class that keys the label's <see cref="FlowchartLayout.ElementRules"/>, or <c>null</c> when it has none.</summary>
	public string? LabelStyleClass { get; set; }

	public bool Invisible { get; init; }

	public bool Thick { get; init; }

	public FlowchartLabelBox? Label { get; init; }

	/// <summary>The centre of the label.</summary>
	public double LabelX { get; init; }

	/// <inheritdoc cref="LabelX"/>
	public double LabelY { get; init; }
}
