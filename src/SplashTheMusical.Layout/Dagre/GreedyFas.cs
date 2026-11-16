namespace SplashTheMusical.Layout.Dagre;

/// <summary>
/// A port of dagre's <c>greedy-fas.js</c>: the greedy feedback arc set heuristic of Eades, Lin and Smyth, with weighted
/// edges. dagre uses it when the graph's <c>acyclicer</c> is <c>greedy</c>.
/// </summary>
internal static class GreedyFas
{
	/// <summary>The edges of g to reverse, each multi-edge of a pair in the graph's order.</summary>
	public static List<int> Find(LayoutGraph g)
	{
		var result = new List<int>();
		if (g.NodeCount <= 1)
		{
			return result;
		}

		var (fasGraph, buckets, zeroIdx) = BuildState(g);
		foreach (var (v, w) in DoGreedyFas(fasGraph, buckets, zeroIdx))
		{
			result.AddRange(g.OutEdges(v, w));
		}

		return result;
	}

	/// <summary>The pairs of the input graph's nodes whose edges close a cycle.</summary>
	private static List<(int V, int W)> DoGreedyFas(Graph<Entry, Weight> g, Bucket[] buckets, int zeroIdx)
	{
		var results = new List<(int V, int W)>();
		var sources = buckets[^1];
		var sinks = buckets[0];

		while (g.NodeCount > 0)
		{
			Entry? entry;
			while ((entry = sinks.Dequeue()) is not null)
			{
				RemoveNode(g, buckets, zeroIdx, entry, null);
			}

			while ((entry = sources.Dequeue()) is not null)
			{
				RemoveNode(g, buckets, zeroIdx, entry, null);
			}

			if (g.NodeCount > 0)
			{
				for (var i = buckets.Length - 2; i > 0; --i)
				{
					entry = buckets[i].Dequeue();
					if (entry is not null)
					{
						RemoveNode(g, buckets, zeroIdx, entry, results);
						break;
					}
				}
			}
		}

		return results;
	}

	private static void RemoveNode(Graph<Entry, Weight> g, Bucket[] buckets, int zeroIdx, Entry entry, List<(int V, int W)>? results)
	{
		foreach (var edge in g.InEdges(entry.Handle))
		{
			var weight = g.Edge(edge).Value;
			var uEntry = g.Node(g.EdgeV(edge));
			results?.Add((uEntry.V, entry.V));
			uEntry.Out -= weight;
			AssignBucket(buckets, zeroIdx, uEntry);
		}

		foreach (var edge in g.OutEdges(entry.Handle))
		{
			var weight = g.Edge(edge).Value;
			var wEntry = g.Node(g.EdgeW(edge));
			wEntry.In -= weight;
			AssignBucket(buckets, zeroIdx, wEntry);
		}

		g.RemoveNode(entry.Handle);
	}

	private static (Graph<Entry, Weight> Graph, Bucket[] Buckets, int ZeroIdx) BuildState(LayoutGraph g)
	{
		var fasGraph = new Graph<Entry, Weight>();
		double maxIn = 0;
		double maxOut = 0;

		foreach (var v in g.Nodes())
		{
			var entry = new Entry(v);
			entry.Handle = fasGraph.SetNode(g.Name(v), entry);
		}

		// Sums the weights of a pair's multi-edges into one edge of fasGraph.
		foreach (var e in g.Edges())
		{
			var v = fasGraph.NodeHandle(g.Name(g.EdgeV(e)));
			var w = fasGraph.NodeHandle(g.Name(g.EdgeW(e)));
			var existing = fasGraph.FindEdge(v, w);
			var prevWeight = existing >= 0 ? fasGraph.Edge(existing).Value : 0;
			var weight = g.Edge(e).Weight;
			fasGraph.SetEdge(v, w, new Weight(prevWeight + weight));
			maxOut = Math.Max(maxOut, fasGraph.Node(v).Out += weight);
			maxIn = Math.Max(maxIn, fasGraph.Node(w).In += weight);
		}

		var buckets = new Bucket[(int)(maxOut + maxIn + 3)];
		for (var i = 0; i < buckets.Length; i++)
		{
			buckets[i] = new Bucket();
		}

		var zeroIdx = (int)maxIn + 1;
		foreach (var v in fasGraph.Nodes())
		{
			AssignBucket(buckets, zeroIdx, fasGraph.Node(v));
		}

		return (fasGraph, buckets, zeroIdx);
	}

	private static void AssignBucket(Bucket[] buckets, int zeroIdx, Entry entry)
	{
		if (entry.Out == 0 || double.IsNaN(entry.Out))
		{
			buckets[0].Enqueue(entry);
		}
		else if (entry.In == 0 || double.IsNaN(entry.In))
		{
			buckets[^1].Enqueue(entry);
		}
		else
		{
			buckets[(int)(entry.Out - entry.In) + zeroIdx].Enqueue(entry);
		}
	}

	private sealed class Entry(int v)
	{
		/// <summary>The node of the input graph.</summary>
		public int V { get; } = v;

		/// <summary>The node of fasGraph.</summary>
		public int Handle { get; set; }

		public double In { get; set; }

		public double Out { get; set; }

		public Entry? Prev { get; set; }

		public Entry? Next { get; set; }
	}

	private sealed class Weight(double value)
	{
		public double Value { get; } = value;
	}

	/// <summary>dagre's <c>List</c>: a doubly linked queue that takes entries at the front and gives them from the back.</summary>
	private sealed class Bucket
	{
		private readonly Entry _sentinel = new(-1);

		public Bucket()
		{
			_sentinel.Next = _sentinel;
			_sentinel.Prev = _sentinel;
		}

		public Entry? Dequeue()
		{
			var entry = _sentinel.Prev!;
			if (entry == _sentinel)
			{
				return null;
			}

			Unlink(entry);
			return entry;
		}

		public void Enqueue(Entry entry)
		{
			if (entry.Prev is not null && entry.Next is not null)
			{
				Unlink(entry);
			}

			entry.Next = _sentinel.Next;
			_sentinel.Next!.Prev = entry;
			_sentinel.Next = entry;
			entry.Prev = _sentinel;
		}

		private static void Unlink(Entry entry)
		{
			entry.Prev!.Next = entry.Next;
			entry.Next!.Prev = entry.Prev;
			entry.Next = null;
			entry.Prev = null;
		}
	}
}
