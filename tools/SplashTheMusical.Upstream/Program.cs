using System.Diagnostics;
using System.Text.Json.Nodes;

namespace SplashTheMusical.Upstream;

/// <summary>
/// Generates STM's code from a checkout of mermaid-js/mermaid at the tag of the mermaid that tools/oracle pins.
/// </summary>
/// <remarks>
/// <c>config &lt;checkout&gt; [--check]</c> writes the <c>MermaidConfig</c> classes to src/SplashTheMusical/Config/Generated
/// from <c>packages/mermaid/src/schemas/config.schema.yaml</c>, and <c>ConfigKeys</c> from corpus/config.txt. With <c>--check</c> it writes nothing and fails when a
/// generated file differs from the committed one.
/// <c>fonts [--check]</c> writes the text measurer's width tables to src/SplashTheMusical/Text/Generated from the font
/// files <see cref="FontTableWriter.Families"/> names.
/// </remarks>
public static class Program
{
	private const string Usage = "usage: SplashTheMusical.Upstream config <mermaid checkout> [--check]\n       SplashTheMusical.Upstream fonts [--check]";

	public static int Main(string[] args)
	{
		if (args.Length is 1 or 2 && args[0] == "fonts" && (args.Length == 1 || args[1] == "--check"))
		{
			var root = FindRepository();
			var tables = new Dictionary<string, string> { ["FontTables.g.cs"] = FontTableWriter.Write(root, FontTableWriter.Families) };
			var target = Path.Combine(root, "src", "SplashTheMusical", "Text", "Generated");
			return args.Length == 2 ? GeneratedFiles.Check(target, tables) : GeneratedFiles.Write(target, tables);
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

		var schemaText = File.ReadAllText(Path.Combine(checkout, "packages", "mermaid", "src", "schemas", "config.schema.yaml"));
		var buildText = File.ReadAllText(Path.Combine(checkout, ".build", "jsonSchema.ts"));
		var schema = new ConfigSchema(SchemaYaml.Read(schemaText));
		var files = ConfigWriter.Write(schema, DiagramKeys.Read(buildText), version);
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
