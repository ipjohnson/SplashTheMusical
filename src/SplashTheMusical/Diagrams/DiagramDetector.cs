using System.Text;
using SplashTheMusical.Parsing;

namespace SplashTheMusical.Diagrams;

/// <summary>
/// mermaid.js's <c>detectType</c> in <c>packages/mermaid/src/diagram-api/detectType.ts</c>, over the detectors
/// <c>addDiagrams</c> in <c>diagram-orchestration.ts</c> registers, in their order. Each detector is a regular expression
/// over the text that <c>preprocessDiagram</c> and <c>detectType</c> leave: no front matter, no directives and no
/// <c>%%</c> comments. Here one pass finds the first keyword of that text in the source and a <c>switch</c> on its first
/// character picks the detector.
/// </summary>
/// <remarks>
/// <c>docs/upstream-map.md</c> lists the detectors. Most are prefix matches with no word boundary, so <c>graphs</c> is a
/// flowchart. The <c>c4</c> detector is <c>/^\s*C4Context|C4Container|C4Component|C4Dynamic|C4Deployment/</c>, so the last
/// four keywords match anywhere in the text, ahead of every detector registered after <c>c4</c>.
/// </remarks>
internal static class DiagramDetector
{
	private static readonly string[] AnywhereC4 = ["C4Container", "C4Component", "C4Dynamic", "C4Deployment"];

	/// <summary>
	/// The id of the first of mermaid.js's detectors that matches, or <c>null</c> when none does.
	/// <paramref name="keyword"/> is the offset in <paramref name="source"/> of the text the detectors see.
	/// </summary>
	/// <param name="bodyStart">The offset after the front matter, from <see cref="PreambleParser.TryParse"/>.</param>
	public static string? Detect(ReadOnlySpan<char> source, int bodyStart, out int keyword)
	{
		var body = source[bodyStart..];
		var start = SkipIgnored(body, 0);
		keyword = bodyStart + start;
		var text = body[start..];
		if (IsError(body, start))
		{
			return "error";
		}

		if (text.StartsWith("---", StringComparison.Ordinal))
		{
			return "---";
		}

		if (text.StartsWith("flowchart-elk", StringComparison.Ordinal))
		{
			return "flowchart-elk";
		}

		if (text.StartsWith("mindmap", StringComparison.Ordinal))
		{
			return "mindmap";
		}

		if (text.StartsWith("architecture", StringComparison.Ordinal))
		{
			return "architecture";
		}

		if (Word(body, start, "agentflow-beta"))
		{
			return "agentflow";
		}

		if (text.StartsWith("C4Context", StringComparison.Ordinal) || HasAnywhereC4(body))
		{
			return "c4";
		}

		if (text.IsEmpty)
		{
			return null;
		}

		return text[0] switch
		{
			'k' when Prefix(text, "kanban") => "kanban",
			'c' when Prefix(text, "classDiagram") => "classDiagram",
			'e' when Prefix(text, "erDiagram") => "er",
			'g' when Prefix(text, "gantt") => "gantt",
			'i' when Prefix(text, "info") => "info",
			'p' when Prefix(text, "pie") => "pie",
			'r' when Prefix(text, "requirement") => "requirement",
			's' when Prefix(text, "sequenceDiagram") => "sequence",
			's' when Word(body, start, "swimlane-beta") => "swimlane",
			'g' or 'f' when Prefix(text, "graph") || Prefix(text, "flowchart") => "flowchart-v2",
			't' when Prefix(text, "timeline") => "timeline",
			'g' when Prefix(text, "gitGraph") => "gitGraph",
			's' when Prefix(text, "stateDiagram") => "stateDiagram",
			'j' when Prefix(text, "journey") => "journey",
			'q' when Prefix(text, "quadrantChart") => "quadrantChart",
			's' when Prefix(text, "sankey") => "sankey",
			'p' when Prefix(text, "packet") => "packet",
			'x' when Prefix(text, "xychart") => "xychart",
			'b' when Prefix(text, "block") => "block",
			'e' when Prefix(text, "eventmodeling") => "eventmodeling",
			't' when Prefix(text, "treeView-beta") => "treeView",
			'r' when Prefix(text, "radar-beta") => "radar",
			'i' or 'I' when Word(body, start, "ishikawa", ignoreCase: true) => "ishikawa",
			't' when Prefix(text, "treemap") => "treemap",
			'r' or 'R' when PrefixIgnoreCase(text, "railroad-beta") => "railroad",
			'r' or 'R' when PrefixIgnoreCase(text, "railroad-ebnf-beta") => "railroadEbnf",
			'r' or 'R' when PrefixIgnoreCase(text, "railroad-abnf-beta") => "railroadAbnf",
			'r' or 'R' when PrefixIgnoreCase(text, "railroad-peg-beta") => "railroadPeg",
			'v' when Prefix(text, "venn-beta") => "venn",
			'w' or 'W' when PrefixIgnoreCase(text, "wardley-beta") => "wardley",
			'c' when Prefix(text, "cynefin-beta") && EndsOrSpace(body, start + "cynefin-beta".Length, colon: true) => "cynefin",
			'u' when Prefix(text, "usecase-beta") && EndsOrSpace(body, start + "usecase-beta".Length, colon: false) => "usecase",
			_ => null,
		};
	}

