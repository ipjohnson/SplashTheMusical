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
