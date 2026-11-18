namespace SplashTheMusical.Conformance;

public class OracleRoundTripTests
{
	/// <summary>The first step of <c>mise run oracle-roundtrip</c>, with <see cref="OracleRoundTrip.FolderVariable"/> set. Otherwise it writes nothing.</summary>
	[Test]
	public void WritesTheCasesWhenAsked()
	{
		if (Environment.GetEnvironmentVariable(OracleRoundTrip.FolderVariable) is not { Length: > 0 } folder)
		{
			return;
		}

		var written = OracleRoundTrip.WriteCases(CorpusCheck.CorpusRoot, folder);

		Verify(written > 0, $"was {written}");
	}

	/// <summary>The step of <c>mise run oracle-roundtrip</c> after the oracle has written the facts of the folder, before it runs on <c>&lt;folder&gt;/original</c>.</summary>
	[Test]
	public void CopiesTheDifferingOriginalsWhenAsked()
	{
		if (Environment.GetEnvironmentVariable(OracleRoundTrip.FolderVariable) is not { Length: > 0 } folder)
		{
			return;
		}

		OracleRoundTrip.CopyDifferingOriginals(CorpusCheck.CorpusRoot, folder);

		Verify(Directory.Exists(Path.Combine(folder, OracleRoundTrip.OriginalFolder)));
	}

	/// <summary>The last step of <c>mise run oracle-roundtrip</c>, after the oracle has written the facts of <c>&lt;folder&gt;/original</c>.</summary>
	[Test]
	public void ComparesTheFactsWhenAsked()
	{
		if (Environment.GetEnvironmentVariable(OracleRoundTrip.FolderVariable) is not { Length: > 0 } folder)
		{
			return;
		}

		var roundTripProblems = OracleRoundTrip.Problems(CorpusCheck.CorpusRoot, folder);
		Verify(!roundTripProblems.Any(), $"{string.Join("\n", roundTripProblems)}");
	}

	[Test]
	public void NamesEachCaseTheOracleReadsDifferentlyOrRefuses()
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

			Verify(string.Join("\n", problems) == "corpus/flowchart/other: mermaid.js reads the written text differently\n  expected node\tA\tA\trect\n    actual node\tA\tB\trect\n"
				+ "corpus/flowchart/refused: mermaid.js refused the written text: Parse error\n"
				+ "corpus/flowchart/unread: the oracle wrote no facts\n"
				+ "random/flowchart/0002: mermaid.js refused the written text: Lexical error", $"was {string.Join("\n", problems)}");
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Test]
	public void ComparesADifferingCaseWithTheOraclesFactsOfItsOriginalOnTheSameMachine()
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

			Verify(copied == 3, $"was {copied}");
			Verify(string.Join(",", CorpusCheck.Cases(original).Select(n => n.Replace('\\', '/'))) == "flowchart/fonts,flowchart/unread,flowchart/writer", $"was {string.Join(",", CorpusCheck.Cases(original).Select(n => n.Replace('\\', '/')))}");
			Verify(!(Directory.Exists(Path.Combine(original, "stale"))));

			File.WriteAllText(Path.Combine(original, "flowchart/fonts.facts.txt"), "accept\tyes\nedge-label\tA\n");
			File.WriteAllText(Path.Combine(original, "flowchart/writer.facts.txt"), "accept\tyes\nnode\tA\tA\trect\n");

			Verify(string.Join("\n", OracleRoundTrip.Problems(corpus, folder)) == "original/flowchart/unread: the oracle wrote no facts\n"
				+ "corpus/flowchart/writer: mermaid.js reads the written text differently\n  expected node\tA\tA\trect\n    actual node\tA\tB\trect", $"was {string.Join("\n", OracleRoundTrip.Problems(corpus, folder))}");
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Test]
	public void FailsWhenNothingWasWritten()
	{
		var root = Directory.CreateTempSubdirectory("stm-roundtrip-").FullName;
		try
		{
			var problems = OracleRoundTrip.Problems(root, Path.Combine(root, "out"));

			Verify(problems.Count == 1, $"was {problems.Count}");
			Verify(problems[0].Contains("no written cases"), $"{problems[0]}");
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
