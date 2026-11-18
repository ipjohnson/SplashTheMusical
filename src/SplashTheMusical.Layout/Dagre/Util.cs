namespace SplashTheMusical.Layout.Dagre;

/// <summary>A port of dagre's <c>util.js</c>.</summary>
internal static class Util
{
	public static int AddDummyNode(LayoutGraph g, string type, LayoutNode attrs, string name)
	{
		string v;
		do
		{
			v = g.Ids.Next(name);
		}
		while (g.HasNode(v));

		attrs.Dummy = type;
		return g.SetNode(v, attrs);
	}

	public static int AddBorderNode(LayoutGraph g, string prefix) =>
		AddDummyNode(g, "border", new LayoutNode(), prefix);

	/// <summary>A graph with only simple edges. A multi-edge's weights are summed and its largest minlen kept.</summary>
	public static LayoutGraph Simplify(LayoutGraph g)
	{
		var simplified = new LayoutGraph(g.Label, g.Ids, multigraph: false, compound: false);
		foreach (var v in g.Nodes())
		{
			simplified.SetNode(g.Name(v), g.Node(v));
		}

		foreach (var e in g.Edges())
		{
			var v = simplified.NodeHandle(g.Name(g.EdgeV(e)));
			var w = simplified.NodeHandle(g.Name(g.EdgeW(e)));
			var existing = simplified.FindEdge(v, w);
			var simpleWeight = existing >= 0 ? simplified.Edge(existing).Weight : 0;
			var simpleMinLen = existing >= 0 ? simplified.Edge(existing).MinLen : 1;
			var label = g.Edge(e);
			simplified.SetEdge(v, w, new LayoutEdge
			{
				Weight = simpleWeight + label.Weight,
				MinLen = Math.Max(simpleMinLen, label.MinLen),
			});
		}

		return simplified;
	}

	/// <summary>The graph without its cluster nodes, sharing the labels of the nodes and edges.</summary>
	public static LayoutGraph AsNonCompoundGraph(LayoutGraph g)
	{
		var simplified = new LayoutGraph(g.Label, g.Ids, multigraph: g.IsMultigraph, compound: false);
		foreach (var v in g.Nodes())
		{
			if (!g.HasChildren(v))
			{
				simplified.SetNode(g.Name(v), g.Node(v));
			}
		}

		foreach (var e in g.Edges())
		{
			var v = simplified.NodeHandle(g.Name(g.EdgeV(e)));
			var w = simplified.NodeHandle(g.Name(g.EdgeW(e)));
			if (v < 0 || w < 0)
			{
				throw new DagreException(DagreException.RankOfCluster);
			}

			simplified.SetEdge(v, w, g.Edge(e), g.EdgeName(e));
		}

		return simplified;
	}

	/// <summary>Shifts the ranks so that the smallest is 0.</summary>
	public static void NormalizeRanks(LayoutGraph g)
	{
		var nodes = g.Nodes();
		var min = MinRank(g, nodes);
		foreach (var v in nodes)
		{
			var node = g.Node(v);
			if (node.HasRank)
			{
				node.Rank -= min;
			}
		}
	}

	/// <summary>
	/// Removes the empty ranks that are not a multiple of the nesting graph's rank factor. A node whose rank, less the
	/// smallest, is not an array index is left out, as dagre's sparse <c>layers</c> array leaves it out.
	/// </summary>
	public static void RemoveEmptyRanks(LayoutGraph g)
	{
		var nodes = g.Nodes();
		var offset = MinRank(g, nodes);
		var length = 0;
		foreach (var v in nodes)
		{
			var rank = g.Node(v).Rank - offset;
			if (IsArrayIndex(rank))
			{
				length = Math.Max(length, (int)rank + 1);
			}
		}

		var layers = new List<int>?[length];
		foreach (var v in nodes)
		{
			var rank = g.Node(v).Rank - offset;
			if (IsArrayIndex(rank))
			{
				(layers[(int)rank] ??= []).Add(v);
			}
		}

		var delta = 0;
		var nodeRankFactor = g.Label.NodeRankFactor;
		for (var i = 0; i < layers.Length; i++)
		{
			var vs = layers[i];
			if (vs is null && i % nodeRankFactor != 0)
			{
				--delta;
			}
			else if (delta != 0 && vs is not null)
			{
				foreach (var v in vs)
				{
					g.Node(v).Rank += delta;
				}
			}
		}
	}

	/// <summary>The largest rank of the nodes that have one, or NaN when none has.</summary>
	public static double MaxRank(LayoutGraph g)
	{
		var max = double.NaN;
		foreach (var v in g.Nodes())
		{
			var rank = g.Node(v).Rank;
			if (!double.IsNaN(rank) && (double.IsNaN(max) || rank > max))
			{
				max = rank;
			}
		}

		return max;
	}

	/// <summary>Where the line from the centre of the node's box to the point leaves the box.</summary>
	public static (double X, double Y) IntersectRect(LayoutNode rect, (double X, double Y) point)
	{
		var x = rect.X;
		var y = rect.Y;
		var dx = point.X - x;
		var dy = point.Y - y;
		var w = rect.Width / 2;
		var h = rect.Height / 2;

		if (!IsTruthy(dx) && !IsTruthy(dy))
		{
			throw new DagreException("Not possible to find intersection inside of the rectangle");
		}

		double sx;
		double sy;
		if (Math.Abs(dy) * w > Math.Abs(dx) * h)
		{
			if (dy < 0)
			{
				h = -h;
			}

			sx = h * dx / dy;
			sy = h;
		}
		else
		{
			if (dx < 0)
			{
				w = -w;
			}

			sx = w;
			sy = w * dy / dx;
		}

		return (x + sx, y + sy);
	}

	/// <summary>dagre's <c>buildLayerMatrix</c>: the nodes of each rank, each at its order. A gap holds -1.</summary>
	public static int[][] BuildLayerMatrix(LayoutGraph g)
	{
		var maxRank = MaxRank(g);
		var layers = new List<int>[double.IsNaN(maxRank) ? 0 : (int)maxRank + 1];
		for (var i = 0; i < layers.Length; i++)
		{
			layers[i] = [];
		}

		foreach (var v in g.Nodes())
		{
			var node = g.Node(v);
			if (!node.HasRank)
			{
				continue;
			}

			var layer = layers[(int)node.Rank];
			while (layer.Count <= node.Order)
			{
				layer.Add(-1);
			}

			layer[node.Order] = v;
		}

		var result = new int[layers.Length][];
		for (var i = 0; i < layers.Length; i++)
		{
			result[i] = [.. layers[i]];
		}

		return result;
	}

	/// <summary>lodash's <c>_.min</c> of the nodes' ranks, which skips a missing rank. NaN when no node has one.</summary>
	private static double MinRank(LayoutGraph g, int[] nodes)
	{
		var min = double.NaN;
		foreach (var v in nodes)
		{
			var rank = g.Node(v).Rank;
			if (!double.IsNaN(rank) && (double.IsNaN(min) || rank < min))
			{
				min = rank;
			}
		}

		return min;
	}

	private static bool IsTruthy(double value) => value != 0 && !double.IsNaN(value);

	private static bool IsArrayIndex(double value) =>
		value >= 0 && value <= 4294967294 && Math.Floor(value) == value;
}
