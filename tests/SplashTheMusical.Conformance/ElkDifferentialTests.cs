using System.Globalization;

namespace SplashTheMusical.Conformance;

public class ElkDifferentialTests
{
	/// <summary>The random graphs of a run of <c>mise run elk-diff</c>. Without it <see cref="RunsWhenAsked"/> does nothing.</summary>
	public const string GraphsVariable = "STM_ELK_DIFF_GRAPHS";

	/// <summary>The seed of a run, 1 when it is not set.</summary>
	public const string SeedVariable = "STM_ELK_DIFF_SEED";

	/// <summary>A comma-separated list of the measures a find may fail, every one of the comparer's when it is not set.</summary>
	public const string MeasuresVariable = "STM_ELK_DIFF_MEASURES";

	/// <summary>The most graphs the shrinking of one find lays out, 400 when it is not set.</summary>
	public const string TestsVariable = "STM_ELK_DIFF_TESTS";

	/// <summary>The folder a run writes its report and finds to, <c>.artifacts/elk-diff</c> when it is not set.</summary>
	public const string FolderVariable = "STM_ELK_DIFF_DIR";

	private static readonly Func<ElkInputGraph, CandidateLayout> Dagre = graph => DagreOnElkInput.Layout(graph, DagreOnElkInput.FlowchartDefaults);

	[Test]
	public void RunsWhenAsked()
	{
		if (Environment.GetEnvironmentVariable(GraphsVariable) is not { Length: > 0 } graphs)
		{
			return;
		}

		var seed = Environment.GetEnvironmentVariable(SeedVariable) is { Length: > 0 } s ? long.Parse(s, CultureInfo.InvariantCulture) : 1;
		var measures = Environment.GetEnvironmentVariable(MeasuresVariable) is { Length: > 0 } m
			? m.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
			: [.. ElkDifferential.Measures, ElkDifferential.Error];
		var maxTests = Environment.GetEnvironmentVariable(TestsVariable) is { Length: > 0 } t ? int.Parse(t, CultureInfo.InvariantCulture) : 400;
		var folder = Environment.GetEnvironmentVariable(FolderVariable) is { Length: > 0 } f ? f : Path.Combine(CorpusCheck.RepositoryRoot, ".artifacts", "elk-diff");

		using var probe = new ElkProbeProcess(CorpusCheck.RepositoryRoot, TimeSpan.FromSeconds(30));
		var run = ElkDifferential.Run(int.Parse(graphs, CultureInfo.InvariantCulture), seed, text => probe.Layout(text, out _), Dagre, measures, maxTests);
		if (Directory.Exists(folder))
		{
			Directory.Delete(folder, recursive: true);
		}

		ElkDifferentialReport.Write(folder, run, "dagre port");
		foreach (var group in run.Finds.GroupBy(x => x.Measure + "\n" + x.Smallest.Text(), StringComparer.Ordinal))
		{
			var find = group.First();
			Console.WriteLine($"{find.Measure}, graph {string.Join(", ", group.Select(x => x.Index))}: {find.Failure}");
			Console.Write(find.Smallest.Text());
			Console.WriteLine();
		}

		Console.WriteLine($"{run.Compared} of {run.Graphs} graphs compared with seed {seed}, {run.Finds.Count} finds. Wrote {Path.Combine(folder, ElkDifferentialReport.ReportFile)}");
		Verify(() => run.Compared > 0);
	}

	[Test]
	public void AGraphIsTheSameForTheSameSeedAndIndex()
	{
		var first = ElkDifferential.Generate(7, 3).Text();

		Verify(() => ElkDifferential.Generate(7, 3).Text() == first);
		Verify(() => Enumerable.Range(0, 20).Select(i => ElkDifferential.Generate(7, i).Text()).Distinct().Count() > 15);
	}

	[Test]
	public void AGraphsTextIsAGraphTheRecordsRead()
	{
		var graph = new ProbeGraph(
			"RIGHT",
			[new ProbeNode("a", 60, 45), new ProbeNode("b", 80, 54)],
			[new ProbeEdge("e0", "a", "b", 30, 24), new ProbeEdge("e1", "b", "b", 0, 0)]);

		var input = new ElkInputGraph(Input(graph.Text()));

		Verify(() => input.Direction == "RIGHT");
		Verify(() => string.Join(' ', input.Leaves) == "a b");
		Verify(() => input.Edges.Count == 2);
		Verify(() => input.Edges[0].LabelWidth == 30);
		Verify(() => input.Edges[1].LabelWidth == 0);
	}

