namespace SplashTheMusical.Layout.Dagre;

/// <summary>
/// A port of dagre's <c>add-border-segments.js</c>. Each cluster gets a left and a right border node on every rank
/// from its first to its last, joined into a chain on each side.
/// </summary>
internal static class BorderSegments
{
	public static void Add(LayoutGraph g)
	{
		void Dfs(int v)
		{
			foreach (var child in g.Children(v))
			{
				Dfs(child);
			}

			var node = g.Node(v);
			if (!double.IsNaN(node.MinRank))
			{
				var length = (int)(node.MaxRank - node.MinRank) + 1;
				node.BorderLeft = new int[length];
				node.BorderRight = new int[length];
				for (var i = 0; i < length; i++)
				{
					AddBorderNode(g, "borderLeft", "_bl", v, node.BorderLeft, i, node.MinRank + i);
					AddBorderNode(g, "borderRight", "_br", v, node.BorderRight, i, node.MinRank + i);
				}
			}
		}

		foreach (var v in g.Children())
		{
			Dfs(v);
		}
	}

	private static void AddBorderNode(LayoutGraph g, string type, string prefix, int sg, int[] border, int i, double rank)
	{
		var label = new LayoutNode { Rank = rank, BorderType = type };
		var curr = Util.AddDummyNode(g, "border", label, prefix);
		border[i] = curr;
		g.SetParent(curr, sg);
		if (i > 0)
		{
			g.SetEdge(border[i - 1], curr, new LayoutEdge { Weight = 1 });
		}
	}
}
