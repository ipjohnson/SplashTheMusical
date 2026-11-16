namespace SplashTheMusical.Layout.Dagre;

/// <summary>
/// A port of dagre's <c>order/resolve-conflicts.js</c>, after Forster, "A Fast and Simple Heuristic for Constrained
/// Two-Level Crossing Reduction". Entries whose barycenters break an edge of the constraint graph are merged into one
/// entry that keeps the constraint, with their barycenters and weights combined.
/// </summary>
internal static class ResolveConflicts
{
	/// <summary>Nodes that sort as one, with the lowest index of their entries.</summary>
	public sealed class Resolved(List<int> vs, int i)
	{
		public List<int> Vs { get; set; } = vs;

		public int I { get; set; } = i;

		public double? Barycenter { get; set; }

		public double Weight { get; set; }

		public int Indegree { get; set; }

		public List<Resolved> In { get; } = [];

		public List<Resolved> Out { get; } = [];

		public bool Merged { get; set; }
	}

	public static List<Resolved> Run(LayerGraph g, List<SortSubgraph.Entry> entries, ConstraintGraph cg)
	{
		var mappedEntries = new Dictionary<string, Resolved>(StringComparer.Ordinal);
		var keys = new List<(long ArrayIndex, Resolved Entry)>(entries.Count);
		for (var i = 0; i < entries.Count; i++)
		{
			var entry = entries[i];
			var name = g.Name(entry.V);
			var tmp = new Resolved([entry.V], i);
			if (entry.Barycenter is not null)
			{
				tmp.Barycenter = entry.Barycenter;
				tmp.Weight = entry.Weight;
			}

			mappedEntries[name] = tmp;
			keys.Add((JsKeys.ArrayIndex(name), tmp));
		}

		foreach (var e in cg.Edges())
		{
			if (mappedEntries.TryGetValue(cg.Name(cg.EdgeV(e)), out var entryV)
				&& mappedEntries.TryGetValue(cg.Name(cg.EdgeW(e)), out var entryW))
			{
				entryW.Indegree++;
				entryV.Out.Add(entryW);
			}
		}

		// _.filter over mappedEntries visits them in the order of the object's keys.
		var sourceSet = new List<Resolved>();
		foreach (var (_, entry) in keys.Where(key => key.ArrayIndex >= 0).OrderBy(key => key.ArrayIndex))
		{
			if (entry.Indegree == 0)
			{
				sourceSet.Add(entry);
			}
		}

		foreach (var (_, entry) in keys.Where(key => key.ArrayIndex < 0))
		{
			if (entry.Indegree == 0)
			{
				sourceSet.Add(entry);
			}
		}

		return DoResolveConflicts(sourceSet);
	}

	private static List<Resolved> DoResolveConflicts(List<Resolved> sourceSet)
	{
		var entries = new List<Resolved>();
		while (sourceSet.Count > 0)
		{
			var entry = sourceSet[^1];
			sourceSet.RemoveAt(sourceSet.Count - 1);
			entries.Add(entry);

			entry.In.Reverse();
			foreach (var uEntry in entry.In)
			{
				if (uEntry.Merged)
				{
					continue;
				}

				if (uEntry.Barycenter is null || entry.Barycenter is null || uEntry.Barycenter >= entry.Barycenter)
				{
					MergeEntries(entry, uEntry);
				}
			}

			foreach (var wEntry in entry.Out)
			{
				wEntry.In.Add(entry);
				if (--wEntry.Indegree == 0)
				{
					sourceSet.Add(wEntry);
				}
			}
		}

		return entries.FindAll(entry => !entry.Merged);
	}

	private static void MergeEntries(Resolved target, Resolved source)
	{
		double sum = 0;
		double weight = 0;

		if (IsTruthy(target.Weight))
		{
			sum += target.Barycenter!.Value * target.Weight;
			weight += target.Weight;
		}

		if (IsTruthy(source.Weight))
		{
			sum += source.Barycenter!.Value * source.Weight;
			weight += source.Weight;
		}

		var vs = new List<int>(source.Vs.Count + target.Vs.Count);
		vs.AddRange(source.Vs);
		vs.AddRange(target.Vs);
		target.Vs = vs;
		target.Barycenter = sum / weight;
		target.Weight = weight;
		target.I = Math.Min(source.I, target.I);
		source.Merged = true;
	}

	private static bool IsTruthy(double value) => value != 0 && !double.IsNaN(value);
}

/// <summary>
/// The graph of order constraints between clusters that one sweep builds up, keyed by the layout graph's names, as
/// dagre's <c>new Graph()</c> in <c>sweepLayerGraphs</c>.
/// </summary>
internal sealed class ConstraintGraph : Graph<object, object>
{
	private readonly object _label = new();

	public void SetEdge(string v, string w) => SetEdge(EnsureNode(v), EnsureNode(w), _label);
}
