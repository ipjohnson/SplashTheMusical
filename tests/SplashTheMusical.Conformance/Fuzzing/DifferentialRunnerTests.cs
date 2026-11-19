using SplashTheMusical.Conformance.Fuzzing;

namespace SplashTheMusical.Conformance;

public class DifferentialRunnerTests
{
	private const string Seed = "flowchart LR\n    A --> B\n    B --> C\n    C --> D\n    D --> E\n    E --> F\n";

	[Test]
	public void FindsAndMinimizesASourceTheTwoSidesReadDifferently()
	{
		var stm = new StmFactsProvider();
		// An oracle whose rule for thick arrows is broken: it reads ==> as -->.
		var runner = new DifferentialRunner(source => stm.Facts(source.Replace("==>", "-->", StringComparison.Ordinal)), stm.Facts) { MaxFinds = 1 };

		var run = runner.Run("flowchart", [Seed], null, seed: 1, maxSources: 2000, TimeSpan.FromMinutes(5));

		Verify(() => run.Finds.Count == 1);
		var find = run.Finds[0];
		Verify(find.Minimized.Contains("==>", StringComparison.Ordinal), $"{find.Minimized}");
		Verify(() => find.Minimized.Length < find.Original.Length);
		Verify(() => SourceTokens.Lines(find.Minimized).Count <= 2);
		Verify(() => find.Disagreement.Signature == "L1 edge");
		Verify(runner.Disagree(find.Minimized)?.Shows("L1 edge") == true);
		Verify(() => find.Name == CorpusCheck.SourceHash(find.Minimized));
		Verify(() => DifferentialRunner.Source([Seed], null, 1, find.Index) == find.Original);
	}

	[Test]
	public void KeepsNothingWhenTheTwoSidesAgree()
	{
		var stm = new StmFactsProvider();
		var runner = new DifferentialRunner(stm.Facts, stm.Facts);

		var run = runner.Run("flowchart", [Seed], null, seed: 1, maxSources: 100, TimeSpan.FromMinutes(5));

		Verify(() => run.Finds.Count == 0);
		Verify(() => run.Sources == 100);
		Verify(() => run.Compared == 100);
	}

	[Test]
	public void SkipsASourceTheOracleGivesNoFactsFor()
	{
		var runner = new DifferentialRunner(_ => null, _ => throw new InvalidOperationException("STM should not run"));

		var run = runner.Run("flowchart", [Seed], null, seed: 1, maxSources: 10, TimeSpan.FromMinutes(1));

		Verify(() => run.Compared == 0);
		Verify(() => run.Finds.Count == 0);
	}

	[Test]
	public void TellsAnAcceptanceDifferenceAndAnExceptionFromAFactDifference()
	{
		var stm = new StmFactsProvider();
		var refusing = new DifferentialRunner(_ => CaseFacts.Refused("Parse error"), stm.Facts);
		var throwing = new DifferentialRunner(stm.Facts, _ => throw new InvalidOperationException("broken"));

		Verify(() => refusing.Disagree(Seed)?.Signature == "L0 STM-accepts");
		Verify(() => refusing.Disagree("flowchart LR\n  A-->") is null);
		Verify(() => throwing.Disagree(Seed)?.Signature == "exception InvalidOperationException");
	}

	[Test]
	public void WritesTheFindsAndAReport()
	{
		var folder = Directory.CreateTempSubdirectory("stm-differential-").FullName;
		try
		{
			var disagreement = new Disagreement("L1", new HashSet<string>(["edge"], StringComparer.Ordinal), ["- edge\tA\tB", "+ edge\tA\tC"]);
			var find = new DifferentialFind("flowchart", "0123456789abcdef", 42, "flowchart LR\n  A==>B\n  C\n", "flowchart\nA==>B", disagreement, 17);
			Directory.CreateDirectory(Path.Combine(folder, DifferentialReport.FindsFolder, "stale"));

			DifferentialReport.Write(folder, [new DifferentialRun("flowchart", 3, 100, 90, [find]), new DifferentialRun("state", 2, 50, 50, [])], 9, "12.3.0");

			Verify(() => File.ReadAllText(Path.Combine(folder, "finds", "flowchart", "0123456789abcdef.mmd")) == "flowchart\nA==>B");
			Verify(() => File.ReadAllText(Path.Combine(folder, "original", "flowchart", "0123456789abcdef.mmd")) == "flowchart LR\n  A==>B\n  C\n");
			Verify(!(Directory.Exists(Path.Combine(folder, "finds", "stale"))));
			var report = File.ReadAllText(Path.Combine(folder, DifferentialReport.ReportFile));
			Verify(() => report.StartsWith("Differential fuzzing of STM against mermaid.js 12.3.0 with seed 9: 1 find.\n", StringComparison.Ordinal));
			Verify(report.Contains("| `flowchart` | 3 | 100 | 90 | 1 |\n| `state` | 2 | 50 | 50 | 0 |\n"), $"{report}");
			Verify(report.Contains("## `flowchart/0123456789abcdef`: L1 edge\n\nSource 42 of the run, minimized from 3 lines to 2 in 17 tests.\n\n```text\nflowchart\nA==>B\n```\n", StringComparison.Ordinal), $"{report}");
			Verify(report.Contains("```text\n- edge\tA\tB\n+ edge\tA\tC\n```\n"), $"{report}");
			Verify(DifferentialReport.Markdown([new DifferentialRun("info", 1, 10, 10, [])], 1, "12.3.0").Contains("with seed 1: no finds."), $"{DifferentialReport.Markdown([new DifferentialRun("info", 1, 10, 10, [])], 1, "12.3.0")}");
		}
		finally
		{
			Directory.Delete(folder, recursive: true);
		}
	}

	[Test]
	public void ReadsTheAnswersOfTheOracleProcess()
	{
		var accepted = OracleProcess.Parse(["# mermaid.js facts", "# mermaid 12.3.0", "accept\tyes", "node\tA\tA\trect"]);
		var refused = OracleProcess.Parse(["accept\tno", "# error Parse error on line 3:"]);

		Verify(accepted!.Accepted);
		Verify(() => string.Join("|", accepted.Facts) == "node\tA\tA\trect");
		Verify(!(refused!.Accepted));
		Verify(() => refused.Error == "Parse error on line 3:");
		Verify(() => OracleProcess.Parse(["# unknown-type zenuml"]) is null);
		Verify(() => OracleProcess.Parse(["# oracle-error Target closed"]) is null);
	}
}
