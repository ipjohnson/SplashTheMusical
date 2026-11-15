namespace SplashTheMusical.Parsing;

/// <summary>
/// Reads the <c>accTitle</c> and <c>accDescr</c> statements that every diagram grammar shares. The jison grammars, such
/// as <c>packages/mermaid/src/diagrams/flowchart/parser/flow.jison</c>, lex them as <c>accTitle\s*":"\s*</c> and
/// <c>accDescr\s*":"\s*</c> followed by the rest of the line, and <c>accDescr\s*"{"\s*</c> followed by everything up to
/// <c>}</c>. The text is kept without the space around it.
/// </summary>
internal static class AccessibilityReader
{
	/// <summary>
	/// Reads the statement at the cursor into <paramref name="document"/> and moves past it, or returns <c>false</c>
	/// without moving when the cursor is not at one.
	/// </summary>
	public static bool TryRead(ref SourceCursor cursor, MermaidDocument document)
	{
		var source = cursor.Remaining;
		var isTitle = source.StartsWith("accTitle", StringComparison.Ordinal);
		if (!isTitle && !source.StartsWith("accDescr", StringComparison.Ordinal))
		{
			return false;
		}

		var at = SkipSpace(source, "accTitle".Length);
		if (at >= source.Length || !(source[at] == ':' || (!isTitle && source[at] == '{')))
		{
			return false;
		}

		var multiline = source[at] == '{';
		var start = SkipSpace(source, at + 1);
		int end;
		int next;
		if (multiline)
		{
			var close = source[start..].IndexOf('}');
			end = close < 0 ? source.Length : start + close;
			next = close < 0 ? end : end + 1;
		}
		else
		{
			var lineEnd = source[start..].IndexOfAny('\r', '\n');
			end = next = lineEnd < 0 ? source.Length : start + lineEnd;
		}

		var text = source[start..end].Trim().ToString();
		if (isTitle)
		{
			document.AccessibilityTitle = text;
		}
		else
		{
			document.AccessibilityDescription = text;
		}

		cursor.Advance(next);
		return true;
	}

	private static int SkipSpace(ReadOnlySpan<char> source, int start)
	{
		var i = start;
		while (i < source.Length && PreambleParser.IsSpace(source[i]))
		{
			i++;
		}

		return i;
	}
}