	[Test]
	public void WithNodesDropsTheEdgesOfARemovedNode()
	{
		var graph = new ProbeGraph(
			"DOWN",
			[new ProbeNode("a", 60, 45), new ProbeNode("b", 60, 45), new ProbeNode("c", 60, 45)],
			[new ProbeEdge("e0", "a", "b", 0, 0), new ProbeEdge("e1", "b", "c", 0, 0)]);

		var smaller = graph.WithNodes([graph.Nodes[0], graph.Nodes[1]]);

		Verify(() => smaller.Nodes.Count == 2);
		Verify(() => smaller.Edges.Count == 1);
		Verify(() => smaller.Edges[0].Id == "e0");
	}

	[Test]
	public void ARunShrinksEachDifferenceToTheSmallestGraphThatStillDiffers()
	{
		var run = ElkDifferential.Run(40, 1, BendsIntoAJoin, Dagre, [.. ElkDifferential.Measures], 400);

		Verify(() => run.Compared == 40);
		Verify(() => run.Finds.Count > 0);
		foreach (var find in run.Finds)
		{
			Verify(() => find.Measure == "bends");
			Verify(() => find.Smallest.Edges.Count == 2);
			Verify(() => find.Smallest.Edges[0].Target == find.Smallest.Edges[1].Target);
			Verify(() => find.Smallest.Nodes.Count <= 3);
			Verify(() => find.Smallest.Edges.All(e => e.LabelWidth == 0));
			Verify(() => find.Smallest.Direction == "DOWN");
			Verify(() => find.Tests > 0);
		}
	}

	[Test]
	public void AGraphTheFakeElkAgreesOnIsNoFind()
	{
		var run = ElkDifferential.Run(20, 1, Agreeing, Dagre, [.. ElkDifferential.Measures], 400);

		Verify(() => run.Compared == 20);
		Verify(() => run.Finds.Count == 0);
	}

	[Test]
	public void AGraphElkRefusesIsCountedAndNotCompared()
	{
		var run = ElkDifferential.Run(5, 1, _ => null, Dagre, [.. ElkDifferential.Measures], 400);

		Verify(() => run.Refused == 5);
		Verify(() => run.Compared == 0);
	}

	[Test]
	public void AMeasureOutsideTheRunsIsNoFind()
	{
		var run = ElkDifferential.Run(40, 1, BendsIntoAJoin, Dagre, ["layers", "order"], 400);

		Verify(() => run.Finds.Count == 0);
	}

	[Test]
	public void AnEngineThatThrowsIsAnErrorFind()
	{
		var run = ElkDifferential.Run(3, 1, Agreeing, _ => throw new InvalidOperationException("no layout"), [ElkDifferential.Error], 50);

		Verify(() => run.Finds.Count == 3);
		Verify(() => run.Finds.All(f => f.Measure == ElkDifferential.Error && f.Failure == "InvalidOperationException: no layout"));
		Verify(() => run.Finds.All(f => f.Smallest.Nodes.Count == 1 && f.Smallest.Edges.Count == 0));
	}

	[Test]
	public void TheReportListsEachDistinctSmallestGraphOnce()
	{
		var smallest = new ProbeGraph("DOWN", [new ProbeNode("n0", 60, 45)], []);
		var original = new ProbeGraph("DOWN", [new ProbeNode("n0", 60, 45), new ProbeNode("n1", 60, 45)], []);
		var run = new ElkDifferentialRun(9, 10, 9, 1, [
			new ElkFind(2, "boxes", "the canvas is 1 x 1, ELK's is 2 x 2", original, smallest, 5),
			new ElkFind(4, "boxes", "the canvas is 1 x 1, ELK's is 2 x 2", original, smallest, 6),
		]);

		var lines = ElkDifferentialReport.Render(run, "dagre port").Split('\n');

		Verify(lines.Contains("Seed 9: 10 random graphs, 9 laid out by ELK and 1 refused by elkjs. The dagre port differs from ELK on 2 of them, which shrink to 1 distinct graphs."), $"{string.Join('\n', lines)}");
		Verify(lines.Contains("## boxes"), $"{string.Join('\n', lines)}");
		Verify(lines.Contains("the canvas is 1 x 1, ELK's is 2 x 2. From graph 2, 4, shrunk from 2 nodes and 0 edges to 1 and 0 in 5 tests, `finds/boxes/2.txt`:"), $"{string.Join('\n', lines)}");
		Verify(() => lines.Count(l => l == "node\tn0\troot\t60\t45") == 1);
		Verify(() => ElkDifferentialReport.FindText(run, run.Finds[0]).StartsWith("# elk-diff seed 9 graph 2: boxes: ", StringComparison.Ordinal));
	}