	/// <summary><c>(?:\s|$)</c>, or <c>(?:[\s:]|$)</c> with <paramref name="colon"/>, after a keyword.</summary>
	private static bool EndsOrSpace(ReadOnlySpan<char> body, int at, bool colon) =>
		Next(body, at) is not { } next || PreambleParser.IsSpace(next) || (colon && next == ':');

	/// <summary>
	/// The text the detectors see, which mermaid.js's error for an unknown type quotes: <paramref name="body"/> with the
	/// directives and comments taken out as <c>removeDirectives</c>, <c>cleanupComments</c> and <c>stripAnyComments</c>
	/// take them out.
	/// </summary>
	public static string DetectedText(ReadOnlySpan<char> body)
	{
		var text = new StringBuilder(body.Length);
		var at = 0;
		while (at < body.Length)
		{
			var next = body[at..].IndexOf("%%{", StringComparison.Ordinal);
			if (next < 0)
			{
				text.Append(body[at..]);
				break;
			}

			text.Append(body.Slice(at, next));
			at += next;
			var length = DirectiveReader.MatchLength(body[at..]);
			if (length == 0)
			{
				text.Append('%');
				at++;
			}
			else
			{
				at += length;
			}
		}

		var withoutDirectives = text.Replace("\r\n", "\n").Replace('\r', '\n').ToString();
		return StripAnyComments(CleanupComments(withoutDirectives));
	}

	/// <summary>
	/// The offset of the first character the detectors see: after space, directives and the comments that
	/// <c>cleanupComments</c> and <c>stripAnyComments</c> take out. A <c>%%</c> with no line break after it stays.
	/// </summary>
	private static int SkipIgnored(ReadOnlySpan<char> body, int at)
	{
		while (true)
		{
			while (at < body.Length && PreambleParser.IsSpace(body[at]))
			{
				at++;
			}

			if (!body[at..].StartsWith("%%", StringComparison.Ordinal))
			{
				return at;
			}

			var directive = DirectiveReader.MatchLength(body[at..]);
			if (directive > 0)
			{
				at += directive;
				continue;
			}

			var lineEnd = body[at..].IndexOfAny('\r', '\n');
			if (lineEnd >= 0)
			{
				at += lineEnd + 1;
				continue;
			}

			// cleanupComments takes out a last line of `%%` and more, except `%%{`.
			return at + 2 < body.Length && body[at + 2] != '{' ? body.Length : at;
		}
	}

