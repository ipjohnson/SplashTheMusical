namespace SplashTheMusical.Layout.Dagre;

/// <summary>A port of dagre's <c>layout.js</c>, from dagre-d3-es 7.0.14, the version mermaid 12.3.0 uses.</summary>
internal static class DagreLayout
{
	/// <summary>
	/// dagre's <c>buildLayoutGraph</c>: the layout graph of the input, with dagre's defaults for what the input leaves
	/// out. Nodes and edges are added in the input's order. A node's parent is added when the node is, so a parent
	/// listed after its child comes right after the child.
	/// </summary>
	public static LayoutGraph BuildLayoutGraph(DagreInput input, UniqueIds? ids = null)
	{
		var options = input.Options;
		var label = new GraphLabel
		{
			RankDir = options.RankDir ?? "tb",
			NodeSep = options.NodeSep ?? 50,
			EdgeSep = options.EdgeSep ?? 20,
			RankSep = options.RankSep ?? 50,
			MarginX = options.MarginX,
			MarginY = options.MarginY,
			Align = options.Align,
			Acyclicer = options.Acyclicer,
			Ranker = options.Ranker,
		};
		var g = new LayoutGraph(label, ids ?? new UniqueIds());

		foreach (var node in input.Nodes)
		{
			var v = g.SetNode(node.Id, new LayoutNode { Width = node.Width, Height = node.Height });
			g.SetParent(v, node.Parent is null ? Graph<LayoutNode, LayoutEdge>.Root : g.EnsureNode(node.Parent));
		}

		foreach (var edge in input.Edges)
		{
			g.SetEdge(g.EnsureNode(edge.V), g.EnsureNode(edge.W), new LayoutEdge
			{
				MinLen = edge.MinLen,
				Weight = edge.Weight,
				Width = edge.Width,
				Height = edge.Height,
				LabelOffset = edge.LabelOffset,
				LabelPos = edge.LabelPos,
			}, edge.Name);
		}

		return g;
	}

	/// <summary>The phases of dagre's <c>runLayout</c> up to <c>normalize.run</c>, which leave each node its rank.</summary>
	public static void RunRankPhases(LayoutGraph g)
	{
		MakeSpaceForEdgeLabels(g);
		RemoveSelfEdges(g);
		Acyclic.Run(g);
		NestingGraph.Run(g);
		Rank.Run(Util.AsNonCompoundGraph(g));
		InjectEdgeLabelProxies(g);
		Util.RemoveEmptyRanks(g);
		NestingGraph.Cleanup(g);
		Util.NormalizeRanks(g);
		AssignRankMinMax(g);
		RemoveEdgeLabelProxies(g);
	}

	/// <summary>
	/// Splits each rank in half by doubling minlen and halving ranksep, so labels can sit between nodes (Gansner et al.).
	/// A label not centred on its edge gets its offset as padding. dagre compares rankdir with <c>TB</c> and <c>BT</c>
	/// without lower-casing it, so <c>tb</c> and <c>TD</c> pad the height.
	/// </summary>
	private static void MakeSpaceForEdgeLabels(LayoutGraph g)
	{
		var graph = g.Label;
		graph.RankSep /= 2;
		foreach (var e in g.Edges())
		{
			var edge = g.Edge(e);
			edge.MinLen *= 2;
			if (!string.Equals(edge.LabelPos, "c", StringComparison.OrdinalIgnoreCase))
			{
				if (graph.RankDir is "TB" or "BT")
				{
					edge.Width += edge.LabelOffset;
				}
				else
				{
					edge.Height += edge.LabelOffset;
				}
			}
		}
	}

	private static void RemoveSelfEdges(LayoutGraph g)
	{
		foreach (var e in g.Edges())
		{
			var v = g.EdgeV(e);
			if (v == g.EdgeW(e))
			{
				var node = g.Node(v);
				(node.SelfEdges ??= []).Add(new SelfEdge(v, v, g.EdgeName(e), g.Edge(e)));
				g.RemoveEdge(e);
			}
		}
	}

	/// <summary>
	/// Adds a dummy node on the rank of each edge's label, if it has a non-zero width and height, so that
	/// <c>removeEmptyRanks</c> keeps that rank.
	/// </summary>
	private static void InjectEdgeLabelProxies(LayoutGraph g)
	{
		foreach (var e in g.Edges())
		{
			var edge = g.Edge(e);
			if (IsTruthy(edge.Width) && IsTruthy(edge.Height))
			{
				var v = g.Node(g.EdgeV(e));
				var w = g.Node(g.EdgeW(e));
				var label = new LayoutNode { Rank = ((w.Rank - v.Rank) / 2) + v.Rank, ProxyEdge = e };
				Util.AddDummyNode(g, "edge-proxy", label, "_ep");
			}
		}
	}

	private static void AssignRankMinMax(LayoutGraph g)
	{
		var maxRank = 0.0;
		foreach (var v in g.Nodes())
		{
			var node = g.Node(v);
			if (node.BorderTop >= 0)
			{
				node.MinRank = g.Node(node.BorderTop).Rank;
				node.MaxRank = g.Node(node.BorderBottom).Rank;

				// dagre calls _.max(maxRank, node.maxRank), which is undefined for a number.
				maxRank = double.NaN;
			}
		}

		g.Label.MaxRank = maxRank;
	}

	private static void RemoveEdgeLabelProxies(LayoutGraph g)
	{
		foreach (var v in g.Nodes())
		{
			var node = g.Node(v);
			if (node.Dummy == "edge-proxy")
			{
				g.Edge(node.ProxyEdge).LabelRank = node.Rank;
				g.RemoveNode(v);
			}
		}
	}

	private static bool IsTruthy(double value) => value != 0 && !double.IsNaN(value);
}
