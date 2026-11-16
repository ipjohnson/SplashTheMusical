namespace SplashTheMusical.Layout.Dagre;

/// <summary>
/// A port of dagre's <c>coordinate-system.js</c>. The position phase lays ranks out top to bottom. <c>adjust</c> swaps
/// widths and heights for LR and RL first, and <c>undo</c> turns the result to the graph's rankdir.
/// </summary>
internal static class CoordinateSystem
{
	public static void Adjust(LayoutGraph g)
	{
		var rankDir = g.Label.RankDir.ToLowerInvariant();
		if (rankDir is "lr" or "rl")
		{
			SwapWidthHeight(g);
		}
	}

	public static void Undo(LayoutGraph g)
	{
		var rankDir = g.Label.RankDir.ToLowerInvariant();
		if (rankDir is "bt" or "rl")
		{
			ReverseY(g);
		}

		if (rankDir is "lr" or "rl")
		{
			SwapXY(g);
			SwapWidthHeight(g);
		}
	}

	private static void SwapWidthHeight(LayoutGraph g)
	{
		foreach (var v in g.Nodes())
		{
			var node = g.Node(v);
			(node.Width, node.Height) = (node.Height, node.Width);
		}

		foreach (var e in g.Edges())
		{
			var edge = g.Edge(e);
			(edge.Width, edge.Height) = (edge.Height, edge.Width);
		}
	}

	private static void ReverseY(LayoutGraph g)
	{
		foreach (var v in g.Nodes())
		{
			var node = g.Node(v);
			node.Y = -node.Y;
		}

		foreach (var e in g.Edges())
		{
			var edge = g.Edge(e);
			if (edge.Points is { } points)
			{
				for (var i = 0; i < points.Count; i++)
				{
					points[i] = (points[i].X, -points[i].Y);
				}
			}

			if (edge.HasPosition)
			{
				edge.Y = -edge.Y;
			}
		}
	}

	private static void SwapXY(LayoutGraph g)
	{
		foreach (var v in g.Nodes())
		{
			var node = g.Node(v);
			(node.X, node.Y) = (node.Y, node.X);
		}

		foreach (var e in g.Edges())
		{
			var edge = g.Edge(e);
			if (edge.Points is { } points)
			{
				for (var i = 0; i < points.Count; i++)
				{
					points[i] = (points[i].Y, points[i].X);
				}
			}

			if (edge.HasPosition)
			{
				(edge.X, edge.Y) = (edge.Y, edge.X);
			}
		}
	}
}
