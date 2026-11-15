using AwesomeAssertions;

namespace SplashTheMusical.Conformance;

public class CorpusTests
{
	[Test]
	public void EveryCaseHasOracleOutputOfItsCurrentText() =>
		CorpusCheck.Problems(CorpusCheck.CorpusRoot, CorpusCheck.PinnedMermaid()).Should().BeEmpty();

	[Test]
	public void TheCorpusHoldsEveryCaseOfMermaider() =>
		CorpusCheck.Cases(CorpusCheck.CorpusRoot).Count.Should().BeGreaterThanOrEqualTo(4364);

	[Test]
	public void AnEditedCaseAMissingFactsFileAndAFactsFileWithoutACaseAreReported()
	{
		var corpus = Directory.CreateTempSubdirectory("stm-corpus-").FullName;
		try
		{
			var accepted = Copy(corpus, Path.Combine("pie", "docs-pie-001"));
			var refused = Copy(corpus, Path.Combine("pie", "parser-pie-010"));
			var untouched = Copy(corpus, Path.Combine("flowchart", "docs-flowchart-001"));
			CorpusCheck.Problems(corpus, CorpusCheck.PinnedMermaid()).Should().BeEmpty();

			File.AppendAllText(Path.Combine(corpus, accepted + ".mmd"), "\n%% edited\n");
			File.Delete(Path.Combine(corpus, refused + ".facts.txt"));
			File.Delete(Path.Combine(corpus, untouched + ".mmd"));

			CorpusCheck
				.Problems(corpus, CorpusCheck.PinnedMermaid())
				.Should()
				.SatisfyRespectively(
					p => p.Should().StartWith(accepted + ".facts.txt: source "),
					p => p.Should().StartWith(accepted + ".boxes.txt: source "),
					p => p.Should().Be(refused + ": no facts file"),
					p => p.Should().Be(untouched + ".boxes.txt: no case"),
					p => p.Should().Be(untouched + ".dagre.txt: no case"),
					p => p.Should().Be(untouched + ".facts.txt: no case")
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
