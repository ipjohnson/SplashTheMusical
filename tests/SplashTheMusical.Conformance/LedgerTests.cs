namespace SplashTheMusical.Conformance;

public class LedgerTests
{
	private const string Mermaid = "12.3.0";

	private static readonly CaseFacts Oracle = new(true, null, ["label\tA", "node\tA\tA\trect", "type\tpie"], ["box\tnode\tA\t0\t0\t10\t10"], "canvas\t0\t0\t10\t10");

	[Test]
	public async Task StmDiffersOnlyWhereTheCommittedLedgersListABug()
	{
		var results = CorpusComparison.Run(CorpusCheck.CorpusRoot, new StmFactsProvider());
		if (Environment.GetEnvironmentVariable(LedgerFolder.WriteVariable) is { Length: > 0 } issue)
		{
			LedgerFolder.Write(LedgerFolder.Root, results, issue, CorpusCheck.PinnedMermaid());
		}

		await Assert.That(LedgerFolder.Problems(LedgerFolder.Root, results, CorpusCheck.PinnedMermaid())).IsEmpty();
		var problems = new List<string>();
		await Assert.That(LedgerFolder.Read(LedgerFolder.Root, problems).SelectMany(l => l.Entries)).All(e => e.Category == LedgerCategory.Bug);
	}

	[Test]
	public Task AnUnlistedDifferenceFails() =>
		WithFolder(async folder =>
		{
			LedgerFolder.Write(folder, [Passes("pie/a"), Fails("pie/b")], "#13", Mermaid);

			await LineAssert.Equal(
				LedgerFolder.Problems(folder, [Fails("pie/a"), Fails("pie/b")], Mermaid),
				"pie/a L0: differs from mermaid.js and pie.txt does not list it",
				"pie/a L1: differs from mermaid.js and pie.txt does not list it",
				"pie/a L2: differs from mermaid.js and pie.txt does not list it",
				"pie/a L3: differs from mermaid.js and pie.txt does not list it"
			);
		});

	[Test]
	public Task AListedDifferenceThatNoLongerOccursFails() =>
		WithFolder(async folder =>
		{
			LedgerFolder.Write(folder, [Passes("pie/a"), Fails("pie/b")], "#13", Mermaid);

			await LineAssert.Equal(
				LedgerFolder.Problems(folder, [Passes("pie/a"), Passes("pie/b")], Mermaid),
				"pie/b L0: pie.txt lists it and it no longer differs",
				"pie/b L1: pie.txt lists it and it no longer differs",
				"pie/b L2: pie.txt lists it and it no longer differs",
				"pie/b L3: pie.txt lists it and it no longer differs"
			);
		});

	[Test]
	public Task RaisingABugCountFailsUntilTheMermaidVersionChanges() =>
		WithFolder(async folder =>
		{
			LedgerFolder.Write(folder, [Passes("pie/a"), Fails("pie/b")], "#13", Mermaid);
			var worse = new[] { Fails("pie/a"), Fails("pie/b") };

			LedgerFolder.Write(folder, worse, "#13", Mermaid);

			await LineAssert.Equal(
				LedgerFolder.Problems(folder, worse, Mermaid),
				"pie: 8 bugs, bugs.txt allows 4. The count may only go up when the mermaid version changes"
			);

			LedgerFolder.Write(folder, worse, "#13", "12.4.0");

			await Assert.That(LedgerFolder.Problems(folder, worse, "12.4.0")).IsEmpty();
		});

	[Test]
	public Task ALowerBugCountIsWrittenAndThenHeld() =>
		WithFolder(async folder =>
		{
			LedgerFolder.Write(folder, [Fails("pie/a"), Fails("pie/b")], "#13", Mermaid);
			File.WriteAllText(Path.Combine(folder, "pie.txt"), "bug #13\nb\tL0\nb\tL1\nb\tL2\nb\tL3\n");
			var better = new[] { Passes("pie/a"), Fails("pie/b") };

			await LineAssert.Equal(LedgerFolder.Problems(folder, better, Mermaid), "pie: 4 bugs, bugs.txt records 8. Lower it to 4");

			LedgerFolder.Write(folder, better, "#13", Mermaid);

			await Assert.That(File.ReadAllText(Path.Combine(folder, BugCounts.FileName))).Contains("pie\t4\n");
			await Assert.That(LedgerFolder.Problems(folder, better, Mermaid)).IsEmpty();
		});

	[Test]
	public Task ARewriteKeepsTheCategoryOfAListedDifference() =>
		WithFolder(async folder =>
		{
			File.WriteAllText(Path.Combine(folder, "pie.txt"), "out-of-scope\na\tL0\nby-design\na\tL1\nbug owner/repo#7\na\tL2\n");

			LedgerFolder.Write(folder, [Fails("pie/a"), Fails("pie/b")], "#13", Mermaid);

			await LineAssert.Equal(
				File.ReadAllLines(Path.Combine(folder, "pie.txt")),
				"# The parity ledger of pie. corpus/ledger/README.md describes it.",
				"bug #13",
				"a\tL3",
				"b\tL0",
				"b\tL1",
				"b\tL2",
				"b\tL3",
				"bug owner/repo#7",
				"a\tL2",
				"by-design",
				"a\tL1",
				"out-of-scope",
				"a\tL0"
			);
			await Assert.That(File.ReadAllText(Path.Combine(folder, BugCounts.FileName))).Contains("pie\t6\n");
		});

	[Test]
	public async Task AMalformedLedgerIsReported()
	{
		var problems = new List<string>();
		var lines = new[] { "a\tL0", "bug", "bug 13", "bug #13", "a\tL4", "a\tL1", "a\tL1", "wontfix" };

		var ledger = Ledger.Parse("pie", "pie.txt", lines, problems);

		await Assert.That(ledger.Entries).Count().IsEqualTo(1);
		await Assert.That(ledger.Entries[0]).IsEqualTo(new LedgerEntry("a", Level.L1, LedgerCategory.Bug, "#13"));
		await LineAssert.Equal(
			problems,
			"pie.txt:1: a is not under a category",
			"pie.txt:2: bug is not by-design, out-of-scope or bug with an issue such as #13",
			"pie.txt:3: bug 13 is not by-design, out-of-scope or bug with an issue such as #13",
			"pie.txt:5: L4 is not L0, L1, L2 or L3",
			"pie.txt:7: a L1 is listed twice",
			"pie.txt:8: wontfix is not by-design, out-of-scope or bug with an issue such as #13"
		);
	}

	[Test]
	public Task BugCountsOfAnotherMermaidVersionAreReported() =>
		WithFolder(async folder =>
		{
			LedgerFolder.Write(folder, [Fails("pie/a")], "#13", Mermaid);

			await LineAssert.Equal(
				LedgerFolder.Problems(folder, [Fails("pie/a")], "12.4.0"),
				"bugs.txt counts bugs for mermaid 12.3.0 and tools/oracle pins 12.4.0. Rewrite it with STM_WRITE_LEDGER"
			);
		});

	private static CaseComparison Passes(string name) => Comparer.Compare(name, Oracle, Oracle);

	private static CaseComparison Fails(string name) => Comparer.Compare(name, Oracle, CaseFacts.Refused("no"));

	private static async Task WithFolder(Func<string, Task> test)
	{
		var folder = Directory.CreateTempSubdirectory("stm-ledger-").FullName;
		try
		{
			await test(folder);
		}
		finally
		{
			Directory.Delete(folder, recursive: true);
		}
	}
}
