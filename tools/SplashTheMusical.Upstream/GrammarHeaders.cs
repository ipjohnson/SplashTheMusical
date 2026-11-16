namespace SplashTheMusical.Upstream;

/// <summary>
/// Reads the grammar header of STM's parser sources. A parser source starts with one line per upstream file it
/// implements, such as <c>// Upstream grammar: packages/parser/src/language/info/info.langium</c>. The path is relative
/// to the root of mermaid-js/mermaid.
/// </summary>
public static class GrammarHeaders
{
	public const string Prefix = "// Upstream grammar: ";

	/// <summary>The paths the header of <paramref name="source"/> names, in order. Empty when it has no header.</summary>
	public static List<string> Read(string source)
	{
		var paths = new List<string>();
		foreach (var line in source.AsSpan().EnumerateLines())
		{
			if (!line.StartsWith(Prefix, StringComparison.Ordinal))
			{
				break;
			}

			paths.Add(line[Prefix.Length..].Trim().ToString());
		}

		return paths;
	}

	/// <summary>The parser sources of the core: every <c>*Parser.cs</c> under src/SplashTheMusical/Diagrams.</summary>
	public static List<string> ParserSources(string repository)
	{
		var folder = Path.Combine(repository, "src", "SplashTheMusical", "Diagrams");
		return Directory.Exists(folder)
			? [.. Directory.EnumerateFiles(folder, "*Parser.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal)]
			: [];
	}

	/// <summary>Each upstream path a source under src/SplashTheMusical names, with the sources that name it, relative to the repository.</summary>
	public static SortedDictionary<string, List<string>> Map(string repository)
	{
		var map = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
		var folder = Path.Combine(repository, "src", "SplashTheMusical");
		foreach (var file in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
		{
			foreach (var path in Read(File.ReadAllText(file)))
			{
				if (!map.TryGetValue(path, out var sources))
				{
					map[path] = sources = [];
				}

				sources.Add(Path.GetRelativePath(repository, file).Replace('\\', '/'));
			}
		}

		return map;
	}
}