	/// <summary>The <c>error</c> detector: the text, lowered and trimmed, is <c>error</c>.</summary>
	private static bool IsError(ReadOnlySpan<char> body, int start)
	{
		const string Error = "error";
		if (body.Length - start < Error.Length || !body.Slice(start, Error.Length).Equals(Error, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		var at = start + Error.Length;
		var lineStart = false;
		while (true)
		{
			while (at < body.Length && PreambleParser.IsSpace(body[at]))
			{
				lineStart |= body[at] is '\r' or '\n';
				at++;
			}

			if (at == body.Length)
			{
				return true;
			}

			if (!body[at..].StartsWith("%%", StringComparison.Ordinal))
			{
				return false;
			}

			var directive = DirectiveReader.MatchLength(body[at..]);
			if (directive > 0)
			{
				at += directive;
				continue;
			}

			var lineEnd = body[at..].IndexOfAny('\r', '\n');
			if (lineEnd >= 0)
			{
				at += lineEnd;
				continue;
			}

			return lineStart && at + 2 < body.Length && body[at + 2] != '{';
		}
	}

	/// <summary>Whether one of the keywords the <c>c4</c> detector finds anywhere is in the text the detectors see.</summary>
	private static bool HasAnywhereC4(ReadOnlySpan<char> body)
	{
		var found = false;
		foreach (var keyword in AnywhereC4)
		{
			found |= body.Contains(keyword, StringComparison.Ordinal);
		}

		if (!found)
		{
			return false;
		}

		var text = DetectedText(body);
		foreach (var keyword in AnywhereC4)
		{
			if (text.Contains(keyword, StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}

	private static bool Prefix(ReadOnlySpan<char> text, string keyword) => text.StartsWith(keyword, StringComparison.Ordinal);

	/// <summary>A prefix match of the <c>i</c> flag, which folds ASCII letters only.</summary>
	private static bool PrefixIgnoreCase(ReadOnlySpan<char> text, string keyword) =>
		text.Length >= keyword.Length && Ascii.EqualsIgnoreCase(text[..keyword.Length], keyword);

	/// <summary>A prefix match followed by <c>\b</c>: the next character the detectors see is not a word character.</summary>
	private static bool Word(ReadOnlySpan<char> body, int start, string keyword, bool ignoreCase = false)
	{
		var text = body[start..];
		if (!(ignoreCase ? PrefixIgnoreCase(text, keyword) : Prefix(text, keyword)))
		{
			return false;
		}

		return Next(body, start + keyword.Length) is not { } next || !(char.IsAsciiLetterOrDigit(next) || next == '_');
	}

	/// <summary>
	/// The character the detectors see at <paramref name="at"/> of the source, or <c>null</c> at the end: a directive is
	/// taken out, and a comment before a line break becomes the line break.
	/// </summary>
	private static char? Next(ReadOnlySpan<char> body, int at)
	{
		while (at < body.Length && body[at..].StartsWith("%%", StringComparison.Ordinal))
		{
			var directive = DirectiveReader.MatchLength(body[at..]);
			if (directive == 0)
			{
				return body[at..].IndexOfAny('\r', '\n') >= 0 ? '\n' : '%';
			}

			at += directive;
		}

		return at < body.Length ? body[at] : null;
	}

	/// <summary><c>cleanupComments</c>: <c>/^\s*%%(?!{)[^\n]+\n?/gm</c> taken out, then the space at the start.</summary>
	private static string CleanupComments(string text)
	{
		var result = new StringBuilder(text.Length);
		var at = 0;
		while (at < text.Length)
		{
			var lineEnd = text.IndexOf('\n', at);
			var end = lineEnd < 0 ? text.Length : lineEnd + 1;
			var line = text.AsSpan(at, end - at);
			var indent = 0;
			while (indent < line.Length && line[indent] != '\n' && PreambleParser.IsSpace(line[indent]))
			{
				indent++;
			}

			var content = line[indent..];
			var isComment = content.StartsWith("%%", StringComparison.Ordinal)
				&& content.Length > 2
				&& content[2] is not '{' and not '\n';
			if (!isComment)
			{
				result.Append(line);
			}

			at = end;
		}

		var start = 0;
		while (start < result.Length && PreambleParser.IsSpace(result[start]))
		{
			start++;
		}

		return result.ToString(start, result.Length - start);
	}

	/// <summary>
	/// <c>stripAnyComments</c>: each <c>%%</c> that a line break follows, with the space before it, up to and including
	/// that line break, becomes a line break.
	/// </summary>
	private static string StripAnyComments(string text)
	{
		var result = new StringBuilder(text.Length);
		var consumed = 0;
		var searchFrom = 0;
		while (searchFrom < text.Length)
		{
			var marker = text.IndexOf("%%", searchFrom, StringComparison.Ordinal);
			if (marker < 0)
			{
				break;
			}

			var lineEnd = text.IndexOf('\n', marker);
			if (lineEnd < 0)
			{
				break;
			}

			var start = marker;
			while (start > consumed && PreambleParser.IsSpace(text[start - 1]))
			{
				start--;
			}

			result.Append(text, consumed, start - consumed).Append('\n');
			consumed = lineEnd + 1;
			searchFrom = consumed;
		}

		return result.Append(text, consumed, text.Length - consumed).ToString();
	}
}
