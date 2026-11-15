using System.Globalization;
using System.Text;
using SplashTheMusical.Parsing;

namespace SplashTheMusical.Writing;

/// <summary>
/// Writes the parts of a document that come before its body: the front matter, then one line per directive. The
/// inverse of <see cref="PreambleParser"/>, so a parse of the output gives back the same title, display mode, config and
/// directives.
/// </summary>
internal static class PreambleWriter
{
	private const string Indent = "  ";

	public static void Write(StringBuilder builder, MermaidDocument document)
	{
		if (document.Title is not null || document.DisplayMode is not null || document.FrontMatterConfig is not null)
		{
			builder.Append("---\n");
			if (document.Title is not null)
			{
				builder.Append("title: ");
				WriteString(builder, document.Title);
				builder.Append('\n');
			}

			if (document.DisplayMode is not null)
			{
				builder.Append("displayMode: ");
				WriteString(builder, document.DisplayMode);
				builder.Append('\n');
			}

			if (document.FrontMatterConfig is { } config)
			{
				builder.Append("config:");
				WriteAfterKey(builder, config, Indent);
			}

			builder.Append("---\n");
		}

		foreach (var directive in document.Directives)
		{
			WriteDirective(builder, directive);
		}
	}

	/// <summary>The <c>accTitle</c> and <c>accDescr</c> statements, each line starting with <paramref name="indent"/>.</summary>
	public static void WriteAccessibility(StringBuilder builder, MermaidDocument document, string indent)
	{
		if (document.AccessibilityTitle is not null)
		{
			builder.Append(indent).Append("accTitle: ").Append(document.AccessibilityTitle).Append('\n');
		}

		if (document.AccessibilityDescription is { } description)
		{
			if (description.AsSpan().IndexOfAny('\r', '\n') < 0)
			{
				builder.Append(indent).Append("accDescr: ").Append(description).Append('\n');
			}
			else
			{
				builder.Append(indent).Append("accDescr {\n").Append(description).Append('\n').Append(indent).Append("}\n");
			}
		}
	}

	private static void WriteDirective(StringBuilder builder, MermaidDirective directive)
	{
		builder.Append("%%{").Append(directive.Type);
		if (directive.InvalidArguments is not null)
		{
			builder.Append(": ").Append(directive.InvalidArguments);
		}
		else if (directive.Arguments is { } arguments)
		{
			builder.Append(": ");
			if (arguments.Kind == MermaidValueKind.String && IsWord(arguments.String))
			{
				builder.Append(arguments.String);
			}
			else
			{
				WriteJson(builder, arguments);
			}
		}

		builder.Append("}%%\n");
	}

	private static bool IsWord(string text)
	{
		if (text.Length == 0)
		{
			return false;
		}

		foreach (var c in text)
		{
			if (!char.IsAsciiLetterOrDigit(c) && c != '_')
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// JSON for a directive. mermaid.js replaces every <c>'</c> in a directive with <c>"</c>, and its pattern ends a
	/// directive at the first <c>}%%</c>, so strings escape both <c>'</c> and <c>%</c>.
	/// </summary>
	private static void WriteJson(StringBuilder builder, MermaidValue value)
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
				builder.Append(double.IsFinite(value.Number) ? JsNumber.ToString(value.Number) : "null");
				break;
			case MermaidValueKind.String:
				WriteQuoted(builder, value.String, escapeDirectiveCharacters: true);
				break;
			case MermaidValueKind.Sequence:
				builder.Append('[');
				for (var i = 0; i < value.Items.Count; i++)
				{
					builder.Append(i > 0 ? ", " : "");
					WriteJson(builder, value.Items[i]);
				}

				builder.Append(']');
				break;
			default:
				builder.Append('{');
				var first = true;
				foreach (var (key, item) in value.Entries)
				{
					builder.Append(first ? "" : ", ");
					first = false;
					WriteQuoted(builder, key, escapeDirectiveCharacters: true);
					builder.Append(": ");
					WriteJson(builder, item);
				}

				builder.Append('}');
				break;
		}
	}

