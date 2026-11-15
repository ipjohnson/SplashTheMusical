using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SplashTheMusical.Upstream;

/// <summary>
/// Writes one C# class per object of mermaid's config schema. Each property is nullable, because mermaid.js treats every
/// key as optional, except the nested config objects, which always exist. A property's initializer is the default
/// config's value at the first place the class is used. Another place with other defaults gets an object initializer.
/// </summary>
public sealed class ConfigWriter
{
	private const string RootName = "MermaidConfig";

	private readonly ConfigSchema _schema;
	private readonly List<ConfigClass> _classes = [];
	private readonly Dictionary<string, ConfigClass> _defClasses = [];

	private ConfigWriter(ConfigSchema schema) => _schema = schema;

	/// <summary>The generated files by file name.</summary>
	public static SortedDictionary<string, string> Write(ConfigSchema schema, IReadOnlyList<string> diagramKeys, string version)
	{
		var writer = new ConfigWriter(schema);
		var root = writer.Build(RootName, schema.Root, "The config of a diagram, as mermaid.js's MermaidConfig type. A new instance holds mermaid.js's default config.");
		root.Defaults = schema.DefaultConfig(diagramKeys);
		CheckKeys(root, root.Defaults);
		var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
		for (var i = 0; i < writer._classes.Count; i++)
		{
			var configClass = writer._classes[i];
			files.Add(configClass.Name + ".g.cs", writer.Class(configClass, version));
		}

		return files;
	}

	private ConfigClass Build(string name, JsonObject schema, string? summary)
	{
		if (_classes.Any(c => c.Name == name))
		{
			throw new InvalidDataException($"Two config classes are named {name}.");
		}

		var resolved = _schema.Resolve(schema);
		var configClass = new ConfigClass(name, summary ?? Text(resolved, "description") ?? Text(resolved, "title"));
		_classes.Add(configClass);
		var properties = new OrderedDictionary<string, JsonObject>(StringComparer.Ordinal);
		Collect(resolved, properties);
		foreach (var (key, property) in properties)
		{
			if (Property(key, property) is { } configProperty)
			{
				if (configProperty.Name == name || configClass.Properties.Any(p => p.Name == configProperty.Name))
				{
					throw new InvalidDataException($"{name} has two members named {configProperty.Name}.");
				}

				configClass.Properties.Add(configProperty);
			}
		}

		return configClass;
	}

	/// <summary>The properties of a schema and of its <c>allOf</c> schemas. A schema's own property takes the place of a base's.</summary>
	private void Collect(JsonObject schema, OrderedDictionary<string, JsonObject> properties)
	{
		schema = _schema.Resolve(schema);
		foreach (var item in schema["allOf"]?.AsArray() ?? [])
		{
			Collect(item!.AsObject(), properties);
		}

		foreach (var (key, property) in schema["properties"]?.AsObject() ?? [])
		{
			properties[key] = property!.AsObject();
		}
	}

	private ConfigProperty? Property(string key, JsonObject schema)
	{
		var resolved = _schema.Resolve(schema);
		var kind = Kind(resolved, key);
		if (kind is null)
		{
			return null;
		}

		ConfigClass? configClass = null;
		if (kind == ValueKind.Object)
		{
			var defName = ConfigSchema.DefName(schema);
			if (defName is null)
			{
				configClass = Build(Pascal(key) + "Config", resolved, Text(resolved, "description"));
			}
			else if (!_defClasses.TryGetValue(defName, out configClass))
			{
				configClass = Build(Pascal(defName), _schema.Defs[defName]!.AsObject(), null);
				_defClasses.Add(defName, configClass);
			}
		}

		var values = resolved["enum"]?.AsArray().Select(v => v!.ToJsonString()).ToList();
		var deprecated = resolved["deprecated"]?.GetValue<bool>() == true;
		return new ConfigProperty(key, Pascal(key), kind.Value, configClass, Text(resolved, "description"), values, deprecated);
	}

	/// <summary>The C# shape of a property's schema, or null for a function, which is not config data.</summary>
	private ValueKind? Kind(JsonObject schema, string key)
	{
		if (Text(schema, "tsType") is { } tsType)
		{
			if (tsType.Contains("=>", StringComparison.Ordinal))
			{
				return null;
			}

			if (tsType == "any" || tsType.StartsWith("import(", StringComparison.Ordinal))
			{
				return ValueKind.Value;
			}

			var parts = tsType.Split('|', StringSplitOptions.TrimEntries);
			if (parts.All(p => p.Length > 1 && p[0] is '"' or '\'' && p[^1] == p[0]))
			{
				return ValueKind.String;
			}

			return parts.Length > 1 ? ValueKind.Value : Primitive(parts[0], schema, key);
		}

		if (schema["type"] is { } type)
		{
			var types = type is JsonArray list ? list.Select(t => t!.GetValue<string>()).ToList() : [type.GetValue<string>()];
			types.Remove("null");
			return types.Count > 1 ? ValueKind.Value : Primitive(types[0], schema, key);
		}

		if ((schema["oneOf"] ?? schema["anyOf"]) is JsonArray members)
		{
			var kinds = members.Select(m => Kind(_schema.Resolve(m!.AsObject()), key)).Distinct().ToList();
			return kinds.Count == 1 ? kinds[0] : ValueKind.Value;
		}

		if (schema["enum"] is JsonArray values && values.All(v => v!.GetValueKind() == JsonValueKind.String))
		{
			return ValueKind.String;
		}

		throw new InvalidDataException($"The schema of {key} has no type the generator knows.");
	}

