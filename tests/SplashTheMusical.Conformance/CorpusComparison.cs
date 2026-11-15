using System.Text;

namespace SplashTheMusical.Conformance;

/// <summary>
/// Compares a provider with the committed oracle facts of every case of a corpus folder, with the tolerances of its
/// <c>tolerances</c> folder. <see cref="Tolerance.Start"/> stands in for a type without a valid file, which
/// <see cref="Tolerance.Problems"/> reports.
/// </summary>
public static class CorpusComparison
{
	public static IReadOnlyList<CaseComparison> Run(string corpus, IFactsProvider provider)
	{
		var tolerances = Tolerances(corpus);
		return [.. CorpusCheck.Cases(corpus).Select(name => Run(corpus, name, provider, tolerances))];
	}

	public static CaseComparison Run(string corpus, string name, IFactsProvider provider) => Run(corpus, name, provider, Tolerances(corpus));

	// The oracle reads the text with any byte order mark, so the provider gets it too.
	public static string Source(string corpus, string name) => Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(corpus, name + ".mmd")));

	private static CaseComparison Run(string corpus, string name, IFactsProvider provider, IReadOnlyDictionary<string, Tolerance> tolerances)
	{
		var type = name.Split('/', '\\')[0];
		return Comparer.Compare(name, OracleFiles.Read(corpus, name), provider.Facts(Source(corpus, name)), tolerances.GetValueOrDefault(type));
	}

	private static IReadOnlyDictionary<string, Tolerance> Tolerances(string corpus) => Tolerance.Read(Path.Combine(corpus, "tolerances"), []);
}
