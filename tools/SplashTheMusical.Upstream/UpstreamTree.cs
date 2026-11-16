namespace SplashTheMusical.Upstream;

/// <summary>
/// What the delta report reads from a checkout of mermaid-js/mermaid: the diagram folders and detector ids, the grammar
/// files, the edge markers and the layout algorithms. A file or folder that is missing reads as empty.
/// </summary>
public sealed class UpstreamTree(string checkout)
{
	public const string DiagramsFolder = "packages/mermaid/src/diagrams";

	public const string LangiumFolder = "packages/parser/src/language";

	public const string MarkersFile = "packages/mermaid/src/rendering-util/rendering-elements/markers.js";

	public const string RenderFile = "packages/mermaid/src/rendering-util/render.ts";

	public const string ElkAlgorithmsFile = "packages/mermaid/src/rendering-util/layout-algorithms/elk/algorithms.ts";

	public const string LayoutFolder = "packages/mermaid/src/rendering-util/layout-algorithms";

	/// <summary>The folders under layout-algorithms that hold shared code rather than a layout.</summary>
	private static readonly string[] SharedLayoutFolders = ["common", "layout-utils"];

	public string Checkout { get; } = checkout;

	/// <summary>The folders under packages/mermaid/src/diagrams.</summary>
	public SortedSet<string> DiagramFolders() => Folders(DiagramsFolder);

	/// <summary>The ids the detectors register: each <c>const id = '...'</c> in a .ts file under the diagrams folder.</summary>
	public SortedSet<string> DiagramIds()
	{
		var ids = new SortedSet<string>(StringComparer.Ordinal);
		foreach (var file in Files(DiagramsFolder, "*.ts").Where(f => !f.EndsWith(".spec.ts", StringComparison.Ordinal)))
		{
			ids.UnionWith(Quoted(File.ReadAllText(Path.Combine(Checkout, file)), "const id = "));
		}

		return ids;
	}

	/// <summary>The jison grammars under the diagrams folder and the langium files of packages/parser, relative to the checkout.</summary>
	public SortedSet<string> GrammarFiles()
	{
		var files = new SortedSet<string>(Files(DiagramsFolder, "*.jison"), StringComparer.Ordinal);
		files.UnionWith(Files(LangiumFolder, "*.langium"));
		return files;
	}

	/// <summary>The text of a file of the checkout, or null when it is missing.</summary>
	public string? Read(string path)
	{
		var full = Path.Combine(Checkout, path);
		return File.Exists(full) ? File.ReadAllText(full) : null;
	}

	/// <summary>The edge markers: the names in the <c>markers</c> object of markers.js.</summary>
	public SortedSet<string> Markers()
	{
		var names = new SortedSet<string>(StringComparer.Ordinal);
		if (Block(Read(MarkersFile), "const markers = {", '}') is not { } block)
		{
			return names;
		}

		foreach (var part in block.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			var colon = part.IndexOf(':', StringComparison.Ordinal);
			names.Add(colon < 0 ? part : part[..colon].Trim());
		}

		return names;
	}

	/// <summary>The layout names render.ts registers, with the ELK algorithms it registers through <c>ELK_ALGORITHMS</c>.</summary>
	public SortedSet<string> Layouts()
	{
		var names = new SortedSet<string>(Quoted(Read(RenderFile) ?? "", "name: "), StringComparer.Ordinal);
		if (Block(Read(ElkAlgorithmsFile), "ELK_ALGORITHMS = [", ']') is { } algorithms)
		{
			names.UnionWith(Quoted(algorithms, ""));
		}

		return names;
	}

	/// <summary>The folders under layout-algorithms that hold a layout, registered or not.</summary>
	public SortedSet<string> LayoutFolders()
	{
		var folders = Folders(LayoutFolder);
		folders.ExceptWith(SharedLayoutFolders);
		return folders;
	}

	private SortedSet<string> Folders(string path)
	{
		var full = Path.Combine(Checkout, path);
		return Directory.Exists(full)
			? new(Directory.EnumerateDirectories(full).Select(Path.GetFileName).OfType<string>(), StringComparer.Ordinal)
			: new(StringComparer.Ordinal);
	}

	private IEnumerable<string> Files(string path, string pattern)
	{
		var full = Path.Combine(Checkout, path);
		return Directory.Exists(full)
			? Directory.EnumerateFiles(full, pattern, SearchOption.AllDirectories)
				.Where(f => !f.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
				.Select(f => Path.GetRelativePath(Checkout, f).Replace('\\', '/'))
			: [];
	}

	/// <summary>The text between <paramref name="start"/> and the first <paramref name="end"/> after it.</summary>
	private static string? Block(string? text, string start, char end)
	{
		var open = text?.IndexOf(start, StringComparison.Ordinal) ?? -1;
		if (open < 0)
		{
			return null;
		}

		open += start.Length;
		var close = text!.IndexOf(end, open);
		return close < 0 ? null : text[open..close];
	}

	/// <summary>Each single-quoted string that directly follows <paramref name="before"/>.</summary>
	private static IEnumerable<string> Quoted(string text, string before)
	{
		var marker = before + "'";
		for (var at = text.IndexOf(marker, StringComparison.Ordinal); at >= 0; at = text.IndexOf(marker, at, StringComparison.Ordinal))
		{
			at += marker.Length;
			var close = text.IndexOf('\'', at);
			if (close < 0)
			{
				yield break;
			}

			yield return text[at..close];
			at = close + 1;
		}
	}
}
