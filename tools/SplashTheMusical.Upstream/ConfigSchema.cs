using System.Text.Json.Nodes;

namespace SplashTheMusical.Upstream;

/// <summary>mermaid's config schema, with <c>$ref</c> resolution and the default config mermaid builds from it.</summary>
public sealed class ConfigSchema(JsonObject root)
{
	private const string DefsPrefix = "#/$defs/";

	public JsonObject Root { get; } = root;

	public JsonObject Defs => Root["$defs"]!.AsObject();

	/// <summary>
	/// The schema with its <c>$ref</c> replaced by the referenced schema. The keywords beside the <c>$ref</c>, such as
	/// <c>default</c>, win over the referenced schema's.
	/// </summary>
	public JsonObject Resolve(JsonObject schema)
	{
		while (schema["$ref"] is JsonValue reference)
		{
			var merged = Pointer(reference.GetValue<string>()).DeepClone().AsObject();
			foreach (var (key, value) in schema)
			{
				if (key != "$ref")
				{
					merged[key] = value?.DeepClone();
				}
			}

			schema = merged;
		}

		return schema;
	}

	/// <summary>The name of the <c>$defs</c> entry a schema refers to, directly or as its only <c>allOf</c>, or null.</summary>
	public static string? DefName(JsonObject schema)
	{
		var reference = schema["$ref"] ?? (schema["allOf"] is JsonArray { Count: 1 } all ? all[0]!["$ref"] : null);
		var text = reference?.GetValue<string>();
		return text is not null && text.StartsWith(DefsPrefix, StringComparison.Ordinal) && text.IndexOf('/', DefsPrefix.Length) < 0
			? text[DefsPrefix.Length..]
			: null;
	}

	/// <summary>
	/// mermaid's default config before its non-JSON values: what .build/jsonSchema.ts's <c>generateDefaults</c> gives,
	/// with the changes packages/mermaid/src/defaultConfig.ts makes to it. Functions, <c>undefined</c> values,
	/// <c>themeVariables</c> and the keys the schema does not have are left out.
	/// </summary>
	public JsonObject DefaultConfig(IReadOnlyList<string> diagramKeys)
	{
		var properties = Root["properties"]!.AsObject();
		var baseProperties = Defs["BaseDiagramConfig"]!["properties"]!.AsObject();
		var config = new JsonObject();
		foreach (var key in diagramKeys)
		{
			var diagram = Resolve(properties[key]!.AsObject());
			var defaults = new JsonObject();
			foreach (var required in diagram["required"]?.AsArray() ?? [])
			{
				var name = required!.GetValue<string>();
				if (diagram["properties"]?[name] is null && baseProperties[name]?["default"] is { } value)
				{
					defaults[name] = value.DeepClone();
				}
			}

			ApplyDefaults(diagram, defaults);
			config[key] = defaults;
		}

		ApplyDefaults(Root, config);

		// defaultConfig.ts sets elk, which no schema default does, to the schema's values.
		var elk = new JsonObject();
		ApplyDefaults(properties["elk"]!.AsObject(), elk);
		config["elk"] = elk;

		// defaultConfig.ts builds class from scratch with only these keys.
		var schemaClass = config["class"]!.AsObject();
		var classConfig = new JsonObject();
		foreach (var key in (string[])["theme", "look", "layout"])
		{
			if (schemaClass[key] is { } value)
			{
				classConfig[key] = value.DeepClone();
			}
		}

		classConfig["hideEmptyMembersBox"] = false;
		classConfig["hierarchicalNamespaces"] = true;
		config["class"] = classConfig;

		config["pie"]!["useWidth"] = 984d;

		RemoveReferences(config);
		return config;
	}

	/// <summary>
	/// ajv's <c>useDefaults</c> on <paramref name="target"/>: each <c>allOf</c> schema first, so a base schema's default wins
	/// over the same key's default in the schema that includes it, then each missing property with a default, then the
	/// properties that hold objects.
	/// </summary>
	public void ApplyDefaults(JsonObject schema, JsonObject target)
	{
		schema = Resolve(schema);
		foreach (var item in schema["allOf"]?.AsArray() ?? [])
		{
			ApplyDefaults(item!.AsObject(), target);
		}

		if (schema["properties"] is not JsonObject properties)
		{
			return;
		}

		foreach (var (key, property) in properties)
		{
			if (!target.ContainsKey(key) && Resolve(property!.AsObject()).TryGetPropertyValue("default", out var value))
			{
				target[key] = value?.DeepClone();
			}
		}

		foreach (var (key, property) in properties)
		{
			if (target[key] is JsonObject child)
			{
				ApplyDefaults(property!.AsObject(), child);
			}
		}
	}

	/// <summary>
	/// Removes <c>$ref</c> keys. xyChart's <c>xAxis</c> default is <c>{ $ref: ... }</c>, which ajv copies as a value.
	/// </summary>
	private static void RemoveReferences(JsonObject node)
	{
		node.Remove("$ref");
		foreach (var (_, value) in node)
		{
			if (value is JsonObject child)
			{
				RemoveReferences(child);
			}
		}
	}

	private JsonObject Pointer(string reference)
	{
		if (!reference.StartsWith("#/", StringComparison.Ordinal))
		{
			throw new InvalidDataException($"Only local references are supported, not {reference}.");
		}

		JsonNode node = Root;
		foreach (var part in reference[2..].Split('/'))
		{
			node = node[part.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal)]
				?? throw new InvalidDataException($"{reference} does not resolve.");
		}

		return node.AsObject();
	}
}
