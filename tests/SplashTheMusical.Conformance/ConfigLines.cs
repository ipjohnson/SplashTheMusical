using System.Globalization;
using System.Text;
using SplashTheMusical.Parsing;

namespace SplashTheMusical.Conformance;

/// <summary>
/// Writes a config as the oracle writes <c>preamble-config</c> facts and the lines of config.txt: one line per path, with
/// nested mappings and sequences flattened into dotted paths and each value written as JSON.
/// </summary>
public static class ConfigLines
{
	/// <summary>Adds a line of <paramref name="kind"/>, path and value for each value in <paramref name="value"/>.</summary>
	public static void Flatten(List<string> lines, string kind, string prefix, MermaidValue value)
	{
		switch (value.Kind)
		{
			case MermaidValueKind.Mapping:
				if (value.Entries.Count == 0 && prefix.Length > 0)
				{
					lines.Add(Line(kind, prefix, "{}"));
				}

				foreach (var (key, item) in value.Entries)
				{
					Flatten(lines, kind, prefix.Length > 0 ? prefix + "." + key : key, item);
				}

				break;
			case MermaidValueKind.Sequence:
				if (value.Items.Count == 0)
				{
					lines.Add(Line(kind, prefix, "[]"));
				}

				for (var i = 0; i < value.Items.Count; i++)
				{
					Flatten(lines, kind, prefix + "." + i.ToString(CultureInfo.InvariantCulture), value.Items[i]);
				}

				break;
			default:
				lines.Add(Line(kind, prefix, Json(value)));
				break;
		}
	}

	/// <summary>A fact line as extract.mjs's <c>tab</c> writes it: each field with its space folded and trimmed, and no trailing empty fields.</summary>
	public static string Line(params string[] fields) =>
		string.Join('\t', fields.Select(Fold)).TrimEnd('\t');

	/// <summary>JavaScript's <c>JSON.stringify</c> of a scalar.</summary>
	private static string Json(MermaidValue value)
	{
		switch (value.Kind)
		{
			case MermaidValueKind.Null:
				return "null";
			case MermaidValueKind.Boolean:
				return value.Boolean ? "true" : "false";
			case MermaidValueKind.Number:
				return double.IsFinite(value.Number) ? JsNumber.ToString(value.Number) : "null";
			default:
				var builder = new StringBuilder("\"");
				foreach (var c in value.String)
				{
					builder.Append(c switch
					{
						'"' => "\\\"",
						'\\' => "\\\\",
						'\b' => "\\b",
						'\f' => "\\f",
						'\n' => "\\n",
						'\r' => "\\r",
						'\t' => "\\t",
						< ' ' => "\\u" + ((int)c).ToString("x4", CultureInfo.InvariantCulture),
						_ => c.ToString(),
					});
				}

				return builder.Append('"').ToString();
		}
	}

	private static string Fold(string text)
	{
		var builder = new StringBuilder(text.Length);
		var space = false;
		foreach (var c in text)
		{
			if (PreambleParser.IsSpace(c))
			{
				space = true;
				continue;
			}

			if (space && builder.Length > 0)
			{
				builder.Append(' ');
			}

			space = false;
			builder.Append(c);
		}

		return builder.ToString();
	}
}
