namespace SplashTheMusical.Layout.Dagre;

/// <summary>
/// A port of dagre's <c>rank/feasible-tree.js</c>. Builds a spanning tree of tight edges, edges whose length is their
/// minlen, and shifts the ranks of the graph's nodes to make it.
/// </summary>
internal static class FeasibleTree
{
	public static SpanningTree Find(LayoutGraph g)
	{
		var t = new SpanningTree(g);
		var start = g.FirstNode();
		var size = g.NodeCount;
		t.Add(start);

		while (TightTree(t, g) < size)
		{
			var edge = FindMinSlackEdge(t, g);
			var delta = t.Contains(g.EdgeV(edge)) ? Rank.Slack(g, edge) : -Rank.Slack(g, edge);
			foreach (var v in t.Graph.Nodes())
			{
				g.Node(t.Graph.Node(v).G).Rank += delta;
			}
		}

		return t;
	}

	/// <summary>Grows the tree by tight edges from each of its nodes in the tree's order and returns its size.</summary>
	private static int TightTree(SpanningTree t, LayoutGraph g)
	{
		void Dfs(int v)
		{
			foreach (var e in g.NodeEdges(v))
			{
				var edgeV = g.EdgeV(e);
				var w = v == edgeV ? g.EdgeW(e) : edgeV;
				var slack = Rank.Slack(g, e);
				if (!t.Contains(w) && (slack == 0 || double.IsNaN(slack)))
				{
					t.Add(w);
					t.Graph.SetEdge(t.Of(v), t.Of(w), new TreeEdge());
					Dfs(w);
				}
			}
		}

		foreach (var v in t.Graph.Nodes())
		{
			Dfs(t.Graph.Node(v).G);
		}

		return t.Graph.NodeCount;
	}

	/// <summary>lodash's <c>_.minBy</c> of the slack of the edges with one end in the tree: the first smallest.</summary>
	private static int FindMinSlackEdge(SpanningTree t, LayoutGraph g)
	{
		var best = -1;
		var bestSlack = double.NaN;
		foreach (var e in g.Edges())
		{
			if (t.Contains(g.EdgeV(e)) != t.Contains(g.EdgeW(e)))
			{
				var slack = Rank.Slack(g, e);
				if (!double.IsNaN(slack) && (best < 0 || slack < bestSlack))
				{
					best = e;
					bestSlack = slack;
				}
			}
		}

		return best >= 0 ? best : throw new InvalidOperationException("dagre cannot rank a graph that is not connected.");
	}
}

/// <summary>
/// The undirected tree that <see cref="FeasibleTree"/> builds and <see cref="NetworkSimplex"/> changes. Its nodes have
/// the names of the graph's nodes, so graphlib's key order and its orientation of undirected edges carry over.
/// </summary>
internal sealed class SpanningTree(LayoutGraph g)
{
	private readonly Dictionary<int, int> _handles = [];

	public Graph<TreeNode, TreeEdge> Graph { get; } = new(directed: false);

	public bool Contains(int gv) => _handles.ContainsKey(gv);

	/// <summary>The tree's handle of the graph's node.</summary>
	public int Of(int gv) => _handles[gv];

	public void Add(int gv) => _handles.Add(gv, Graph.SetNode(g.Name(gv), new TreeNode(gv)));
}

/// <summary>A tree node's label: the graph's node, and the low, lim and parent of <c>initLowLimValues</c>.</summary>
internal sealed class TreeNode(int g)
{
	public int G { get; } = g;

	public int Low { get; set; }

	public int Lim { get; set; }

	/// <summary>The parent in the tree, or -1 for the root.</summary>
	public int Parent { get; set; } = -1;
}

internal sealed class TreeEdge
{
	/// <summary>NaN until <c>initCutValues</c> sets it, as dagre's is undefined.</summary>
	public double CutValue { get; set; } = double.NaN;
}
