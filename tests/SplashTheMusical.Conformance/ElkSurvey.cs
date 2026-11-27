using System.Globalization;
using System.Text;

namespace SplashTheMusical.Conformance;

/// <summary>The ELK calls of one corpus case, read from its <c>.elk.txt</c> file.</summary>
/// <param name="Name">The case's path relative to the corpus folder, without the extension.</param>
/// <param name="Type">The case's folder, its diagram type.</param>
internal sealed record ElkSurveyCase(string Name, string Type, IReadOnlyList<ElkRecord> Records);

/// <summary>
/// What the graphs mermaid.js gives ELK in one case hold. A case with more than one call adds the counts of its calls
/// and has a feature when any call has it.
/// </summary>
/// <param name="Nodes">The nodes other than the root.</param>
/// <param name="Containers">The nodes that have children.</param>
/// <param name="Depth">The deepest node's depth, where a child of the root is at 1.</param>
/// <param name="Components">The weakly connected components of the nodes, where an edge and a child's parent connect two nodes.</param>
internal sealed record ElkCaseShape(
	int Calls,
	int Nodes,
	int Edges,
	int Containers,
	int Depth,
	int Components,
	int Ports,
	int SelfLoops,
	int LabelledEdges,
	int NodeLabels,
	int ParallelEdges,
	int ReverseEdgePairs,
	int Hyperedges,
	int CrossHierarchyEdges,
	int ContainerEdges,
	int NestedEdges,
	bool HasCycle);

/// <summary>
/// The survey of docs/layered-layout/corpus-survey.md: which ELK options and graph features the recorded cases use,
/// counted from the committed <c>.elk.txt</c> files.
/// </summary>
internal static class ElkSurvey
{
	/// <summary>The environment variable that rewrites the committed survey when it is set to any value.</summary>
	public const string WriteVariable = "STM_WRITE_SURVEY";

	/// <summary>An owner's key with more distinct values than this gets one row instead of one per value.</summary>
	private const int MaxValueRows = 12;

	private const int Largest = 20;

	private static readonly string[] OwnerOrder = ["root", "container", "node", "port", "edge", "node-label", "port-label", "edge-label"];

	private static readonly (int Low, int High)[] Buckets = [(0, 0), (1, 5), (6, 10), (11, 20), (21, 50), (51, 100), (101, int.MaxValue)];

	public static string FilePath => Path.Combine(CorpusCheck.RepositoryRoot, "docs", "layered-layout", "corpus-survey.md");

	/// <summary>Every case of the corpus that has an ELK file, in ordinal order.</summary>
	public static IReadOnlyList<ElkSurveyCase> Read(string corpus) =>
		[.. ElkRecords.Files(corpus).Select(file =>
		{
			var name = file[..^".elk.txt".Length];
			var slash = name.IndexOf('/', StringComparison.Ordinal);
			return new ElkSurveyCase(name, slash < 0 ? "" : name[..slash], ElkRecords.Read(Path.Combine(corpus, file)));
		})];

	public static ElkCaseShape Shape(IReadOnlyList<ElkRecord> records)
	{
		var shape = new ElkCaseShape(records.Count, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, false);
		foreach (var record in records)
		{
			var one = Shape(record.Input);
			shape = new ElkCaseShape(
				shape.Calls,
				shape.Nodes + one.Nodes,
				shape.Edges + one.Edges,
				shape.Containers + one.Containers,
				Math.Max(shape.Depth, one.Depth),
				shape.Components + one.Components,
				shape.Ports + one.Ports,
				shape.SelfLoops + one.SelfLoops,
				shape.LabelledEdges + one.LabelledEdges,
				shape.NodeLabels + one.NodeLabels,
				shape.ParallelEdges + one.ParallelEdges,
				shape.ReverseEdgePairs + one.ReverseEdgePairs,
				shape.Hyperedges + one.Hyperedges,
				shape.CrossHierarchyEdges + one.CrossHierarchyEdges,
				shape.ContainerEdges + one.ContainerEdges,
				shape.NestedEdges + one.NestedEdges,
				shape.HasCycle || one.HasCycle);
		}

		return shape;
	}

