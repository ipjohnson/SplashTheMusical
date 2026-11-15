using System.Globalization;
using System.Text;
using SplashTheMusical.Parsing;

namespace SplashTheMusical.Tests.Parsing;

/// <summary>Writes a <see cref="MermaidValue"/> as compact JSON, so a test can compare a whole tree with one string.</summary>
internal static class ValueJson
{
	public static string Write(MermaidValue value)
	{
		var builder = new StringBuilder();
		Write(builder, value);
		return builder.ToString();
	}

	private static void Write(StringBuilder builder, MermaidValue value)
	{
		switch (value.Kind)
		{
			case MermaidValueKind.Null:
				builder.Append("null");
				break;
			case MermaidValueKind.Boolean:
				builder.Append(value.Boolean ? "true" : "false");
				break;
			case MermaidValueKind.Number:
				builder.Append(value.Number.ToString("R", CultureInfo.InvariantCulture));
				break;
			case MermaidValueKind.String:
				WriteString(builder, value.String);
				break;
			case MermaidValueKind.Sequence:
				builder.Append('[');
				for (var i = 0; i < value.Items.Count; i++)
				{
					if (i > 0)
					{
						builder.Append(',');
					}

					Write(builder, value.Items[i]);
				}

				builder.Append(']');
				break;
			case MermaidValueKind.Mapping:
				builder.Append('{');
				var first = true;
				foreach (var (key, item) in value.Entries)
				{
					if (!first)
					{
						builder.Append(',');
					}

					first = false;
					WriteString(builder, key);
					builder.Append(':');
					Write(builder, item);
				}

				builder.Append('}');
				break;
		}
	}

	private static void WriteString(StringBuilder builder, string text)
	{
		builder.Append('"');
		foreach (var c in text)
		{
			switch (c)
			{
				case '"':
					builder.Append("\\\"");
					break;
				case '\\':
					builder.Append("\\\\");
					break;
				case '\n':
					builder.Append("\\n");
					break;
				default:
					builder.Append(c);
					break;
			}
		}

		builder.Append('"');
	}
}
