using SplashTheMusical.Conformance.Fuzzing;

namespace SplashTheMusical.Conformance;

public class DifferentialRunnerTests
{
	private const string Seed = "flowchart LR\n    A --> B\n    B --> C\n    C --> D\n    D --> E\n    E --> F\n";

	[Test]
	public async Task FindsAndMinimizesASourceTheTwoSidesReadDifferently()
	{
		var stm = new StmFactsProvider();
		// An oracle whose rule for thick arrows is broken: it reads ==> as -->.
		var runner = new DifferentialRunner(source => stm.Facts(source.Replace("==>", "-->", StringComparison.Ordinal)), stm.Facts) { MaxFinds = 1 };

		var run = runner.Run("flowchart", [Seed], null, seed: 1, maxSources: 2000, TimeSpan.FromMinutes(5));

		await Assert.That(run.Finds.Count).IsEqualTo(1);
		var find = run.Finds[0];
		await Assert.That(find.Minimized).Contains("==>");
		await Assert.That(find.Minimized.Length).IsLessThan(find.Original.Length);
		await Assert.That(SourceTokens.Lines(find.Minimized).Count).IsLessThanOrEqualTo(2);
		await Assert.That(find.Disagreement.Signature).IsEqualTo("L1 edge");
		await Assert.That(runner.Disagree(find.Minimized)?.Shows("L1 edge")).IsTrue();
		await Assert.That(find.Name).IsEqualTo(CorpusCheck.SourceHash(find.Minimized));
		await Assert.That(DifferentialRunner.Source([Seed], null, 1, find.Index)).IsEqualTo(find.Original);
	}

	[Test]
	public async Task KeepsNothingWhenTheTwoSidesAgree()
	{
		var stm = new StmFactsProvider();
		var runner = new DifferentialRunner(stm.Facts, stm.Facts);

		var run = runner.Run("flowchart", [Seed], null, seed: 1, maxSources: 100, TimeSpan.FromMinutes(5));

		await Assert.That(run.Finds.Count).IsEqualTo(0);
		await Assert.That(run.Sources).IsEqualTo(100);
		await Assert.That(run.Compared).IsEqualTo(100);
	}

	[Test]
	public async Task SkipsASourceTheOracleGivesNoFactsFor()
	{
		var runner = new DifferentialRunner(_ => null, _ => throw new InvalidOperationException("STM should not run"));

		var run = runner.Run("flowchart", [Seed], null, seed: 1, maxSources: 10, TimeSpan.FromMinutes(1));

		await Assert.That(run.Compared).IsEqualTo(0);
		await Assert.That(run.Finds.Count).IsEqualTo(0);
	}

	[Test]
	public async Task TellsAnAcceptanceDifferenceAndAnExceptionFromAFactDifference()
	{
		var stm = new StmFactsProvider();
		var refusing = new DifferentialRunner(_ => CaseFacts.Refused("Parse error"), stm.Facts);
		var throwing = new DifferentialRunner(stm.Facts, _ => throw new InvalidOperationException("broken"));

		await Assert.That(refusing.Disagree(Seed)?.Signature).IsEqualTo("L0 STM-accepts");
		await Assert.That(refusing.Disagree("flowchart LR\n  A-->")).IsNull();
		await Assert.That(throwing.Disagree(Seed)?.Signature).IsEqualTo("exception InvalidOperationException");
	}

	[Test]
	public async Task WritesTheFindsAndAReport()
	{
		var folder = Directory.CreateTempSubdirectory("stm-differential-").FullName;
		try
		{
			var disagreement = new Disagreement("L1", new HashSet<string>(["edge"], StringComparer.Ordinal), ["- edge\tA\tB", "+ edge\tA\tC"]);
			var find = new DifferentialFind("flowchart", "0123456789abcdef", 42, "flowchart LR\n  A==>B\n  C\n", "flowchart\nA==>B", disagreement, 17);
			Directory.CreateDirectory(Path.Combine(folder, DifferentialReport.FindsFolder, "stale"));

			DifferentialReport.Write(folder, [new DifferentialRun("flowchart", 3, 100, 90, [find]), new DifferentialRun("state", 2, 50, 50, [])], 9, "12.3.0");

			await Assert.That(File.ReadAllText(Path.Combine(folder, "finds", "flowchart", "0123456789abcdef.mmd"))).IsEqualTo("flowchart\nA==>B");
			await Assert.That(File.ReadAllText(Path.Combine(folder, "original", "flowchart", "0123456789abcdef.mmd"))).IsEqualTo("flowchart LR\n  A==>B\n  C\n");
			await Assert.That(Directory.Exists(Path.Combine(folder, "finds", "stale"))).IsFalse();
			var report = File.ReadAllText(Path.Combine(folder, DifferentialReport.ReportFile));
			await Assert.That(report).StartsWith("Differential fuzzing of STM against mermaid.js 12.3.0 with seed 9: 1 find.\n");
			await Assert.That(report).Contains("| `flowchart` | 3 | 100 | 90 | 1 |\n| `state` | 2 | 50 | 50 | 0 |\n");
			await Assert.That(report).Contains("## `flowchart/0123456789abcdef`: L1 edge\n\nSource 42 of the run, minimized from 3 lines to 2 in 17 tests.\n\n```text\nflowchart\nA==>B\n```\n");
			await Assert.That(report).Contains("```text\n- edge\tA\tB\n+ edge\tA\tC\n```\n");
			await Assert.That(DifferentialReport.Markdown([new DifferentialRun("info", 1, 10, 10, [])], 1, "12.3.0")).Contains("with seed 1: no finds.");
		}
		finally
		{
			Directory.Delete(folder, recursive: true);
		}
	}

	[Test]
	public async Task ReadsTheAnswersOfTheOracleProcess()
	{
		var accepted = OracleProcess.Parse(["# mermaid.js facts", "# mermaid 12.3.0", "accept\tyes", "node\tA\tA\trect"]);
		var refused = OracleProcess.Parse(["accept\tno", "# error Parse error on line 3:"]);

		await Assert.That(accepted!.Accepted).IsTrue();
		await Assert.That(string.Join("|", accepted.Facts)).IsEqualTo("node\tA\tA\trect");
		await Assert.That(refused!.Accepted).IsFalse();
		await Assert.That(refused.Error).IsEqualTo("Parse error on line 3:");
		await Assert.That(OracleProcess.Parse(["# unknown-type zenuml"])).IsNull();
		await Assert.That(OracleProcess.Parse(["# oracle-error Target closed"])).IsNull();
	}
}
