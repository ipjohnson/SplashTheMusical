using System.Diagnostics;
using System.Text.Json.Nodes;

namespace SplashTheMusical.Upstream;

/// <summary>
/// Generates STM's code from a checkout of mermaid-js/mermaid at the tag of the mermaid that tools/oracle pins.
/// </summary>
/// <remarks>
/// <c>config &lt;checkout&gt; [--check]</c> writes the <c>MermaidConfig</c> classes to src/SplashTheMusical/Config/Generated
/// from the config schema <see cref="ConfigSource"/> finds, packages/mermaid/src/schemas/config.schema.yaml in mermaid 12 and
/// packages/core/schema/config.schema.yaml in 13, and <c>ConfigKeys</c> from corpus/config.txt. With <c>--check</c> it writes nothing and fails when a
/// generated file differs from the committed one.
/// <c>fonts [--check]</c> writes the text measurer's width tables to src/SplashTheMusical/Text/Generated from the font
/// files <see cref="FontTableWriter.Families"/> names.
/// <c>compatibility [--check]</c> writes <c>MermaidCompatibility</c> to src/SplashTheMusical/Generated from the mermaid
/// version in tools/oracle/package-lock.json.
/// <c>delta &lt;old checkout&gt; &lt;new checkout&gt; &lt;before&gt; &lt;after&gt;</c> writes &lt;after&gt;/upstream-delta.md, the
/// <see cref="DeltaReport"/> of a bump. &lt;before&gt; holds a copy of corpus/ and the conformance results.txt and
/// results.trx from before the bump, and &lt;after&gt; holds the results from after it. The new corpus is corpus/.
/// </remarks>
public static class Program
{
	private const string Usage =
		"usage: SplashTheMusical.Upstream config <mermaid checkout> [--check]\n       SplashTheMusical.Upstream fonts [--check]\n       SplashTheMusical.Upstream compatibility [--check]\n       SplashTheMusical.Upstream delta <old checkout> <new checkout> <before folder> <after folder>";

	public static int Main(string[] args)
	{
		if (args.Length is 1 or 2 && args[0] == "fonts" && (args.Length == 1 || args[1] == "--check"))
		{
			var root = FindRepository();
			var tables = new Dictionary<string, string> { ["FontTables.g.cs"] = FontTableWriter.Write(root, FontTableWriter.Families) };
			var target = Path.Combine(root, "src", "SplashTheMusical", "Text", "Generated");
			return args.Length == 2 ? GeneratedFiles.Check(target, tables) : GeneratedFiles.Write(target, tables);
		}

		if (args.Length is 1 or 2 && args[0] == "compatibility" && (args.Length == 1 || args[1] == "--check"))
		{
			var root = FindRepository();
			var compatibility = new Dictionary<string, string> { [CompatibilityWriter.FileName] = CompatibilityWriter.Write(root) };
			var target = CompatibilityWriter.Folder(root);
			return args.Length == 2 ? GeneratedFiles.Check(target, compatibility) : GeneratedFiles.Write(target, compatibility);
		}

		if (args.Length == 5 && args[0] == "delta")
		{
			var root = FindRepository();
			var before = Path.GetFullPath(args[3]);
			var after = Path.GetFullPath(args[4]);
			var oldSide = new DeltaSide(
				OracleVersion(Path.Combine(before, "corpus")),
				Path.GetFullPath(args[1]),
				Path.Combine(before, "corpus"),
				Path.Combine(before, "results.txt"),
				Path.Combine(before, "results.trx")
			);
			var newSide = new DeltaSide(
				OracleVersion(Path.Combine(root, "corpus")),
				Path.GetFullPath(args[2]),
				Path.Combine(root, "corpus"),
				Path.Combine(after, "results.txt"),
				Path.Combine(after, "results.trx")
			);
			var report = Path.Combine(after, "upstream-delta.md");
			File.WriteAllText(report, DeltaReport.Write(oldSide, newSide, GrammarHeaders.Map(root)));
			Console.WriteLine($"wrote {report}");
			return 0;
		}

		if (args.Length is < 2 or > 3 || args[0] != "config" || (args.Length == 3 && args[2] != "--check"))
		{
			Console.Error.WriteLine(Usage);
			return 2;
		}

		var repository = FindRepository();
		var checkout = Path.GetFullPath(args[1]);
		var version = PinnedVersion(repository);
		if (!CheckTag(checkout, version))
		{
			return 1;
		}

		var files = ConfigWriter.Write(ConfigSource.Read(checkout), version);
		files.Add("ConfigKeys.g.cs", ConfigKeysWriter.Write(File.ReadAllText(Path.Combine(repository, "corpus", "config.txt")), version));
		var folder = Path.Combine(repository, "src", "SplashTheMusical", "Config", "Generated");
		return args.Length == 3 ? GeneratedFiles.Check(folder, files) : GeneratedFiles.Write(folder, files);
	}

	/// <summary>The folder above the working directory that holds SplashTheMusical.slnx.</summary>
	private static string FindRepository()
	{
		for (var folder = Directory.GetCurrentDirectory(); folder is not null; folder = Path.GetDirectoryName(folder))
		{
			if (File.Exists(Path.Combine(folder, "SplashTheMusical.slnx")))
			{
				return folder;
			}
		}

		throw new InvalidOperationException("Run from inside the SplashTheMusical repository.");
	}

	/// <summary>The mermaid version in tools/oracle/package.json.</summary>
	private static string PinnedVersion(string repository)
	{
		var package = JsonNode.Parse(File.ReadAllText(Path.Combine(repository, "tools", "oracle", "package.json")))!;
		return package["devDependencies"]!["mermaid"]!.GetValue<string>();
	}

	/// <summary>The mermaid version the second comment line of a corpus's config.txt names.</summary>
	private static string OracleVersion(string corpus)
	{
		const string Prefix = "# mermaid ";
		var line = File.ReadLines(Path.Combine(corpus, "config.txt")).FirstOrDefault(l => l.StartsWith(Prefix, StringComparison.Ordinal));
		return line?[Prefix.Length..] ?? "unknown";
	}

	private static bool CheckTag(string checkout, string version)
	{
		var start = new ProcessStartInfo("git", ["-C", checkout, "tag", "--points-at", "HEAD"]) { RedirectStandardOutput = true };
		using var git = Process.Start(start)!;
		var tags = git.StandardOutput.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		git.WaitForExit();
		var tag = "mermaid@" + version;
		if (git.ExitCode == 0 && tags.Contains(tag))
		{
			return true;
		}

		Console.Error.WriteLine($"{checkout} is at {(tags.Length > 0 ? string.Join(", ", tags) : "no tag")}, not {tag}, the mermaid that tools/oracle pins");
		return false;
	}
}
