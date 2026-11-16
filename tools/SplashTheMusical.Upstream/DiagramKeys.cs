using System.Text.Json.Nodes;

namespace SplashTheMusical.Upstream;

/// <summary>
/// Reads <c>MERMAID_CONFIG_DIAGRAM_KEYS</c> from mermaid's .build/jsonSchema.ts: the config keys whose schema defaults
/// mermaid's build writes into the default config. A diagram key that is not listed gets no schema defaults.
/// </summary>
public static class DiagramKeys
{
	private const string Start = "MERMAID_CONFIG_DIAGRAM_KEYS = [";

	public static List<string> Read(string text)
	{
		var start = text.IndexOf(Start, StringComparison.Ordinal);
		var end = start < 0 ? -1 : text.IndexOf(']', start + Start.Length);
		if (end < 0)
		{
			throw new InvalidDataException("MERMAID_CONFIG_DIAGRAM_KEYS is not in .build/jsonSchema.ts.");
		}

		var keys = new List<string>();
		var list = text.AsSpan(start + Start.Length, end - start - Start.Length);
		while (list.IndexOf('\'') is var open and >= 0)
		{
			list = list[(open + 1)..];
			var close = list.IndexOf('\'');
			keys.Add(list[..close].ToString());
			list = list[(close + 1)..];
		}

		return keys;
	}

	/// <summary>
	/// The diagram keys of a mermaid 13 checkout, in package order: the config section of each packages/diagram-* whose
	/// workspace.yaml sets <c>configDefaults: true</c>, which is the <c>section</c> of the <c>mermaid</c> manifest in its
	/// package.json, else its <c>id</c>. Then swimlane, whose defaults core resolves, as tools/gen-config's
	/// <c>CORE_SECTIONS</c> says.
	/// </summary>
	public static List<string> ReadPackages(string checkout)
	{
		var keys = new List<string>();
		var packages = Path.Combine(checkout, "packages");
		foreach (var folder in Directory.EnumerateDirectories(packages, "diagram-*").Order(StringComparer.Ordinal))
		{
			var workspace = Path.Combine(folder, "workspace.yaml");
			var package = Path.Combine(folder, "package.json");
			if (!File.Exists(workspace) || !File.Exists(package) || !File.ReadLines(workspace).Any(l => l.Trim() == "configDefaults: true"))
			{
				continue;
			}

			var manifest = JsonNode.Parse(File.ReadAllText(package))?["mermaid"];
			if (manifest?["plugin"]?.GetValue<string>() == "diagram" && (manifest["section"] ?? manifest["id"])?.GetValue<string>() is { } section)
			{
				keys.Add(section);
			}
		}

		keys.Add("swimlane");
		return keys;
	}
}
