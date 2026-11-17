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

	/// <summary>dagre's <c>layout</c> without <c>updateInputGraph</c>: the laid out graph, whose nodes and edges have the input's names.</summary>
	public static LayoutGraph Run(DagreInput input)
	{
		var g = BuildLayoutGraph(input);
		RunRankPhases(g);
		RunOrderPhases(g);
		RunPositionPhases(g);
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
	/// The phases of dagre's <c>runLayout</c> from <c>normalize.run</c> through <c>order</c>, which leave each node its
	/// order in its rank. They follow <see cref="RunRankPhases"/>.
	/// </summary>
	public static void RunOrderPhases(LayoutGraph g)
	{
		Normalize.Run(g);
		ParentDummyChains.Run(g);
		BorderSegments.Add(g);
		Order.Run(g);
	}

	/// <summary>
	/// The phases of dagre's <c>runLayout</c> after <c>order</c>, which give each node its x and y, each edge its points
	/// and each edge label its position. They follow <see cref="RunOrderPhases"/>.
	/// </summary>
	public static void RunPositionPhases(LayoutGraph g)
	{
		InsertSelfEdges(g);
		CoordinateSystem.Adjust(g);
		Position.Run(g);
		PositionSelfEdges(g);
		RemoveBorderNodes(g);
		Normalize.Undo(g);
		FixupEdgeLabelCoords(g);
		CoordinateSystem.Undo(g);
		TranslateGraph(g);
		AssignNodeIntersects(g);
		ReversePointsForReversedEdges(g);
		Acyclic.Undo(g);
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

	/// <summary>
	/// Renumbers each rank's order, and puts a <c>selfedge</c> dummy, sized as the edge's label, right after each node
	/// for each of its self edges, so that the position phase leaves room for the loop.
	/// </summary>
	private static void InsertSelfEdges(LayoutGraph g)
	{
		foreach (var layer in Util.BuildLayerMatrix(g))
		{
			var orderShift = 0;
			for (var i = 0; i < layer.Length; i++)
			{
				if (layer[i] < 0)
				{
					continue;
				}

				var node = g.Node(layer[i]);
				node.Order = i + orderShift;
				if (node.SelfEdges is { } selfEdges)
				{
					foreach (var selfEdge in selfEdges)
					{
						var attrs = new LayoutNode
						{
							Width = selfEdge.Label.Width,
							Height = selfEdge.Label.Height,
							Rank = node.Rank,
							Order = i + ++orderShift,
							SelfEdge = selfEdge,
						};
						Util.AddDummyNode(g, "selfedge", attrs, "_se");
					}

					node.SelfEdges = null;
				}
			}
		}
	}

	/// <summary>Puts each self edge back as a loop from its node's right side around its dummy, and removes the dummy.</summary>
	private static void PositionSelfEdges(LayoutGraph g)
	{
		foreach (var v in g.Nodes())
		{
			var node = g.Node(v);
			if (node.Dummy != "selfedge")
			{
				continue;
			}

			var selfEdge = node.SelfEdge!;
			var selfNode = g.Node(selfEdge.V);
			var x = selfNode.X + (selfNode.Width / 2);
			var y = selfNode.Y;
			var dx = node.X - x;
			var dy = selfNode.Height / 2;
			g.SetEdge(selfEdge.V, selfEdge.W, selfEdge.Label, selfEdge.Name);
			g.RemoveNode(v);
			selfEdge.Label.Points =
			[
				(x + (2 * dx / 3), y - dy),
				(x + (5 * dx / 6), y - dy),
				(x + dx, y),
				(x + (5 * dx / 6), y + dy),
				(x + (2 * dx / 3), y + dy),
			];
			selfEdge.Label.HasPosition = true;
			selfEdge.Label.X = node.X;
			selfEdge.Label.Y = node.Y;
		}
	}

	/// <summary>Sizes and places each cluster from its border nodes, then removes the border nodes.</summary>
	private static void RemoveBorderNodes(LayoutGraph g)
	{
		foreach (var v in g.Nodes())
		{
			if (!g.HasChildren(v))
			{
				continue;
			}

			var node = g.Node(v);
			var t = g.Node(node.BorderTop);
			var b = g.Node(node.BorderBottom);
			var l = g.Node(node.BorderLeft![^1]);
			var r = g.Node(node.BorderRight![^1]);

			node.Width = Math.Abs(r.X - l.X);
			node.Height = Math.Abs(b.Y - t.Y);
			node.X = l.X + (node.Width / 2);
			node.Y = t.Y + (node.Height / 2);
		}

		foreach (var v in g.Nodes())
		{
			if (g.Node(v).Dummy == "border")
			{
				g.RemoveNode(v);
			}
		}
	}

	/// <summary>Moves a label that sits to the left or right of its edge off the edge, by the padding it was given.</summary>
	private static void FixupEdgeLabelCoords(LayoutGraph g)
	{
		foreach (var e in g.Edges())
		{
			var edge = g.Edge(e);
			if (!edge.HasPosition)
			{
				continue;
			}

			if (edge.LabelPos is "l" or "r")
			{
				edge.Width -= edge.LabelOffset;
			}

			switch (edge.LabelPos)
			{
				case "l":
					edge.X -= (edge.Width / 2) + edge.LabelOffset;
					break;
				case "r":
					edge.X += (edge.Width / 2) + edge.LabelOffset;
					break;
			}
		}
	}

	/// <summary>Moves the layout so that its nodes and labels start at the margins, and sets the graph's size.</summary>
	private static void TranslateGraph(LayoutGraph g)
	{
		var minX = double.PositiveInfinity;
		var maxX = 0.0;
		var minY = double.PositiveInfinity;
		var maxY = 0.0;
		var graphLabel = g.Label;
		var marginX = OrZero(graphLabel.MarginX);
		var marginY = OrZero(graphLabel.MarginY);

		void GetExtremes(double x, double y, double w, double h)
		{
			minX = Math.Min(minX, x - (w / 2));
			maxX = Math.Max(maxX, x + (w / 2));
			minY = Math.Min(minY, y - (h / 2));
			maxY = Math.Max(maxY, y + (h / 2));
		}

		foreach (var v in g.Nodes())
		{
			var node = g.Node(v);
			GetExtremes(node.X, node.Y, node.Width, node.Height);
		}

		foreach (var e in g.Edges())
		{
			var edge = g.Edge(e);
			if (edge.HasPosition)
			{
				GetExtremes(edge.X, edge.Y, edge.Width, edge.Height);
			}
		}

		minX -= marginX;
		minY -= marginY;

		foreach (var v in g.Nodes())
		{
			var node = g.Node(v);
			node.X -= minX;
			node.Y -= minY;
		}

		foreach (var e in g.Edges())
		{
			var edge = g.Edge(e);
			if (edge.Points is { } points)
			{
				for (var i = 0; i < points.Count; i++)
				{
					points[i] = (points[i].X - minX, points[i].Y - minY);
				}
			}

			if (edge.HasPosition)
			{
				edge.X -= minX;
				edge.Y -= minY;
			}
		}

		graphLabel.Width = maxX - minX + marginX;
		graphLabel.Height = maxY - minY + marginY;
	}

	/// <summary>Adds the points where each edge leaves its tail's box and enters its head's.</summary>
	private static void AssignNodeIntersects(LayoutGraph g)
	{
		foreach (var e in g.Edges())
		{
			var edge = g.Edge(e);
			var nodeV = g.Node(g.EdgeV(e));
			var nodeW = g.Node(g.EdgeW(e));
			(double X, double Y) p1;
			(double X, double Y) p2;
			if (edge.Points is not { } points)
			{
				points = [];
				edge.Points = points;
				p1 = (nodeW.X, nodeW.Y);
				p2 = (nodeV.X, nodeV.Y);
			}
			else
			{
				p1 = points[0];
				p2 = points[^1];
			}

			points.Insert(0, Util.IntersectRect(nodeV, p1));
			points.Add(Util.IntersectRect(nodeW, p2));
		}
	}

	private static void ReversePointsForReversedEdges(LayoutGraph g)
	{
		foreach (var e in g.Edges())
		{
			var edge = g.Edge(e);
			if (edge.Reversed)
			{
				edge.Points!.Reverse();
			}
		}
	}

	/// <summary>JavaScript's <c>value || 0</c> for an attribute that may be missing.</summary>
	private static double OrZero(double? value) => value is { } number && !double.IsNaN(number) ? number : 0;

	private static bool IsTruthy(double value) => value != 0 && !double.IsNaN(value);
}
