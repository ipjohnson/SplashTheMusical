namespace SplashTheMusical.Layout.Dagre;

/// <summary>
/// A port of dagre's <c>order/build-layer-graph.js</c>: the graph that sorts one rank. It holds the rank's nodes under
/// their clusters, with the rank's top-level nodes under <see cref="RootName"/>, and the edges to the rank before it
/// (<c>inEdges</c>) or after it (<c>outEdges</c>) with their weights summed. Names are the layout graph's.
/// </summary>
internal sealed class LayerGraph : Graph<LayerNode, LayerEdge>
{
	private LayerGraph(string rootName, int capacity)
		: base(directed: true, multigraph: false, compound: true, capacity)
	{
		RootName = rootName;
	}

	/// <summary>graphlib's <c>graph().root</c>. The node is only added when a node of the rank is.</summary>
	public string RootName { get; }

	/// <summary>The root node, or -1 when the rank has no node.</summary>
	public int RootNode => NodeHandle(RootName);

	/// <summary>
	/// The nodes <see cref="Build"/> takes for each rank from 0 to <paramref name="maxRank"/>, in the layout graph's
	/// order: a node on the rank, or a cluster that spans it. dagre filters every node of the graph for each rank,
	/// which makes the order phase quadratic in the number of nodes.
	/// </summary>
	public static List<int>[] NodesByRank(LayoutGraph g, int maxRank)
	{
		var result = new List<int>[maxRank + 1];
		for (var r = 0; r <= maxRank; r++)
		{
			result[r] = [];
		}

		foreach (var v in g.Nodes())
		{
			var node = g.Node(v);
			var low = Math.Max(0, Math.Ceiling(node.MinRank));
			var high = Math.Min(maxRank, Math.Floor(node.MaxRank));
			for (var r = low; r <= high; r++)
			{
				result[(int)r].Add(v);
			}

			var rank = node.Rank;
			if (rank >= 0 && rank <= maxRank && rank == Math.Floor(rank) && !(rank >= low && rank <= high))
			{
				result[(int)rank].Add(v);
			}
		}

		return result;
	}

	/// <param name="nodes">The rank's entry of <see cref="NodesByRank"/>.</param>
	public static LayerGraph Build(LayoutGraph g, List<int> nodes, double rank, bool inEdges)
	{
		string rootName;
		do
		{
			rootName = g.Ids.Next("_root");
		}
		while (g.HasNode(rootName));

		// The rank's nodes, about as many neighbors on the next rank, and the root.
		var result = new LayerGraph(rootName, (2 * nodes.Count) + 1);
		foreach (var v in nodes)
		{
			var node = g.Node(v);
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
