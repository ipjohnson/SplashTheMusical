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
	public void WritesTheSvgOfEachCaseTheProviderDraws()
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

			Verify(() => written == 1);
			Verify(() => File.ReadAllText(Path.Combine(folder, "pie", "a.svg")) == "<svg/>");
			Verify(!(File.Exists(Path.Combine(folder, "xychart", "a.svg"))));
			Verify(!(Directory.Exists(Path.Combine(folder, "old"))));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	/// <summary><c>mise run stm-svg</c> runs this with <see cref="StmSvgs.FolderVariable"/> set. Otherwise it writes nothing.</summary>
	[Test]
	public void WritesStmsSvgsWhenAsked()
	{
		if (Environment.GetEnvironmentVariable(StmSvgs.FolderVariable) is not { Length: > 0 } folder)
		{
			return;
		}

		var written = StmSvgs.Write(CorpusCheck.CorpusRoot, new StmFactsProvider(), folder);

		Verify(() => written == 1606);
	}
}
