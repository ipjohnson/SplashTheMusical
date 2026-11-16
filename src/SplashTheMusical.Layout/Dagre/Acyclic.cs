namespace SplashTheMusical.Layout.Dagre;

/// <summary>A port of dagre's <c>acyclic.js</c>: reverses a feedback arc set so the graph has no cycle.</summary>
internal static class Acyclic
{
	public static void Run(LayoutGraph g)
	{
		var fas = g.Label.Acyclicer == "greedy" ? GreedyFas.Find(g) : DfsFas(g);
		foreach (var e in fas)
		{
			var label = g.Edge(e);
			g.RemoveEdge(e);
			label.ForwardName = g.EdgeName(e);
			label.Reversed = true;
			g.SetEdge(g.EdgeW(e), g.EdgeV(e), label, g.Ids.Next("rev"));
		}
	}

	public static void Undo(LayoutGraph g)
	{
		foreach (var e in g.Edges())
		{
			var label = g.Edge(e);
			if (label.Reversed)
			{
				g.RemoveEdge(e);
				var forwardName = label.ForwardName;
				label.Reversed = false;
				label.ForwardName = null;
				g.SetEdge(g.EdgeW(e), g.EdgeV(e), label, forwardName);
			}
		}
	}

	/// <summary>The edges that close a cycle in a depth-first search that starts from each node in the graph's order.</summary>
	private static List<int> DfsFas(LayoutGraph g)
	{
		var fas = new List<int>();
		var visited = new HashSet<int>();
		var stack = new HashSet<int>();

		void Dfs(int v)
		{
			if (!visited.Add(v))
			{
				return;
			}

			stack.Add(v);
			foreach (var e in g.OutEdges(v))
			{
				var w = g.EdgeW(e);
				if (stack.Contains(w))
				{
					fas.Add(e);
				}
				else
				{
					Dfs(w);
				}
			}

			stack.Remove(v);
		}

		foreach (var v in g.Nodes())
		{
			Dfs(v);
		}

		return fas;
	}
}
