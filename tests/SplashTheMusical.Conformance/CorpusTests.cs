namespace SplashTheMusical.Conformance;

public class CorpusTests
{
	[Test]
	public void EveryCaseHasOracleOutputOfItsCurrentText()
	{
		var corpusProblems = CorpusCheck.Problems(CorpusCheck.CorpusRoot, CorpusCheck.PinnedMermaid());
		Verify(corpusProblems.Count == 0, $"{string.Join('\n', corpusProblems)}");
	}

	[Test]
	public void TheCorpusHoldsEveryCaseOfMermaider()
	{
		var cases = CorpusCheck.Cases(CorpusCheck.CorpusRoot).Count;
		Verify(() => cases >= 4364);
	}

	[Test]
	public void AnEditedCaseAMissingFactsFileAndAFactsFileWithoutACaseAreReported()
	{
		var corpus = Directory.CreateTempSubdirectory("stm-corpus-").FullName;
		try
		{
			var accepted = Copy(corpus, Path.Combine("pie", "docs-pie-001"));
			var refused = Copy(corpus, Path.Combine("pie", "parser-pie-010"));
			var untouched = Copy(corpus, Path.Combine("flowchart", "docs-flowchart-001"));
			var corpusProblems = CorpusCheck.Problems(corpus, CorpusCheck.PinnedMermaid());
			Verify(corpusProblems.Count == 0, $"{string.Join('\n', corpusProblems)}");

			File.AppendAllText(Path.Combine(corpus, accepted + ".mmd"), "\n%% edited\n");
			File.Delete(Path.Combine(corpus, refused + ".facts.txt"));
			File.Delete(Path.Combine(corpus, untouched + ".mmd"));

			var problems = CorpusCheck.Problems(corpus, CorpusCheck.PinnedMermaid());
			Verify(() => problems.Count() == 6);
			Verify(() => problems[0].StartsWith(accepted + ".facts.txt: source ", StringComparison.Ordinal));
			Verify(() => problems[1].StartsWith(accepted + ".boxes.txt: source ", StringComparison.Ordinal));
			LineAssert.Equal(
				problems.Skip(2),
				refused + ": no facts file",
				untouched + ".boxes.txt: no case",
				untouched + ".dagre.txt: no case",
				untouched + ".facts.txt: no case"
			);
		}
		finally
		{
			Directory.Delete(corpus, recursive: true);
		}
	}

	// Copies a case of the real corpus and its oracle output into another corpus folder.
	private static string Copy(string corpus, string name)
	{
		_ = Directory.CreateDirectory(Path.Combine(corpus, Path.GetDirectoryName(name)!));
		foreach (var suffix in new[] { ".mmd", ".facts.txt", ".boxes.txt", ".dagre.txt" })
		{
			var from = Path.Combine(CorpusCheck.CorpusRoot, name + suffix);
			if (File.Exists(from))
			{
				File.Copy(from, Path.Combine(corpus, name + suffix));
			}
		}

		return name;
	}
}
