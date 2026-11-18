using SplashTheMusical.Parsing;

namespace SplashTheMusical.Diagrams.Flowchart;

/// <summary>
/// The text mermaid.js hands <c>flow.jison</c>, built from the diagram's body. Each step is one of mermaid.js's
/// rewrites, in its order:
/// <list type="number">
/// <item><c>cleanupText</c> in <c>preprocess.ts</c>: <c>\r\n?</c> becomes <c>\n</c>, and <c>="..."</c> inside an HTML tag becomes <c>='...'</c>.</item>
/// <item><c>removeDirectives</c>: every <c>%%{ }%%</c> directive is taken out.</item>
/// <item><c>cleanupComments</c>: <c>/^\s*%%(?!{)[^\n]+\n?/gm</c> is taken out, then the space at the start.</item>
/// <item><c>encodeEntities</c> in <c>utils.ts</c>: the last <c>;</c> of a <c>style</c> or <c>classDef</c> line with a <c>:...#</c> before it is dropped, and <c>#name;</c> becomes a placeholder.</item>
/// <item><c>Diagram.fromText</c> adds <c>\n</c>.</item>
/// <item><c>flowParser.ts</c>: <c>/}\s*\n/g</c> becomes <c>}\n</c>.</item>
/// </list>
/// The placeholders are mermaid.js's: <c>#name;</c> becomes <c>ﬂ°name¶ß</c> and <c>#123;</c> becomes <c>ﬂ°°123¶ß</c>.
/// They matter to the lexer, which has no rule for <c>°</c> or <c>¶</c> outside labels. <see cref="Restore"/> turns
/// them back into the source's text.
/// </summary>
internal static class FlowchartText
{
	public const char Ligature = 'ﬂ';
	private const string NamedEntity = "ﬂ°";
	private const string NumericEntity = "ﬂ°°";
	private const string EntityEnd = "¶ß";
	private const char LineSeparator = (char)0x2028;
	private const char ParagraphSeparator = (char)0x2029;

	/// <summary>Writes the text the lexer reads for <paramref name="body"/> to <paramref name="output"/>.</summary>
	/// <param name="foldSpaceAfterBraces">
	/// Whether to run the last step, which only <c>flowParser.ts</c> does. Other jison diagrams, such as state, get the
	/// text of the steps before it.
	/// </param>
	public static void Prepare(ReadOnlySpan<char> body, ref PooledList<char> output, bool foldSpaceAfterBraces = true)
	{
		var first = new PooledList<char>(body.Length + 1);
		var second = new PooledList<char>(body.Length + 1);
		try
		{
			FoldLineBreaks(body, ref first);
			QuoteTagAttributes(first.AsSpan(), ref second);
			first.Truncate(0);
			RemoveDirectives(second.AsSpan(), ref first);
			second.Truncate(0);
			CleanupComments(first.AsSpan(), ref second);
			first.Truncate(0);
			EncodeEntities(second.AsSpan(), ref first);
			first.Add('\n');
			if (foldSpaceAfterBraces)
			{
				FoldSpaceAfterBraces(first.AsSpan(), ref output);
			}
			else
			{
				output.AddRange(first.AsSpan());
			}
		}
		finally
		{
			first.Dispose();
			second.Dispose();
		}
	}

	/// <summary>Whether <paramref name="text"/> holds a placeholder that <see cref="Restore"/> would change.</summary>
	public static bool HasPlaceholder(ReadOnlySpan<char> text) => text.Contains(Ligature);

	/// <summary>
	/// Writes <paramref name="text"/> with mermaid.js's entity placeholders turned back into <c>#name;</c> and
	/// <c>#123;</c>, as the source wrote them.
	/// </summary>
	public static void Restore(ReadOnlySpan<char> text, ref PooledList<char> output)
	{
		int at;
		while ((at = text.IndexOfAny(Ligature, '¶')) >= 0)
		{
			output.AddRange(text[..at]);
			var rest = text[at..];
			if (rest.StartsWith(NumericEntity, StringComparison.Ordinal))
			{
				output.Add('#');
				text = rest[NumericEntity.Length..];
			}
			else if (rest.StartsWith(NamedEntity, StringComparison.Ordinal))
			{
				output.Add('#');
				text = rest[NamedEntity.Length..];
			}
			else if (rest.StartsWith(EntityEnd, StringComparison.Ordinal))
			{
				output.Add(';');
				text = rest[EntityEnd.Length..];
			}
			else
			{
				output.Add(rest[0]);
				text = rest[1..];
			}
		}

		output.AddRange(text);
	}

