namespace SplashTheMusical.Layout.Dagre;

/// <summary>
/// A port of dagre's <c>rank/index.js</c> and <c>rank/util.js</c>. Assigns each node a rank that respects the minlen
/// of its edges, after Gansner et al., "A Technique for Drawing Directed Graphs". Ranks can start at any number.
/// </summary>
internal static class Rank
{
	public static void Run(LayoutGraph g)
	{
		switch (g.Label.Ranker)
		{
			case "tight-tree":
				LongestPath(g);
				FeasibleTree.Find(g);
				break;
			case "longest-path":
				LongestPath(g);
				break;
			default:
				NetworkSimplex.Run(g);
				break;
		}
	}

	/// <summary>
	/// Puts each node on the lowest rank its out-edges allow, from a depth-first search of each source in the graph's
	/// order. The ranks are not normalized.
	/// </summary>
	public static void LongestPath(LayoutGraph g)
	{
		var visited = new HashSet<int>();

		double Dfs(int v)
		{
			var label = g.Node(v);
			if (!visited.Add(v))
			{
				return label.Rank;
			}

			// lodash's _.min: the first strictly smaller value wins, and NaN is skipped. An empty list gives 0.
			var rank = double.NaN;
			foreach (var e in g.OutEdges(v))
			{
				var candidate = Dfs(g.EdgeW(e)) - g.Edge(e).MinLen;
				if (!double.IsNaN(candidate) && (double.IsNaN(rank) || candidate < rank))
				{
					rank = candidate;
				}
			}

			if (double.IsNaN(rank) || double.IsPositiveInfinity(rank))
			{
				rank = 0;
			}

			return label.Rank = rank;
		}

		foreach (var v in g.Sources())
		{
			Dfs(v);
		}
	}

	/// <summary>How much longer the edge is than its minlen.</summary>
	public static double Slack(LayoutGraph g, int e) =>
		g.Node(g.EdgeW(e)).Rank - g.Node(g.EdgeV(e)).Rank - g.Edge(e).MinLen;
}