	private static ElkCaseShape Shape(ElkInput input)
	{
		var root = input.Nodes[0].Id;
		var parent = new Dictionary<string, string?>(StringComparer.Ordinal);
		var depth = new Dictionary<string, int>(StringComparer.Ordinal);
		foreach (var node in input.Nodes)
		{
			parent[node.Id] = node.Parent;
			depth[node.Id] = node.Parent is { } p && depth.TryGetValue(p, out var d) ? d + 1 : 0;
		}

		var containers = input.Nodes.Where(n => n.Parent is not null && n.Parent != root).Select(n => n.Parent!).ToHashSet(StringComparer.Ordinal);
		var portNode = input.Ports.ToDictionary(p => p.Id, p => p.Node, StringComparer.Ordinal);
		string End(string id) => portNode.TryGetValue(id, out var node) ? node : id;

		var ids = input.Nodes.Skip(1).Select(n => n.Id).ToList();
		var index = ids.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i, StringComparer.Ordinal);
		var components = new UnionFind(ids.Count);
		foreach (var node in input.Nodes.Skip(1))
		{
			if (node.Parent is { } p && index.TryGetValue(p, out var pi))
			{
				components.Union(index[node.Id], pi);
			}
		}

		var labelled = input.Labels.Where(l => l is { OwnerKind: "edge", Width: > 0, Height: > 0 }).Select(l => l.Owner).ToHashSet(StringComparer.Ordinal);
		var successors = ids.Select(_ => new List<int>()).ToList();
		var pairs = new Dictionary<(string, string), int>();
		int selfLoops = 0, hyperedges = 0, crossHierarchy = 0, containerEdges = 0, nested = 0;
		foreach (var edge in input.Edges)
		{
			var sources = edge.Sources.Select(End).ToList();
			var targets = edge.Targets.Select(End).ToList();
			if (sources.Count > 1 || targets.Count > 1)
			{
				hyperedges++;
			}

			if (edge.Container != root)
			{
				nested++;
			}

			var ends = sources.Concat(targets).ToList();
			if (ends.Any(containers.Contains))
			{
				containerEdges++;
			}

			if (ends.Select(e => parent.GetValueOrDefault(e)).Distinct(StringComparer.Ordinal).Skip(1).Any())
			{
				crossHierarchy++;
			}

			if (sources.Any(targets.Contains))
			{
				selfLoops++;
			}

			foreach (var source in sources)
			{
				foreach (var target in targets)
				{
					pairs[(source, target)] = pairs.GetValueOrDefault((source, target)) + 1;
					if (index.TryGetValue(source, out var s) && index.TryGetValue(target, out var t))
					{
						components.Union(s, t);
						if (s != t)
						{
							successors[s].Add(t);
						}
					}
				}
			}
		}

