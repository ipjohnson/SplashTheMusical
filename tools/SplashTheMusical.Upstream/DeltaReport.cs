using System.Diagnostics;
using System.Text;
using System.Xml.Linq;

namespace SplashTheMusical.Upstream;

/// <summary>One side of a bump: the mermaid version, its checkout, the corpus with its oracle files, and the conformance results.</summary>
/// <param name="Results">The file <c>STM_RESULTS_FILE</c> named for the conformance run.</param>
/// <param name="Trx">The TRX report of the same run.</param>
public sealed record DeltaSide(string Version, string Checkout, string Corpus, string Results, string Trx);

/// <summary>
/// Writes the upstream delta report of a mermaid bump, section 8 of docs/mermaid-parity-plan.md. It has one section per
/// category, and a section with no difference says so.
/// </summary>
public static class DeltaReport
{
	/// <summary>The most fact lines shown for one corpus case.</summary>
	public const int FactLinesPerCase = 8;

	public static string Write(DeltaSide before, DeltaSide after, IReadOnlyDictionary<string, List<string>> parsers)
	{
		var oldTree = new UpstreamTree(before.Checkout);
		var newTree = new UpstreamTree(after.Checkout);
		var sections = new List<(string Title, string Body)>
		{
			("Diagram types", DiagramTypes(oldTree, newTree)),
			("Grammar files", Grammars(oldTree, newTree, parsers)),
			("Config keys", Keyed(OracleFile(before, "config.txt"), OracleFile(after, "config.txt"), 2)),
			("Theme variables", Keyed(OracleFile(before, "themes.txt"), OracleFile(after, "themes.txt"), 3)),
			("Shapes, edge types and layout algorithms", ShapesEdgesLayouts(before, after, oldTree, newTree)),
			("Corpus cases", Corpus(before.Corpus, after.Corpus)),
			("Conformance", Conformance(before, after)),
		};

		var text = new StringBuilder();
		text.Append("# Upstream delta: mermaid ").Append(before.Version).Append(" to ").Append(after.Version).Append("\n\n");
		var changed = sections.Where(s => s.Body.Length > 0).Select(s => s.Title).ToList();
		text.Append(changed.Count == 0 ? "Nothing changed." : "Changed: " + string.Join(", ", changed) + ".").Append('\n');
		foreach (var (title, body) in sections)
		{
			text.Append("\n## ").Append(title).Append("\n\n").Append(body.Length == 0 ? "No change." : body.TrimEnd('\n')).Append('\n');
		}

		return text.ToString();
	}

	private static string DiagramTypes(UpstreamTree before, UpstreamTree after)
	{
		var text = new StringBuilder();
		Sets(text, "Diagram folders", before.DiagramFolders(), after.DiagramFolders());
		Sets(text, "Detector ids", before.DiagramIds(), after.DiagramIds());
		return text.ToString();
	}

	private static string Grammars(UpstreamTree before, UpstreamTree after, IReadOnlyDictionary<string, List<string>> parsers)
	{
		var text = new StringBuilder();
		var paths = new SortedSet<string>(before.GrammarFiles(), StringComparer.Ordinal);
		paths.UnionWith(after.GrammarFiles());
		paths.UnionWith(parsers.Keys);
		foreach (var path in paths)
		{
			var old = before.Read(path);
			var current = after.Read(path);
			if (old == current)
			{
				continue;
			}

			var status = old is null ? "added" : current is null ? "removed" : "changed";
			text.Append("### ").Append(Code(path)).Append(", ").Append(status).Append("\n\n");
			text.Append(parsers.TryGetValue(path, out var sources)
				? "STM parser: " + string.Join(", ", sources.Select(Code))
				: "No STM parser names it.").Append("\n\n");
			if (old is not null && current is not null)
			{
				Fenced(text, "diff", Diff(Path.Combine(before.Checkout, path), Path.Combine(after.Checkout, path)));
			}
		}

		return text.ToString();
	}

