using System.Reflection;
using Mermaider;
using SplashTheMusical.Benchmarks;

return args switch
{
	["bench", .. var rest] => Bench(Option(rest, "--out") ?? ".artifacts/bench/stm-results.md", Jobs(rest)),
	["mermaider", .. var rest] => Baseline(Option(rest, "--out") ?? "docs/benchmarks/mermaider-baseline.md", Jobs(rest)),
	["select"] => Select(),
	["compare", var committed, var run] => Comparison.Run(committed, run, Console.Out),
	["size", .. var rest] => Size(rest),
	["dagre", .. var rest] => Dagre(Option(rest, "--out") ?? "docs/benchmarks/dagre.md", Jobs(rest)),
	["dagre-layout", var nodes] => DagreLayoutOnce(int.Parse(nodes, System.Globalization.CultureInfo.InvariantCulture)),
	_ => Usage(),
};

static int Usage()
{
	Console.Error.WriteLine("""
		usage:
		  bench [--jobs JIT,AOT] [--out file]       run STM's stages on the benchmark set and write a result file
		  mermaider [--jobs JIT,AOT] [--out file]   run the Mermaider baseline and write its result file
		  select                                    pick the benchmark set from the corpus and rewrite its manifest
		  compare <committed> <run>                 exit 1 when the run is more than 10% worse than the committed numbers
		  size [--sizes file] [--dll file] [--cli file] [--accept]
		                                            exit 1 when a binary is more than 5% larger than its committed size
		  dagre [--jobs JIT,AOT] [--out file]       run the dagre port on graphs of 10, 100 and 1,000 nodes and write its result file
		  dagre-layout <nodes>                      lay out one generated graph of that many nodes and print the time
		STM_BENCH_CASES limits a run to the cases whose id contains it.
		""");
	return 2;
}

static string? Option(string[] args, string name)
{
	var index = Array.IndexOf(args, name);
	return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

static string[] Jobs(string[] args) => Option(args, "--jobs")?.Split(',') ?? Runs.AllJobs;

static int Bench(string output, string[] jobs)
{
	var path = Repository.Resolve(output);
	var preamble = "STM's stages on the benchmark set in `corpus/benchmarks/cases.txt`. Written by `mise run bench`.";
	if (!StmBenchmarks.Cases().Any())
	{
		ResultFile.Write(path, "STM benchmark results", preamble, [("Date", DateTime.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture))], [],
			"STM has no stage to measure yet. `StmBenchmarks.Stages` lists them.");
		Console.WriteLine($"No STM stage to measure. Wrote {path}");
		return 0;
	}

	var (summary, rows) = Runs.Run<StmBenchmarks>(jobs);
	ResultFile.Write(path, "STM benchmark results", preamble, Runs.Properties(summary, jobs), rows);
	Console.WriteLine($"Wrote {path}");
	return 0;
}

static int Baseline(string output, string[] jobs)
{
	var version = typeof(MermaidRenderer).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
	var plus = version.IndexOf('+', StringComparison.Ordinal);
	version = plus >= 0 ? version[..plus] : version;
	var skipped = BenchmarkSet.Selected().Where(c => !MermaiderBenchmarks.Runs("render", c.Source)).Select(c => $"- {c.Id} (`{c.CorpusCase}`)").ToList();
	var (summary, rows) = Runs.Run<MermaiderBenchmarks>(jobs);

	var path = Repository.Resolve(output);
	ResultFile.Write(
		path,
		"Mermaider baseline",
		"""
		The time and allocation of the Mermaider package on the benchmark set in `corpus/benchmarks/cases.txt`. STM's budget is to be
		no slower and to allocate no more than these numbers. Section 6 of [the parity plan](../mermaid-parity-plan.md) sets it.
		Written by `mise run bench-mermaider`.

		Mermaider's public API has two entry points. `parse` is `MermaidRenderer.Parse`. `render` is `MermaidRenderer.RenderSvg`,
		which parses, lays out and renders, so it is the budget for STM's parse, layout and render together. `Parse` handles only the
		graph types, so the other types have no `parse` row. `ns` is the mean time per call and `Bytes` the bytes allocated per call.
		The per-character columns divide them by `Chars`, the length of the source.
		""",
		[("Package", $"Mermaider {version}"), .. Runs.Properties(summary, jobs)],
		rows,
		skipped.Count == 0 ? null : "## Cases Mermaider does not render\n\nMermaider throws on these, so they have no baseline.\n\n" + string.Join('\n', skipped));
	Console.WriteLine($"Wrote {path}");
	return 0;
}

static int Select()
{
	var count = BenchmarkSet.Select(source => MermaiderBenchmarks.Runs("render", source));
	Console.WriteLine($"Wrote {count} cases to {BenchmarkSet.Manifest}");
	return 0;
}

static int Dagre(string output, string[] jobs)
{
	var path = Repository.Resolve(output);
	DagreBenchmarks.Write(path, jobs);
	Console.WriteLine($"Wrote {path}");
	return 0;
}

static int DagreLayoutOnce(int nodes)
{
	var input = DagreGraphs.Flowchart(nodes);
	var stopwatch = System.Diagnostics.Stopwatch.StartNew();
	var g = SplashTheMusical.Layout.Dagre.DagreLayout.BuildLayoutGraph(input);
	SplashTheMusical.Layout.Dagre.DagreLayout.RunRankPhases(g);
	var rank = stopwatch.Elapsed.TotalMilliseconds;
	SplashTheMusical.Layout.Dagre.DagreLayout.RunOrderPhases(g);
	var order = stopwatch.Elapsed.TotalMilliseconds;
	var normalized = g.NodeCount;
	SplashTheMusical.Layout.Dagre.DagreLayout.RunPositionPhases(g);
	var total = stopwatch.Elapsed.TotalMilliseconds;
	// FNV-1a over the bits of every position, so two builds of the port can be compared on graphs the corpus lacks.
	var hash = 14695981039346656037UL;
	void Add(double value) => hash = (hash ^ (ulong)BitConverter.DoubleToInt64Bits(value)) * 1099511628211UL;
	foreach (var v in g.Nodes())
	{
		Add(g.Node(v).X);
		Add(g.Node(v).Y);
	}

	foreach (var e in g.Edges())
	{
		foreach (var (x, y) in g.Edge(e).Points ?? [])
		{
			Add(x);
			Add(y);
		}
	}

	Console.WriteLine($"{nodes} nodes, {input.Edges.Count} edges, {normalized} nodes after normalize: {total:F1} ms (rank {rank:F1}, order {order - rank:F1}, position {total - order:F1}), {g.Label.Width:F0} x {g.Label.Height:F0}, positions {hash:x16}");
	return 0;
}

static int Size(string[] args)
{
	var cli = OperatingSystem.IsWindows() ? "stm.exe" : "stm";
	var binaries = new List<(string, string, string)>
	{
		("SplashTheMusical.dll", "any", Option(args, "--dll") ?? Repository.Resolve(".artifacts/bin/SplashTheMusical/release/SplashTheMusical.dll")),
		("stm", SizeCheck.HostRid, Option(args, "--cli") ?? Repository.Resolve(".artifacts/publish/SplashTheMusical.Cli/release/" + cli)),
	};
	var sizes = Option(args, "--sizes") ?? Repository.Resolve("docs/benchmarks/sizes.md");
	return SizeCheck.Run(sizes, binaries, args.Contains("--accept"), Console.Out);
}