	private static ValueKind Primitive(string type, JsonObject schema, string key) => type switch
	{
		"boolean" => ValueKind.Boolean,
		"number" or "integer" => ValueKind.Number,
		"string" => ValueKind.String,
		"array" when schema["items"]?["type"]?.GetValue<string>() == "string" => ValueKind.StringList,
		"object" when schema["properties"] is not null || schema["allOf"] is not null => ValueKind.Object,
		"object" when schema["additionalProperties"]?["type"]?.GetValue<string>() == "string" => ValueKind.StringMap,
		_ => throw new InvalidDataException($"The schema of {key} has type {type}, which the generator does not know."),
	};

	private string Class(ConfigClass configClass, string version)
	{
		var defaults = configClass.Defaults ?? throw new InvalidOperationException($"{configClass.Name} has no defaults.");
		var code = new StringBuilder();
		code.Append("// <auto-generated>\n");
		code.Append($"// Generated by tools/SplashTheMusical.Upstream from packages/mermaid/src/schemas/config.schema.yaml at mermaid@{version}.\n");
		code.Append("// Run mise run upstream-generate to rewrite it.\n");
		code.Append("// </auto-generated>\n\n");
		code.Append("#nullable enable\n\n");
		code.Append("namespace SplashTheMusical.Config;\n\n");
		Documentation(code, "", configClass.Summary, null);
		code.Append($"public sealed partial class {configClass.Name}\n{{\n");
		foreach (var property in configClass.Properties)
		{
			Documentation(code, "\t", property.Description, Remarks(property));
			code.Append($"\tpublic {TypeName(property)} {property.Name} {{ get; init; }}");
			if (Literal(property, defaults[property.Key]) is { } literal)
			{
				code.Append($" = {literal};");
			}

			code.Append("\n\n");
		}

		code.Append("\t/// <summary>The config as mermaid.js holds it. A null property is left out, as is a nested config with no values.</summary>\n");
		code.Append("\tinternal MermaidValue ToValue()\n\t{\n");
		code.Append("\t\tvar entries = new OrderedDictionary<string, MermaidValue>();\n");
		foreach (var property in configClass.Properties)
		{
			code.Append(property.Kind == ValueKind.Object
				? $"\t\tConfigValues.AddObject(entries, {Quote(property.Key)}, {property.Name}.ToValue());\n"
				: $"\t\tConfigValues.Add(entries, {Quote(property.Key)}, {property.Name});\n");
		}

		code.Append("\t\treturn MermaidValue.FromEntries(entries);\n\t}\n}\n");
		return code.ToString();
	}

	private static string TypeName(ConfigProperty property) => property.Kind switch
	{
		ValueKind.Boolean => "bool?",
		ValueKind.Number => "double?",
		ValueKind.String => "string?",
		ValueKind.Value => "MermaidValue?",
		ValueKind.StringList => "IReadOnlyList<string>?",
		ValueKind.StringMap => "IReadOnlyDictionary<string, string>?",
		_ => property.Class!.Name,
	};

	/// <summary>The C# expression of a default value, or null when the property has no default.</summary>
	private string? Literal(ConfigProperty property, JsonNode? value)
	{
		if (property.Kind == ValueKind.Object)
		{
			return Expression(property.Class!, value as JsonObject ?? []);
		}

		if (value is null)
		{
			return null;
		}

		var kind = value.GetValueKind();
		return (property.Kind, kind) switch
		{
			(ValueKind.Boolean, JsonValueKind.True or JsonValueKind.False) => kind == JsonValueKind.True ? "true" : "false",
			(ValueKind.Number, JsonValueKind.Number) => Number(value),
			(ValueKind.String, JsonValueKind.String) => Quote(value.GetValue<string>()),
			(ValueKind.Value, JsonValueKind.Number) => $"MermaidValue.FromNumber({Number(value)})",
			(ValueKind.Value, JsonValueKind.String) => $"MermaidValue.FromString({Quote(value.GetValue<string>())})",
			(ValueKind.Value, JsonValueKind.True or JsonValueKind.False) => kind == JsonValueKind.True ? "MermaidValue.True" : "MermaidValue.False",
			(ValueKind.StringList, JsonValueKind.Array) => "[" + string.Join(", ", value.AsArray().Select(v => Quote(v!.GetValue<string>()))) + "]",
			(ValueKind.StringMap, JsonValueKind.Object) => value.AsObject().Count == 0
				? "new OrderedDictionary<string, string>()"
				: "new OrderedDictionary<string, string> { " + string.Join(", ", value.AsObject().Select(e => $"[{Quote(e.Key)}] = {Quote(e.Value!.GetValue<string>())}")) + " }",
			_ => throw new InvalidDataException($"The default of {property.Key} is a {kind}, which does not fit {property.Kind}."),
		};
	}

