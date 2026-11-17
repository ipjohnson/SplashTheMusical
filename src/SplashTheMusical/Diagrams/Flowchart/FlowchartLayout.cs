using SplashTheMusical.Layout.Dagre;
using SplashTheMusical.Rendering.Shapes;

namespace SplashTheMusical.Diagrams.Flowchart;

/// <summary>
/// A laid out flowchart: the size of the root <c>svg</c>, its viewBox, and the drawing mermaid.js's dagre renderer
/// leaves, a <c>g.root</c> of clusters, edges, edge labels and nodes in which an extracted subgraph is a node holding a
/// <c>g.root</c> of its own.
/// </summary>
public sealed class FlowchartLayout : DiagramLayout
{
	internal FlowchartLayout(double width, double? height, SvgViewBox viewBox, FlowchartDrawing root, FlowchartTitle? title, FlowchartStyle style, IReadOnlyList<DagreInput> dagreInputs)
		: base(width, height)
	{
		ViewBox = viewBox;
		Root = root;
		Title = title;
		Style = style;
		DagreInputs = dagreInputs;
	}

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
	internal IReadOnlyList<FlowchartBox> Boxes()
	{
		var boxes = new List<FlowchartBox>();
		var edges = new List<(FlowchartEdgeDrawing Edge, double Dx, double Dy)>();
		Collect(Root, 0, 0, boxes, edges);
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

/// <summary>The theme values the renderer paints with, until the flowchart stylesheet does.</summary>
internal sealed record FlowchartStyle(string DiagramType, string Look, string? NodeFill, string? NodeStroke, string? ClusterFill, string? ClusterStroke, string? LineColor, string? TextColor, double FontSize);

/// <summary><c>insertTitle</c>'s text: the front matter title, centred over the drawing.</summary>
internal sealed record FlowchartTitle(string Text, double X, double Y);

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

	public required string Classes { get; init; }

	public double X { get; init; }

	public double Y { get; init; }

	public double Width { get; init; }

	public double Height { get; init; }

	public required FlowchartLabelBox Label { get; init; }

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

	public bool Invisible { get; init; }

	public bool Thick { get; init; }

	public FlowchartLabelBox? Label { get; init; }

	/// <summary>The centre of the label.</summary>
	public double LabelX { get; init; }

	/// <inheritdoc cref="LabelX"/>
	public double LabelY { get; init; }
}
