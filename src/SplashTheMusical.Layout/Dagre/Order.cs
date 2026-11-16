namespace SplashTheMusical.Layout.Dagre;

/// <summary>
/// A port of dagre's <c>order/index.js</c>, <c>order/init-order.js</c>, <c>order/cross-count.js</c> and
/// <c>order/add-subgraph-constraints.js</c>. It sweeps the ranks down and up, sorting each by barycenters, and keeps
/// the order with the fewest weighted crossings, which it writes to each node's <see cref="LayoutNode.Order"/>.
/// </summary>
internal static class Order
{
	public static void Run(LayoutGraph g)
	{
		var maxRank = Util.MaxRank(g);
		if (double.IsNaN(maxRank))
		{
			return;
		}

		var nodesByRank = LayerGraph.NodesByRank(g, (int)maxRank);
		var downLayerGraphs = new List<LayerGraph>();
		for (var rank = 1; rank < maxRank + 1; rank++)
		{
			downLayerGraphs.Add(LayerGraph.Build(g, nodesByRank[rank], rank, inEdges: true));
		}

		var upLayerGraphs = new List<LayerGraph>();
		for (var rank = maxRank - 1; rank > -1; rank--)
		{
			upLayerGraphs.Add(LayerGraph.Build(g, nodesByRank[(int)rank], rank, inEdges: false));
		}

		var layering = InitOrder(g);
		AssignOrder(g, layering);

		var bestCC = double.PositiveInfinity;
		int[][]? best = null;

		for (int i = 0, lastBest = 0; lastBest < 4; ++i, ++lastBest)
		{
			SweepLayerGraphs(i % 2 == 1 ? downLayerGraphs : upLayerGraphs, i % 4 >= 2);

			layering = Util.BuildLayerMatrix(g);
			var cc = CrossCount(g, layering);
			if (cc < bestCC)
			{
				lastBest = 0;
				best = new int[layering.Length][];
				for (var r = 0; r < layering.Length; r++)
				{
					best[r] = (int[])layering[r].Clone();
				}

				bestCC = cc;
			}
		}

		AssignOrder(g, best!);
	}

	/// <summary>
	/// dagre's <c>initOrder</c>, from Gansner et al., "A Technique for Drawing Directed Graphs". A depth-first search
	/// from the leaf nodes in rank order puts each node at the end of its rank when it first reaches it.
	/// </summary>
	public static int[][] InitOrder(LayoutGraph g)
	{
		var simpleNodes = Array.FindAll(g.Nodes(), v => !g.HasChildren(v));
		var maxRank = double.NaN;
		foreach (var v in simpleNodes)
		{
			var rank = g.Node(v).Rank;
			if (!double.IsNaN(rank) && (double.IsNaN(maxRank) || rank > maxRank))
			{
				maxRank = rank;
			}
		}

		var layers = new List<int>[double.IsNaN(maxRank) ? 0 : (int)maxRank + 1];
		for (var i = 0; i < layers.Length; i++)
		{
			layers[i] = [];
		}

		var visited = new HashSet<int>();

		void Dfs(int v)
		{
			if (!visited.Add(v))
			{
				return;
			}

			layers[(int)g.Node(v).Rank].Add(v);
			foreach (var w in g.Successors(v))
			{
				Dfs(w);
			}
		}

		// lodash's sortBy on the rank, which is stable.
		foreach (var v in simpleNodes.OrderBy(v => g.Node(v).Rank))
		{
			Dfs(v);
		}

		var result = new int[layers.Length][];
		for (var i = 0; i < layers.Length; i++)
		{
			result[i] = [.. layers[i]];
		}

		return result;
	}

	/// <summary>
	/// dagre's <c>crossCount</c>: the weighted crossings between each pair of adjacent ranks, from Barth et al.,
	/// "Bilayer Cross Counting".
	/// </summary>
	public static double CrossCount(LayoutGraph g, int[][] layering)
	{
		double cc = 0;
		for (var i = 1; i < layering.Length; ++i)
		{
			cc += TwoLayerCrossCount(g, layering[i - 1], layering[i]);
		}

		return cc;
	}

	private static double TwoLayerCrossCount(LayoutGraph g, int[] northLayer, int[] southLayer)
	{
		var southPos = new Dictionary<int, int>(southLayer.Length);
		for (var i = 0; i < southLayer.Length; i++)
		{
			southPos[southLayer[i]] = i;
		}

		// An edge whose head is not on the south layer has no position. dagre sorts it last and it adds nothing.
		var southEntries = new List<(int Pos, double Weight)>();
		var entries = new List<(int Pos, double Weight)>();
		foreach (var v in northLayer)
		{
			entries.Clear();
			foreach (var e in g.OutEdges(v))
			{
				if (southPos.TryGetValue(g.EdgeW(e), out var pos))
				{
					entries.Add((pos, g.Edge(e).Weight));
				}
			}

			// lodash's sortBy on the position, which is stable.
			southEntries.AddRange(entries.OrderBy(entry => entry.Pos));
		}

		var firstIndex = 1;
		while (firstIndex < southLayer.Length)
		{
			firstIndex <<= 1;
		}

		var treeSize = (2 * firstIndex) - 1;
		firstIndex -= 1;
		var tree = new double[treeSize];

		double cc = 0;
		foreach (var (pos, weight) in southEntries)
		{
			var index = pos + firstIndex;
			tree[index] += weight;
			double weightSum = 0;
			while (index > 0)
			{
				if (index % 2 == 1)
				{
					weightSum += tree[index + 1];
				}

				index = (index - 1) >> 1;
				tree[index] += weight;
			}

			cc += weight * weightSum;
		}

		return cc;
	}

	private static void SweepLayerGraphs(List<LayerGraph> layerGraphs, bool biasRight)
	{
		var cg = new ConstraintGraph();
		foreach (var lg in layerGraphs)
		{
			var root = lg.RootNode;
			if (root < 0)
			{
				continue;
			}

			var sorted = SortSubgraph.Run(lg, root, cg, biasRight);
			for (var i = 0; i < sorted.Vs.Count; i++)
			{
				lg.Node(sorted.Vs[i]).Shared!.Order = i;
			}

			AddSubgraphConstraints(lg, cg, sorted.Vs);
		}
	}

	/// <summary>dagre's <c>addSubgraphConstraints</c>: keeps sibling clusters in the order this rank gave them.</summary>
	private static void AddSubgraphConstraints(LayerGraph g, ConstraintGraph cg, List<int> vs)
	{
		var prev = new Dictionary<int, int>();
		var rootPrev = Graph<LayerNode, LayerEdge>.Root;

		foreach (var v in vs)
		{
			var child = g.Parent(v);
			while (child != Graph<LayerNode, LayerEdge>.Root)
			{
				var parent = g.Parent(child);
				int prevChild;
				if (parent != Graph<LayerNode, LayerEdge>.Root)
				{
					prevChild = prev.GetValueOrDefault(parent, Graph<LayerNode, LayerEdge>.Root);
					prev[parent] = child;
				}
				else
				{
					prevChild = rootPrev;
					rootPrev = child;
				}

				if (prevChild != Graph<LayerNode, LayerEdge>.Root && prevChild != child)
				{
					cg.SetEdge(g.Name(prevChild), g.Name(child));
					break;
				}

				child = parent;
			}
		}
	}

	private static void AssignOrder(LayoutGraph g, int[][] layering)
	{
		foreach (var layer in layering)
		{
			for (var i = 0; i < layer.Length; i++)
			{
				g.Node(layer[i]).Order = i;
			}
		}
	}
}
