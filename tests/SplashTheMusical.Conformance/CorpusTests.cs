namespace SplashTheMusical.Conformance;

public class CorpusTests
{
	[Test]
	public async Task EveryCaseHasOracleOutputOfItsCurrentText() =>
		await Assert.That(CorpusCheck.Problems(CorpusCheck.CorpusRoot, CorpusCheck.PinnedMermaid())).IsEmpty();

	[Test]
	public async Task TheCorpusHoldsEveryCaseOfMermaider() =>
		await Assert.That(CorpusCheck.Cases(CorpusCheck.CorpusRoot).Count).IsGreaterThanOrEqualTo(4364);

	[Test]
	public async Task AnEditedCaseAMissingFactsFileAndAFactsFileWithoutACaseAreReported()
	{
		var corpus = Directory.CreateTempSubdirectory("stm-corpus-").FullName;
		try
		{
			var accepted = Copy(corpus, Path.Combine("pie", "docs-pie-001"));
			var refused = Copy(corpus, Path.Combine("pie", "parser-pie-010"));
			var untouched = Copy(corpus, Path.Combine("flowchart", "docs-flowchart-001"));
			await Assert.That(CorpusCheck.Problems(corpus, CorpusCheck.PinnedMermaid())).IsEmpty();

			File.AppendAllText(Path.Combine(corpus, accepted + ".mmd"), "\n%% edited\n");
			File.Delete(Path.Combine(corpus, refused + ".facts.txt"));
			File.Delete(Path.Combine(corpus, untouched + ".mmd"));

			var problems = CorpusCheck.Problems(corpus, CorpusCheck.PinnedMermaid());
			await Assert.That(problems).Count().IsEqualTo(6);
			await Assert.That(problems[0]).StartsWith(accepted + ".facts.txt: source ");
			await Assert.That(problems[1]).StartsWith(accepted + ".boxes.txt: source ");
			await LineAssert.Equal(
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