	private static string ShapesEdgesLayouts(DeltaSide before, DeltaSide after, UpstreamTree oldTree, UpstreamTree newTree)
	{
		var text = new StringBuilder();
		var shapes = Keyed(OracleFile(before, "shapes.txt"), OracleFile(after, "shapes.txt"), 2);
		if (shapes.Length > 0)
		{
			text.Append("Shape table:\n\n").Append(shapes);
		}

		Sets(text, "Edge markers", oldTree.Markers(), newTree.Markers());
		Sets(text, "Registered layout algorithms", oldTree.Layouts(), newTree.Layouts());
		Sets(text, "Layout folders", oldTree.LayoutFolders(), newTree.LayoutFolders());
		return text.ToString();
	}

	private static string Corpus(string before, string after)
	{
		var oldCases = Cases(before);
		var newCases = Cases(after);
		var text = new StringBuilder();
		Sets(text, "Cases", oldCases, newCases);
		var changed = new StringBuilder();
		var count = 0;
		foreach (var name in newCases.Where(oldCases.Contains))
		{
			var old = Facts(before, name);
			var current = Facts(after, name);
			if (old.SequenceEqual(current))
			{
				continue;
			}

			count++;
			changed.Append("- ").Append(Code(name)).Append('\n');
			var lines = old.Except(current).Select(l => "- " + l).Concat(current.Except(old).Select(l => "+ " + l)).ToList();
			foreach (var line in lines.Take(FactLinesPerCase))
			{
				changed.Append("  - ").Append(Code(line)).Append('\n');
			}

			if (lines.Count > FactLinesPerCase)
			{
				changed.Append("  - and ").Append(lines.Count - FactLinesPerCase).Append(" more\n");
			}
		}

		if (count > 0)
		{
			text.Append("Cases whose oracle facts changed, ").Append(count).Append(":\n\n").Append(changed);
		}

		return text.ToString();
	}

	private static string Conformance(DeltaSide before, DeltaSide after)
	{
		var text = new StringBuilder();
		if (!File.Exists(after.Results) || !File.Exists(before.Results))
		{
			return "The conformance results file of one side is missing, so nothing was compared.\n";
		}

		var old = Outcomes(File.ReadAllText(before.Results));
		var current = Outcomes(File.ReadAllText(after.Results));
		List(text, "Corpus levels that newly fail", current.Where(r => r.Value == "fail" && old.GetValueOrDefault(r.Key) != "fail").Select(r => r.Key));
		List(text, "Corpus levels that newly pass", current.Where(r => r.Value == "pass" && old.GetValueOrDefault(r.Key) == "fail").Select(r => r.Key));
		var oldFailed = FailedTests(before.Trx);
		var newFailed = FailedTests(after.Trx);
		if (oldFailed is null || newFailed is null)
		{
			text.Append("The TRX report of one side is missing, so failing tests were not compared.\n\n");
		}
		else
		{
			List(text, "Tests that newly fail", newFailed.Except(oldFailed));
		}

		return text.ToString();
	}

	/// <summary>Compares two tab-separated oracle files whose first <paramref name="keyFields"/> fields name a line.</summary>
	public static string Keyed(string before, string after, int keyFields)
	{
		var old = Lines(before, keyFields);
		var current = Lines(after, keyFields);
		var text = new StringBuilder();
		List(text, "Added", current.Keys.Where(k => !old.ContainsKey(k)).Select(k => Pair(k, current[k])), quote: false);
		List(text, "Removed", old.Keys.Where(k => !current.ContainsKey(k)).Select(k => Pair(k, old[k])), quote: false);
		List(
			text,
			"Changed",
			current.Keys.Where(k => old.TryGetValue(k, out var value) && value != current[k]).Select(k => $"{Code(k)}: {Code(old[k])} to {Code(current[k])}"),
			quote: false
		);
		return text.ToString();
	}

	private static string Pair(string key, string value) => value.Length == 0 ? Code(key) : $"{Code(key)}: {Code(value)}";

	private static SortedDictionary<string, string> Lines(string text, int keyFields)
	{
		var lines = new SortedDictionary<string, string>(StringComparer.Ordinal);
		foreach (var line in text.Split('\n'))
		{
			if (line.Length == 0 || line[0] == '#')
			{
				continue;
			}

			var fields = line.Split('\t');
			var key = string.Join(' ', fields.Take(keyFields));
			lines[key] = string.Join('\t', fields.Skip(keyFields));
		}

		return lines;
	}

