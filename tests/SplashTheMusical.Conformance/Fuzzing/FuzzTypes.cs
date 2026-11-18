namespace SplashTheMusical.Conformance.Fuzzing;

/// <summary>The diagram types the fuzzers cover, and the corpus cases and upstream grammar of each.</summary>
public static class FuzzTypes
{
	private const string GrammarHeader = "// Upstream grammar: ";

	/// <summary>The corpus folders with a case STM parses, which are those of the types of <see cref="Mermaid.CoreModules"/>.</summary>
	public static IReadOnlyList<string> Folders(string corpus)
	{
		var stm = new Mermaid(new MermaidOptions { Limits = ResourceLimits.Unlimited });
		var folders = new List<string>();
		foreach (var directory in Directory.EnumerateDirectories(corpus).Order(StringComparer.Ordinal))
		{
			foreach (var file in Directory.EnumerateFiles(directory, "*.mmd"))
			{
				if (stm.TryParse(File.ReadAllText(file), out _, out _))
				{
					folders.Add(Path.GetFileName(directory));
					break;
				}
			}
		}

		return folders;
	}

	/// <summary>The text of every case of a corpus folder, in name order.</summary>
	public static IReadOnlyList<string> Sources(string corpus, string folder) =>
		[.. CorpusCheck.Cases(Path.Combine(corpus, folder)).Select(name => CorpusComparison.Source(Path.Combine(corpus, folder), name))];

	/// <summary>
	/// The path in mermaid-js/mermaid of the grammar the type's parser implements: the first <c>// Upstream grammar:</c>
	/// line of the sources in <c>src/SplashTheMusical/Diagrams/&lt;Type&gt;</c>, or null when it has none.
	/// </summary>
	public static string? GrammarPath(string repositoryRoot, string folder)
	{
		var diagrams = Path.Combine(repositoryRoot, "src", "SplashTheMusical", "Diagrams");
		var directory = Directory.EnumerateDirectories(diagrams).FirstOrDefault(d => string.Equals(Path.GetFileName(d), folder, StringComparison.OrdinalIgnoreCase));
		if (directory is null)
		{
			return null;
		}

		foreach (var file in Directory.EnumerateFiles(directory, "*.cs").Order(StringComparer.Ordinal))
		{
			foreach (var line in File.ReadLines(file))
			{
				if (!line.StartsWith(GrammarHeader, StringComparison.Ordinal))
				{
					break;
				}

				return line[GrammarHeader.Length..].Trim();
			}
		}

		return null;
	}

	/// <summary>The grammar at <paramref name="path"/> in a mermaid checkout, read as jison or langium by its extension.</summary>
	public static Grammar ReadGrammar(string checkout, string path)
	{
		var file = Path.Combine(checkout, path);
		return Path.GetExtension(path) == ".langium" ? LangiumGrammar.Read(file) : JisonGrammar.Read(File.ReadAllText(file));
	}
}
