namespace SplashTheMusical.Upstream;

/// <summary>
/// What the delta report reads from a checkout of mermaid: the diagram folders and detector ids, the grammar files, the
/// edge markers and the layout algorithms. A checkout of mermaid 12, mermaid-js/mermaid, keeps its diagrams in
/// packages/mermaid/src/diagrams. A checkout of mermaid 13, mermaid-js/mermaid-v13, has none, and keeps each diagram in
/// a packages/diagram-* package. A file or folder that is missing reads as empty.
/// </summary>
public sealed class UpstreamTree(string checkout)
{
	public const string DiagramsFolder = "packages/mermaid/src/diagrams";

	public const string LangiumFolder = "packages/parser/src/language";

	public const string MarkersFile = "packages/mermaid/src/rendering-util/rendering-elements/markers.js";

	public const string RenderFile = "packages/mermaid/src/rendering-util/render.ts";

	public const string ElkAlgorithmsFile = "packages/mermaid/src/rendering-util/layout-algorithms/elk/algorithms.ts";

	public const string LayoutFolder = "packages/mermaid/src/rendering-util/layout-algorithms";

	public const string PackagesFolder = "packages";

	public const string DiagramPackagePrefix = "diagram-";

	public const string LayoutPackagePrefix = "layout-";

	public const string DetectorsFile = "packages/mermaid/src/generated/detectors.gen.ts";

	public const string CoreLayoutFolder = "packages/core/src/layout";

	/// <summary>The folders under layout-algorithms that hold shared code rather than a layout.</summary>
	private static readonly string[] SharedLayoutFolders = ["common", "layout-utils"];

	/// <summary>The folders under 13's packages/core/src/layout that hold shared code: hops draws the line hops of every layout.</summary>
	private static readonly string[] SharedCoreLayoutFolders = ["hops"];

	public string Checkout { get; } = checkout;

	/// <summary>Whether the checkout has mermaid 13's layout: no packages/mermaid/src/diagrams.</summary>
	public bool IsV13 => !Directory.Exists(Path.Combine(Checkout, DiagramsFolder));

	/// <summary>The folders under packages/mermaid/src/diagrams, or in 13 the packages/diagram-* folders without the prefix.</summary>
	public SortedSet<string> DiagramFolders() => IsV13 ? Packages(DiagramPackagePrefix) : Folders(DiagramsFolder);

	/// <summary>
	/// The ids the detectors register: each <c>const id = '...'</c> in a .ts file under the diagrams folder, or in 13 each
	/// <c>type: '...'</c> of packages/mermaid/src/generated/detectors.gen.ts.
	/// </summary>
	public SortedSet<string> DiagramIds()
	{
		if (IsV13)
		{
			return new(Quoted(Read(DetectorsFile) ?? "", "type: "), StringComparer.Ordinal);
		}

		var ids = new SortedSet<string>(StringComparer.Ordinal);
		foreach (var file in Files(DiagramsFolder, "*.ts").Where(f => !f.EndsWith(".spec.ts", StringComparison.Ordinal)))
		{
			ids.UnionWith(Quoted(File.ReadAllText(Path.Combine(Checkout, file)), "const id = "));
		}

		return ids;
	}

	/// <summary>
	/// The grammar files, relative to the checkout: the jison grammars under the diagrams folder and the langium files of
	/// packages/parser, or in 13 the EBNF grammars in the grammar folder of each packages/diagram-*.
	/// </summary>
	public SortedSet<string> GrammarFiles()
	{
		if (IsV13)
		{
			return new(
				Packages(DiagramPackagePrefix)
					.SelectMany(name => Files($"{PackagesFolder}/{DiagramPackagePrefix}{name}/grammar", "*.ebnf", SearchOption.TopDirectoryOnly)),
				StringComparer.Ordinal
			);
		}

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

	/// <summary>The edge markers: the names in the <c>markers</c> object of markers.js, or null when there is no markers.js, as in 13.</summary>
	public SortedSet<string>? Markers()
	{
		if (Read(MarkersFile) is not { } markers)
		{
			return null;
		}

		var names = new SortedSet<string>(StringComparer.Ordinal);
		if (Block(markers, "const markers = {", '}') is not { } block)
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

	/// <summary>
	/// The layout names render.ts registers, with the ELK algorithms it registers through <c>ELK_ALGORITHMS</c>, or null
	/// when there is no render.ts, as in 13.
	/// </summary>
	public SortedSet<string>? Layouts()
	{
		if (Read(RenderFile) is not { } render)
		{
			return null;
		}

		var names = new SortedSet<string>(Quoted(render, "name: "), StringComparer.Ordinal);
		if (Block(Read(ElkAlgorithmsFile), "ELK_ALGORITHMS = [", ']') is { } algorithms)
		{
			names.UnionWith(Quoted(algorithms, ""));
		}

		return names;
	}

	/// <summary>
	/// The folders that hold a layout, registered or not: those under layout-algorithms, or in 13 those under
	/// packages/core/src/layout and the packages/layout-* folders without the prefix.
	/// </summary>
	public SortedSet<string> LayoutFolders()
	{
		if (!IsV13)
		{
			var folders = Folders(LayoutFolder);
			folders.ExceptWith(SharedLayoutFolders);
			return folders;
		}

		var layouts = Folders(CoreLayoutFolder);
		layouts.ExceptWith(SharedCoreLayoutFolders);
		layouts.UnionWith(Packages(LayoutPackagePrefix));
		return layouts;
	}

	/// <summary>The names of the folders under packages that start with <paramref name="prefix"/>, without it.</summary>
	private SortedSet<string> Packages(string prefix) =>
		new(
			Folders(PackagesFolder).Where(f => f.StartsWith(prefix, StringComparison.Ordinal)).Select(f => f[prefix.Length..]),
			StringComparer.Ordinal
		);

	private SortedSet<string> Folders(string path)
	{
		var full = Path.Combine(Checkout, path);
		return Directory.Exists(full)
			? new(Directory.EnumerateDirectories(full).Select(Path.GetFileName).OfType<string>(), StringComparer.Ordinal)
			: new(StringComparer.Ordinal);
	}

	private IEnumerable<string> Files(string path, string pattern, SearchOption search = SearchOption.AllDirectories)
	{
		var full = Path.Combine(Checkout, path);
		return Directory.Exists(full)
			? Directory.EnumerateFiles(full, pattern, search)
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
