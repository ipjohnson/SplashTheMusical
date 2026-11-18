namespace SplashTheMusical.Conformance;

public class StmSvgsTests
{
	private sealed class PieOnly : IFactsProvider
	{
		public string Name => "pie-only";

		public CaseFacts Facts(string source) =>
			source.StartsWith("pie", StringComparison.Ordinal) ? new CaseFacts(true, null, [], [], Svg: "<svg/>") : CaseFacts.Refused("no");
	}

	[Test]
	public async Task WritesTheSvgOfEachCaseTheProviderDraws()
	{
		var root = Directory.CreateTempSubdirectory("stm-svgs-").FullName;
		try
		{
			var corpus = Path.Combine(root, "corpus");
			var folder = Path.Combine(root, "svg");
			Directory.CreateDirectory(Path.Combine(corpus, "pie"));
			Directory.CreateDirectory(Path.Combine(corpus, "xychart"));
			File.WriteAllText(Path.Combine(corpus, "pie", "a.mmd"), "pie\n");
			File.WriteAllText(Path.Combine(corpus, "xychart", "a.mmd"), "xychart\n");
			Directory.CreateDirectory(Path.Combine(folder, "old"));
			File.WriteAllText(Path.Combine(folder, "old", "left-over.svg"), "<svg/>");

			var written = StmSvgs.Write(corpus, new PieOnly(), folder);

			await Assert.That(written).IsEqualTo(1);
			await Assert.That(File.ReadAllText(Path.Combine(folder, "pie", "a.svg"))).IsEqualTo("<svg/>");
			await Assert.That(File.Exists(Path.Combine(folder, "xychart", "a.svg"))).IsFalse();
			await Assert.That(Directory.Exists(Path.Combine(folder, "old"))).IsFalse();
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	/// <summary><c>mise run stm-svg</c> runs this with <see cref="StmSvgs.FolderVariable"/> set. Otherwise it writes nothing.</summary>
	[Test]
	public async Task WritesStmsSvgsWhenAsked()
	{
		if (Environment.GetEnvironmentVariable(StmSvgs.FolderVariable) is not { Length: > 0 } folder)
		{
			return;
		}

		var written = StmSvgs.Write(CorpusCheck.CorpusRoot, new StmFactsProvider(), folder);

		await Assert.That(written).IsEqualTo(1166);
	}
}
