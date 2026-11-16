namespace SplashTheMusical.Layout.Dagre;

/// <summary>
/// A port of dagre's <c>rank/network-simplex.js</c>, after Gansner et al., "A Technique for Drawing Directed Graphs".
/// It starts from the longest path ranking, makes a feasible tight tree, and exchanges a tree edge with a negative cut
/// value for a non-tree edge until no cut value is negative. Each exchange recomputes the low, lim and cut values of
/// the whole tree, as dagre does.
/// </summary>
internal static class NetworkSimplex
{
	public static void Run(LayoutGraph input)
	{
		var g = Util.Simplify(input);
		Rank.LongestPath(g);
		var t = FeasibleTree.Find(g);
		InitLowLimValues(t.Graph);
		InitCutValues(t, g);

		int e;
		while ((e = LeaveEdge(t.Graph)) >= 0)
		{
			var f = EnterEdge(t, g, e);
			ExchangeEdges(t, g, e, f);
		}
	}

	public static void InitCutValues(SpanningTree t, LayoutGraph g)
	{
		var tree = t.Graph;
		var vs = Traverse(tree, tree.Nodes(), postorder: true);
		for (var i = 0; i < vs.Count - 1; i++)
		{
			var child = vs[i];
			var parent = tree.Node(child).Parent;
			tree.Edge(tree.FindEdge(child, parent)).CutValue = CalcCutValue(t, g, child);
		}
	}

	/// <summary>The cut value of the tree edge between the child and its parent.</summary>
	public static double CalcCutValue(SpanningTree t, LayoutGraph g, int child)
	{
		var tree = t.Graph;
		var parent = tree.Node(child).Parent;
		var gChild = tree.Node(child).G;
		var gParent = tree.Node(parent).G;

		// True if the child is the tail of the edge in the directed graph.
		var childIsTail = true;
		var graphEdge = g.FindEdge(gChild, gParent);
		if (graphEdge < 0)
		{
			childIsTail = false;
			graphEdge = g.FindEdge(gParent, gChild);
		}

		var cutValue = g.Edge(graphEdge).Weight;
		foreach (var e in g.NodeEdges(gChild))
		{
			var isOutEdge = g.EdgeV(e) == gChild;
			var other = isOutEdge ? g.EdgeW(e) : g.EdgeV(e);
			if (other != gParent)
			{
				var pointsToHead = isOutEdge == childIsTail;
				var otherWeight = g.Edge(e).Weight;
				cutValue += pointsToHead ? otherWeight : -otherWeight;

				var treeEdge = tree.FindEdge(child, t.Of(other));
				if (treeEdge >= 0)
				{
					var otherCutValue = tree.Edge(treeEdge).CutValue;
					cutValue += pointsToHead ? -otherCutValue : otherCutValue;
				}
			}
		}

		return cutValue;
	}

	/// <summary>Numbers the tree in postorder from its first node, which is the root.</summary>
	public static void InitLowLimValues(Graph<TreeNode, TreeEdge> tree)
	{
		var visited = new HashSet<int>();

		int Dfs(int nextLim, int v, int parent)
		{
			var low = nextLim;
			var label = tree.Node(v);
			visited.Add(v);
			foreach (var w in tree.Neighbors(v))
			{
				if (!visited.Contains(w))
				{
					nextLim = Dfs(nextLim, w, v);
				}
			}

			label.Low = low;
			label.Lim = nextLim++;
			label.Parent = parent;
			return nextLim;
		}

		Dfs(1, tree.FirstNode(), -1);
	}

	/// <summary>The first tree edge, in the tree's edge order, with a negative cut value, or -1.</summary>
	public static int LeaveEdge(Graph<TreeNode, TreeEdge> tree)
	{
		foreach (var e in tree.Edges())
		{
			if (tree.Edge(e).CutValue < 0)
			{
				return e;
			}
		}

		return -1;
	}

