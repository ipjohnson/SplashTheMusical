namespace SplashTheMusical.Layout.Dagre;

/// <summary>
/// A port of dagre's <c>position/bk.js</c>, coordinate assignment after Brandes and Köpf, "Fast and Simple Horizontal
/// Coordinate Assignment". Nodes are the handles of the graph without its clusters, so per-node state is held in
/// arrays indexed by handle.
/// </summary>
internal static class BrandesKopf
{
	private static readonly string[] Alignments = ["ul", "ur", "dl", "dr"];

	/// <summary>The x of each node of the layering, indexed by handle. A handle outside the layering holds NaN.</summary>
	public static double[] PositionX(LayoutGraph g, int[][] layering)
	{
		var size = 0;
		foreach (var layer in layering)
		{
			foreach (var v in layer)
			{
				size = Math.Max(size, v + 1);
			}
		}

		var conflicts = new HashSet<(int, int)>();
		FindType1Conflicts(g, layering, conflicts);
		FindType2Conflicts(g, layering, conflicts);

		var xss = new double[4][];
		for (var vert = 0; vert < 2; vert++)
		{
			var up = vert == 0;
			var adjusted = up ? layering : [.. layering.Reverse()];
			for (var horiz = 0; horiz < 2; horiz++)
			{
				var right = horiz == 1;
				if (right)
				{
					adjusted = [.. adjusted.Select(inner => inner.Reverse().ToArray())];
				}

				var (root, align) = VerticalAlignment(g, adjusted, conflicts, up, size);
				var xs = HorizontalCompaction(g, adjusted, root, align, right, size);
				if (right)
				{
					for (var v = 0; v < xs.Length; v++)
					{
						xs[v] = -xs[v];
					}
				}

				xss[(vert * 2) + horiz] = xs;
			}
		}

		var smallestWidth = FindSmallestWidthAlignment(g, layering, xss);
		AlignCoordinates(layering, xss, smallestWidth);
		return Balance(layering, xss, g.Label.Align, size);
	}

	/// <summary>
	/// Marks each edge where a non-inner segment crosses an inner segment, one whose ends are both dummies. Each rank
	/// is scanned against the one before it.
	/// </summary>
	private static void FindType1Conflicts(LayoutGraph g, int[][] layering, HashSet<(int, int)> conflicts)
	{
		for (var l = 1; l < layering.Length; l++)
		{
			var prevLayer = layering[l - 1];
			var layer = layering[l];
			var k0 = 0;
			var scanPos = 0;
			var lastNode = layer.Length > 0 ? layer[^1] : -1;
			for (var i = 0; i < layer.Length; i++)
			{
				var v = layer[i];
				var w = FindOtherInnerSegmentNode(g, v);
				var k1 = w >= 0 ? g.Node(w).Order : prevLayer.Length;
				if (w >= 0 || v == lastNode)
				{
					for (var j = scanPos; j <= i; j++)
					{
						var scanNode = layer[j];
						foreach (var u in g.Predecessors(scanNode))
						{
							var uLabel = g.Node(u);
							var uPos = uLabel.Order;
							if ((uPos < k0 || k1 < uPos) && !(uLabel.Dummy is not null && g.Node(scanNode).Dummy is not null))
							{
								AddConflict(conflicts, u, scanNode);
							}
						}
					}

					scanPos = i + 1;
					k0 = k1;
				}
			}
		}
	}

	/// <summary>
	/// Marks each inner segment that crosses a cluster's border. dagre runs the last scan of a rank inside the loop over
	/// its nodes, and starts it from an undefined north border, which NaN stands for here.
	/// </summary>
	private static void FindType2Conflicts(LayoutGraph g, int[][] layering, HashSet<(int, int)> conflicts)
	{
		for (var l = 1; l < layering.Length; l++)
		{
			var north = layering[l - 1];
			var south = layering[l];
			var prevNorthPos = -1.0;
			var nextNorthPos = double.NaN;
			var southPos = 0;
			for (var southLookahead = 0; southLookahead < south.Length; southLookahead++)
			{
				var v = south[southLookahead];
				if (g.Node(v).Dummy == "border")
				{
					var predecessors = g.Predecessors(v);
					if (predecessors.Length > 0)
					{
						nextNorthPos = g.Node(predecessors[0]).Order;
						Scan(g, south, southPos, southLookahead, prevNorthPos, nextNorthPos, conflicts);
						southPos = southLookahead;
						prevNorthPos = nextNorthPos;
					}
				}

				Scan(g, south, southPos, south.Length, nextNorthPos, north.Length, conflicts);
			}
		}
	}

