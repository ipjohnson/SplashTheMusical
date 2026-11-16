namespace SplashTheMusical.Layout.Dagre;

/// <summary>
/// A port of dagre's <c>order/build-layer-graph.js</c>: the graph that sorts one rank. It holds the rank's nodes under
/// their clusters, with the rank's top-level nodes under <see cref="RootName"/>, and the edges to the rank before it
/// (<c>inEdges</c>) or after it (<c>outEdges</c>) with their weights summed. Names are the layout graph's.
/// </summary>
internal sealed class LayerGraph : Graph<LayerNode, LayerEdge>
{
	private LayerGraph(string rootName)
		: base(directed: true, multigraph: false, compound: true)
	{
		RootName = rootName;
	}

	/// <summary>graphlib's <c>graph().root</c>. The node is only added when a node of the rank is.</summary>
	public string RootName { get; }

	/// <summary>The root node, or -1 when the rank has no node.</summary>
	public int RootNode => NodeHandle(RootName);

	public static LayerGraph Build(LayoutGraph g, double rank, bool inEdges)
	{
		string rootName;
		do
		{
			rootName = g.Ids.Next("_root");
		}
		while (g.HasNode(rootName));

		var result = new LayerGraph(rootName);
		foreach (var v in g.Nodes())
		{
			var node = g.Node(v);
			if (node.Rank == rank || (node.MinRank <= rank && rank <= node.MaxRank))
			{
				var lv = result.Ensure(g, g.Name(v));
				var parent = g.Parent(v);
				result.SetParent(lv, result.Ensure(g, parent == Root ? rootName : g.Name(parent)));

				// This assumes we have only short edges, as dagre does.
				foreach (var e in inEdges ? g.InEdges(v) : g.OutEdges(v))
				{
					var u = g.EdgeV(e) == v ? g.EdgeW(e) : g.EdgeV(e);
					var lu = result.Ensure(g, g.Name(u));
					var existing = result.FindEdge(lu, lv);
					var weight = existing >= 0 ? result.Edge(existing).Weight : 0;
					result.SetEdge(lu, lv, new LayerEdge(g.Edge(e).Weight + weight));
				}

				if (!double.IsNaN(node.MinRank))
				{
					var index = (int)(rank - node.MinRank);
					result.SetNode(g.Name(v), new LayerNode(null)
					{
						BorderLeft = g.Name(node.BorderLeft![index]),
						BorderRight = g.Name(node.BorderRight![index]),
					});
				}
			}
		}

		return result;
	}

	/// <summary>graphlib's <c>setNode(v)</c> with the default label, which is the layout graph's label of the node.</summary>
	private int Ensure(LayoutGraph g, string name)
	{
		var v = NodeHandle(name);
		if (v >= 0)
		{
			return v;
		}

		var source = g.NodeHandle(name);
		return SetNode(name, new LayerNode(source >= 0 ? g.Node(source) : null));
	}
}

/// <summary>
/// The label of a node of a <see cref="LayerGraph"/>. dagre shares the layout graph's label, so the order a sweep
/// gives a node is the layout graph's. A cluster of the rank gets its own label with its border nodes on the rank.
/// </summary>
internal sealed class LayerNode(LayoutNode? shared)
{
	/// <summary>The layout graph's label, or null for the root and a cluster.</summary>
	public LayoutNode? Shared { get; } = shared;

	public string? BorderLeft { get; init; }

	public string? BorderRight { get; init; }
}

internal sealed class LayerEdge(double weight)
{
	public double Weight { get; } = weight;
}
