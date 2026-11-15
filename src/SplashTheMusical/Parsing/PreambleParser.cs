using System.Text;

namespace SplashTheMusical.Parsing;

/// <summary>
/// Reads the parts of a diagram that come before its body, the way mermaid.js's <c>preprocessDiagram</c> in
/// <c>packages/mermaid/src/preprocess.ts</c> does: the front matter (<c>matchFrontMatter</c> in
/// <c>diagram-api/regexes.ts</c> and <c>extractFrontMatter</c> in <c>diagram-api/frontmatter.ts</c>), and the
/// <c>%%{ }%%</c> directives, which mermaid.js reads anywhere in the text after the front matter.
/// </summary>
internal static class PreambleParser
{
	private const string Fence = "---";

	/// <summary>
	/// Reads the front matter and every directive of <paramref name="source"/> into <paramref name="document"/>. Returns
	/// <c>false</c> when the front matter is not valid YAML, which mermaid.js refuses, with the error in
	/// <paramref name="diagnostics"/>. <paramref name="bodyStart"/> is the offset of the first character after the front
	/// matter and the blank lines, comments and directives that follow it.
	/// </summary>
	/// <exception cref="MermaidResourceLimitException">The front matter or a directive nests deeper than <see cref="ResourceLimits.MaxRecursionDepth"/>.</exception>
	public static bool TryParse(
		ReadOnlySpan<char> source,
		MermaidDocument document,
		List<MermaidDiagnostic> diagnostics,
		ResourceLimits limits,
		out int bodyStart)
	{
		bodyStart = 0;
		var cursor = new SourceCursor(source);
		if (MatchFrontMatter(source, out var indentLength, out var yamlStart, out var yamlEnd, out var length))
		{
			cursor.Advance(yamlStart);
			if (!ReadFrontMatter(source[yamlStart..yamlEnd], source[..indentLength], document, diagnostics, limits, cursor.Line))
			{
				return false;
			}

			cursor.Advance(length - yamlStart);
		}

		bodyStart = SkipToBody(ref cursor, document, diagnostics, limits);
		ReadDirectives(ref cursor, document, diagnostics, limits);
		return true;
	}

	/// <summary>
	/// mermaid.js's <c>matchFrontMatter</c>. The opening <c>---</c> may follow spaces on the first line, and that indent
	/// is taken off every line of the body. A closing fence is the indent and <c>---</c> after a line break, followed by
	/// space that holds a line break. The opening line break is the last one before the last closing fence, and the
	/// first closing fence after it ends the front matter.
	/// </summary>
	private static bool MatchFrontMatter(
		ReadOnlySpan<char> text,
		out int indentLength,
		out int bodyStart,
		out int bodyEnd,
		out int length)
	{
		bodyStart = bodyEnd = length = 0;
		indentLength = 0;
		while (indentLength < text.Length && IsSpace(text[indentLength]) && !IsLineBreak(text[indentLength]))
		{
			indentLength++;
		}

		if (!text[indentLength..].StartsWith(Fence, StringComparison.Ordinal))
		{
			return false;
		}

		var firstOpening = -1;
		var openingRunEnd = indentLength + Fence.Length;
		for (; openingRunEnd < text.Length && IsSpace(text[openingRunEnd]); openingRunEnd++)
		{
			if (firstOpening < 0 && IsLineBreak(text[openingRunEnd]))
			{
				firstOpening = openingRunEnd;
			}
		}

		if (firstOpening < 0)
		{
			return false;
		}

		var indent = text[..indentLength];
		var lastClosing = -1;
		for (var i = firstOpening + 1; i < text.Length; i++)
		{
			if (ClosingEnd(text, i, indent) >= 0)
			{
				lastClosing = i;
			}
		}

		if (lastClosing < 0)
		{
			return false;
		}

		var opening = firstOpening;
		for (var i = firstOpening; i < openingRunEnd && i < lastClosing; i++)
		{
			if (IsLineBreak(text[i]))
			{
				opening = i;
			}
		}

		bodyStart = opening + 1;
		for (var i = bodyStart; i <= lastClosing; i++)
		{
			var end = ClosingEnd(text, i, indent);
			if (end >= 0)
			{
				bodyEnd = i;
				length = end + 1;
				return true;
			}
		}

		return false;
	}

	/// <summary>The offset of the last line break in the space after a closing fence that starts after the line break at <paramref name="at"/>, or -1.</summary>
	private static int ClosingEnd(ReadOnlySpan<char> text, int at, ReadOnlySpan<char> indent)
	{
		if (!IsLineBreak(text[at])
			|| !text[(at + 1)..].StartsWith(indent, StringComparison.Ordinal)
			|| !text[(at + 1 + indent.Length)..].StartsWith(Fence, StringComparison.Ordinal))
		{
			return -1;
		}

		var last = -1;
		for (var i = at + 1 + indent.Length + Fence.Length; i < text.Length && IsSpace(text[i]); i++)
		{
			if (IsLineBreak(text[i]))
			{
				last = i;
			}
		}

		return last;
	}