	private static void Scan(LayoutGraph g, int[] south, int southPos, int southEnd, double prevNorthBorder, double nextNorthBorder, HashSet<(int, int)> conflicts)
	{
		for (var i = southPos; i < southEnd; i++)
		{
			var v = south[i];
			if (g.Node(v).Dummy is null)
			{
				continue;
			}

			foreach (var u in g.Predecessors(v))
			{
				var uNode = g.Node(u);
				if (uNode.Dummy is not null && (uNode.Order < prevNorthBorder || uNode.Order > nextNorthBorder))
				{
					AddConflict(conflicts, u, v);
				}
			}
		}
	}

	/// <summary>The first predecessor of a dummy that is a dummy too, or -1.</summary>
	private static int FindOtherInnerSegmentNode(LayoutGraph g, int v)
	{
		if (g.Node(v).Dummy is null)
		{
			return -1;
		}

		foreach (var u in g.Predecessors(v))
		{
			if (g.Node(u).Dummy is not null)
			{
				return u;
			}
		}

		return -1;
	}

	private static void AddConflict(HashSet<(int, int)> conflicts, int v, int w) =>
		conflicts.Add(v < w ? (v, w) : (w, v));

	private static bool HasConflict(HashSet<(int, int)> conflicts, int v, int w) =>
		conflicts.Contains(v < w ? (v, w) : (w, v));

	/// <summary>
	/// Aligns each node with a median neighbour into a block, unless the edge to it is a conflict or an earlier node of
	/// the rank aligned with a neighbour after it. Positions are the layering's, which the caller has turned for the
	/// alignment.
	/// </summary>
	private static (int[] Root, int[] Align) VerticalAlignment(LayoutGraph g, int[][] layering, HashSet<(int, int)> conflicts, bool up, int size)
	{
		var root = new int[size];
		var align = new int[size];
		var pos = new int[size];
		foreach (var layer in layering)
		{
			for (var order = 0; order < layer.Length; order++)
			{
				var v = layer[order];
				root[v] = v;
				align[v] = v;
				pos[v] = order;
			}
		}

		foreach (var layer in layering)
		{
			var prevIdx = -1;
			foreach (var v in layer)
			{
				var ws = up ? g.Predecessors(v) : g.Successors(v);
				if (ws.Length == 0)
				{
					continue;
				}

				ws = [.. ws.OrderBy(w => pos[w])];
				var mp = (ws.Length - 1) / 2.0;
				for (int i = (int)Math.Floor(mp), il = (int)Math.Ceiling(mp); i <= il; ++i)
				{
					var w = ws[i];
					if (align[v] == v && prevIdx < pos[w] && !HasConflict(conflicts, v, w))
					{
						align[w] = v;
						align[v] = root[v] = root[w];
						prevIdx = pos[w];
					}
				}
			}
		}

		return (root, align);
	}

	/// <summary>
	/// dagre's compaction, which differs from Brandes and Köpf's. It builds a graph of the blocks, gives each block the
	/// smallest x its predecessors allow, then moves it to the largest x its successors allow, unless it is a cluster's
	/// border on the side the alignment leans to.
	/// </summary>
	private static double[] HorizontalCompaction(LayoutGraph g, int[][] layering, int[] root, int[] align, bool reverseSep, int size)
	{
		var blocks = new BlockGraph(size);
		var label = g.Label;
		foreach (var layer in layering)
		{
			var u = -1;
			foreach (var v in layer)
			{
				var vRoot = root[v];
				blocks.SetNode(vRoot);
				if (u >= 0)
				{
					blocks.SetEdgeMax(root[u], vRoot, Sep(g, label.NodeSep, label.EdgeSep, reverseSep, v, u));
				}

				u = v;
			}
		}

		var xs = new double[size];
		Array.Fill(xs, double.NaN);
		var borderType = reverseSep ? "borderLeft" : "borderRight";

		blocks.Iterate(forward: false, elem =>
		{
			var acc = 0.0;
			foreach (var (from, weight) in blocks.In(elem))
			{
				acc = Math.Max(acc, xs[from] + weight);
			}

			xs[elem] = acc;
		});

		blocks.Iterate(forward: true, elem =>
		{
			var min = double.PositiveInfinity;
			foreach (var (to, weight) in blocks.Out(elem))
			{
				min = Math.Min(min, xs[to] - weight);
			}

			if (min != double.PositiveInfinity && g.Node(elem).BorderType != borderType)
			{
				xs[elem] = Math.Max(xs[elem], min);
			}
		});

		foreach (var layer in layering)
		{
			foreach (var node in layer)
			{
				var v = align[node];
				xs[v] = xs[root[v]];
			}
		}

		return xs;
	}