	/// <summary>
	/// The expression of a nested config with <paramref name="defaults"/>. The first place a class is used sets its
	/// initializers, so it is <c>new()</c>. Another place lists the properties whose defaults differ.
	/// </summary>
	private string Expression(ConfigClass configClass, JsonObject defaults)
	{
		CheckKeys(configClass, defaults);
		if (configClass.Defaults is null)
		{
			configClass.Defaults = defaults;
			return "new()";
		}

		var changes = new List<string>();
		foreach (var property in configClass.Properties)
		{
			var value = defaults[property.Key];
			if (!JsonNode.DeepEquals(configClass.Defaults[property.Key], value))
			{
				changes.Add($"{property.Name} = {Literal(property, value) ?? "null"}");
			}
		}

		return changes.Count == 0 ? "new()" : $"new() {{ {string.Join(", ", changes)} }}";
	}

	private static void CheckKeys(ConfigClass configClass, JsonObject defaults)
	{
		foreach (var (key, _) in defaults)
		{
			if (!configClass.Properties.Any(p => p.Key == key))
			{
				throw new InvalidDataException($"The default config has {key}, which {configClass.Name} does not have.");
			}
		}
	}

	private static string? Remarks(ConfigProperty property)
	{
		var remarks = new StringBuilder($"The <c>{Escape(property.Key)}</c> key.");
		if (property.Values is { Count: > 0 } values)
		{
			remarks.Append(" One of ").AppendJoin(", ", values.Select(v => $"<c>{Escape(v)}</c>")).Append('.');
		}

		if (property.Deprecated)
		{
			remarks.Append(" Deprecated.");
		}

		return remarks.ToString();
	}

	private static void Documentation(StringBuilder code, string indent, string? summary, string? remarks)
	{
		if (summary is not null)
		{
			code.Append(indent).Append("/// <summary>\n");
			var lines = summary.Replace("\r\n", "\n", StringComparison.Ordinal).Trim('\n').Split('\n');
			foreach (var line in lines)
			{
				var text = Escape(line.TrimEnd());
				code.Append(indent).Append(text.Length == 0 ? "///\n" : $"/// {text}\n");
			}

			code.Append(indent).Append("/// </summary>\n");
		}

		if (remarks is not null)
		{
			code.Append(indent).Append($"/// <remarks>{remarks}</remarks>\n");
		}
	}

	private static string? Text(JsonObject schema, string key) => schema[key] is JsonValue value ? value.GetValue<string>() : null;

	private static string Number(JsonNode value) => value.GetValue<double>().ToString("R", CultureInfo.InvariantCulture);

	/// <summary><c>c4ShapeMargin</c> is <c>C4ShapeMargin</c>, and <c>external_person_bg_color</c> is <c>ExternalPersonBgColor</c>.</summary>
	private static string Pascal(string key)
	{
		var name = new StringBuilder(key.Length);
		foreach (var part in key.Split('_', '-', '$'))
		{
			if (part.Length > 0)
			{
				name.Append(char.ToUpperInvariant(part[0])).Append(part.AsSpan(1));
			}
		}

		return name.ToString();
	}

	private static string Escape(string text) =>
		text.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);

	private static string Quote(string text)
	{
		var quoted = new StringBuilder("\"");
		foreach (var c in text)
		{
			quoted.Append(c switch
			{
				'"' => "\\\"",
				'\\' => "\\\\",
				'\n' => "\\n",
				'\r' => "\\r",
				'\t' => "\\t",
				< ' ' => "\\u" + ((int)c).ToString("x4", CultureInfo.InvariantCulture),
				_ => c.ToString(),
			});
		}

		return quoted.Append('"').ToString();
	}

	private enum ValueKind
	{
		Boolean,
		Number,
		String,
		Value,
		StringList,
		StringMap,
		Object,
	}

	private sealed class ConfigClass(string name, string? summary)
	{
		public string Name { get; } = name;

		public string? Summary { get; } = summary;

		public List<ConfigProperty> Properties { get; } = [];

		public JsonObject? Defaults { get; set; }
	}

	private sealed record ConfigProperty(
		string Key,
		string Name,
		ValueKind Kind,
		ConfigClass? Class,
		string? Description,
		List<string>? Values,
		bool Deprecated);
}
