using System.Globalization;
using System.Text;
using SplashTheMusical.Conformance.Fuzzing;

namespace SplashTheMusical.Conformance;

internal sealed record ProbeNode(string Id, double Width, double Height);

/// <param name="LabelWidth">The width of the edge's label, 0 for an edge without text.</param>
internal sealed record ProbeEdge(string Id, string Source, string Target, double LabelWidth, double LabelHeight);

/// <summary>
/// A flat graph the differential run writes for <c>tools/oracle/elk-probe.mjs</c>. Its text sets no
/// <c>elk.algorithm</c>, so the probe gives it the options mermaid.js gives a flowchart.
/// </summary>
internal sealed record ProbeGraph(string Direction, IReadOnlyList<ProbeNode> Nodes, IReadOnlyList<ProbeEdge> Edges)
{
	/// <summary>The graph as the input lines of an <c>.elk.txt</c> file, which the probe reads as a graph file.</summary>
	public string Text()
	{
		var text = new StringBuilder("node\troot\n");
		if (Direction != "DOWN")
		{
			text.Append(CultureInfo.InvariantCulture, $"option\tnode\troot\t\telk.direction\t{Direction}\n");
		}

		foreach (var node in Nodes)
		{
			text.Append(CultureInfo.InvariantCulture, $"node\t{node.Id}\troot\t{node.Width}\t{node.Height}\n");
		}

		foreach (var edge in Edges)
		{
			text.Append(CultureInfo.InvariantCulture, $"edge\t{edge.Id}\troot\t{edge.Source}\t{edge.Target}\n");
			if (edge.LabelWidth > 0)
			{
				text.Append(CultureInfo.InvariantCulture, $"label\tedge\t{edge.Id}\t0\tlabel\t{edge.LabelWidth}\t{edge.LabelHeight}\n");
			}
		}

		return text.ToString();
	}

	/// <summary>The graph with only <paramref name="nodes"/> and the edges between them.</summary>
	public ProbeGraph WithNodes(List<ProbeNode> nodes)
	{
		var kept = nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
		return this with { Nodes = nodes, Edges = [.. Edges.Where(e => kept.Contains(e.Source) && kept.Contains(e.Target))] };
	}
}

/// <summary>A random graph on which an engine and ELK differ, and the smallest graph it shrank to that still differs.</summary>
/// <param name="Index">The graph's index in the run, which with the seed makes it again.</param>
/// <param name="Measure">The first of <see cref="ElkDifferential.Measures"/> the engine fails on the original graph.</param>
/// <param name="Failure">The comparer's failure on the smallest graph.</param>
/// <param name="Tests">The graphs the shrinking laid out.</param>
internal sealed record ElkFind(long Index, string Measure, string Failure, ProbeGraph Original, ProbeGraph Smallest, int Tests);

internal sealed record ElkDifferentialRun(long Seed, int Graphs, int Compared, int Refused, IReadOnlyList<ElkFind> Finds);

/// <summary>
/// The differential run of ipjohnson/SplashTheMusical#204: seeded random graphs go to ELK through the probe and to an
/// engine, <see cref="ElkLayoutComparer"/> scores the engine's layout against ELK's, and each graph they differ on is
/// shrunk with <see cref="Minimizer.DeltaDebug"/> to the smallest that fails the same measure.
/// </summary>
internal static class ElkDifferential
{
	/// <summary>The comparer's measures, in the order a find is named by.</summary>
	public static readonly IReadOnlyList<string> Measures = ["layers", "order", "boxes", "bends"];

	/// <summary>The measure of an engine that throws.</summary>
	public const string Error = "error";

	private static readonly double[] Widths = [60, 80, 100, 120, 152];

	private static readonly double[] Heights = [45, 54, 70];

	private static readonly double[] LabelWidths = [30, 50, 80];

