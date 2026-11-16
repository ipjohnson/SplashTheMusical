using System.Text.Json.Nodes;

namespace SplashTheMusical.Upstream;

/// <summary>
/// Where a checkout of mermaid keeps the config schema, and the diagram keys whose schema defaults go into the default
/// config. mermaid 12 keeps the schema in packages/mermaid/src/schemas and lists the keys in .build/jsonSchema.ts.
/// mermaid 13 keeps it in packages/core/schema, and each diagram package owns its section's defaults.
/// </summary>
/// <param name="SchemaPath">The schema's path, relative to the checkout.</param>
public sealed record ConfigSource(string SchemaPath, ConfigSchema Schema, IReadOnlyList<string> DiagramKeys)
{
	public const string V12SchemaPath = "packages/mermaid/src/schemas/config.schema.yaml";

	public const string V12BuildPath = ".build/jsonSchema.ts";

	public const string V13SchemaPath = "packages/core/schema/config.schema.yaml";

	public static ConfigSource Read(string checkout)
	{
		var v12 = Path.Combine(checkout, V12SchemaPath);
		if (File.Exists(v12))
		{
			var schema = new ConfigSchema(SchemaYaml.Read(File.ReadAllText(v12)));
			return new(V12SchemaPath, schema, Upstream.DiagramKeys.Read(File.ReadAllText(Path.Combine(checkout, V12BuildPath))));
		}

		var v13 = Path.Combine(checkout, V13SchemaPath);
		if (!File.Exists(v13))
		{
			throw new FileNotFoundException($"{checkout} has neither {V12SchemaPath} nor {V13SchemaPath}.");
		}

		var root = SchemaYaml.Read(File.ReadAllText(v13));
		var properties = root["properties"]?.AsObject() ?? new JsonObject();
		var keys = Upstream.DiagramKeys.ReadPackages(checkout).Where(properties.ContainsKey).ToList();
		return new(V13SchemaPath, new ConfigSchema(root), keys);
	}
}
