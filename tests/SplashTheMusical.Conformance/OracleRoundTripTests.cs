namespace SplashTheMusical.Conformance;

public class OracleRoundTripTests
{
	/// <summary>The first step of <c>mise run oracle-roundtrip</c>, with <see cref="OracleRoundTrip.FolderVariable"/> set. Otherwise it writes nothing.</summary>
	[Test]
	public async Task WritesTheCasesWhenAsked()
	{
		if (Environment.GetEnvironmentVariable(OracleRoundTrip.FolderVariable) is not { Length: > 0 } folder)
		{
			return;
		}

		var written = OracleRoundTrip.WriteCases(CorpusCheck.CorpusRoot, folder);

		await Assert.That(written).IsGreaterThan(0);
	}

	/// <summary>The step of <c>mise run oracle-roundtrip</c> after the oracle has written the facts of the folder, before it runs on <c>&lt;folder&gt;/original</c>.</summary>
	[Test]
	public async Task CopiesTheDifferingOriginalsWhenAsked()
	{
		if (Environment.GetEnvironmentVariable(OracleRoundTrip.FolderVariable) is not { Length: > 0 } folder)
		{
			return;
		}

		OracleRoundTrip.CopyDifferingOriginals(CorpusCheck.CorpusRoot, folder);

		await Assert.That(Directory.Exists(Path.Combine(folder, OracleRoundTrip.OriginalFolder))).IsTrue();
	}

	/// <summary>The last step of <c>mise run oracle-roundtrip</c>, after the oracle has written the facts of <c>&lt;folder&gt;/original</c>.</summary>
	[Test]
	public async Task ComparesTheFactsWhenAsked()
	{
		if (Environment.GetEnvironmentVariable(OracleRoundTrip.FolderVariable) is not { Length: > 0 } folder)
		{
			return;
		}

		await Assert.That(string.Join("\n", OracleRoundTrip.Problems(CorpusCheck.CorpusRoot, folder))).IsEmpty();
	}

	[Test]
	public async Task NamesEachCaseTheOracleReadsDifferentlyOrRefuses()
	{
		var root = Directory.CreateTempSubdirectory("stm-roundtrip-").FullName;
		try
		{
			var corpus = Path.Combine(root, "corpus");
			var folder = Path.Combine(root, "out");
			Write(corpus, "flowchart/same", "accept\tyes\nnode\tA\tA\trect\n");
			Write(corpus, "flowchart/other", "accept\tyes\nnode\tA\tA\trect\n");
			Write(corpus, "flowchart/refused", "accept\tyes\n");
			Write(Path.Combine(folder, OracleRoundTrip.CorpusFolder), "flowchart/same", "# mermaid 12.3.0\n# source 0\naccept\tyes\nnode\tA\tA\trect\n");
			Write(Path.Combine(folder, OracleRoundTrip.CorpusFolder), "flowchart/other", "accept\tyes\nnode\tA\tB\trect\n");
			Write(Path.Combine(folder, OracleRoundTrip.CorpusFolder), "flowchart/refused", "# error Parse error\naccept\tno\n");
			Write(Path.Combine(folder, OracleRoundTrip.CorpusFolder), "flowchart/unread", null);
			Write(Path.Combine(folder, OracleRoundTrip.RandomFolder), "flowchart/0001", "accept\tyes\n");
			Write(Path.Combine(folder, OracleRoundTrip.RandomFolder), "flowchart/0002", "# error Lexical error\naccept\tno\n");

			var problems = OracleRoundTrip.Problems(corpus, folder);

			await Assert.That(string.Join("\n", problems)).IsEqualTo(
				"corpus/flowchart/other: mermaid.js reads the written text differently\n  expected node\tA\tA\trect\n    actual node\tA\tB\trect\n"
				+ "corpus/flowchart/refused: mermaid.js refused the written text: Parse error\n"
				+ "corpus/flowchart/unread: the oracle wrote no facts\n"
				+ "random/flowchart/0002: mermaid.js refused the written text: Lexical error");
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Test]
	public async Task ComparesADifferingCaseWithTheOraclesFactsOfItsOriginalOnTheSameMachine()
	{
		var root = Directory.CreateTempSubdirectory("stm-roundtrip-").FullName;
		try
		{
			var corpus = Path.Combine(root, "corpus");
			var folder = Path.Combine(root, "out");
			var written = Path.Combine(folder, OracleRoundTrip.CorpusFolder);
			var original = Path.Combine(folder, OracleRoundTrip.OriginalFolder);
			Write(corpus, "flowchart/same", "accept\tyes\nnode\tA\tA\trect\n");
			Write(corpus, "flowchart/fonts", "accept\tyes\nedge-label\tA wraps\n");
			Write(corpus, "flowchart/writer", "accept\tyes\nnode\tA\tA\trect\n");
			Write(corpus, "flowchart/unread", "accept\tyes\nnode\tA\tA\trect\n");
			Write(written, "flowchart/same", "accept\tyes\nnode\tA\tA\trect\n");
			Write(written, "flowchart/fonts", "accept\tyes\nedge-label\tA\n");
			Write(written, "flowchart/writer", "accept\tyes\nnode\tA\tB\trect\n");
			Write(written, "flowchart/unread", "accept\tyes\nnode\tA\tC\trect\n");
			Directory.CreateDirectory(Path.Combine(original, "stale"));

			var copied = OracleRoundTrip.CopyDifferingOriginals(corpus, folder);

			await Assert.That(copied).IsEqualTo(3);
			await Assert.That(string.Join(",", CorpusCheck.Cases(original))).IsEqualTo("flowchart/fonts,flowchart/unread,flowchart/writer");
			await Assert.That(Directory.Exists(Path.Combine(original, "stale"))).IsFalse();

			File.WriteAllText(Path.Combine(original, "flowchart/fonts.facts.txt"), "accept\tyes\nedge-label\tA\n");
			File.WriteAllText(Path.Combine(original, "flowchart/writer.facts.txt"), "accept\tyes\nnode\tA\tA\trect\n");

			await Assert.That(string.Join("\n", OracleRoundTrip.Problems(corpus, folder))).IsEqualTo(
				"original/flowchart/unread: the oracle wrote no facts\n"
				+ "corpus/flowchart/writer: mermaid.js reads the written text differently\n  expected node\tA\tA\trect\n    actual node\tA\tB\trect");
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Test]
	public async Task FailsWhenNothingWasWritten()
	{
		var root = Directory.CreateTempSubdirectory("stm-roundtrip-").FullName;
		try
		{
			var problems = OracleRoundTrip.Problems(root, Path.Combine(root, "out"));

			await Assert.That(problems.Count).IsEqualTo(1);
			await Assert.That(problems[0]).Contains("no written cases");
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	/// <summary>A case's .mmd, and its facts file unless <paramref name="facts"/> is <c>null</c>.</summary>
	private static void Write(string folder, string name, string? facts)
	{
		var path = Path.Combine(folder, name);
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.WriteAllText(path + ".mmd", "flowchart\n");
		if (facts is not null)
		{
			File.WriteAllText(path + ".facts.txt", facts);
		}
	}
}