	/// <summary>The alignment whose nodes span the least width. The first wins a tie, as lodash's <c>_.minBy</c> keeps it.</summary>
	private static int FindSmallestWidthAlignment(LayoutGraph g, int[][] layering, double[][] xss)
	{
		var best = -1;
		var bestWidth = double.NaN;
		for (var a = 0; a < xss.Length; a++)
		{
			var max = double.NegativeInfinity;
			var min = double.PositiveInfinity;
			foreach (var layer in layering)
			{
				foreach (var v in layer)
				{
					var halfWidth = g.Node(v).Width / 2;
					max = Math.Max(xss[a][v] + halfWidth, max);
					min = Math.Min(xss[a][v] - halfWidth, min);
				}
			}

			var width = max - min;
			if (!double.IsNaN(width) && (best < 0 || width < bestWidth))
			{
				best = a;
				bestWidth = width;
			}
		}

		return best;
	}

	/// <summary>
	/// Shifts the left alignments so their smallest x is the narrowest alignment's, and the right alignments so their
	/// largest x is its.
	/// </summary>
	private static void AlignCoordinates(int[][] layering, double[][] xss, int alignTo)
	{
		var (alignToMin, alignToMax) = alignTo >= 0 ? Extremes(layering, xss[alignTo]) : (double.NaN, double.NaN);
		for (var a = 0; a < xss.Length; a++)
		{
			if (a == alignTo)
			{
				continue;
			}

			var (min, max) = Extremes(layering, xss[a]);
			var delta = a % 2 == 0 ? alignToMin - min : alignToMax - max;
			if (delta != 0 && !double.IsNaN(delta))
			{
				var xs = xss[a];
				for (var v = 0; v < xs.Length; v++)
				{
					xs[v] += delta;
				}
			}
		}
	}

	/// <summary>The graph's align alignment, or the mean of the two middle values of the four alignments.</summary>
	private static double[] Balance(int[][] layering, double[][] xss, string? align, int size)
	{
		var result = new double[size];
		Array.Fill(result, double.NaN);
		var chosen = -1;
		if (align is not null)
		{
			chosen = Array.IndexOf(Alignments, align.ToLowerInvariant());
			if (chosen < 0)
			{
				throw new InvalidOperationException($"dagre has no alignment '{align}'.");
			}
		}

		Span<double> values = stackalloc double[4];
		foreach (var layer in layering)
		{
			foreach (var v in layer)
			{
				if (chosen >= 0)
				{
					result[v] = xss[chosen][v];
					continue;
				}

				for (var a = 0; a < 4; a++)
				{
					values[a] = xss[a][v];
				}

				values.Sort();
				result[v] = (values[1] + values[2]) / 2;
			}
		}

		return result;
	}

	/// <summary>lodash's <c>_.min</c> and <c>_.max</c> of the layering's x values, which skip NaN.</summary>
	private static (double Min, double Max) Extremes(int[][] layering, double[] xs)
	{
		var min = double.NaN;
		var max = double.NaN;
		foreach (var layer in layering)
		{
			foreach (var v in layer)
			{
				var x = xs[v];
				if (double.IsNaN(x))
				{
					continue;
				}

				if (double.IsNaN(min) || x < min)
				{
					min = x;
				}

				if (double.IsNaN(max) || x > max)
				{
					max = x;
				}
			}
		}

		return (min, max);
	}

