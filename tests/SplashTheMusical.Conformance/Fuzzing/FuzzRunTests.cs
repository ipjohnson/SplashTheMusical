using System.Globalization;
using SplashTheMusical.Conformance.Fuzzing;

namespace SplashTheMusical.Conformance;

/// <summary>
/// The fuzz runs of <c>mise run fuzz-crash</c> and <c>mise run fuzz-diff</c>, which set the environment variables these
/// read. Without them each test does nothing.
/// </summary>
public class FuzzRunTests
{
	/// <summary>The sources per type of a crash fuzz run.</summary>
	public const string CrashSourcesVariable = "STM_FUZZ_CRASH_SOURCES";

	/// <summary>The seconds per type of a differential run.</summary>
	public const string DifferentialSecondsVariable = "STM_FUZZ_DIFF_SECONDS";

	/// <summary>The seed of a run, 1 when it is not set.</summary>
	public const string SeedVariable = "STM_FUZZ_SEED";

	/// <summary>A comma-separated list of corpus folders, every one of <see cref="FuzzTypes.Folders"/> when it is not set.</summary>
	public const string TypesVariable = "STM_FUZZ_TYPES";

	/// <summary>The folder a run writes its finds and report to, <c>.artifacts/fuzz</c> when it is not set.</summary>
	public const string FolderVariable = "STM_FUZZ_DIR";

	/// <summary>A checkout of mermaid-js/mermaid at the pinned tag, whose grammars a differential run takes sentences from.</summary>
	public const string MermaidVariable = "STM_FUZZ_MERMAID";

	[Test]
	public async Task CrashFuzzesWhenAsked()
	{
		if (Environment.GetEnvironmentVariable(CrashSourcesVariable) is not { Length: > 0 } sources)
		{
			return;
		}

		var count = long.Parse(sources, CultureInfo.InvariantCulture);
		var seed = Seed();
		var corpus = CorpusCheck.CorpusRoot;
		var folder = Path.Combine(Folder(), "crash");
		var problems = new List<string>();
		foreach (var type in Types(corpus))
		{
			var run = CrashFuzzer.Run(type, FuzzTypes.Sources(corpus, type), count, seed, CrashFuzzer.Stm, ResourceLimits.Default.RenderDeadline!.Value, Environment.ProcessorCount);
			Console.WriteLine($"{type}: {run.Sources} sources with seed {seed}, {run.Finds.Count} finds");
			foreach (var find in run.Finds)
			{
				var path = Path.Combine(folder, type, find.Index.ToString(CultureInfo.InvariantCulture) + ".mmd");
				Directory.CreateDirectory(Path.GetDirectoryName(path)!);
				File.WriteAllText(path, find.Source);
				File.WriteAllText(Path.ChangeExtension(path, ".txt"), find.Problem + "\n");
				var lines = find.Problem.Split('\n');
				var frame = lines.Length > 1 ? "\n  " + lines[1].Trim() : "";
				problems.Add($"{type} source {find.Index} of seed {seed}, written to {path}: {lines[0]}{frame}\n  {Mutator.Escape(find.Source)}");
			}

			if (run.Sources < count && run.Finds.Count == 0)
			{
				problems.Add($"{type}: ran {run.Sources} of {count} sources");
			}
		}

		await Assert.That(string.Join("\n", problems)).IsEmpty();
	}

	[Test]
	public async Task DifferentialFuzzesWhenAsked()
	{
		if (Environment.GetEnvironmentVariable(DifferentialSecondsVariable) is not { Length: > 0 } seconds)
		{
			return;
		}

		var budget = TimeSpan.FromSeconds(double.Parse(seconds, CultureInfo.InvariantCulture));
		var seed = Seed();
		var root = CorpusCheck.RepositoryRoot;
		var corpus = CorpusCheck.CorpusRoot;
		var checkout = Environment.GetEnvironmentVariable(MermaidVariable) is { Length: > 0 } m ? m : null;
		var stmProvider = new StmFactsProvider(new Mermaid(new MermaidOptions { SiteConfig = StmFactsProvider.OracleSite, Limits = ResourceLimits.Default }));
		using var oracle = new OracleProcess(root, TimeSpan.FromSeconds(30));
		// A source mermaid.js reads as a type STM has no module for, such as a mutated header, says nothing about STM's parsers.
		var supported = new HashSet<string>(Mermaid.CoreModules.Select(m => ConfigLines.Line("type", m.DiagramType)), StringComparer.Ordinal);
		var runner = new DifferentialRunner(
			source => oracle.Facts("fuzz", source) is { } facts && (!facts.Accepted || facts.Facts.Any(supported.Contains)) ? facts : null,
			stmProvider.Facts);
		var runs = new List<DifferentialRun>();
		foreach (var type in Types(corpus))
		{
			var seeds = DifferentialRunner.AgreeingSeeds(corpus, type, stmProvider.Facts);
			var grammar = checkout is not null && FuzzTypes.GrammarPath(root, type) is { } path ? FuzzTypes.ReadGrammar(checkout, path) : null;
			var run = runner.Run(type, seeds, grammar, seed, int.MaxValue, budget);
			Console.WriteLine($"{type}: {run.Sources} sources from {seeds.Count} seed cases{(grammar is null ? "" : " and its grammar")}, {run.Compared} compared, {run.Finds.Count} finds");
			runs.Add(run);
		}

		var folder = Folder();
		Directory.CreateDirectory(folder);
		DifferentialReport.Write(folder, runs, seed, CorpusCheck.PinnedMermaid());
		Console.WriteLine($"Wrote {Path.Combine(folder, DifferentialReport.ReportFile)}");

		await Assert.That(runs.All(r => r.Compared > 0)).IsTrue();
	}

	private static long Seed() =>
		Environment.GetEnvironmentVariable(SeedVariable) is { Length: > 0 } seed ? long.Parse(seed, CultureInfo.InvariantCulture) : 1;

	private static string Folder() =>
		Environment.GetEnvironmentVariable(FolderVariable) is { Length: > 0 } folder ? folder : Path.Combine(CorpusCheck.RepositoryRoot, ".artifacts", "fuzz");

	private static IReadOnlyList<string> Types(string corpus) =>
		Environment.GetEnvironmentVariable(TypesVariable) is { Length: > 0 } types
			? types.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
			: FuzzTypes.Folders(corpus);
}
