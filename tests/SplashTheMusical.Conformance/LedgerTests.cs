using AwesomeAssertions;

namespace SplashTheMusical.Conformance;

public class LedgerTests
{
	private const string Mermaid = "12.3.0";

	private static readonly CaseFacts Oracle = new(true, null, ["label\tA", "node\tA\tA\trect", "type\tpie"], ["box\tnode\tA\t0\t0\t10\t10"]);

	[Test]
	public void TheStubDiffersOnlyWhereTheCommittedLedgersListABug()
	{
		var results = CorpusComparison.Run(CorpusCheck.CorpusRoot, new StubFactsProvider());
		if (Environment.GetEnvironmentVariable(LedgerFolder.WriteVariable) is { Length: > 0 } issue)
		{
			LedgerFolder.Write(LedgerFolder.Root, results, issue, CorpusCheck.PinnedMermaid());
		}

		LedgerFolder.Problems(LedgerFolder.Root, results, CorpusCheck.PinnedMermaid()).Should().BeEmpty();
		var problems = new List<string>();
		LedgerFolder.Read(LedgerFolder.Root, problems).SelectMany(l => l.Entries).Should().OnlyContain(e => e.Category == LedgerCategory.Bug);
	}

	[Test]
	public void AnUnlistedDifferenceFails() =>
		WithFolder(folder =>
		{
			LedgerFolder.Write(folder, [Passes("pie/a"), Fails("pie/b")], "#13", Mermaid);

			LedgerFolder
				.Problems(folder, [Fails("pie/a"), Fails("pie/b")], Mermaid)
				.Should()
				.Equal(
					"pie/a L0: differs from mermaid.js and pie.txt does not list it",
					"pie/a L1: differs from mermaid.js and pie.txt does not list it",
					"pie/a L2: differs from mermaid.js and pie.txt does not list it"
				);
		});

	[Test]
	public void AListedDifferenceThatNoLongerOccursFails() =>
		WithFolder(folder =>
		{
			LedgerFolder.Write(folder, [Passes("pie/a"), Fails("pie/b")], "#13", Mermaid);

			LedgerFolder
				.Problems(folder, [Passes("pie/a"), Passes("pie/b")], Mermaid)
				.Should()
				.Equal(
					"pie/b L0: pie.txt lists it and it no longer differs",
					"pie/b L1: pie.txt lists it and it no longer differs",
					"pie/b L2: pie.txt lists it and it no longer differs"
				);
		});

	[Test]
	public void RaisingABugCountFailsUntilTheMermaidVersionChanges() =>
		WithFolder(folder =>
		{
			LedgerFolder.Write(folder, [Passes("pie/a"), Fails("pie/b")], "#13", Mermaid);
			var worse = new[] { Fails("pie/a"), Fails("pie/b") };

			LedgerFolder.Write(folder, worse, "#13", Mermaid);

			LedgerFolder
				.Problems(folder, worse, Mermaid)
				.Should()
				.Equal("pie: 6 bugs, bugs.txt allows 3. The count may only go up when the mermaid version changes");

			LedgerFolder.Write(folder, worse, "#13", "12.4.0");

			LedgerFolder.Problems(folder, worse, "12.4.0").Should().BeEmpty();
		});

	[Test]
	public void ALowerBugCountIsWrittenAndThenHeld() =>
		WithFolder(folder =>
		{
			LedgerFolder.Write(folder, [Fails("pie/a"), Fails("pie/b")], "#13", Mermaid);
			File.WriteAllText(Path.Combine(folder, "pie.txt"), "bug #13\nb\tL0\nb\tL1\nb\tL2\n");
			var better = new[] { Passes("pie/a"), Fails("pie/b") };

			LedgerFolder.Problems(folder, better, Mermaid).Should().Equal("pie: 3 bugs, bugs.txt records 6. Lower it to 3");

			LedgerFolder.Write(folder, better, "#13", Mermaid);

			File.ReadAllText(Path.Combine(folder, BugCounts.FileName)).Should().Contain("pie\t3\n");
			LedgerFolder.Problems(folder, better, Mermaid).Should().BeEmpty();
		});

	[Test]
	public void ARewriteKeepsTheCategoryOfAListedDifference() =>
		WithFolder(folder =>
		{
			File.WriteAllText(Path.Combine(folder, "pie.txt"), "out-of-scope\na\tL0\nby-design\na\tL1\nbug owner/repo#7\na\tL2\n");

			LedgerFolder.Write(folder, [Fails("pie/a"), Fails("pie/b")], "#13", Mermaid);

			File.ReadAllLines(Path.Combine(folder, "pie.txt"))
				.Should()
				.Equal(
					"# The parity ledger of pie. corpus/ledger/README.md describes it.",
					"bug #13",
					"b\tL0",
					"b\tL1",
					"b\tL2",
					"bug owner/repo#7",
					"a\tL2",
					"by-design",
					"a\tL1",
					"out-of-scope",
					"a\tL0"
				);
			File.ReadAllText(Path.Combine(folder, BugCounts.FileName)).Should().Contain("pie\t4\n");
		});

	[Test]
	public void AMalformedLedgerIsReported()
	{
		var problems = new List<string>();
		var lines = new[] { "a\tL0", "bug", "bug 13", "bug #13", "a\tL3", "a\tL1", "a\tL1", "wontfix" };

		var ledger = Ledger.Parse("pie", "pie.txt", lines, problems);

		ledger.Entries.Should().Equal(new LedgerEntry("a", Level.L1, LedgerCategory.Bug, "#13"));
		problems
			.Should()
			.Equal(
				"pie.txt:1: a is not under a category",
				"pie.txt:2: bug is not by-design, out-of-scope or bug with an issue such as #13",
				"pie.txt:3: bug 13 is not by-design, out-of-scope or bug with an issue such as #13",
				"pie.txt:5: L3 is not L0, L1 or L2",
				"pie.txt:7: a L1 is listed twice",
				"pie.txt:8: wontfix is not by-design, out-of-scope or bug with an issue such as #13"
			);
	}

	[Test]
	public void BugCountsOfAnotherMermaidVersionAreReported() =>
		WithFolder(folder =>
		{
			LedgerFolder.Write(folder, [Fails("pie/a")], "#13", Mermaid);

			LedgerFolder
				.Problems(folder, [Fails("pie/a")], "12.4.0")
				.Should()
				.Equal("bugs.txt counts bugs for mermaid 12.3.0 and tools/oracle pins 12.4.0. Rewrite it with STM_WRITE_LEDGER");
		});

	private static CaseComparison Passes(string name) => Comparer.Compare(name, Oracle, Oracle);

	private static CaseComparison Fails(string name) => Comparer.Compare(name, Oracle, CaseFacts.Refused("no"));

	private static void WithFolder(Action<string> test)
	{
		var folder = Directory.CreateTempSubdirectory("stm-ledger-").FullName;
		try
		{
			test(folder);
		}
		finally
		{
			Directory.Delete(folder, recursive: true);
		}
	}
}
