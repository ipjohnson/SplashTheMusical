namespace SplashTheMusical.Layout.Dagre;

/// <summary>
/// A port of dagre's <c>rank/network-simplex.js</c>, after Gansner et al., "A Technique for Drawing Directed Graphs".
/// It starts from the longest path ranking, makes a feasible tight tree, and exchanges a tree edge with a negative cut
/// value for a non-tree edge until no cut value is negative. <see cref="ExchangeEdges"/> updates only the part of the
/// tree an exchange changes, where dagre recomputes the whole tree, and gives the same values.
/// </summary>
internal static class NetworkSimplex
{
	public static void Run(LayoutGraph input)
	{
		var g = Util.Simplify(input);
		Rank.LongestPath(g);
		var t = FeasibleTree.Find(g);
		InitLowLimValues(t);
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
	public static void InitLowLimValues(SpanningTree t)
	{
		t.ByLim = new int[t.Graph.NodeCount + 1];
		Number(t, t.Graph.FirstNode(), -1, 1);
	}

	/// <summary>
	/// dagre's <c>dfs</c> in <c>initLowLimValues</c>, from the node down, with lims from <paramref name="nextLim"/>. In a
	/// tree the only neighbor dagre's search has visited is the parent.
	/// </summary>
	/// <returns>The next lim.</returns>
	private static int Number(SpanningTree t, int v, int parent, int nextLim)
	{
		var tree = t.Graph;
		var low = nextLim;
		foreach (var w in tree.Neighbors(v))
		{
			if (w != parent)
			{
				nextLim = Number(t, w, v, nextLim);
			}
		}

		var label = tree.Node(v);
		label.Low = low;
		label.Lim = nextLim;
		label.Parent = parent;
		t.ByLim[nextLim] = v;
		return nextLim + 1;
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

	/// <summary>
	/// Swaps the tree edge <paramref name="e"/> for the graph edge <paramref name="f"/>. dagre then recomputes the low,
	/// lim and cut values of the whole tree and every rank, which makes an exchange cost a pass over the tree. This gives
	/// the same values with Gansner et al.'s update, which only visits what the exchange changes:
	/// <list type="bullet">
	/// <item>The ranks of the subtree <paramref name="e"/> cuts off shift by the slack of <paramref name="f"/>, which makes
	/// it tight. dagre's preorder walk from the root gives the same ranks, as every tree edge is tight.</item>
	/// <item>Only the subtree under the lowest common ancestor of <paramref name="f"/>'s ends changes shape, so only its
	/// nodes get new low, lim and parent values. They keep the same range of lims.</item>
	/// <item>A cut value depends only on which nodes the tree edge splits apart, so only the edges of the cycle
	/// <paramref name="f"/> closes change. They are the paths from each end of <paramref name="e"/> up to the ancestor,
	/// and each is recomputed after its children.</item>
	/// </list>
	/// </summary>
	public static void ExchangeEdges(SpanningTree t, LayoutGraph g, int e, int f)
	{
		var tree = t.Graph;
		var child = tree.EdgeV(e);
		var parent = tree.EdgeW(e);
		if (tree.Node(child).Parent != parent)
		{
			(child, parent) = (parent, child);
		}

		var childLabel = tree.Node(child);
		var tail = t.Of(g.EdgeV(f));
		var head = t.Of(g.EdgeW(f));
		var slack = Rank.Slack(g, f);
		var delta = IsDescendant(tree.Node(tail), childLabel) ? slack : -slack;
		for (var lim = childLabel.Low; lim <= childLabel.Lim; lim++)
		{
			g.Node(tree.Node(t.ByLim[lim]).G).Rank += delta;
		}

		var ancestor = tail;
		while (!IsDescendant(tree.Node(head), tree.Node(ancestor)))
		{
			ancestor = tree.Node(ancestor).Parent;
		}

		tree.RemoveEdge(e);
		tree.SetEdge(tail, head, new TreeEdge());

		var ancestorLabel = tree.Node(ancestor);
		Number(t, ancestor, ancestorLabel.Parent, ancestorLabel.Low);

		foreach (var start in (ReadOnlySpan<int>)[child, parent])
		{
			for (var v = start; v != ancestor; v = tree.Node(v).Parent)
			{
				tree.Edge(tree.FindEdge(v, tree.Node(v).Parent)).CutValue = CalcCutValue(t, g, v);
			}
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
