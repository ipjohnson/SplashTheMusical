namespace SplashTheMusical.Layout.Dagre;

/// <summary>
/// A port of dagre's <c>normalize.js</c>. <c>run</c> splits every edge that spans more than one rank into a chain of
/// <c>edge</c> dummies, one per rank, and marks the dummy on the edge's label rank as <c>edge-label</c>.
/// </summary>
internal static class Normalize
{
	public static void Run(LayoutGraph g)
	{
		g.Label.DummyChains.Clear();
		foreach (var e in g.Edges())
		{
			NormalizeEdge(g, e);
		}
	}

	private static void NormalizeEdge(LayoutGraph g, int e)
	{
		var v = g.EdgeV(e);
		var vRank = g.Node(v).Rank;
		var w = g.EdgeW(e);
		var wRank = g.Node(w).Rank;
		var name = g.EdgeName(e);
		var edgeLabel = g.Edge(e);
		var labelRank = edgeLabel.LabelRank;

		if (wRank == vRank + 1)
		{
			return;
		}

		g.RemoveEdge(e);

		var i = 0;
		for (++vRank; vRank < wRank; ++i, ++vRank)
		{
			edgeLabel.Points = [];
			var attrs = new LayoutNode { EdgeLabel = edgeLabel, EdgeObj = e, Rank = vRank };
			var dummy = Util.AddDummyNode(g, "edge", attrs, "_d");
			if (vRank == labelRank)
			{
				attrs.Width = edgeLabel.Width;
				attrs.Height = edgeLabel.Height;
				attrs.Dummy = "edge-label";
				attrs.LabelPos = edgeLabel.LabelPos;
			}

			g.SetEdge(v, dummy, new LayoutEdge { Weight = edgeLabel.Weight }, name);
			if (i == 0)
			{
				g.Label.DummyChains.Add(dummy);
			}

			v = dummy;
		}

		g.SetEdge(v, w, new LayoutEdge { Weight = edgeLabel.Weight }, name);
	}

	/// <summary>
	/// Puts each split edge back with its dummies' positions as its points, and the <c>edge-label</c> dummy's position
	/// and size as its label's.
	/// </summary>
	public static void Undo(LayoutGraph g)
	{
		foreach (var first in g.Label.DummyChains)
		{
			var v = first;
			var node = g.Node(v);
			var origLabel = node.EdgeLabel!;
			var e = node.EdgeObj;
			g.SetEdge(g.EdgeV(e), g.EdgeW(e), origLabel, g.EdgeName(e));
			while (node.Dummy is not null)
			{
				var w = g.Successors(v)[0];
				g.RemoveNode(v);
				origLabel.Points!.Add((node.X, node.Y));
				if (node.Dummy == "edge-label")
				{
					origLabel.HasPosition = true;
					origLabel.X = node.X;
					origLabel.Y = node.Y;
					origLabel.Width = node.Width;
					origLabel.Height = node.Height;
				}

				v = w;
				node = g.Node(v);
			}
		}
	}
}
