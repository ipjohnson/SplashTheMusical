using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SplashTheMusical.Conformance;

/// <summary>
/// Checks that the oracle output committed in a corpus folder belongs to its cases. corpus/README.md describes the
/// layout, and tools/oracle/README.md the header each output file starts with.
/// </summary>
internal static class CorpusCheck
{
	private const string Facts = ".facts.txt";
	private const string Boxes = ".boxes.txt";
	private const string Dagre = ".dagre.txt";

	public static string RepositoryRoot
	{
		get
		{
			var dir = new DirectoryInfo(AppContext.BaseDirectory);
			while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SplashTheMusical.slnx")))
			{
				dir = dir.Parent;
			}

			return dir?.FullName ?? throw new InvalidOperationException("SplashTheMusical.slnx not found above " + AppContext.BaseDirectory);
		}
	}

	public static string CorpusRoot => Path.Combine(RepositoryRoot, "corpus");

	/// <summary>The mermaid version tools/oracle/package.json pins.</summary>
	public static string PinnedMermaid()
	{
		using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "oracle", "package.json")));
		return json.RootElement.GetProperty("devDependencies").GetProperty("mermaid").GetString()!;
	}

	/// <summary>Every case of a corpus folder, as a path relative to it without the .mmd extension.</summary>
	public static IReadOnlyList<string> Cases(string corpus) =>
		[.. Directory
			.EnumerateFiles(corpus, "*.mmd", SearchOption.AllDirectories)
			.Select(f => Path.GetRelativePath(corpus, f)[..^".mmd".Length])
			.Order(StringComparer.Ordinal)];

	/// <summary>The first 16 hex digits of the SHA-256 of a case's text with CRLF folded to LF, as the oracle writes it.</summary>
	public static string SourceHash(string source) =>
		Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(source.Replace("\r\n", "\n", StringComparison.Ordinal))))[..16];

	/// <summary>One line per file that is missing, left over or older than its case.</summary>
	public static IReadOnlyList<string> Problems(string corpus, string mermaidVersion)
	{
		var problems = new List<string>();
		var cases = Cases(corpus);
		var known = new HashSet<string>(cases, StringComparer.Ordinal);
		foreach (var name in cases)
		{
			// File.ReadAllText drops a byte order mark, and the oracle hashes it, so the text is decoded as is.
			var hash = SourceHash(Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(corpus, name + ".mmd"))));
			var facts = Header(corpus, name + Facts);
			if (facts is null)
			{
				problems.Add($"{name}: no facts file");
				continue;
			}

			CheckHeader(problems, name + Facts, facts, hash, mermaidVersion);
			var hasBoxes = File.Exists(Path.Combine(corpus, name + Boxes));
			if (facts.Accepted && !hasBoxes)
			{
				problems.Add($"{name}: mermaid.js accepts it and it has no boxes file");
			}
			else if (!facts.Accepted && hasBoxes)
			{
				problems.Add($"{name}: mermaid.js refuses it and it has a boxes file");
			}

			foreach (var suffix in new[] { Boxes, Dagre })
			{
				if (Header(corpus, name + suffix) is { } header)
				{
					CheckHeader(problems, name + suffix, header, hash, mermaidVersion);
				}
			}
		}

		foreach (var file in Directory.EnumerateFiles(corpus, "*.txt", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
		{
			var relative = Path.GetRelativePath(corpus, file);
			var suffix = new[] { Facts, Boxes, Dagre }.FirstOrDefault(s => relative.EndsWith(s, StringComparison.Ordinal));
			if (suffix is not null && !known.Contains(relative[..^suffix.Length]))
			{
				problems.Add($"{relative}: no case");
			}
		}

		return problems;
	}

	private sealed record FileHeader(string? Version, string? Source, bool Accepted);

	private static void CheckHeader(List<string> problems, string file, FileHeader header, string hash, string mermaidVersion)
	{
		if (header.Source != hash)
		{
			problems.Add($"{file}: source {header.Source ?? "missing"}, the case hashes to {hash}. Run mise run corpus-oracle");
		}

		if (header.Version != mermaidVersion)
		{
			problems.Add($"{file}: mermaid {header.Version ?? "missing"}, tools/oracle pins {mermaidVersion}");
		}
	}

	// The comment lines at the top of a facts, boxes or dagre file, and the facts file's accept line.
	private static FileHeader? Header(string corpus, string file)
	{
		var path = Path.Combine(corpus, file);
		if (!File.Exists(path))
		{
			return null;
		}

		string? version = null;
		string? source = null;
		var accepted = false;
		foreach (var line in File.ReadLines(path).Take(4))
		{
			if (line.StartsWith("# mermaid ", StringComparison.Ordinal))
			{
				version = line["# mermaid ".Length..];
			}
			else if (line.StartsWith("# source ", StringComparison.Ordinal))
			{
				source = line["# source ".Length..];
			}
			else if (line == "accept\tyes")
			{
				accepted = true;
			}
		}

		return new FileHeader(version, source, accepted);
	}
}
