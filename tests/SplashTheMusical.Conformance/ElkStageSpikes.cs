using System.Globalization;
using System.Text;
using SplashTheMusical.Layout.Dagre;

namespace SplashTheMusical.Conformance;

/// <summary>
/// The spikes of <c>docs/layered-layout/design.md</c>. Each one scores one stage's approach on every recording, with
/// the stages before it held still at what ELK's recorded result gives: the layers and order read as
/// <see cref="ElkLayoutComparer"/> reads them, ELK's node boxes, and ELK's route ends. A spike is a measurement, not
/// engine code, and nothing in <c>src</c> uses it.
/// </summary>
internal static class ElkStageSpikes
{
	private const double Epsilon = 0.01;

	/// <summary>The outcome of one spike on one case: null when the measure does not apply to the case.</summary>
	internal sealed record Outcome(string Case, string Type, bool Nested, string Spike, bool? Passed);

	/// <summary>Counts of a value, for the rules a spike reads from the recordings.</summary>
	internal sealed class Histogram
	{
		private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);

		public void Add(string value) => _counts[value] = _counts.GetValueOrDefault(value) + 1;

		public IEnumerable<(string Value, int Count)> Top(int n) =>
			_counts.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).Take(n).Select(p => (p.Key, p.Value));

		public int Total => _counts.Values.Sum();
	}

	internal sealed class Run
	{
		public List<Outcome> Outcomes { get; } = [];

		public Histogram LayerGap { get; } = new();

		public Histogram NodeGap { get; } = new();

		public Histogram Margin { get; } = new();

		public Histogram SelfLoopBends { get; } = new();

		public Histogram EdgeBends { get; } = new();
	}

	public static readonly string[] Spikes =
	[
		"cycles: depth first, sources first",
		"cycles: depth first, model order",
		"layers: network simplex",
		"layers: longest path",
		"order: model order",
		"order: barycenter sweeps",
		"placement: dagre's Brandes-Koepf, ELK spacing",
		"routing: Z routes between ELK's ends",
		"self loops: 2 bends",
		"labels: centre of the middle segment",
		"nested: children's box and padding",
		"components: model order across",
		"ports: (w - 20) / (k + 1) apart",
		"layers: network simplex, a label takes a layer",
		"order: barycenter sweeps, edge order first",
	];

	public static Run Measure(string corpus)
	{
		var run = new Run();
		foreach (var file in ElkRecords.Files(corpus))
		{
			var name = file[..^".elk.txt".Length];
			var type = file[..file.IndexOf('/', StringComparison.Ordinal)];
			var results = new Dictionary<string, bool?>(StringComparer.Ordinal);
			var nested = false;
			foreach (var record in ElkRecords.Read(Path.Combine(corpus, file)))
			{
				var graph = new ElkInputGraph(record.Input);
				nested |= graph.Clusters.Count > 0;
				foreach (var (spike, passed) in Score(corpus, name, record, graph, run))
				{
					if (passed is { } p)
					{
						results[spike] = (results.GetValueOrDefault(spike) ?? true) && p;
					}
				}
			}

			foreach (var spike in Spikes)
			{
				run.Outcomes.Add(new Outcome(name, type, nested, spike, results.GetValueOrDefault(spike)));
			}
		}

		return run;
	}

	private static IEnumerable<(string Spike, bool? Passed)> Score(string corpus, string name, ElkRecord record, ElkInputGraph graph, Run run)
	{
		var elk = ElkLayoutComparer.ElkLayout(record, graph);
		var layers = ElkLayoutComparer.Layers(graph, elk);
		var layerOf = new Dictionary<string, int>(StringComparer.Ordinal);
		for (var i = 0; i < layers.Count; i++)
		{
			foreach (var id in layers[i])
			{
				layerOf[id] = i;
			}
		}

		var model = graph.Leaves.Select((id, i) => (id, i)).ToDictionary(p => p.id, p => p.i, StringComparer.Ordinal);
		var edges = graph.Edges.Where(e => e.Source != e.Target && layerOf.ContainsKey(e.Source) && layerOf.ContainsKey(e.Target)).ToList();

		// Cycle breaking: ELK reversed an edge when its target is in an earlier layer than its source.
		var elkReversed = edges.Where(e => layerOf[e.Source] != layerOf[e.Target]).ToDictionary(e => e.Id, e => layerOf[e.Target] < layerOf[e.Source], StringComparer.Ordinal);
		foreach (var (spike, sourcesFirst) in new[] { (Spikes[0], true), (Spikes[1], false) })
		{
			var reversed = DepthFirstBackEdges(graph.Leaves, edges, sourcesFirst);
			var applies = elkReversed.Values.Any(r => r) || reversed.Count > 0;
			yield return (spike, applies ? elkReversed.All(p => p.Value == reversed.Contains(p.Key)) : null);
		}

		// Layering, with ELK's orientation of each edge.
		var oriented = edges
			.Where(e => layerOf[e.Source] != layerOf[e.Target])
			.Select(e => layerOf[e.Source] < layerOf[e.Target] ? (e.Id, V: e.Source, W: e.Target) : (e.Id, V: e.Target, W: e.Source))
			.ToList();
		var labelWidth = edges.ToDictionary(e => e.Id, e => e.LabelWidth, StringComparer.Ordinal);
		foreach (var (spike, ranker) in new[] { (Spikes[2], "network-simplex"), (Spikes[3], "longest-path"), (Spikes[13], "network-simplex") })
		{
			if (layers.Count < 2)
			{
				yield return (spike, null);
				continue;
			}

			var nodes = graph.Leaves.Select(id => new DagreNodeInput(id, null, elk.Boxes[id].Width, elk.Boxes[id].Height)).ToList();
			var labelled = spike == Spikes[13];
			var dagreEdges = oriented.Select(e => new DagreEdgeInput(e.V, e.W, e.Id, MinLen: labelled && labelWidth[e.Id] > 0 ? 2 : 1)).ToList();
			var g = DagreLayout.Run(new DagreInput(new DagreOptions(RankDir: "TB", Ranker: ranker), nodes, dagreEdges));
			var ranks = graph.Leaves
				.GroupBy(id => Math.Round(g.Node(g.NodeHandle(id)).Y, 2))
				.OrderBy(group => group.Key)
				.Select(group => (IReadOnlyList<string>)[.. group.OrderBy(id => g.Node(g.NodeHandle(id)).X)])
				.ToList();
			yield return (spike, ElkLayoutComparer.Score(graph, elk, new CandidateLayout(0, 0, elk.Boxes, elk.Edges, ranks)).SameLayers);
		}

		// Ordering, with ELK's layers.
		var ordering = layers.Any(l => l.Count > 1);
		var modelOrder = layers.Select(l => (IReadOnlyList<string>)[.. l.OrderBy(id => model[id])]).ToList();
		yield return (Spikes[4], ordering ? ElkLayoutComparer.Score(graph, elk, new CandidateLayout(0, 0, elk.Boxes, elk.Edges, modelOrder)).SameOrder : null);
		yield return (Spikes[5], ordering ? ElkLayoutComparer.Score(graph, elk, new CandidateLayout(0, 0, elk.Boxes, elk.Edges, Barycenter(layers, layerOf, model, oriented))).SameOrder : null);

		// The probe's tie rule: siblings take the order of the edges that reach them, so a layer starts in the order of
		// each node's first incoming edge, then of the nodes.
		var firstIn = new Dictionary<string, int>(StringComparer.Ordinal);
		for (var i = 0; i < oriented.Count; i++)
		{
			firstIn.TryAdd(oriented[i].W, i);
		}

		var edgeFirst = model.ToDictionary(p => p.Key, p => ((firstIn.TryGetValue(p.Key, out var e) ? e : -1) * 100_000) + p.Value, StringComparer.Ordinal);
		yield return (Spikes[14], ordering ? ElkLayoutComparer.Score(graph, elk, new CandidateLayout(0, 0, elk.Boxes, elk.Edges, Barycenter(layers, layerOf, edgeFirst, oriented))).SameOrder : null);

		// Node placement. The rules come from flat graphs laid out down; the spike is scored on the cases whose layers
		// and order the dagre port already gets right, so the stages before it are held still.
		var flatDown = graph.Clusters.Count == 0 && graph.Direction == "DOWN";
		if (flatDown)
		{
			for (var i = 1; i < layers.Count; i++)
			{
				var bottom = layers[i - 1].Max(id => elk.Boxes[id].Y + elk.Boxes[id].Height);
				var top = layers[i].Min(id => elk.Boxes[id].Y);
				run.LayerGap.Add(Number(top - bottom));
			}

			foreach (var layer in layers)
			{
				var sorted = layer.Select(id => elk.Boxes[id]).OrderBy(b => b.X).ToList();
				for (var i = 1; i < sorted.Count; i++)
				{
					run.NodeGap.Add(Number(sorted[i].X - (sorted[i - 1].X + sorted[i - 1].Width)));
				}
			}

			if (graph.Leaves.Count > 0)
			{
				run.Margin.Add(Number(graph.Leaves.Min(id => elk.Boxes[id].X)) + " " + Number(graph.Leaves.Min(id => elk.Boxes[id].Y)));
			}
		}

		var path = Path.Combine(corpus, name + ".dagre.txt");
		var options = File.Exists(path) && DagreRecords.Read(path) is [var first, ..] ? first.Input.Options : DagreOnElkInput.FlowchartDefaults;
		var dagre = DagreOnElkInput.Layout(graph, options with { NodeSep = 40, RankSep = 40, MarginX = 12, MarginY = 12 });
		var held = ElkLayoutComparer.Score(graph, elk, dagre);
		yield return (Spikes[6], held.SameLayers && held.SameOrder && graph.Leaves.Count > 0 ? held.BoxesOverlap : null);

		// Edge routing, with ELK's node boxes and route ends: a route between two ends that line up across the layout
		// direction is straight, and any other has one jog of two bends.
		var routed = 0;
		var routing = true;
		var loops = 0;
		var loopsPass = true;
		foreach (var edge in graph.Edges)
		{
			if (!elk.Edges.TryGetValue(edge.Id, out var points) || points.Count < 2)
			{
				continue;
			}

			var bends = ElkLayoutComparer.Bends(points);
			if (edge.Source == edge.Target)
			{
				run.SelfLoopBends.Add(bends.ToString(CultureInfo.InvariantCulture));
				loops++;
				loopsPass &= bends == 2;
				continue;
			}

			run.EdgeBends.Add(bends.ToString(CultureInfo.InvariantCulture));
			routed++;
			var across = graph.Direction is "RIGHT" or "LEFT" ? Math.Abs(points[0].Y - points[^1].Y) : Math.Abs(points[0].X - points[^1].X);
			routing &= bends == (across <= Epsilon ? 0 : 2);
		}

		yield return (Spikes[7], routed > 0 ? routing : null);
		yield return (Spikes[8], loops > 0 ? loopsPass : null);
		yield return (Spikes[9], Labels(record, graph, elk));
		yield return (Spikes[10], Nested(record, graph, elk));
		yield return (Spikes[11], Components(graph, elk, model));
		yield return (Spikes[12], Ports(graph, elk, layerOf, edges));
	}

	/// <summary>
	/// Each route end on a leaf against ELK's, with ELK's node boxes held still. The k ends on the side of a node that
	/// faces a later or an earlier layer are spaced (w - 20) / (k + 1) apart around the side's centre, where w is the
	/// side's length, in the order of the other ends across the layout direction. The rule is the probe's, from
	/// <c>Fixtures/elk-probes/ports.txt</c>.
	/// </summary>
	private static bool? Ports(ElkInputGraph graph, CandidateLayout elk, Dictionary<string, int> layerOf, List<ElkInputEdge> edges)
	{
		var horizontal = graph.Direction is "RIGHT" or "LEFT";
		double Centre(LayoutBox b) => horizontal ? b.Y + b.Height / 2 : b.X + b.Width / 2;
		double Length(LayoutBox b) => horizontal ? b.Height : b.Width;
		var ends = new List<(string Node, bool Later, string Other, int Edge, double Actual)>();
		for (var i = 0; i < edges.Count; i++)
		{
			var edge = edges[i];
			if (edge.SourceCluster is not null || edge.TargetCluster is not null || layerOf[edge.Source] == layerOf[edge.Target] || !elk.Edges.TryGetValue(edge.Id, out var points) || points.Count < 2)
			{
				continue;
			}

			var forward = layerOf[edge.Source] < layerOf[edge.Target];
			ends.Add((edge.Source, forward, edge.Target, i, horizontal ? points[0].Y : points[0].X));
			ends.Add((edge.Target, !forward, edge.Source, i, horizontal ? points[^1].Y : points[^1].X));
		}

		if (ends.Count == 0)
		{
			return null;
		}

		foreach (var side in ends.GroupBy(e => (e.Node, e.Later)))
		{
			var box = elk.Boxes[side.Key.Node];
			var sorted = side.OrderBy(e => Centre(elk.Boxes[e.Other])).ThenBy(e => e.Edge).ToList();
			var gap = (Length(box) - 20) / (sorted.Count + 1);
			for (var i = 0; i < sorted.Count; i++)
			{
				var expected = Centre(box) + ((i - ((sorted.Count - 1) / 2.0)) * gap);
				if (Math.Abs(expected - sorted[i].Actual) > 0.5)
				{
					return false;
				}
			}
		}

		return true;
	}

	/// <summary>The edges a depth-first search reverses: the edges to a node on the search's stack.</summary>
	private static HashSet<string> DepthFirstBackEdges(IReadOnlyList<string> leaves, List<ElkInputEdge> edges, bool sourcesFirst)
	{
		var outgoing = leaves.ToDictionary(id => id, _ => new List<ElkInputEdge>(), StringComparer.Ordinal);
		var incoming = leaves.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);
		foreach (var edge in edges)
		{
			outgoing[edge.Source].Add(edge);
			incoming[edge.Target]++;
		}

		var state = new Dictionary<string, int>(StringComparer.Ordinal);
		var reversed = new HashSet<string>(StringComparer.Ordinal);

		void Visit(string v)
		{
			state[v] = 1;
			foreach (var edge in outgoing[v])
			{
				switch (state.GetValueOrDefault(edge.Target))
				{
					case 0:
						Visit(edge.Target);
						break;
					case 1:
						reversed.Add(edge.Id);
						break;
				}
			}

			state[v] = 2;
		}

		var roots = sourcesFirst ? leaves.Where(id => incoming[id] == 0).Concat(leaves.Where(id => incoming[id] > 0)) : leaves;
		foreach (var root in roots)
		{
			if (!state.ContainsKey(root))
			{
				Visit(root);
			}
		}

		return reversed;
	}

	/// <summary>
	/// Layer sweeps with the barycenter heuristic of Sugiyama, Tagawa and Toda (1981): long edges get a dummy node in
	/// each layer they cross, each layer starts in model order, and a node without neighbours in the layer before keeps
	/// its place. The order with the fewest crossings wins, the earlier one on a tie.
	/// </summary>
	private static List<IReadOnlyList<string>> Barycenter(
		IReadOnlyList<IReadOnlyList<string>> layers,
		Dictionary<string, int> layerOf,
		Dictionary<string, int> model,
		List<(string Id, string V, string W)> edges)
	{
		var order = layers.Select(l => l.OrderBy(id => model[id]).ToList()).ToList();
		var links = new List<(string V, string W)>();
		foreach (var (id, v, w) in edges)
		{
			var previous = v;
			for (var layer = layerOf[v] + 1; layer < layerOf[w]; layer++)
			{
				var dummy = "\0" + id + "\0" + layer.ToString(CultureInfo.InvariantCulture);
				order[layer].Add(dummy);
				links.Add((previous, dummy));
				previous = dummy;
			}

			links.Add((previous, w));
		}

		var up = links.GroupBy(l => l.W, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Select(l => l.V).ToList(), StringComparer.Ordinal);
		var down = links.GroupBy(l => l.V, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Select(l => l.W).ToList(), StringComparer.Ordinal);

		var best = order.Select(l => l.ToList()).ToList();
		var bestCrossings = Crossings(best, links);
		for (var sweep = 0; sweep < 8 && bestCrossings > 0; sweep++)
		{
			var downward = sweep % 2 == 0;
			var range = downward ? Enumerable.Range(1, order.Count - 1) : Enumerable.Range(0, order.Count - 1).Reverse();
			foreach (var layer in range)
			{
				var fixedLayer = order[downward ? layer - 1 : layer + 1];
				var position = fixedLayer.Select((id, i) => (id, i)).ToDictionary(p => p.id, p => (double)p.i, StringComparer.Ordinal);
				var neighbours = downward ? up : down;
				var current = order[layer];
				var keys = current.Select((id, i) =>
				{
					var adjacent = neighbours.TryGetValue(id, out var list) ? list.Where(position.ContainsKey).ToList() : [];
					return (id, Key: adjacent.Count > 0 ? adjacent.Average(n => position[n]) : i, Index: i);
				}).ToList();
				order[layer] = [.. keys.OrderBy(k => k.Key).ThenBy(k => k.Index).Select(k => k.id)];
			}

			var crossings = Crossings(order, links);
			if (crossings < bestCrossings)
			{
				bestCrossings = crossings;
				best = order.Select(l => l.ToList()).ToList();
			}
		}

		return [.. best.Select(l => (IReadOnlyList<string>)[.. l.Where(id => id[0] != '\0')])];
	}

	private static int Crossings(List<List<string>> order, List<(string V, string W)> links)
	{
		var position = new Dictionary<string, (int Layer, int Index)>(StringComparer.Ordinal);
		for (var layer = 0; layer < order.Count; layer++)
		{
			for (var i = 0; i < order[layer].Count; i++)
			{
				position[order[layer][i]] = (layer, i);
			}
		}

		var crossings = 0;
		foreach (var group in links.GroupBy(l => position[l.V].Layer))
		{
			var list = group.Select(l => (V: position[l.V].Index, W: position[l.W].Index)).ToList();
			for (var i = 0; i < list.Count; i++)
			{
				for (var j = i + 1; j < list.Count; j++)
				{
					if ((list[i].V - list[j].V) * (list[i].W - list[j].W) < 0)
					{
						crossings++;
					}
				}
			}
		}

		return crossings;
	}

	/// <summary>
	/// Each centre edge label's box against ELK's, with ELK's route held still: the label is centred on the middle of
	/// the route's segment that crosses the midpoint between its ends along the layout direction.
	/// </summary>
	private static bool? Labels(ElkRecord record, ElkInputGraph graph, CandidateLayout elk)
	{
		var placements = record.Output.Labels.Where(l => l.OwnerKind == "edge" && l.Index == 0).ToDictionary(l => l.Owner, StringComparer.Ordinal);
		var centre = record.Input.Options
			.Where(o => o.OwnerKind == "edge-label" && o.Key == "edgeLabels.placement" && o.Value == "CENTER" && o.LabelIndex == 0)
			.Select(o => o.Owner)
			.ToHashSet(StringComparer.Ordinal);
		var places = record.Output.Places.ToDictionary(p => p.Id, StringComparer.Ordinal);
		var horizontal = graph.Direction is "RIGHT" or "LEFT";
		bool? result = null;
		foreach (var edge in graph.Edges)
		{
			if (edge.LabelWidth <= 0 || edge.Source == edge.Target || !centre.Contains(edge.Id) || !placements.TryGetValue(edge.Id, out var placed) || !elk.Edges.TryGetValue(edge.Id, out var points) || points.Count < 2)
			{
				continue;
			}

			var route = record.Output.Routes.First(r => r.Id == edge.Id);
			var offset = route.ElkContainer is { } container && places.ContainsKey(container) ? Origin(places, container) : (0, 0);
			var expected = new LayoutBox(placed.X + offset.X, placed.Y + offset.Y, placed.Width, placed.Height);

			double Along((double X, double Y) p) => horizontal ? p.X : p.Y;
			var middle = (Along(points[0]) + Along(points[^1])) / 2;
			var (cx, cy) = ((points[0].X + points[^1].X) / 2, (points[0].Y + points[^1].Y) / 2);
			for (var i = 1; i < points.Count; i++)
			{
				var (a, b) = (points[i - 1], points[i]);
				if ((Along(a) - middle) * (Along(b) - middle) <= 0)
				{
					(cx, cy) = ((a.X + b.X) / 2, (a.Y + b.Y) / 2);
					break;
				}
			}

			var box = new LayoutBox(cx - edge.LabelWidth / 2, cy - edge.LabelHeight / 2, edge.LabelWidth, edge.LabelHeight);
			result = (result ?? true) && ElkLayoutComparer.Overlap(expected, box) >= ElkLayoutComparer.MinOverlap;
		}

		return result;
	}

	/// <summary>
	/// Each cluster's box against ELK's, with its children's boxes held still: the box of its children, grown by its
	/// <c>elk.padding</c> and to its <c>nodeSize.minimum</c> width.
	/// </summary>
	private static bool? Nested(ElkRecord record, ElkInputGraph graph, CandidateLayout elk)
	{
		if (graph.Clusters.Count == 0)
		{
			return null;
		}

		var options = record.Input.Options.Where(o => o.OwnerKind == "node" && !o.IsProperty).ToLookup(o => o.Owner, StringComparer.Ordinal);
		foreach (var cluster in graph.Clusters)
		{
			if (!elk.Boxes.TryGetValue(cluster, out var expected))
			{
				continue;
			}

			var children = graph.Children[cluster].Where(elk.Boxes.ContainsKey).Select(id => elk.Boxes[id]).ToList();
			var (top, left, bottom, right) = (12.0, 12.0, 12.0, 12.0);
			if (options[cluster].FirstOrDefault(o => o.Key == "elk.padding") is { } padding)
			{
				foreach (var part in padding.Value.Trim('[', ']').Split(','))
				{
					var pair = part.Split('=');
					var value = double.Parse(pair[1], CultureInfo.InvariantCulture);
					switch (pair[0].Trim())
					{
						case "top": top = value; break;
						case "left": left = value; break;
						case "bottom": bottom = value; break;
						case "right": right = value; break;
					}
				}
			}

			var x0 = children.Min(b => b.X) - left;
			var y0 = children.Min(b => b.Y) - top;
			var x1 = children.Max(b => b.X + b.Width) + right;
			var y1 = children.Max(b => b.Y + b.Height) + bottom;
			if (options[cluster].FirstOrDefault(o => o.Key == "nodeSize.minimum") is { } minimum)
			{
				var width = double.Parse(minimum.Value.Trim('(', ')').Split(',')[0], CultureInfo.InvariantCulture);
				if (x1 - x0 < width)
				{
					var grow = (width - (x1 - x0)) / 2;
					(x0, x1) = (x0 - grow, x1 + grow);
				}
			}

			if (ElkLayoutComparer.Overlap(expected, new LayoutBox(x0, y0, x1 - x0, y1 - y0)) < ElkLayoutComparer.MinOverlap)
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Whether the root's connected components follow each other across the layout direction in the model order of
	/// their first node. Null when the root has one component.
	/// </summary>
	private static bool? Components(ElkInputGraph graph, CandidateLayout elk, Dictionary<string, int> model)
	{
		var top = graph.Children[graph.Root];
		if (top.Count < 2)
		{
			return null;
		}

		string TopOf(string id)
		{
			while (graph.Parents.TryGetValue(id, out var parent) && parent != graph.Root)
			{
				id = parent;
			}

			return id;
		}

		var parentOf = top.ToDictionary(id => id, id => id, StringComparer.Ordinal);
		string Find(string id) => parentOf[id] == id ? id : parentOf[id] = Find(parentOf[id]);
		foreach (var edge in graph.Edges)
		{
			var (a, b) = (Find(TopOf(edge.Source)), Find(TopOf(edge.Target)));
			if (a != b)
			{
				parentOf[a] = b;
			}
		}

		var components = top.GroupBy(Find, StringComparer.Ordinal).Select(g => g.ToList()).ToList();
		if (components.Count < 2 || components.Any(c => c.Any(id => !elk.Boxes.ContainsKey(id))))
		{
			return null;
		}

		var horizontal = graph.Direction is "RIGHT" or "LEFT";
		var byModel = components.OrderBy(c => top.IndexOf(c[0])).ToList();
		var byPlace = components.OrderBy(c => c.Min(id => horizontal ? elk.Boxes[id].Y : elk.Boxes[id].X)).ThenBy(c => c.Min(id => horizontal ? elk.Boxes[id].X : elk.Boxes[id].Y)).ToList();
		return byModel.SequenceEqual(byPlace);
	}

	private static (double X, double Y) Origin(Dictionary<string, ElkPlace> places, string id)
	{
		var place = places[id];
		var parent = place.Parent is { } p && places.ContainsKey(p) ? Origin(places, p) : (0, 0);
		return (parent.X + place.X, parent.Y + place.Y);
	}

	private static string Number(double value) => Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);

	public static string Report(Run run)
	{
		var text = new StringBuilder();
		text.Append("# ELK stage spikes\n\n");
		text.Append("| Spike | Type | Applies | Passes | Share | Flat | Nested |\n|---|---|---:|---:|---:|---:|---:|\n");
		foreach (var spike in Spikes)
		{
			var outcomes = run.Outcomes.Where(o => o.Spike == spike && o.Passed is not null).ToList();
			foreach (var type in outcomes.Select(o => o.Type).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Append(null))
			{
				var cases = outcomes.Where(o => type is null || o.Type == type).ToList();
				text.Append(CultureInfo.InvariantCulture, $"| {spike} | {type ?? "**all**"} | {cases.Count} | {cases.Count(o => o.Passed == true)} | {Share(cases)} | {Share([.. cases.Where(o => !o.Nested)])} | {Share([.. cases.Where(o => o.Nested)])} |\n");
			}
		}

		foreach (var (title, histogram) in new[]
		{
			("Gap between layers, flat graphs laid out down", run.LayerGap),
			("Gap between neighbours in a layer, flat graphs laid out down", run.NodeGap),
			("Least x and y of a leaf, flat graphs laid out down", run.Margin),
			("Bends of a self loop", run.SelfLoopBends),
			("Bends of an edge that is not a self loop", run.EdgeBends),
		})
		{
			text.Append(CultureInfo.InvariantCulture, $"\n## {title}\n\n| Value | Count |\n|---|---:|\n");
			foreach (var (value, count) in histogram.Top(8))
			{
				text.Append(CultureInfo.InvariantCulture, $"| {value} | {count} |\n");
			}

			text.Append(CultureInfo.InvariantCulture, $"| **total** | {histogram.Total} |\n");
		}

		return text.ToString();
	}

	private static string Share(List<Outcome> cases) =>
		cases.Count == 0 ? "n/a" : string.Create(CultureInfo.InvariantCulture, $"{100.0 * cases.Count(o => o.Passed == true) / cases.Count:0.0}%");
}
