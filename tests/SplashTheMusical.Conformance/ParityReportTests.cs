namespace SplashTheMusical.Conformance;

public class ParityReportTests
{
	private static readonly CaseFacts Accepted = new(true, null, ["label\tA", "type\tpie"], ["box\tnode\tA\t0\t0\t10\t10"], "canvas\t0\t0\t10\t10");

	private static readonly CaseFacts Refused = CaseFacts.Refused("no");

	[Test]
	public void TheCommittedReportIsStmsReport()
	{
		var results = StmCorpusResults.All;
		var problems = new List<string>();
		var report = ParityReport.Render(
			results,
			LedgerFolder.Read(LedgerFolder.Root, problems),
			RasterOutcomes.Read(RasterOutcomes.FilePath, problems),
			new StmFactsProvider().Name,
			CorpusCheck.PinnedMermaid()
		);
		if (Environment.GetEnvironmentVariable(ParityReport.WriteVariable) is { Length: > 0 })
		{
			File.WriteAllText(ParityReport.FilePath, report);
		}

		Verify(problems.Count == 0, $"{string.Join('\n', problems)}");
		var committed = File.Exists(ParityReport.FilePath) ? File.ReadAllText(ParityReport.FilePath) : "";
		LineAssert.Equal(committed.Split('\n'), report.Split('\n'), "mise run parity rewrites docs/parity-report.md");
	}

	[Test]
	public void EachTypeHasARowOfRatesAndLedgerCounts()
	{
		var results = new[]
		{
			Comparer.Compare("pie/a", Accepted, Accepted),
			Comparer.Compare("pie/b", Accepted, Refused),
			Comparer.Compare("pie/c", Refused, Refused),
			Comparer.Compare("pie/d", Refused, Accepted),
			Comparer.Compare("xychart/a", Refused, Refused),
			Comparer.Compare("mindmap/a", Accepted, Accepted),
		};
		var l4 = new Dictionary<string, Outcome>
		{
			["pie/a"] = Outcome.Pass,
			["pie/b"] = Outcome.Fail,
			["pie/c"] = Outcome.NotApplicable,
			["pie/d"] = Outcome.Fail,
			["xychart/a"] = Outcome.NotApplicable,
			["mindmap/a"] = Outcome.ReportOnly,
		};
		var ledgers = new[]
		{
			new Ledger(
				"pie",
				[
					new LedgerEntry("b", Level.L0, LedgerCategory.Bug, "#13"),
					new LedgerEntry("b", Level.L1, LedgerCategory.ByDesign, null),
					new LedgerEntry("b", Level.L2, LedgerCategory.OutOfScope, null),
				]
			),
		};

		var lines = ParityReport.Render(results, ledgers, l4, "test", "12.3.0").Split('\n');

		Verify(lines.Contains("| pie | 4 | 2 | 50.0% (2 of 4) | 33.3% (1 of 3) | 33.3% (1 of 3) | 33.3% (1 of 3) | 33.3% (1 of 3) | 1 | 1 | 1 |"), $"{string.Join('\n', lines)}");
		Verify(lines.Contains("| xychart | 1 | 1 | 100.0% (1 of 1) | n/a | n/a | n/a | n/a | 0 | 0 | 0 |"), $"{string.Join('\n', lines)}");
		Verify(lines.Contains("| mindmap | 1 | 0 | 100.0% (1 of 1) | 100.0% (1 of 1) | 100.0% (1 of 1) | 100.0% (1 of 1) | report-only | 0 | 0 | 0 |"), $"{string.Join('\n', lines)}");
		Verify(lines.Contains("| **Total** | 6 | 3 | 66.7% (4 of 6) | 50.0% (2 of 4) | 50.0% (2 of 4) | 50.0% (2 of 4) | 33.3% (1 of 3) | 1 | 1 | 1 |"), $"{string.Join('\n', lines)}");
	}
}
