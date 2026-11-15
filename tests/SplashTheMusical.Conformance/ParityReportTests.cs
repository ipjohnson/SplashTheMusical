namespace SplashTheMusical.Conformance;

public class ParityReportTests
{
	private static readonly CaseFacts Accepted = new(true, null, ["label\tA", "type\tpie"], ["box\tnode\tA\t0\t0\t10\t10"]);

	private static readonly CaseFacts Refused = CaseFacts.Refused("no");

	[Test]
	public async Task TheCommittedReportIsTheStubsReport()
	{
		var results = CorpusComparison.Run(CorpusCheck.CorpusRoot, new StubFactsProvider());
		var problems = new List<string>();
		var report = ParityReport.Render(results, LedgerFolder.Read(LedgerFolder.Root, problems), new StubFactsProvider().Name, CorpusCheck.PinnedMermaid());
		if (Environment.GetEnvironmentVariable(ParityReport.WriteVariable) is { Length: > 0 })
		{
			File.WriteAllText(ParityReport.FilePath, report);
		}

		await Assert.That(problems).IsEmpty();
		var committed = File.Exists(ParityReport.FilePath) ? File.ReadAllText(ParityReport.FilePath) : "";
		await Assert.That(committed).IsEqualTo(report).Because("mise run parity rewrites docs/parity-report.md");
	}

	[Test]
	public async Task EachTypeHasARowOfRatesAndLedgerCounts()
	{
		var results = new[]
		{
			Comparer.Compare("pie/a", Accepted, Accepted),
			Comparer.Compare("pie/b", Accepted, Refused),
			Comparer.Compare("pie/c", Refused, Refused),
			Comparer.Compare("pie/d", Refused, Accepted),
			Comparer.Compare("xychart/a", Refused, Refused),
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

		var lines = ParityReport.Render(results, ledgers, "test", "12.3.0").Split('\n');

		await Assert.That(lines).Contains("| pie | 4 | 2 | 50.0% (2 of 4) | 33.3% (1 of 3) | 33.3% (1 of 3) | 1 | 1 | 1 |");
		await Assert.That(lines).Contains("| xychart | 1 | 1 | 100.0% (1 of 1) | n/a | n/a | 0 | 0 | 0 |");
		await Assert.That(lines).Contains("| **Total** | 5 | 3 | 60.0% (3 of 5) | 33.3% (1 of 3) | 33.3% (1 of 3) | 1 | 1 | 1 |");
	}
}