	/// <summary>
	/// The least distance between the centres of v and the node w before it in the rank: half of each width, half of
	/// each one's separation, and the offset of an edge label that sits to one side of its dummy.
	/// </summary>
	private static double Sep(LayoutGraph g, double nodeSep, double edgeSep, bool reverseSep, int v, int w)
	{
		var vLabel = g.Node(v);
		var wLabel = g.Node(w);
		var sum = vLabel.Width / 2;
		var delta = 0.0;
		switch (vLabel.LabelPos?.ToLowerInvariant())
		{
			case "l":
				delta = -vLabel.Width / 2;
				break;
			case "r":
				delta = vLabel.Width / 2;
				break;
		}

		if (delta != 0 && !double.IsNaN(delta))
		{
			sum += reverseSep ? delta : -delta;
		}

		delta = 0;
		sum += (vLabel.Dummy is not null ? edgeSep : nodeSep) / 2;
		sum += (wLabel.Dummy is not null ? edgeSep : nodeSep) / 2;
		sum += wLabel.Width / 2;
		switch (wLabel.LabelPos?.ToLowerInvariant())
		{
			case "l":
				delta = wLabel.Width / 2;
				break;
			case "r":
				delta = -wLabel.Width / 2;
				break;
		}

		if (delta != 0 && !double.IsNaN(delta))
		{
			sum += reverseSep ? delta : -delta;
		}

		return sum;
	}

	/// <summary>
	/// The block graph of <c>buildBlockGraph</c>: a simple directed graph over block roots whose edge weight is the
	/// largest separation between the two blocks on any rank.
	/// </summary>
	private sealed class BlockGraph(int size)
	{
		private readonly List<int> _nodes = [];
		private readonly bool[] _present = new bool[size];
		private readonly Dictionary<(int, int), int> _edgeIndex = [];
		private readonly List<(int From, int To, double Weight)> _edges = [];
		private readonly List<int>?[] _in = new List<int>?[size];
		private readonly List<int>?[] _out = new List<int>?[size];

		public void SetNode(int v)
		{
			if (!_present[v])
			{
				_present[v] = true;
				_nodes.Add(v);
			}
		}

		/// <summary>Sets the edge's weight to the larger of the separation and its present weight, or 0 for a new edge.</summary>
		public void SetEdgeMax(int from, int to, double separation)
		{
			SetNode(from);
			SetNode(to);
			if (_edgeIndex.TryGetValue((from, to), out var e))
			{
				_edges[e] = (from, to, Math.Max(separation, _edges[e].Weight));
				return;
			}

			_edgeIndex.Add((from, to), _edges.Count);
			(_in[to] ??= []).Add(_edges.Count);
			(_out[from] ??= []).Add(_edges.Count);
			_edges.Add((from, to, Math.Max(separation, 0)));
		}

		public IEnumerable<(int From, double Weight)> In(int v)
		{
			foreach (var e in _in[v] ?? [])
			{
				yield return (_edges[e].From, _edges[e].Weight);
			}
		}

		public IEnumerable<(int To, double Weight)> Out(int v)
		{
			foreach (var e in _out[v] ?? [])
			{
				yield return (_edges[e].To, _edges[e].Weight);
			}
		}

		/// <summary>
		/// dagre's <c>iterate</c>: a depth-first walk that sets a block after the blocks it depends on, its predecessors
		/// for the first pass and its successors for the second. A block is set again each time it is popped after its
		/// first visit, which leaves its value as it was.
		/// </summary>
		public void Iterate(bool forward, Action<int> setXs)
		{
			var stack = new List<int>(_nodes);
			var visited = new bool[_present.Length];
			while (stack.Count > 0)
			{
				var elem = stack[^1];
				stack.RemoveAt(stack.Count - 1);
				if (visited[elem])
				{
					setXs(elem);
					continue;
				}

				visited[elem] = true;
				stack.Add(elem);
				foreach (var e in (forward ? _out[elem] : _in[elem]) ?? [])
				{
					stack.Add(forward ? _edges[e].To : _edges[e].From);
				}
			}
		}
	}
}