	/// <summary>
	/// Graph <paramref name="index"/> of a run with <paramref name="seed"/>: 2 to 8 nodes of flowchart sizes, up to two
	/// more edges than nodes with an occasional self loop or label, laid out down or, one time in four, right.
	/// </summary>
	public static ProbeGraph Generate(long seed, long index)
	{
		var random = FuzzRandom.For(seed, index);
		var direction = random.Next(4) == 0 ? "RIGHT" : "DOWN";
		var count = random.Between(2, 8);
		var nodes = new List<ProbeNode>(count);
		for (var i = 0; i < count; i++)
		{
			nodes.Add(new ProbeNode("n" + i.ToString(CultureInfo.InvariantCulture), random.Pick(Widths), random.Pick(Heights)));
		}

		var edgeCount = random.Between(1, count + 2);
		var edges = new List<ProbeEdge>(edgeCount);
		for (var i = 0; i < edgeCount; i++)
		{
			var source = random.Next(count);
			var target = random.Next(count);
			if (source == target && random.Next(4) != 0)
			{
				target = (source + 1) % count;
			}

			var labelled = random.Next(4) == 0;
			edges.Add(new ProbeEdge(
				"e" + i.ToString(CultureInfo.InvariantCulture),
				nodes[source].Id,
				nodes[target].Id,
				labelled ? random.Pick(LabelWidths) : 0,
				labelled ? 24 : 0));
		}

		return new ProbeGraph(direction, nodes, edges);
	}

	/// <param name="elk">ELK's layout of a graph's text, or null when elkjs refuses it.</param>
	/// <param name="engine">The engine's layout of the input graph the probe gave ELK.</param>
	/// <param name="measures">The measures a find may fail. A graph that fails only others is not a find.</param>
	/// <param name="maxTests">The most graphs the shrinking of one find lays out.</param>
	public static ElkDifferentialRun Run(
		int graphs,
		long seed,
		Func<string, ElkRecord?> elk,
		Func<ElkInputGraph, CandidateLayout> engine,
		IReadOnlyCollection<string> measures,
		int maxTests)
	{
		var compared = 0;
		var refused = 0;
		var finds = new List<ElkFind>();
		for (long index = 0; index < graphs; index++)
		{
			var graph = Generate(seed, index);
			if (elk(graph.Text()) is not { } record)
			{
				refused++;
				continue;
			}

			compared++;
			if (Difference(record, engine, measures) is not { } difference)
			{
				continue;
			}

			var smallest = Shrink(graph, difference.Measure, elk, engine, maxTests, out var tests);
			var failure = elk(smallest.Text()) is { } last && Difference(last, engine, [difference.Measure]) is { } d ? d.Failure : difference.Failure;
			finds.Add(new ElkFind(index, difference.Measure, failure, graph, smallest, tests));
		}

		return new ElkDifferentialRun(seed, graphs, compared, refused, finds);
	}

	/// <summary>The first of <paramref name="measures"/> the engine fails on ELK's graph, with the comparer's text, or null.</summary>
	public static (string Measure, string Failure)? Difference(ElkRecord record, Func<ElkInputGraph, CandidateLayout> engine, IReadOnlyCollection<string> measures)
	{
		var graph = new ElkInputGraph(record.Input);
		CandidateLayout layout;
		try
		{
			layout = engine(graph);
		}
		catch (Exception e) when (e is not OutOfMemoryException)
		{
			return measures.Contains(Error) ? (Error, e.GetType().Name + ": " + e.Message) : null;
		}

		var score = ElkLayoutComparer.Score(graph, ElkLayoutComparer.ElkLayout(record, graph), layout);
		string?[] failures = [score.LayerFailure, score.OrderFailure, score.BoxFailure, score.BendFailure];
		for (var i = 0; i < Measures.Count; i++)
		{
			if (failures[i] is { } failure && measures.Contains(Measures[i]))
			{
				return (Measures[i], failure);
			}
		}

		return null;
	}

	/// <summary>
	/// The smallest graph that still fails <paramref name="measure"/>: edges, then nodes with their edges, then labels
	/// are removed, and a right layout is tried down, until a round removes nothing or the tests run out.
	/// </summary>
	public static ProbeGraph Shrink(
		ProbeGraph graph,
		string measure,
		Func<string, ElkRecord?> elk,
		Func<ElkInputGraph, CandidateLayout> engine,
		int maxTests,
		out int tests)
	{
		var used = 0;
		bool Keeps(ProbeGraph candidate) =>
			candidate.Nodes.Count > 0 && elk(candidate.Text()) is { } record && Difference(record, engine, [measure])?.Measure == measure;

		List<T> Reduce<T>(List<T> units, Func<List<T>, ProbeGraph> make)
		{
			units = Minimizer.DeltaDebug(units, u => Keeps(make(u)), maxTests - used, out var t);
			used += t;
			if (units.Count == 1 && used < maxTests)
			{
				used++;
				if (Keeps(make([])))
				{
					units = [];
				}
			}

			return units;
		}

		while (used < maxTests)
		{
			var before = graph.Text();
			var current = graph;
			var edges = Reduce([.. current.Edges], e => current with { Edges = e });
			graph = current = current with { Edges = edges };
			var nodes = Reduce([.. current.Nodes], current.WithNodes);
			graph = current = current.WithNodes(nodes);
			var labelled = Reduce([.. current.Edges.Where(e => e.LabelWidth > 0).Select(e => e.Id)], ids => Unlabelled(current, ids));
			graph = current = Unlabelled(current, labelled);
			if (current.Direction != "DOWN" && used < maxTests)
			{
				used++;
				if (Keeps(current with { Direction = "DOWN" }))
				{
					graph = current with { Direction = "DOWN" };
				}
			}

			if (graph.Text() == before)
			{
				break;
			}
		}

		tests = used;
		return graph;
	}