	[Test]
	public void TheProbeLaysOutARecordedGraphAsELKDid()
	{
		if (!Directory.Exists(Path.Combine(CorpusCheck.RepositoryRoot, "tools", "oracle", "node_modules")))
		{
			return;
		}

		var path = Path.Combine(CorpusCheck.CorpusRoot, "flowchart", "docs-flowchart-007.elk.txt");
		var recorded = ElkRecords.Read(path)[0];
		using var probe = new ElkProbeProcess(CorpusCheck.RepositoryRoot, TimeSpan.FromSeconds(30));

		var record = probe.Layout(File.ReadAllText(path), out var error);
		var refused = probe.Layout("node\troot\nedge\te\troot\ta\tb\n", out var refusal);

		Verify(() => error == null);
		Verify(record is not null);
		Verify(() => record!.Output.Places.SequenceEqual(recorded.Output.Places));
		Verify(() => record!.Output.Routes.Count == recorded.Output.Routes.Count);
		Verify(() => refused == null);
		Verify(() => refusal != null);
	}

	/// <summary>
	/// A fake ELK that lays a graph out as the dagre port does, except that each edge into a node with two incoming edges
	/// gets one more bend.
	/// </summary>
	private static ElkRecord? BendsIntoAJoin(string text) => Fake(text, 1);

	/// <summary>A fake ELK that lays a graph out as the dagre port does.</summary>
	private static ElkRecord? Agreeing(string text) => Fake(text, 0);

	/// <summary>The input of a graph's text, as the records read it.</summary>
	private static ElkInput Input(string text) => ElkRecords.Parse(["layout\t1", .. text.Split('\n'), "result"], "fake")[0].Input;

	/// <summary>
	/// A record of the graph's text with the dagre port's layout as ELK's result. Each edge into a node with two or more
	/// incoming edges is drawn as a staircase with <paramref name="extra"/> more bends than dagre's route.
	/// </summary>
	private static ElkRecord Fake(string text, int extra)
	{
		var input = Input(text);
		var graph = new ElkInputGraph(input);
		var layout = Dagre(graph);
		var incoming = graph.Edges.GroupBy(e => e.Target, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
		var places = new List<ElkPlace> { new("root", null, 0, 0, layout.Width, layout.Height) };
		places.AddRange(graph.Nodes.Select(n => new ElkPlace(n.Id, "root", layout.Boxes[n.Id].X, layout.Boxes[n.Id].Y, layout.Boxes[n.Id].Width, layout.Boxes[n.Id].Height)));
		var routes = graph.Edges
			.Where(e => layout.Edges.ContainsKey(e.Id))
			.Select(e => Route(e.Id, layout.Edges[e.Id], incoming[e.Target] >= 2 ? extra : 0))
			.ToList();
		return new ElkRecord(1, input, new ElkOutput(places, [], [], routes));
	}

	private static ElkRoute Route(string id, IReadOnlyList<(double X, double Y)> points, int extra)
	{
		var drawn = points;
		if (extra > 0)
		{
			var bends = ElkLayoutComparer.Bends(points) + extra;
			var staircase = new List<(double X, double Y)>();
			for (var i = 0; i < bends + 2; i++)
			{
				staircase.Add(((i + 1) / 2 * 10, i / 2 * 10));
			}

			drawn = staircase;
		}

		var section = drawn.Count < 2
			? new ElkSection(id + "_s0", null, null, [], [], [], [], [])
			: new ElkSection(id + "_s0", drawn[0], drawn[^1], [.. drawn.Skip(1).Take(drawn.Count - 2)], [], [], [], []);
		return new ElkRoute(id, "root", "root", [], [section]);
	}
}
