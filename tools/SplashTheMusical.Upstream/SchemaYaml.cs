using System.Globalization;
using System.Text.Json.Nodes;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace SplashTheMusical.Upstream;

/// <summary>
/// Reads config.schema.yaml into JSON nodes, as mermaid's .build/jsonSchema.ts loads it with js-yaml's <c>JSON_SCHEMA</c>:
/// a plain <c>null</c>, <c>true</c>, <c>false</c> or number resolves to that JSON value, and every other scalar is a string.
/// Aliases resolve to a copy of their anchor's value.
/// </summary>
public static class SchemaYaml
{
	public static JsonObject Read(string text)
	{
		var stream = new YamlStream();
		stream.Load(new StringReader(text));
		return Convert(stream.Documents.Single().RootNode)?.AsObject()
			?? throw new InvalidDataException("config.schema.yaml is not a mapping.");
	}

	private static JsonNode? Convert(YamlNode node)
	{
		switch (node)
		{
			case YamlMappingNode mapping:
				var result = new JsonObject();
				foreach (var (key, value) in mapping.Children)
				{
					result[((YamlScalarNode)key).Value!] = Convert(value);
				}

				return result;
			case YamlSequenceNode sequence:
				return new JsonArray([.. sequence.Children.Select(Convert)]);
			case YamlScalarNode scalar:
				return Scalar(scalar);
			default:
				throw new InvalidDataException($"Unexpected YAML node {node.NodeType} at {node.Start}.");
		}
	}

	private static JsonNode? Scalar(YamlScalarNode scalar)
	{
		var text = scalar.Value ?? "";
		if (scalar.Style is not ScalarStyle.Plain)
		{
			return JsonValue.Create(text);
		}

		switch (text)
		{
			case "" or "~" or "null":
				return null;
			case "true":
				return JsonValue.Create(true);
			case "false":
				return JsonValue.Create(false);
		}

		var numeric = text.Length > 0 && (char.IsAsciiDigit(text[0]) || text[0] is '-' or '+' or '.');
		return numeric && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
			? JsonValue.Create(number)
			: JsonValue.Create(text);
	}
}
