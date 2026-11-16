namespace SplashTheMusical.Diagrams;

/// <summary>
/// A graph layout a diagram's <c>layout</c> config names, such as <c>dagre</c> or <c>elk</c>. The layout step of a
/// diagram module that draws a graph hands its nodes and edges to the engine of that name.
/// </summary>
/// <remarks>An implementation is stateless and thread safe.</remarks>
public interface ILayoutEngine
{
	/// <summary>The <c>layout</c> name the engine answers to.</summary>
	string Name { get; }

	/// <summary>Places the nodes of <paramref name="graph"/> and routes its edges, writing the positions into it.</summary>
	void Layout(LayoutGraph graph);
}

/// <summary>A graph for an <see cref="ILayoutEngine"/>. Nodes are referred to by their index in <see cref="Nodes"/>.</summary>
public sealed class LayoutGraph
{
	/// <summary>The direction ranks run in, as mermaid writes it: <c>TB</c>, <c>BT</c>, <c>LR</c> or <c>RL</c>.</summary>
	public string Direction { get; init; } = "TB";

	public List<LayoutNode> Nodes { get; init; } = [];

	public List<LayoutEdge> Edges { get; init; } = [];

	/// <summary>The width of the laid out graph, which the engine sets.</summary>
	public double Width { get; set; }

	/// <summary>The height of the laid out graph, which the engine sets.</summary>
	public double Height { get; set; }
}

/// <summary>A node of a <see cref="LayoutGraph"/>. The engine sets <see cref="X"/> and <see cref="Y"/>, the node's centre.</summary>
public sealed class LayoutNode
{
	public required double Width { get; init; }

	public required double Height { get; init; }

	/// <summary>The index of the group node that holds this node, or -1.</summary>
	public int Parent { get; init; } = -1;

	public double X { get; set; }

	public double Y { get; set; }
}

/// <summary>An edge of a <see cref="LayoutGraph"/>. The engine fills <see cref="Points"/>, from the source to the target.</summary>
public sealed class LayoutEdge
{
	public required int Source { get; init; }

	public required int Target { get; init; }

	public List<LayoutPoint> Points { get; init; } = [];
}

public readonly record struct LayoutPoint(double X, double Y);