	private static bool ReadFrontMatter(
		ReadOnlySpan<char> yaml,
		ReadOnlySpan<char> indent,
		MermaidDocument document,
		List<MermaidDiagnostic> diagnostics,
		ResourceLimits limits,
		int firstLine)
	{
		var value = indent.IsEmpty
			? YamlReader.Read(yaml, diagnostics, limits, firstLine)
			: YamlReader.Read(Unindent(yaml, indent), diagnostics, limits, firstLine);
		if (value is null)
		{
			return false;
		}

		if (value.Kind != MermaidValueKind.Mapping)
		{
			return true;
		}

		document.DisplayMode = StringIfTruthy(value["displayMode"]);
		document.Title = StringIfTruthy(value["title"]);
		var config = value["config"];
		if (config is not null && IsTruthy(config))
		{
			if (config.Kind == MermaidValueKind.Mapping)
			{
				document.FrontMatterConfig = config;
			}
			else
			{
				diagnostics.Add(new MermaidDiagnostic(
					DiagnosticSeverity.Warning,
					$"The front matter config is ignored because it is a {config.Kind}, not a mapping.",
					firstLine,
					1));
			}
		}

		return true;
	}

	/// <summary>The body with <paramref name="indent"/> taken off each line that starts with it, as mermaid.js does.</summary>
	private static string Unindent(ReadOnlySpan<char> yaml, ReadOnlySpan<char> indent)
	{
		var builder = new StringBuilder(yaml.Length);
		while (true)
		{
			var lineEnd = yaml.IndexOf('\n');
			var line = lineEnd < 0 ? yaml : yaml[..(lineEnd + 1)];
			builder.Append(line.StartsWith(indent, StringComparison.Ordinal) ? line[indent.Length..] : line);
			if (lineEnd < 0)
			{
				return builder.ToString();
			}

			yaml = yaml[(lineEnd + 1)..];
		}
	}

	/// <summary>Skips the space, <c>%%</c> comments and directives before the body, reading the directives.</summary>
	private static int SkipToBody(
		ref SourceCursor cursor,
		MermaidDocument document,
		List<MermaidDiagnostic> diagnostics,
		ResourceLimits limits)
	{
		while (true)
		{
			var remaining = cursor.Remaining;
			var space = 0;
			while (space < remaining.Length && IsSpace(remaining[space]))
			{
				space++;
			}

			cursor.Advance(space);
			if (cursor.Current != '%' || cursor.Peek(1) != '%')
			{
				return cursor.Position;
			}

			if (cursor.Peek(2) == '{')
			{
				var start = cursor.Position;
				if (!DirectiveReader.TryRead(ref cursor, diagnostics, limits, out var directive))
				{
					return start;
				}

				document.Directives.Add(directive!);
				continue;
			}

			var lineEnd = cursor.Remaining.IndexOfAny('\r', '\n');
			cursor.Advance(lineEnd < 0 ? cursor.Remaining.Length : lineEnd);
		}
	}

	/// <summary>Reads the directives in the rest of the text, as mermaid.js's directive pattern finds them anywhere.</summary>
	private static void ReadDirectives(
		ref SourceCursor cursor,
		MermaidDocument document,
		List<MermaidDiagnostic> diagnostics,
		ResourceLimits limits)
	{
		while (true)
		{
			var next = cursor.Remaining.IndexOf("%%{", StringComparison.Ordinal);
			if (next < 0)
			{
				return;
			}

			cursor.Advance(next);
			if (DirectiveReader.TryRead(ref cursor, diagnostics, limits, out var directive))
			{
				document.Directives.Add(directive!);
			}
			else
			{
				cursor.Advance(1);
			}
		}
	}

	/// <summary>The value's JavaScript string form when the value is truthy, as <c>extractFrontMatter</c> reads title and displayMode.</summary>
	private static string? StringIfTruthy(MermaidValue? value) =>
		value is not null && IsTruthy(value) ? ToJsString(value) : null;

	internal static bool IsTruthy(MermaidValue value) => value.Kind switch
	{
		MermaidValueKind.Null => false,
		MermaidValueKind.Boolean => value.Boolean,
		MermaidValueKind.Number => value.Number != 0 && !double.IsNaN(value.Number),
		MermaidValueKind.String => value.String.Length > 0,
		_ => true,
	};

	/// <summary>JavaScript's <c>String(value)</c>. An array joins its items with commas, and an object is <c>[object Object]</c>.</summary>
	internal static string ToJsString(MermaidValue value)
	{
		switch (value.Kind)
		{
			case MermaidValueKind.Null:
				return "null";
			case MermaidValueKind.Boolean:
				return value.Boolean ? "true" : "false";
			case MermaidValueKind.Number:
				return JsNumber.ToString(value.Number);
			case MermaidValueKind.String:
				return value.String;
			case MermaidValueKind.Sequence:
				var builder = new StringBuilder();
				for (var i = 0; i < value.Items.Count; i++)
				{
					if (i > 0)
					{
						builder.Append(',');
					}

					var item = value.Items[i];
					if (item.Kind != MermaidValueKind.Null)
					{
						builder.Append(ToJsString(item));
					}
				}

				return builder.ToString();
			default:
				return "[object Object]";
		}
	}

	/// <summary>JavaScript's <c>\s</c>.</summary>
	internal static bool IsSpace(char c) => c == (char)0xFEFF || (char.IsWhiteSpace(c) && c != (char)0x85);

	private static bool IsLineBreak(char c) => c is '\n' or '\r';
}