	/// <summary>JavaScript's <c>\w</c>.</summary>
	public static bool IsWordChar(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';

	private static void FoldLineBreaks(ReadOnlySpan<char> text, ref PooledList<char> output)
	{
		int at;
		while ((at = text.IndexOf('\r')) >= 0)
		{
			output.AddRange(text[..at]);
			output.Add('\n');
			text = text[(at + 1)..];
			if (!text.IsEmpty && text[0] == '\n')
			{
				text = text[1..];
			}
		}

		output.AddRange(text);
	}

	/// <summary><c>/&lt;(\w+)([^&gt;]*)&gt;/g</c>, with <c>/="([^"]*)"/g</c> replaced by <c>='$1'</c> in the attributes.</summary>
	private static void QuoteTagAttributes(ReadOnlySpan<char> text, ref PooledList<char> output)
	{
		var at = 0;
		while (at < text.Length)
		{
			var open = text[at..].IndexOf('<');
			if (open < 0)
			{
				break;
			}

			open += at;
			var nameEnd = open + 1;
			while (nameEnd < text.Length && IsWordChar(text[nameEnd]))
			{
				nameEnd++;
			}

			var close = nameEnd > open + 1 ? text[nameEnd..].IndexOf('>') : -1;
			if (close < 0)
			{
				output.AddRange(text[at..(open + 1)]);
				at = open + 1;
				continue;
			}

			close += nameEnd;
			output.AddRange(text[at..nameEnd]);
			var attributes = text[nameEnd..close];
			int quote;
			while ((quote = attributes.IndexOf("=\"", StringComparison.Ordinal)) >= 0)
			{
				var end = attributes[(quote + 2)..].IndexOf('"');
				if (end < 0)
				{
					output.AddRange(attributes[..(quote + 1)]);
					attributes = attributes[(quote + 1)..];
					continue;
				}

				output.AddRange(attributes[..quote]);
				output.Add('=');
				output.Add('\'');
				output.AddRange(attributes.Slice(quote + 2, end));
				output.Add('\'');
				attributes = attributes[(quote + 2 + end + 1)..];
			}

			output.AddRange(attributes);
			output.Add('>');
			at = close + 1;
		}

		output.AddRange(text[at..]);
	}

	private static void RemoveDirectives(ReadOnlySpan<char> text, ref PooledList<char> output)
	{
		int at;
		while ((at = text.IndexOf("%%{", StringComparison.Ordinal)) >= 0)
		{
			output.AddRange(text[..at]);
			var length = DirectiveReader.MatchLength(text[at..]);
			if (length == 0)
			{
				output.Add('%');
				length = 1;
			}

			text = text[(at + length)..];
		}

		output.AddRange(text);
	}

	/// <summary>
	/// <c>/^\s*%%(?!{)[^\n]+\n?/gm</c> taken out, then the space at the start. A match takes the blank lines before the
	/// comment with it, because <c>\s*</c> crosses line breaks.
	/// </summary>
	private static void CleanupComments(ReadOnlySpan<char> text, ref PooledList<char> output)
	{
		var lineStart = 0;
		while (lineStart < text.Length)
		{
			var content = lineStart;
			while (content < text.Length && PreambleParser.IsSpace(text[content]))
			{
				content++;
			}

			var lineEnd = text[content..].IndexOf('\n');
			var next = lineEnd < 0 ? text.Length : content + lineEnd + 1;
			var isComment = text[content..].StartsWith("%%", StringComparison.Ordinal)
				&& content + 2 < text.Length
				&& text[content + 2] is not '{' and not '\n';
			if (!isComment)
			{
				output.AddRange(text[lineStart..next]);
			}

			lineStart = next;
		}

		var start = 0;
		while (start < output.Count && PreambleParser.IsSpace(output[start]))
		{
			start++;
		}

		if (start > 0)
		{
			var kept = output.AsSpan()[start..];
			kept.CopyTo(output.AsSpan());
			output.Truncate(kept.Length);
		}
	}

	/// <summary>
	/// The rewrites run line by line, because JavaScript's <c>.</c> stops at a line feed and at U+2028 and U+2029.
	/// <c>\r</c> is already folded into <c>\n</c>.
	/// </summary>
	private static void EncodeEntities(ReadOnlySpan<char> text, ref PooledList<char> output)
	{
		var line = new PooledList<char>(256);
		try
		{
			while (true)
			{
				var end = text.IndexOfAny('\n', LineSeparator, ParagraphSeparator);
				var segment = end < 0 ? text : text[..end];
				line.Truncate(0);
				line.AddRange(segment);
				DropSemicolon(ref line, "style");
				DropSemicolon(ref line, "classDef");
				ReplaceEntities(line.AsSpan(), ref output);
				if (end < 0)
				{
					return;
				}

				output.Add(text[end]);
				text = text[(end + 1)..];
			}
		}
		finally
		{
			line.Dispose();
		}
	}

	/// <summary>
	/// <c>/keyword.*:\S*#.*;/g</c> over one line, with the match's last character dropped. The match starts at the first
	/// <paramref name="keyword"/> and ends at the line's last <c>;</c>, when a <c>#</c> before that <c>;</c> follows a
	/// <c>:</c> after the keyword with no space between them.
	/// </summary>
	private static void DropSemicolon(ref PooledList<char> line, string keyword)
	{
		var text = line.AsSpan();
		var start = text.IndexOf(keyword, StringComparison.Ordinal);
		var semicolon = text.LastIndexOf(';');
		if (start < 0 || semicolon < 0)
		{
			return;
		}

		var colonFrom = start + keyword.Length;
		for (var hash = colonFrom + 1; hash < semicolon; hash++)
		{
			if (text[hash] != '#')
			{
				continue;
			}

			for (var colon = hash - 1; colon >= colonFrom && !PreambleParser.IsSpace(text[colon]); colon--)
			{
				if (text[colon] == ':')
				{
					text[(semicolon + 1)..].CopyTo(text[semicolon..]);
					line.Truncate(line.Count - 1);
					return;
				}
			}
		}
	}

	/// <summary><c>/#\w+;/g</c>, each replaced by its placeholder.</summary>
	private static void ReplaceEntities(ReadOnlySpan<char> text, ref PooledList<char> output)
	{
		int at;
		while ((at = text.IndexOf('#')) >= 0)
		{
			output.AddRange(text[..at]);
			var nameEnd = at + 1;
			while (nameEnd < text.Length && IsWordChar(text[nameEnd]))
			{
				nameEnd++;
			}

			if (nameEnd == at + 1 || nameEnd == text.Length || text[nameEnd] != ';')
			{
				output.Add('#');
				text = text[(at + 1)..];
				continue;
			}

			var name = text[(at + 1)..nameEnd];
			output.AddRange(name.ContainsAnyExceptInRange('0', '9') ? NamedEntity : NumericEntity);
			output.AddRange(name);
			output.AddRange(EntityEnd);
			text = text[(nameEnd + 1)..];
		}

		output.AddRange(text);
	}

	/// <summary><c>/}\s*\n/g</c> replaced by <c>}\n</c>: the space after a <c>}</c>, up to its last line break, folds to one line break.</summary>
	private static void FoldSpaceAfterBraces(ReadOnlySpan<char> text, ref PooledList<char> output)
	{
		int at;
		while ((at = text.IndexOf('}')) >= 0)
		{
			output.AddRange(text[..(at + 1)]);
			var spaceEnd = at + 1;
			var lastBreak = -1;
			while (spaceEnd < text.Length && PreambleParser.IsSpace(text[spaceEnd]))
			{
				if (text[spaceEnd] == '\n')
				{
					lastBreak = spaceEnd;
				}

				spaceEnd++;
			}

			if (lastBreak < 0)
			{
				text = text[(at + 1)..];
				continue;
			}

			output.Add('\n');
			text = text[(lastBreak + 1)..];
		}

		output.AddRange(text);
	}
}
