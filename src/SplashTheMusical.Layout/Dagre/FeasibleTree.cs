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
		var open = new int[g.NodeHandleCount];
		Add(t, g, open, start);

		while (TightTree(t, g, open) < size)
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

	/// <summary>
	/// Grows the tree by tight edges from each of its nodes in the tree's order and returns its size. A node with no
	/// edge out of the tree can add nothing, so its search is skipped. dagre searches from every node of the tree on
	/// every call.
	/// </summary>
	private static int TightTree(SpanningTree t, LayoutGraph g, int[] open)
	{
		void Dfs(int v)
		{
			if (open[v] == 0)
			{
				return;
			}

			foreach (var e in g.NodeEdges(v))
			{
				var edgeV = g.EdgeV(e);
				var w = v == edgeV ? g.EdgeW(e) : edgeV;
				var slack = Rank.Slack(g, e);
				if (!t.Contains(w) && (slack == 0 || double.IsNaN(slack)))
				{
					Add(t, g, open, w);
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

	/// <summary>Adds the node to the tree, and keeps <paramref name="open"/>, each tree node's count of edges out of the tree.</summary>
	private static void Add(SpanningTree t, LayoutGraph g, int[] open, int v)
	{
		t.Add(v);
		foreach (var e in g.NodeEdges(v))
		{
			var edgeV = g.EdgeV(e);
			var w = v == edgeV ? g.EdgeW(e) : edgeV;
			if (w == v)
			{
				continue;
			}

			if (t.Contains(w))
			{
				open[w]--;
			}
			else
			{
				open[v]++;
			}
		}
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
internal sealed class SpanningTree
{
	private readonly LayoutGraph _g;
	private readonly int[] _handles;

	public SpanningTree(LayoutGraph g)
	{
		_g = g;
		_handles = new int[g.NodeHandleCount];
		Array.Fill(_handles, -1);
	}

	public Graph<TreeNode, TreeEdge> Graph { get; } = new(directed: false);

	/// <summary>The tree's nodes by their lim, which <see cref="NetworkSimplex.InitLowLimValues"/> sets.</summary>
	public int[] ByLim { get; set; } = [];

	public bool Contains(int gv) => _handles[gv] >= 0;

	/// <summary>The tree's handle of the graph's node.</summary>
	public int Of(int gv) => _handles[gv];

	public void Add(int gv)
	{
		if (_handles[gv] >= 0)
		{
			throw new ArgumentException($"Node '{_g.Name(gv)}' is in the tree.", nameof(gv));
		}

		_handles[gv] = Graph.SetNode(_g.Name(gv), new TreeNode(gv));
	}
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