	/// <summary>The graph with a label only on the edges of <paramref name="labelled"/>.</summary>
	private static ProbeGraph Unlabelled(ProbeGraph graph, List<string> labelled) => graph with
	{
		Edges = [.. graph.Edges.Select(e => e.LabelWidth > 0 && !labelled.Contains(e.Id) ? e with { LabelWidth = 0, LabelHeight = 0 } : e)],
	};
}

/// <summary>Writes a differential run's report and the smallest graph of each find.</summary>
internal static class ElkDifferentialReport
{
	public const string ReportFile = "report.md";

	/// <summary>A find's graph file, relative to the run's folder.</summary>
	public static string FindFile(ElkFind find) =>
		Path.Combine("finds", find.Measure, find.Index.ToString(CultureInfo.InvariantCulture) + ".txt");

	/// <summary>The text of a find's graph file: a comment that says where it came from, and the graph.</summary>
	public static string FindText(ElkDifferentialRun run, ElkFind find) =>
		string.Create(CultureInfo.InvariantCulture, $"# elk-diff seed {run.Seed} graph {find.Index}: {find.Measure}: {find.Failure}\n") + find.Smallest.Text();

	public static void Write(string folder, ElkDifferentialRun run, string engine)
	{
		Directory.CreateDirectory(folder);
		foreach (var find in run.Finds)
		{
			var path = Path.Combine(folder, FindFile(find));
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			File.WriteAllText(path, FindText(run, find));
		}

		File.WriteAllText(Path.Combine(folder, ReportFile), Render(run, engine));
	}

	/// <summary>
	/// The report: the run's counts, then each distinct smallest graph once with the graphs it came from, by measure.
	/// </summary>
	public static string Render(ElkDifferentialRun run, string engine)
	{
		var text = new StringBuilder();
		text.Append("# ELK differential run\n\n");
		text.Append(CultureInfo.InvariantCulture, $"Seed {run.Seed}: {run.Graphs} random graphs, {run.Compared} laid out by ELK and {run.Refused} refused by elkjs. ");
		text.Append(CultureInfo.InvariantCulture, $"The {engine} differs from ELK on {run.Finds.Count} of them, which shrink to {Distinct(run).Count()} distinct graphs.\n");
		text.Append("Each graph below is a graph file of `node tools/oracle/elk-probe.mjs`.\n");
		foreach (var measure in ElkDifferential.Measures.Append(ElkDifferential.Error))
		{
			var groups = Distinct(run).Where(g => g.First().Measure == measure).ToList();
			if (groups.Count == 0)
			{
				continue;
			}

			text.Append(CultureInfo.InvariantCulture, $"\n## {measure}\n");
			foreach (var group in groups)
			{
				var first = group.First();
				var indexes = string.Join(", ", group.Select(f => f.Index.ToString(CultureInfo.InvariantCulture)));
				text.Append(CultureInfo.InvariantCulture, $"\n{first.Failure}. From graph {indexes}, shrunk from {first.Original.Nodes.Count} nodes and {first.Original.Edges.Count} edges ");
				text.Append(CultureInfo.InvariantCulture, $"to {first.Smallest.Nodes.Count} and {first.Smallest.Edges.Count} in {first.Tests} tests, `{FindFile(first).Replace('\\', '/')}`:\n\n");
				text.Append("```text\n").Append(first.Smallest.Text()).Append("```\n");
			}
		}

		return text.ToString();
	}

	private static IEnumerable<IGrouping<string, ElkFind>> Distinct(ElkDifferentialRun run) =>
		run.Finds.GroupBy(f => f.Measure + "\n" + f.Smallest.Text(), StringComparer.Ordinal);
}