	/// <summary>The value after <c>key:</c>, then a line break. A mapping or a sequence goes on the next lines at <paramref name="indent"/>.</summary>
	private static void WriteAfterKey(StringBuilder builder, MermaidValue value, string indent)
	{
		switch (value.Kind)
		{
			case MermaidValueKind.Mapping when value.Entries.Count > 0:
				builder.Append('\n');
				WriteMapping(builder, value, indent, indent);
				break;
			case MermaidValueKind.Sequence when value.Items.Count > 0:
				builder.Append('\n');
				WriteSequence(builder, value, indent);
				break;
			default:
				builder.Append(' ');
				WriteScalar(builder, value);
				builder.Append('\n');
				break;
		}
	}

	/// <summary>The entries of a mapping. The first key follows <paramref name="firstIndent"/>, which is empty after a sequence's <c>- </c>.</summary>
	private static void WriteMapping(StringBuilder builder, MermaidValue mapping, string firstIndent, string indent)
	{
		var first = true;
		foreach (var (key, value) in mapping.Entries)
		{
			builder.Append(first ? firstIndent : indent);
			first = false;
			WriteKey(builder, key);
			builder.Append(':');
			WriteAfterKey(builder, value, indent + Indent);
		}
	}

	private static void WriteSequence(StringBuilder builder, MermaidValue sequence, string indent)
	{
		foreach (var item in sequence.Items)
		{
			builder.Append(indent).Append("- ");
			switch (item.Kind)
			{
				case MermaidValueKind.Mapping when item.Entries.Count > 0:
					WriteMapping(builder, item, "", indent + Indent);
					break;
				case MermaidValueKind.Sequence when item.Items.Count > 0:
					WriteJson(builder, item);
					builder.Append('\n');
					break;
				default:
					WriteScalar(builder, item);
					builder.Append('\n');
					break;
			}
		}
	}

	private static void WriteScalar(StringBuilder builder, MermaidValue value)
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
				var number = value.Number;
				builder.Append(
					double.IsNaN(number) ? ".nan"
					: double.IsPositiveInfinity(number) ? ".inf"
					: double.IsNegativeInfinity(number) ? "-.inf"
					: JsNumber.ToString(number));
				break;
			case MermaidValueKind.String:
				WriteString(builder, value.String);
				break;
			case MermaidValueKind.Sequence:
				builder.Append("[]");
				break;
			default:
				builder.Append("{}");
				break;
		}
	}

	private static void WriteKey(StringBuilder builder, string key)
	{
		if (IsPlainSafe(key))
		{
			builder.Append(key);
		}
		else
		{
			WriteQuoted(builder, key, escapeDirectiveCharacters: false);
		}
	}

	/// <summary>A YAML string: plain when it would read back as the same string, otherwise double-quoted.</summary>
	private static void WriteString(StringBuilder builder, string text)
	{
		if (IsPlainSafe(text) && YamlReader.ResolvePlain(text).Kind == MermaidValueKind.String)
		{
			builder.Append(text);
		}
		else
		{
			WriteQuoted(builder, text, escapeDirectiveCharacters: false);
		}
	}

	/// <summary>Letters, digits, spaces between words and punctuation that has no meaning in a plain YAML scalar.</summary>
	private static bool IsPlainSafe(string text)
	{
		if (text.Length == 0 || text[^1] == ' ' || !(char.IsLetterOrDigit(text[0]) || text[0] == '_'))
		{
			return false;
		}

		foreach (var c in text)
		{
			if (!char.IsLetterOrDigit(c) && c is not (' ' or '_' or '.' or '-' or '/' or '(' or ')' or '+' or '='))
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>A double-quoted string, which YAML and JSON read the same way.</summary>
	private static void WriteQuoted(StringBuilder builder, string text, bool escapeDirectiveCharacters)
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
				case '\r':
					builder.Append("\\r");
					break;
				case '\t':
					builder.Append("\\t");
					break;
				default:
					if (NeedsEscape(c, escapeDirectiveCharacters))
					{
						builder.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
					}
					else
					{
						builder.Append(c);
					}

					break;
			}
		}

		builder.Append('"');
	}

	/// <summary>Control characters, the Unicode line and paragraph separators and the byte order mark, and for a directive also <c>'</c>, <c>%</c> and surrogates.</summary>
	private static bool NeedsEscape(char c, bool escapeDirectiveCharacters)
	{
		int code = c;
		return code < 0x20
			|| (code >= 0x7F && code <= 0x9F)
			|| code is 0x2028 or 0x2029 or 0xFEFF
			|| (escapeDirectiveCharacters && (c is '\'' or '%' || char.IsSurrogate(c)));
	}
}
