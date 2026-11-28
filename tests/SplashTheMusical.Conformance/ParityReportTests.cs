namespace SplashTheMusical.Conformance;

public class ParityReportTests
{
	private static readonly CaseFacts Accepted = new(true, null, ["label\tA", "type\tpie"], ["box\tnode\tA\t0\t0\t10\t10"], "canvas\t0\t0\t10\t10");

	private static readonly CaseFacts Refused = CaseFacts.Refused("no");

	[Test]
	public void EachCommittedReportIsStmsReport()
	{
		var results = CorpusComparison.Run(CorpusCheck.CorpusRoot, new StmFactsProvider());
		var problems = new List<string>();
		var reports = ParityReport.Render(
			results,
			LedgerFolder.Read(LedgerFolder.Root, problems),
			RasterOutcomes.Read(RasterOutcomes.Folder, problems),
			new StmFactsProvider().Name,
			CorpusCheck.PinnedMermaid()
		);
		if (Environment.GetEnvironmentVariable(ParityReport.WriteVariable) is { Length: > 0 })
		{
			ParityReport.Write(ParityReport.Folder, reports);
		}

		Verify(problems.Count == 0, $"{string.Join('\n', problems)}");
		LineAssert.Equal(ParityReport.Committed(ParityReport.Folder), reports.Keys, "mise run parity writes one file per type in docs/parity");
		foreach (var (type, report) in reports)
		{
			var path = Path.Combine(ParityReport.Folder, ParityReport.FileName(type));
			var committed = File.Exists(path) ? File.ReadAllText(path) : "";
			LineAssert.Equal(committed.Split('\n'), report.Split('\n'), $"mise run parity rewrites docs/parity/{ParityReport.FileName(type)}");
		}
	}

	[Test]
	public void EachTypeHasAFileWithItsRowOfRatesAndLedgerCounts()
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

		var reports = ParityReport.Render(results, ledgers, l4, "test", "12.3.0");

		LineAssert.Equal(reports.Keys, "mindmap", "pie", "xychart");
		LineAssert.Equal(Rows(reports["pie"]), "| pie | 4 | 2 | 50.0% (2 of 4) | 33.3% (1 of 3) | 33.3% (1 of 3) | 33.3% (1 of 3) | 33.3% (1 of 3) | 1 | 1 | 1 |");
		LineAssert.Equal(Rows(reports["xychart"]), "| xychart | 1 | 1 | 100.0% (1 of 1) | n/a | n/a | n/a | n/a | 0 | 0 | 0 |");
		LineAssert.Equal(
			Rows(reports["mindmap"]),
			"| mindmap | 1 | 0 | 100.0% (1 of 1) | 100.0% (1 of 1) | 100.0% (1 of 1) | 100.0% (1 of 1) | report-only | 0 | 0 | 0 |"
		);
		Verify(() => reports["pie"].Contains(ParityReport.Header));
	}

	[Test]
	public void WritingDeletesTheReportOfATypeWithoutResultsAndKeepsTheReadme()
	{
		var folder = Directory.CreateTempSubdirectory("stm-parity-").FullName;
		try
		{
			File.WriteAllText(Path.Combine(folder, "README.md"), "readme");
			File.WriteAllText(Path.Combine(folder, "gone.md"), "old");

			ParityReport.Write(folder, new Dictionary<string, string> { ["pie"] = "pie report" });

			LineAssert.Equal(ParityReport.Committed(folder), "pie");
			Verify(() => File.ReadAllText(Path.Combine(folder, "pie.md")) == "pie report");
			Verify(() => File.Exists(Path.Combine(folder, "README.md")));
		}
		finally
		{
			Directory.Delete(folder, recursive: true);
		}
	}

	// The table rows below the header and separator.
	private static IEnumerable<string> Rows(string report) =>
		report.Split('\n').SkipWhile(l => !l.StartsWith("|---", StringComparison.Ordinal)).Skip(1).Where(l => l.Length > 0);
}