		return new ElkCaseShape(
			1,
			ids.Count,
			input.Edges.Count,
			containers.Count,
			ids.Count == 0 ? 0 : ids.Max(id => depth[id]),
			components.Count,
			input.Ports.Count,
			selfLoops,
			input.Edges.Count(e => labelled.Contains(e.Id)),
			input.Labels.Count(l => l.OwnerKind == "node"),
			pairs.Values.Where(n => n > 1).Sum(n => n - 1),
			pairs.Keys.Count(k => string.CompareOrdinal(k.Item1, k.Item2) < 0 && pairs.ContainsKey((k.Item2, k.Item1))),
			hyperedges,
			crossHierarchy,
			containerEdges,
			nested,
			HasCycle(successors));
	}

	/// <summary>Whether the directed graph has a cycle, found by an iterative depth-first search.</summary>
	private static bool HasCycle(List<List<int>> successors)
	{
		var state = new byte[successors.Count];
		var stack = new Stack<(int Node, int Next)>();
		for (var start = 0; start < successors.Count; start++)
		{
			if (state[start] != 0)
			{
				continue;
			}

			state[start] = 1;
			stack.Push((start, 0));
			while (stack.Count > 0)
			{
				var (node, next) = stack.Pop();
				if (next == successors[node].Count)
				{
					state[node] = 2;
					continue;
				}

				stack.Push((node, next + 1));
				var successor = successors[node][next];
				if (state[successor] == 1)
				{
					return true;
				}

				if (state[successor] == 0)
				{
					state[successor] = 1;
					stack.Push((successor, 0));
				}
			}
		}

		return false;
	}

	public static string Render(IReadOnlyList<ElkSurveyCase> cases, string mermaid)
	{
		var types = cases.Select(c => c.Type).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
		var shapes = cases.ToDictionary(c => c.Name, c => Shape(c.Records), StringComparer.Ordinal);
		var text = new StringBuilder();
		text.Append("# What the recorded cases ask of ELK\n\n");
		text.Append(
			CultureInfo.InvariantCulture,
			$"The graphs mermaid.js {mermaid} gives ELK in the {Count(cases.Count)} corpus cases that have an `.elk.txt` file, with\n"
		);
		text.Append(
			CultureInfo.InvariantCulture,
			$"{Count(cases.Sum(c => c.Records.Count))} ELK calls between them. `mise run elk-survey` writes this file from those files alone, and\n"
		);
		text.Append("`ElkSurveyTests` in `tests/SplashTheMusical.Conformance` fails when it is out of date. The section \"ELK files\" of\n");
		text.Append("[the oracle README](../../tools/oracle/README.md) describes the recordings. The survey scopes the layered layout\n");
		text.Append("engine of ipjohnson/SplashTheMusical#25 under [the clean-room rules](clean-room.md).\n\n");
		text.Append("A count of cases counts a case once however many of its calls, nodes or edges qualify. A case with more than one\n");
		text.Append("call adds the nodes and edges of its calls.\n\n");

		text.Append("## Cases\n\n");
		text.Append("| Type | Cases | ELK calls |\n|---|---:|---:|\n");
		foreach (var type in types)
		{
			var of = cases.Where(c => c.Type == type).ToList();
			text.Append(CultureInfo.InvariantCulture, $"| {type} | {Count(of.Count)} | {Count(of.Sum(c => c.Records.Count))} |\n");
		}

		text.Append(CultureInfo.InvariantCulture, $"| **Total** | {Count(cases.Count)} | {Count(cases.Sum(c => c.Records.Count))} |\n\n");

		Algorithms(text, cases);
		Options(text, cases, types);
		Features(text, cases, types, shapes);
		Sizes(text, cases, types, shapes);
		LargestCases(text, cases, shapes);
		Notes(text, mermaid);
		return text.ToString();
	}

	private static void Algorithms(StringBuilder text, IReadOnlyList<ElkSurveyCase> cases)
	{
		text.Append("## Algorithms\n\n");
		text.Append("The cases whose root, or one of whose containers, sets `elk.algorithm` to each value. A root that does not set it\n");
		text.Append("is counted under \"not set\".\n\n");
		text.Append("| Algorithm | Cases, root | Cases, container |\n|---|---:|---:|\n");
		var root = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
		var container = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
		foreach (var c in cases)
		{
			foreach (var record in c.Records)
			{
				var rootId = record.Input.Nodes[0].Id;
				var rootAlgorithm = "not set";
				foreach (var option in record.Input.Options.Where(o => o is { IsProperty: false, OwnerKind: "node", Key: "elk.algorithm" }))
				{
					if (option.Owner == rootId)
					{
						rootAlgorithm = option.Value;
					}
					else
					{
						Add(container, option.Value, c.Name);
					}
				}

				Add(root, rootAlgorithm, c.Name);
			}
		}

		foreach (var algorithm in root.Keys.Concat(container.Keys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
		{
			text.Append(
				CultureInfo.InvariantCulture,
				$"| `{algorithm}` | {Count(root.GetValueOrDefault(algorithm)?.Count ?? 0)} | {Count(container.GetValueOrDefault(algorithm)?.Count ?? 0)} |\n"
			);
		}

		text.Append('\n');
	}

	private static void Options(StringBuilder text, IReadOnlyList<ElkSurveyCase> cases, IReadOnlyList<string> types)
	{
		text.Append("## Layout options\n\n");
		text.Append("Every `layoutOptions` key mermaid.js sets, by the kind of its owner, with the cases that set each value and the\n");
		text.Append("owners that carry it. A root is the graph's top node, a container a node with children, and a node one without.\n");
		text.Append(
			CultureInfo.InvariantCulture,
			$"A key with more than {MaxValueRows} values on one kind of owner gets one row that counts its distinct values.\n"
		);
		if (!cases.SelectMany(c => c.Records).SelectMany(r => r.Input.Options).Any(o => o.IsProperty))
		{
			text.Append("No case sets a key of `properties`.\n");
		}

		text.Append('\n');

		var options = new Dictionary<(string Owner, string Key), Dictionary<string, OptionUse>>();
		foreach (var c in cases)
		{
			foreach (var record in c.Records)
			{
				var rootId = record.Input.Nodes[0].Id;
				var containers = record.Input.Nodes.Where(n => n.Parent is not null).Select(n => n.Parent!).ToHashSet(StringComparer.Ordinal);
				foreach (var option in record.Input.Options)
				{
					var owner = option.OwnerKind != "node" ? option.OwnerKind : option.Owner == rootId ? "root" : containers.Contains(option.Owner) ? "container" : "node";
					var key = (owner, option.IsProperty ? "properties." + option.Key : option.Key);
					if (!options.TryGetValue(key, out var values))
					{
						options[key] = values = new Dictionary<string, OptionUse>(StringComparer.Ordinal);
					}

					if (!values.TryGetValue(option.Value, out var use))
					{
						values[option.Value] = use = new OptionUse();
					}

					use.Owners++;
					use.Cases.Add(c.Name);
				}
			}
		}

		var typeOf = cases.ToDictionary(c => c.Name, c => c.Type, StringComparer.Ordinal);
		text.Append("| Owner | Key | Value | Cases | Owners |");
		text.AppendJoin("", types.Select(t => $" {t} |"));
		text.Append("\n|---|---|---|---:|---:|");
		text.AppendJoin("", types.Select(_ => "---:|"));
		text.Append('\n');
		foreach (var ((owner, key), values) in options
			.OrderBy(o => Array.IndexOf(OwnerOrder, o.Key.Owner))
			.ThenBy(o => o.Key.Key, StringComparer.Ordinal))
		{
			IEnumerable<(string Value, HashSet<string> Cases, int Owners)> rows = values.Count > MaxValueRows
				? [($"{Count(values.Count)} distinct values", values.Values.SelectMany(v => v.Cases).ToHashSet(StringComparer.Ordinal), values.Values.Sum(v => v.Owners))]
				: values.OrderBy(v => v.Key, StringComparer.Ordinal).Select(v => ($"`{Cell(v.Key)}`", v.Value.Cases, v.Value.Owners));
			foreach (var (value, used, owners) in rows)
			{
				text.Append(CultureInfo.InvariantCulture, $"| {owner} | `{key}` | {value} | {Count(used.Count)} | {Count(owners)} |");
				text.AppendJoin("", types.Select(t => $" {Count(used.Count(n => typeOf[n] == t))} |"));
				text.Append('\n');
			}
		}

		text.Append('\n');
	}

	private static void Features(StringBuilder text, IReadOnlyList<ElkSurveyCase> cases, IReadOnlyList<string> types, Dictionary<string, ElkCaseShape> shapes)
	{
		text.Append("## Graph features\n\n");
		text.Append("The cases that have each feature, and the edges or nodes that have it across all cases. The ends of an edge are\n");
		text.Append("its nodes, with a port taken as its node.\n\n");
		text.Append("- A nested graph is a node with children.\n");
		text.Append("- An edge label is a label with a width and a height. mermaid.js gives most edges without a label an empty one.\n");
		text.Append("- A self loop is an edge with the same node at both ends.\n");
		text.Append("- A parallel edge is an edge whose source and target an earlier edge already joins in the same direction.\n");
		text.Append("- A reverse pair is two nodes joined by edges in both directions.\n");
		text.Append("- A cycle is a directed cycle of two or more nodes. Self loops do not make one.\n");
		text.Append("- The components are the weakly connected components of the nodes other than the root, where an edge joins its\n");
		text.Append("  ends and a child joins its container. A graph with more than one is disconnected.\n");
		text.Append("- A hierarchy-crossing edge has ends with different parents.\n\n");

		(string Name, Func<ElkCaseShape, int> Measure, string Unit)[] features =
		[
			("Nested graphs", s => s.Containers, "containers"),
			("Nesting depth 3 or more", s => s.Depth >= 3 ? 1 : 0, ""),
			("Ports", s => s.Ports, "ports"),
			("Self loops", s => s.SelfLoops, "edges"),
			("Edge labels", s => s.LabelledEdges, "edges"),
			("Node labels", s => s.NodeLabels, "labels"),
			("Parallel edges", s => s.ParallelEdges, "edges"),
			("Reverse pairs", s => s.ReverseEdgePairs, "pairs"),
			("Cycles", s => s.HasCycle ? 1 : 0, ""),
			("Disconnected components", s => s.Components > 1 ? 1 : 0, ""),
			("Hyperedges, more than one source or target", s => s.Hyperedges, "edges"),
			("Hierarchy-crossing edges", s => s.CrossHierarchyEdges, "edges"),
			("Edges that end on a container", s => s.ContainerEdges, "edges"),
			("Edges held by a container", s => s.NestedEdges, "edges"),
		];

		text.Append("| Feature | Cases | Share | Count |");
		text.AppendJoin("", types.Select(t => $" {t} |"));
		text.Append("\n|---|---:|---:|---:|");
		text.AppendJoin("", types.Select(_ => "---:|"));
		text.Append('\n');
		foreach (var (name, measure, unit) in features)
		{
			var with = cases.Where(c => measure(shapes[c.Name]) > 0).ToList();
			var total = unit.Length == 0 ? "" : $"{Count(cases.Sum(c => measure(shapes[c.Name])))} {unit}";
			text.Append(CultureInfo.InvariantCulture, $"| {name} | {Count(with.Count)} | {Share(with.Count, cases.Count)} | {total} |");
			text.AppendJoin("", types.Select(t => $" {Count(with.Count(c => c.Type == t))} |"));
			text.Append('\n');
		}

		text.Append("\nThe cases by nesting depth, where 1 is a graph without containers and 0 a graph without nodes.\n\n");
		var depths = cases.Select(c => shapes[c.Name].Depth).Distinct().Order().ToList();
		CountTable(text, "Depth", depths.Select(d => (d.ToString(CultureInfo.InvariantCulture), (Func<ElkCaseShape, bool>)(s => s.Depth == d))), cases, types, shapes);

		text.Append("\nThe cases by number of components.\n\n");
		CountTable(text, "Components", Buckets.Select(b => (Range(b), (Func<ElkCaseShape, bool>)(s => s.Components >= b.Low && s.Components <= b.High))), cases, types, shapes);
		text.Append('\n');
	}

	private static void Sizes(StringBuilder text, IReadOnlyList<ElkSurveyCase> cases, IReadOnlyList<string> types, Dictionary<string, ElkCaseShape> shapes)
	{
		text.Append("## Graph sizes\n\n");
		text.Append("The nodes of a case are all its nodes other than the root, containers included. The median and the 90th\n");
		text.Append("percentile are nearest-rank.\n\n");
		foreach (var (title, measure) in new (string, Func<ElkCaseShape, int>)[] { ("Nodes", s => s.Nodes), ("Edges", s => s.Edges) })
		{
			text.Append(CultureInfo.InvariantCulture, $"### {title} per case\n\n");
			text.Append("| Type | Cases | Min | Median | 90th percentile | Max | Total |\n|---|---:|---:|---:|---:|---:|---:|\n");
			foreach (var (type, of) in types.Select(t => (t, cases.Where(c => c.Type == t).ToList())).Append(("**All**", [.. cases])))
			{
				var values = of.Select(c => measure(shapes[c.Name])).Order().ToList();
				text.Append(
					CultureInfo.InvariantCulture,
					$"| {type} | {Count(values.Count)} | {Count(values[0])} | {Count(Percentile(values, 50))} | {Count(Percentile(values, 90))} | {Count(values[^1])} | {Count(values.Sum())} |\n"
				);
			}

			text.Append('\n');
			CountTable(text, title, Buckets.Select(b => (Range(b), (Func<ElkCaseShape, bool>)(s => measure(s) >= b.Low && measure(s) <= b.High))), cases, types, shapes);
			text.Append('\n');
		}
	}

	private static void LargestCases(StringBuilder text, IReadOnlyList<ElkSurveyCase> cases, Dictionary<string, ElkCaseShape> shapes)
	{
		text.Append(CultureInfo.InvariantCulture, $"## The {Largest} largest cases\n\n");
		text.Append("By nodes and edges together, then by nodes, then by name.\n\n");
		text.Append("| # | Case | Nodes | Edges | Containers | Depth | Components | Edge labels |\n|---:|---|---:|---:|---:|---:|---:|---:|\n");
		var rank = 0;
		foreach (var c in cases
			.OrderByDescending(c => shapes[c.Name].Nodes + shapes[c.Name].Edges)
			.ThenByDescending(c => shapes[c.Name].Nodes)
			.ThenBy(c => c.Name, StringComparer.Ordinal)
			.Take(Largest))
		{
			var s = shapes[c.Name];
			text.Append(
				CultureInfo.InvariantCulture,
				$"| {++rank} | `{c.Name}` | {Count(s.Nodes)} | {Count(s.Edges)} | {Count(s.Containers)} | {s.Depth} | {Count(s.Components)} | {Count(s.LabelledEdges)} |\n"
			);
		}

		text.Append('\n');
	}

	private static void Notes(StringBuilder text, string mermaid)
	{
		text.Append("## Where mermaid.js sets the options\n\n");
		text.Append(
			CultureInfo.InvariantCulture,
			$"From `packages/mermaid/src/rendering-util/layout-algorithms/elk/render.ts` of mermaid-js/mermaid at `mermaid@{mermaid}`.\n\n"
		);
		text.Append("- The root gets one block of options. Its `elk.direction` comes from the diagram's direction, and `config.elk`\n");
		text.Append("  can override others, such as `elk.layered.nodePlacement.strategy` and `elk.layered.cycleBreaking.strategy`,\n");
		text.Append("  whose defaults come from `config.elk.preset`. A root laid out with `elk.rectpacking` also gets the rectpacking\n");
		text.Append("  options and spacings of 15.\n");
		text.Append("- A subgraph gets its own block of layered options, `elk.padding` of 24, `spacing.baseValue` of 24 and\n");
		text.Append("  `spacing.nodeNode` of 50. A subgraph whose frame paints a title adds `nodeSize.constraints` and\n");
		text.Append("  `nodeSize.minimum` of the title's width, which is why that key has so many values.\n");
		text.Append("- A container that asks for an algorithm with `@{ algorithm: ... }` gets it with `SEPARATE_CHILDREN`. When an edge\n");
		text.Append("  crosses its boundary, mermaid.js drops the algorithm and sets `INCLUDE_CHILDREN` on it and its ancestors.\n");
		text.Append("- `elk.layered.layering.layerConstraint` `FIRST` pins the entry node of a cyclic component when\n");
		text.Append("  `config.elk.keepEntryNodeOnTop` is set.\n");
		text.Append("- `elk.portAlignment.default` `CENTER` is set on a node whose edges are spread along its sides.\n");
		text.Append("- An edge's own label is `CENTER` and `inline`. A label at an edge's start is `TAIL` and one at its end is `HEAD`.\n");
	}

	private static void CountTable(
		StringBuilder text,
		string title,
		IEnumerable<(string Name, Func<ElkCaseShape, bool> Holds)> rows,
		IReadOnlyList<ElkSurveyCase> cases,
		IReadOnlyList<string> types,
		Dictionary<string, ElkCaseShape> shapes)
	{
		text.Append(CultureInfo.InvariantCulture, $"| {title} | Cases |");
		text.AppendJoin("", types.Select(t => $" {t} |"));
		text.Append("\n|---|---:|");
		text.AppendJoin("", types.Select(_ => "---:|"));
		text.Append('\n');
		foreach (var (name, holds) in rows)
		{
			var with = cases.Where(c => holds(shapes[c.Name])).ToList();
			if (with.Count == 0)
			{
				continue;
			}

			text.Append(CultureInfo.InvariantCulture, $"| {name} | {Count(with.Count)} |");
			text.AppendJoin("", types.Select(t => $" {Count(with.Count(c => c.Type == t))} |"));
			text.Append('\n');
		}
	}

	private static int Percentile(List<int> sorted, int percent) =>
		sorted[Math.Max(0, ((sorted.Count * percent) + 99) / 100 - 1)];

	private static string Range((int Low, int High) bucket) =>
		bucket.High == int.MaxValue ? $"{bucket.Low} or more"
		: bucket.Low == bucket.High ? bucket.Low.ToString(CultureInfo.InvariantCulture)
		: $"{bucket.Low} to {bucket.High}";

	private static void Add(Dictionary<string, HashSet<string>> sets, string key, string value)
	{
		if (!sets.TryGetValue(key, out var set))
		{
			sets[key] = set = new HashSet<string>(StringComparer.Ordinal);
		}

		set.Add(value);
	}

	private static string Cell(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);

	private static string Share(int count, int of) =>
		of == 0 ? "n/a" : (100.0 * count / of).ToString("0.0", CultureInfo.InvariantCulture) + "%";

	private static string Count(int count) => count.ToString("#,0", CultureInfo.InvariantCulture);

	private sealed class OptionUse
	{
		public HashSet<string> Cases { get; } = new(StringComparer.Ordinal);

		public int Owners { get; set; }
	}

	private sealed class UnionFind
	{
		private readonly int[] _parent;

		public UnionFind(int count)
		{
			_parent = new int[count];
			for (var i = 0; i < count; i++)
			{
				_parent[i] = i;
			}

			Count = count;
		}

		public int Count { get; private set; }

		public void Union(int a, int b)
		{
			a = Find(a);
			b = Find(b);
			if (a != b)
			{
				_parent[a] = b;
				Count--;
			}
		}

		private int Find(int a)
		{
			while (_parent[a] != a)
			{
				_parent[a] = _parent[_parent[a]];
				a = _parent[a];
			}

			return a;
		}
	}
}
