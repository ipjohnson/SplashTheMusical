namespace SplashTheMusical.Layout.Dagre;

/// <summary>
/// A port of dagre's <c>order/sort-subgraph.js</c>, <c>order/barycenter.js</c> and <c>order/sort.js</c>. It sorts a
/// cluster's nodes on one rank by the barycenter of their neighbours on the fixed rank, keeping each child cluster's
/// nodes together and between the cluster's border nodes.
/// </summary>
internal static class SortSubgraph
{
	/// <summary>The nodes in their new order, and the barycenter and weight of the whole, when it has one.</summary>
	public sealed class Result(List<int> vs)
	{
		public List<int> Vs { get; set; } = vs;

		public double? Barycenter { get; set; }

		public double Weight { get; set; }
	}

	/// <summary>A node and the barycenter of its in-edges. A node with no in-edges has none.</summary>
	public sealed class Entry(int v)
	{
		public int V { get; } = v;

		public double? Barycenter { get; set; }

		public double Weight { get; set; }
	}

	public static Result Run(LayerGraph g, int v, ConstraintGraph cg, bool biasRight)
	{
		var movable = g.Children(v);
		var node = g.Node(v);
		var bl = node.BorderLeft is null ? -1 : g.NodeHandle(node.BorderLeft);
		var br = node.BorderRight is null ? -1 : g.NodeHandle(node.BorderRight);
		var subgraphs = new Dictionary<int, Result>();

		if (node.BorderLeft is not null)
		{
			movable = Array.FindAll(movable, w => w != bl && w != br);
		}

		var barycenters = Barycenters(g, movable);
		foreach (var entry in barycenters)
		{
			if (g.HasChildren(entry.V))
			{
				var subgraphResult = Run(g, entry.V, cg, biasRight);
				subgraphs[entry.V] = subgraphResult;
				if (subgraphResult.Barycenter is not null)
				{
					MergeBarycenters(entry, subgraphResult);
				}
			}
		}

		var entries = ResolveConflicts.Run(g, barycenters, cg);
		ExpandSubgraphs(entries, subgraphs);

		var result = Sort(entries, biasRight);

		if (node.BorderLeft is not null)
		{
			var vs = new List<int>(result.Vs.Count + 2) { bl };
			vs.AddRange(result.Vs);
			vs.Add(br);
			result.Vs = vs;

			var blPreds = g.Predecessors(bl);
			if (blPreds.Length > 0)
			{
				var blPred = g.Node(blPreds[0]).Shared!;
				var brPred = g.Node(g.Predecessors(br)[0]).Shared!;
				if (result.Barycenter is null)
				{
					result.Barycenter = 0;
					result.Weight = 0;
				}

				result.Barycenter = ((result.Barycenter * result.Weight) + blPred.Order + brPred.Order) / (result.Weight + 2);
				result.Weight += 2;
			}
		}

		return result;
	}

	/// <summary>dagre's <c>barycenter</c>: the weighted mean order of each node's in-edge tails.</summary>
	public static List<Entry> Barycenters(LayerGraph g, int[] movable)
	{
		var result = new List<Entry>(movable.Length);
		foreach (var v in movable)
		{
			var entry = new Entry(v);
			var inV = g.InEdges(v);
			if (inV.Length > 0)
			{
				double sum = 0;
				double weight = 0;
				foreach (var e in inV)
				{
					var edgeWeight = g.Edge(e).Weight;
					sum += edgeWeight * g.Node(g.EdgeV(e)).Shared!.Order;
					weight += edgeWeight;
				}

				entry.Barycenter = sum / weight;
				entry.Weight = weight;
			}

			result.Add(entry);
		}

		return result;
	}

	/// <summary>
	/// dagre's <c>sort</c>. The entries with a barycenter are sorted by it, and the others keep their index, as far as
	/// the sorted ones leave room.
	/// </summary>
	public static Result Sort(List<ResolveConflicts.Resolved> entries, bool biasRight)
	{
		var sortable = new List<ResolveConflicts.Resolved>();
		var unsortable = new List<ResolveConflicts.Resolved>();
		foreach (var entry in entries)
		{
			(entry.Barycenter is not null ? sortable : unsortable).Add(entry);
		}

		// lodash's sortBy on -i, which is stable.
		unsortable = [.. unsortable.OrderByDescending(entry => entry.I)];

		var vs = new List<int>();
		double sum = 0;
		double weight = 0;

		JsSort.Sort(sortable, (entryV, entryW) => CompareWithBias(entryV, entryW, biasRight));

		var vsIndex = ConsumeUnsortable(vs, unsortable, 0);
		foreach (var entry in sortable)
		{
			vsIndex += entry.Vs.Count;
			vs.AddRange(entry.Vs);
			sum += entry.Barycenter!.Value * entry.Weight;
			weight += entry.Weight;
			vsIndex = ConsumeUnsortable(vs, unsortable, vsIndex);
		}

		var result = new Result(vs);
		if (weight != 0 && !double.IsNaN(weight))
		{
			result.Barycenter = sum / weight;
			result.Weight = weight;
		}

		return result;
	}

	private static int ConsumeUnsortable(List<int> vs, List<ResolveConflicts.Resolved> unsortable, int index)
	{
		while (unsortable.Count > 0 && unsortable[^1].I <= index)
		{
			vs.AddRange(unsortable[^1].Vs);
			unsortable.RemoveAt(unsortable.Count - 1);
			index++;
		}

		return index;
	}

	private static double CompareWithBias(ResolveConflicts.Resolved entryV, ResolveConflicts.Resolved entryW, bool bias)
	{
		if (entryV.Barycenter < entryW.Barycenter)
		{
			return -1;
		}

		if (entryV.Barycenter > entryW.Barycenter)
		{
			return 1;
		}

		return !bias ? entryV.I - entryW.I : entryW.I - entryV.I;
	}

	private static void ExpandSubgraphs(List<ResolveConflicts.Resolved> entries, Dictionary<int, Result> subgraphs)
	{
		foreach (var entry in entries)
		{
			var vs = new List<int>(entry.Vs.Count);
			foreach (var v in entry.Vs)
			{
				if (subgraphs.TryGetValue(v, out var subgraph))
				{
					vs.AddRange(subgraph.Vs);
				}
				else
				{
					vs.Add(v);
				}
			}

			entry.Vs = vs;
		}
	}

	private static void MergeBarycenters(Entry target, Result other)
	{
		if (target.Barycenter is not null)
		{
			target.Barycenter = ((target.Barycenter * target.Weight) + (other.Barycenter * other.Weight)) / (target.Weight + other.Weight);
			target.Weight += other.Weight;
		}
		else
		{
			target.Barycenter = other.Barycenter;
			target.Weight = other.Weight;
		}
	}
}
