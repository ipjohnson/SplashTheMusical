using System.Text;

namespace SplashTheMusical.Conformance;

/// <summary>Compares a provider with the committed oracle facts of every case of a corpus folder.</summary>
public static class CorpusComparison
{
	public static IReadOnlyList<CaseComparison> Run(string corpus, IFactsProvider provider) =>
		[.. CorpusCheck.Cases(corpus).Select(name => Run(corpus, name, provider))];

	public static CaseComparison Run(string corpus, string name, IFactsProvider provider)
	{
		// The oracle reads the text with any byte order mark, so the provider gets it too.
		var source = Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(corpus, name + ".mmd")));
		return Comparer.Compare(name, OracleFiles.Read(corpus, name), provider.Facts(source));
	}
}