	private static string OracleFile(DeltaSide side, string name)
	{
		var path = Path.Combine(side.Corpus, name);
		return File.Exists(path) ? File.ReadAllText(path) : "";
	}

	/// <summary>The cases of a corpus folder, as their paths without .mmd.</summary>
	private static SortedSet<string> Cases(string corpus) =>
		new(
			Directory.EnumerateFiles(corpus, "*.mmd", SearchOption.AllDirectories)
				.Select(f => Path.GetRelativePath(corpus, f).Replace('\\', '/')[..^".mmd".Length]),
			StringComparer.Ordinal
		);

	/// <summary>The lines of a case's facts file without its comment lines, which name the version and the source hash.</summary>
	private static List<string> Facts(string corpus, string name)
	{
		var path = Path.Combine(corpus, name + ".facts.txt");
		return File.Exists(path) ? [.. File.ReadAllLines(path).Where(l => !l.StartsWith('#'))] : [];
	}

	/// <summary>The outcome of each case and level in a results file.</summary>
	private static Dictionary<string, string> Outcomes(string text)
	{
		var outcomes = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (var line in text.Split('\n'))
		{
			if (line.Length == 0 || line[0] == '\t')
			{
				continue;
			}

			var fields = line.Split('\t');
			outcomes[fields[0] + " " + fields[1]] = fields[2];
		}

		return outcomes;
	}

	/// <summary>The names of the failed tests of a TRX report, or null when there is no report.</summary>
	private static SortedSet<string>? FailedTests(string trx)
	{
		if (!File.Exists(trx))
		{
			return null;
		}

		return new(
			XDocument.Load(trx)
				.Descendants()
				.Where(e => e.Name.LocalName == "UnitTestResult" && (string?)e.Attribute("outcome") == "Failed")
				.Select(e => (string?)e.Attribute("testName") ?? ""),
			StringComparer.Ordinal
		);
	}

	private static void Sets(StringBuilder text, string what, SortedSet<string> before, SortedSet<string> after)
	{
		List(text, what + " added", after.Except(before));
		List(text, what + " removed", before.Except(after));
	}

	private static void List(StringBuilder text, string title, IEnumerable<string> items, bool quote = true)
	{
		var list = items.ToList();
		if (list.Count == 0)
		{
			return;
		}

		text.Append(title).Append(", ").Append(list.Count).Append(":\n\n");
		foreach (var item in list)
		{
			text.Append("- ").Append(quote ? Code(item) : item).Append('\n');
		}

		text.Append('\n');
	}

	/// <summary>The unified diff of two files, from its first hunk.</summary>
	private static string Diff(string before, string after)
	{
		var start = new ProcessStartInfo("git", ["diff", "--no-index", "--no-color", "--", before, after]) { RedirectStandardOutput = true };
		using var git = Process.Start(start)!;
		var diff = git.StandardOutput.ReadToEnd();
		git.WaitForExit();
		var hunk = diff.IndexOf("\n@@", StringComparison.Ordinal);
		return hunk < 0 ? diff : diff[(hunk + 1)..];
	}

	private static void Fenced(StringBuilder text, string language, string body)
	{
		var fence = "```";
		while (body.Contains(fence, StringComparison.Ordinal))
		{
			fence += '`';
		}

		text.Append(fence).Append(language).Append('\n').Append(body);
		if (!body.EndsWith('\n'))
		{
			text.Append('\n');
		}

		text.Append(fence).Append("\n\n");
	}

	/// <summary>Markdown inline code that holds any text, with tabs shown as spaces.</summary>
	private static string Code(string text)
	{
		text = text.Replace('\t', ' ');
		var ticks = "`";
		while (text.Contains(ticks, StringComparison.Ordinal))
		{
			ticks += '`';
		}

		return ticks.Length > 1 ? $"{ticks} {text} {ticks}" : $"`{text}`";
	}
}