	/// <summary>The graph edge with the least slack that crosses the cut the tree edge makes. The first wins a tie.</summary>
	public static int EnterEdge(SpanningTree t, LayoutGraph g, int edge)
	{
		var tree = t.Graph;
		var v = tree.EdgeV(edge);
		var w = tree.EdgeW(edge);

		// v is the tail and w the head from here on, so flip the tree's orientation when the graph's differs.
		if (!g.HasEdge(tree.Node(v).G, tree.Node(w).G))
		{
			(v, w) = (w, v);
		}

		var vLabel = tree.Node(v);
		var wLabel = tree.Node(w);
		var tailLabel = vLabel;
		var flip = false;

		// When the root is in the tail of the edge, flip the test for the head and tail candidates.
		if (vLabel.Lim > wLabel.Lim)
		{
			tailLabel = wLabel;
			flip = true;
		}

		var best = -1;
		var bestSlack = double.NaN;
		foreach (var candidate in g.Edges())
		{
			if (flip == IsDescendant(tree.Node(t.Of(g.EdgeV(candidate))), tailLabel)
				&& flip != IsDescendant(tree.Node(t.Of(g.EdgeW(candidate))), tailLabel))
			{
				var slack = Rank.Slack(g, candidate);
				if (!double.IsNaN(slack) && (best < 0 || slack < bestSlack))
				{
					best = candidate;
					bestSlack = slack;
				}
			}
		}

		return best >= 0 ? best : throw new InvalidOperationException("dagre found no edge to enter the tree.");
	}

	public static void ExchangeEdges(SpanningTree t, LayoutGraph g, int e, int f)
	{
		var tree = t.Graph;
		tree.RemoveEdge(e);
		tree.SetEdge(t.Of(g.EdgeV(f)), t.Of(g.EdgeW(f)), new TreeEdge());
		InitLowLimValues(tree);
		InitCutValues(t, g);
		UpdateRanks(t, g);
	}

	/// <summary>
	/// Sets each node's rank from its parent's in a preorder walk of the tree. dagre picks the root as the first tree node
	/// whose graph label has no <c>parent</c>, and no label of the layout graph has one, so the root is the first node.
	/// </summary>
	private static void UpdateRanks(SpanningTree t, LayoutGraph g)
	{
		var tree = t.Graph;
		var vs = Traverse(tree, [tree.FirstNode()], postorder: false);
		for (var i = 1; i < vs.Count; i++)
		{
			var v = vs[i];
			var gv = tree.Node(v).G;
			var gParent = tree.Node(tree.Node(v).Parent).G;
			var flipped = false;
			var edge = g.FindEdge(gv, gParent);
			if (edge < 0)
			{
				edge = g.FindEdge(gParent, gv);
				flipped = true;
			}

			var minlen = g.Edge(edge).MinLen;
			g.Node(gv).Rank = g.Node(gParent).Rank + (flipped ? minlen : -minlen);
		}
	}

	private static bool IsDescendant(TreeNode vLabel, TreeNode rootLabel) =>
		rootLabel.Low <= vLabel.Lim && vLabel.Lim <= rootLabel.Lim;

	/// <summary>graphlib's <c>alg.preorder</c> and <c>alg.postorder</c> on an undirected graph, which follow neighbors.</summary>
	private static List<int> Traverse(Graph<TreeNode, TreeEdge> tree, int[] roots, bool postorder)
	{
		var visited = new HashSet<int>();
		var acc = new List<int>(tree.NodeCount);

		void Dfs(int v)
		{
			if (!visited.Add(v))
			{
				return;
			}

			if (!postorder)
			{
				acc.Add(v);
			}

			foreach (var w in tree.Neighbors(v))
			{
				Dfs(w);
			}

			if (postorder)
			{
				acc.Add(v);
			}
		}

		foreach (var v in roots)
		{
			Dfs(v);
		}

		return acc;
	}
}
