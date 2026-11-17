using System.Text;

namespace SplashTheMusical.Diagrams.Flowchart;

/// <summary>
/// The text a browser shows for a flowchart label, as mermaid.js's <c>createText</c> draws it. mermaid.js turns the
/// label's entity codes into HTML entities and sanitizes it. An HTML label is then rendered as Markdown or as text with
/// <c>\n</c> as a line break, each <c>fa:fa-name</c> becomes an icon, and the result is set as HTML. An SVG label is
/// split into lines of words, and its tags are drawn as text.
/// </summary>
/// <remarks>An SVG label is not wrapped at <c>wrappingWidth</c>, which takes the renderer's text measurement.</remarks>
internal static class FlowchartLabels
{
	/// <summary>
	/// The label's text, and the Font Awesome icons it draws, as <c>fa:fa-name</c>. An HTML label reads each line break
	/// as a space. An SVG label's lines are separate <c>tspan</c> elements, so they run together.
	/// </summary>
	public static string Text(string label, FlowchartLabelType type, bool htmlLabels = true, List<string>? icons = null)
	{
		var html = new StringBuilder(label.Length + 8);
		WithHtmlEntities(label, html);
		var text = html.ToString();
		html.Clear();
		if (!htmlLabels)
		{
			return SvgText(text, type);
		}

		if (type == FlowchartLabelType.Markdown)
		{
			MarkdownText(text, html);
		}
		else
		{
			LineBreaks(text, html);
		}

		text = html.ToString();
		html.Clear();
		ReplaceIcons(text, html, icons);
		return HtmlText(html.ToString());
	}

	/// <summary>
	/// <c>createText</c> without HTML labels: <c>nonMarkdownToLines</c> or <c>markdownToLines</c> splits the sanitized
	/// label into lines of words, a word is a tag or a run without space or angle brackets, and each line's words are
	/// joined with a space. <c>decodeHTMLEntities</c> then decodes only <c>&amp;amp;</c>, <c>&amp;lt;</c> and <c>&amp;gt;</c>.
	/// </summary>
	private static string SvgText(string html, FlowchartLabelType type)
	{
		var markdown = type == FlowchartLabelType.Markdown;
		var sanitized = new StringBuilder(html.Length);
		Sanitize(html, sanitized);
		var text = sanitized.ToString();
		if (markdown)
		{
			sanitized.Clear();
			MarkdownText(text, sanitized, separateWords: true);
			text = sanitized.ToString();
		}

		var output = new StringBuilder(text.Length);
		var span = text.AsSpan();
		var lineStart = 0;
		for (var i = 0; i <= span.Length; i++)
		{
			var breakLength = i == span.Length ? 0
				: span[i] == '\n' ? 1
				: span[i] == '\\' && !markdown && i + 1 < span.Length && span[i + 1] == 'n' ? 2
				: span[i] == '<' ? BreakTagLength(span[i..])
				: 0;
			if (i < span.Length && breakLength == 0)
			{
				continue;
			}

			if (markdown)
			{
				SpaceSeparatedWords(span[lineStart..i], output);
			}
			else
			{
				Words(span[lineStart..i].Trim(), output);
			}

			i += Math.Max(breakLength, 1) - 1;
			lineStart = i + 1;
		}

		return output.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&").ToString();
	}

	/// <summary>The length of the <c>&lt;br&gt;</c>, <c>&lt;br/&gt;</c> or <c>&lt;br /&gt;</c> at the start of <paramref name="text"/>, or 0.</summary>
	private static int BreakTagLength(ReadOnlySpan<char> text)
	{
		if (text.Length < 4 || !text[1..3].Equals("br", StringComparison.OrdinalIgnoreCase))
		{
			return 0;
		}

		var at = 3;
		while (at < text.Length && char.IsWhiteSpace(text[at]))
		{
			at++;
		}

		if (at < text.Length && text[at] == '/')
		{
			at++;
		}

		return at < text.Length && text[at] == '>' ? at + 1 : 0;
	}

	/// <summary>Writes the words of <c>/&lt;[^&gt;]+&gt;|[^\s&lt;&gt;]+/g</c> joined with a space.</summary>
	private static void Words(ReadOnlySpan<char> line, StringBuilder output)
	{
		var first = true;
		var i = 0;
		while (i < line.Length)
		{
			int end;
			if (line[i] == '<' && line[(i + 1)..].IndexOf('>') is var close and > 0)
			{
				end = i + 1 + close + 1;
			}
			else if (char.IsWhiteSpace(line[i]) || line[i] is '<' or '>')
			{
				i++;
				continue;
			}
			else
			{
				end = i;
				while (end < line.Length && !char.IsWhiteSpace(line[end]) && line[end] is not ('<' or '>'))
				{
					end++;
				}
			}

			if (!first)
			{
				output.Append(' ');
			}

			output.Append(line[i..end]);
			first = false;
			i = end;
		}
	}

	/// <summary><c>markdownToLines</c>'s words: the text split at spaces, joined with a space.</summary>
	private static void SpaceSeparatedWords(ReadOnlySpan<char> line, StringBuilder output)
	{
		var first = true;
		foreach (var range in line.Split(' '))
		{
			if (range.End.GetOffset(line.Length) > range.Start.GetOffset(line.Length))
			{
				if (!first)
				{
					output.Append(' ');
				}

				output.Append(line[range]);
				first = false;
			}
		}
	}

	/// <summary>
	/// DOMPurify's sanitize: a text without <c>&lt;</c> is returned as it is. Any other text is parsed as HTML and
	/// written back. Character references are decoded, and
	/// <c>&amp;</c>, <c>&lt;</c> and <c>&gt;</c> in text are written as references. Tags stay, and comments and the
	/// elements DOMPurify drops with their content are taken out.
	/// </summary>
	private static void Sanitize(ReadOnlySpan<char> html, StringBuilder output)
	{
		if (!html.Contains('<'))
		{
			output.Append(html);
			return;
		}

		var text = new StringBuilder();
		var i = 0;
		while (i < html.Length)
		{
			var c = html[i];
			if (c == '&')
			{
				text.Clear();
				i += Entity(html[i..], text);
				Escaped(text.ToString(), output);
				continue;
			}

			var tag = c == '<' ? Tag(html, i) : default;
			if (tag.End == 0)
			{
				Escaped(c.ToString(), output);
				i++;
				continue;
			}

			if (tag.End < 0)
			{
				return;
			}

			if (tag.Kind == TagKind.Element)
			{
				output.Append(html[i..(tag.End + 1)]);
			}

			i = tag.Kind == TagKind.DroppedElement ? SkipElement(html, tag.End + 1, tag.Name(html)) : tag.End + 1;
		}

		static void Escaped(string text, StringBuilder output)
		{
			foreach (var c in text)
			{
				_ = c switch
				{
					'&' => output.Append("&amp;"),
					'<' => output.Append("&lt;"),
					'>' => output.Append("&gt;"),
					_ => output.Append(c),
				};
			}
		}
	}

	/// <summary><c>encodeEntities</c> and <c>decodeEntities</c>: <c>#name;</c> becomes <c>&amp;name;</c> and <c>#123;</c> becomes <c>&amp;#123;</c>.</summary>
	private static void WithHtmlEntities(ReadOnlySpan<char> text, StringBuilder output)
	{
		int hash;
		while ((hash = text.IndexOf('#')) >= 0)
		{
			output.Append(text[..hash]);
			var rest = text[(hash + 1)..];
			var length = 0;
			while (length < rest.Length && IsWordChar(rest[length]))
			{
				length++;
			}

			if (length > 0 && length < rest.Length && rest[length] == ';')
			{
				output.Append('&');
				if (char.IsAsciiDigit(rest[0]) && !rest[..length].ContainsAnyExceptInRange('0', '9'))
				{
					output.Append('#');
				}

				output.Append(rest[..(length + 1)]);
				text = rest[(length + 1)..];
			}
			else
			{
				output.Append('#');
				text = rest;
			}
		}

		output.Append(text);
	}

	/// <summary><c>nonMarkdownToHTML</c>: a newline and the two characters <c>\n</c> are line breaks.</summary>
	private static void LineBreaks(ReadOnlySpan<char> text, StringBuilder output)
	{
		for (var i = 0; i < text.Length; i++)
		{
			if (text[i] == '\n')
			{
				output.Append("<br>");
			}
			else if (text[i] == '\\' && i + 1 < text.Length && text[i + 1] == 'n')
			{
				output.Append("<br>");
				i++;
			}
			else
			{
				output.Append(text[i]);
			}
		}
	}

	/// <summary>
	/// The text of <c>markdownToHTML</c>'s output for the inline Markdown of a label: the <c>*</c> and <c>_</c> markers of
	/// strong and emphasis are taken off, and a newline is a line break. With <paramref name="separateWords"/>, the
	/// content of strong and emphasis is its own word, as in <c>markdownToLines</c>.
	/// </summary>
	private static void MarkdownText(ReadOnlySpan<char> text, StringBuilder output, bool separateWords = false)
	{
		for (var i = 0; i < text.Length; i++)
		{
			var c = text[i];
			if (c == '\n')
			{
				output.Append("<br>");
				while (i + 1 < text.Length && text[i + 1] == ' ')
				{
					i++;
				}
			}
			else if (c is '*' or '_')
			{
				var run = 1;
				while (i + run < text.Length && text[i + run] == c)
				{
					run++;
				}

				var close = text[(i + run)..].IndexOf(text.Slice(i, run));
				if (close > 0)
				{
					var separator = separateWords ? " " : "";
					output.Append(separator).Append(text.Slice(i + run, close)).Append(separator);
					i += run + close + run - 1;
				}
				else
				{
					output.Append(text.Slice(i, run));
					i += run - 1;
				}
			}
			else
			{
				output.Append(c);
			}
		}
	}

	/// <summary><c>replaceIconSubstring</c>: <c>/(fa[bklrs]?):fa-([\w-]+)/g</c> becomes an empty <c>i</c> element.</summary>
	private static void ReplaceIcons(ReadOnlySpan<char> text, StringBuilder output, List<string>? icons)
	{
		var i = 0;
		while (i < text.Length)
		{
			if (text[i] == 'f' && IconLength(text[i..], out var prefixLength) is var length and > 0)
			{
				icons?.Add(string.Concat(text.Slice(i, prefixLength), ":", text.Slice(i + prefixLength + 1, length - prefixLength - 1)));
				i += length;
				continue;
			}

			output.Append(text[i]);
			i++;
		}
	}

	private static int IconLength(ReadOnlySpan<char> text, out int prefixLength)
	{
		prefixLength = 0;
		if (text.Length < 2 || text[1] != 'a')
		{
			return 0;
		}

		var at = 2;
		if (at < text.Length && text[at] is 'b' or 'k' or 'l' or 'r' or 's')
		{
			at++;
		}

		prefixLength = at;
		if (!text[at..].StartsWith(":fa-", StringComparison.Ordinal))
		{
			return 0;
		}

		var name = at + 4;
		var end = name;
		while (end < text.Length && (IsWordChar(text[end]) || text[end] == '-'))
		{
			end++;
		}

		return end > name ? end : 0;
	}

	/// <summary>The text a browser shows for <paramref name="html"/>, with each <c>br</c> read as a space.</summary>
	private static string HtmlText(ReadOnlySpan<char> html)
	{
		var output = new StringBuilder(html.Length);
		var i = 0;
		while (i < html.Length)
		{
			var c = html[i];
			if (c == '&')
			{
				i += Entity(html[i..], output);
				continue;
			}

			var tag = c == '<' ? Tag(html, i) : default;
			if (tag.End == 0)
			{
				output.Append(c);
				i++;
				continue;
			}

			if (tag.End < 0)
			{
				break;
			}

			if (tag.Kind == TagKind.Element && tag.Name(html).Equals("br", StringComparison.OrdinalIgnoreCase))
			{
				output.Append(' ');
			}

			i = tag.Kind == TagKind.DroppedElement ? SkipElement(html, tag.End + 1, tag.Name(html)) : tag.End + 1;
		}

		return output.ToString();
	}

	private enum TagKind
	{
		/// <summary>A comment, or a bogus comment such as <c>&lt;!x&gt;</c> or <c>&lt;?x&gt;</c>.</summary>
		Ignored,

		/// <summary>A start or end tag.</summary>
		Element,

		/// <summary>The start tag of an element that is dropped with its content.</summary>
		DroppedElement,
	}

	/// <param name="End">The index of the tag's <c>&gt;</c>, 0 when the <c>&lt;</c> is text, and -1 when the tag runs to the end of the text.</param>
	private readonly record struct TagToken(TagKind Kind, int End, int NameStart, int NameEnd)
	{
		public ReadOnlySpan<char> Name(ReadOnlySpan<char> html) => html[NameStart..NameEnd];
	}

	/// <summary>The tag that starts with the <c>&lt;</c> at <paramref name="at"/>, as the HTML tokenizer reads it.</summary>
	private static TagToken Tag(ReadOnlySpan<char> html, int at)
	{
		if (at + 1 >= html.Length)
		{
			return default;
		}

		var next = html[at + 1];
		if (html[(at + 1)..].StartsWith("!--", StringComparison.Ordinal))
		{
			var end = html[(at + 4)..].IndexOf("-->", StringComparison.Ordinal);
			return new(TagKind.Ignored, end < 0 ? -1 : at + 4 + end + 2, 0, 0);
		}

		if (next is '!' or '?' || (next == '/' && at + 2 < html.Length && !char.IsAsciiLetter(html[at + 2])))
		{
			var end = html[at..].IndexOf('>');
			return new(TagKind.Ignored, end < 0 ? -1 : at + end, 0, 0);
		}

		if (!char.IsAsciiLetter(next) && next != '/')
		{
			return default;
		}

		var closing = next == '/';
		var nameStart = at + (closing ? 2 : 1);
		var nameEnd = nameStart;
		while (nameEnd < html.Length && !char.IsWhiteSpace(html[nameEnd]) && html[nameEnd] is not ('/' or '>'))
		{
			nameEnd++;
		}

		var kind = !closing && IsDroppedWithContent(html[nameStart..nameEnd]) ? TagKind.DroppedElement : TagKind.Element;
		return new(kind, TagEnd(html, nameEnd), nameStart, nameEnd);
	}

	private static int TagEnd(ReadOnlySpan<char> html, int at)
	{
		char quote = '\0';
		for (; at < html.Length; at++)
		{
			var c = html[at];
			if (quote != '\0')
			{
				if (c == quote)
				{
					quote = '\0';
				}
			}
			else if (c is '"' or '\'' && at > 0 && html[at - 1] == '=')
			{
				quote = c;
			}
			else if (c == '>')
			{
				return at;
			}
		}

		return -1;
	}

	private static bool IsDroppedWithContent(ReadOnlySpan<char> name) =>
		name.Equals("script", StringComparison.OrdinalIgnoreCase)
		|| name.Equals("style", StringComparison.OrdinalIgnoreCase)
		|| name.Equals("template", StringComparison.OrdinalIgnoreCase)
		|| name.Equals("title", StringComparison.OrdinalIgnoreCase)
		|| name.Equals("iframe", StringComparison.OrdinalIgnoreCase)
		|| name.Equals("noscript", StringComparison.OrdinalIgnoreCase)
		|| name.Equals("textarea", StringComparison.OrdinalIgnoreCase)
		|| name.Equals("xmp", StringComparison.OrdinalIgnoreCase);

	private static int SkipElement(ReadOnlySpan<char> html, int at, ReadOnlySpan<char> name)
	{
		for (var i = at; i + name.Length + 2 <= html.Length; i++)
		{
			if (html[i] == '<' && html[i + 1] == '/' && html.Slice(i + 2, name.Length).Equals(name, StringComparison.OrdinalIgnoreCase))
			{
				var end = html[i..].IndexOf('>');
				return end < 0 ? html.Length : i + end + 1;
			}
		}

		return html.Length;
	}

	/// <summary>Decodes the character reference at the start of <paramref name="text"/>, or writes the <c>&amp;</c>.</summary>
	/// <returns>The number of characters read.</returns>
	private static int Entity(ReadOnlySpan<char> text, StringBuilder output)
	{
		var semicolon = text.IndexOf(';');
		if (semicolon > 1)
		{
			var name = text[1..semicolon];
			Span<char> decoded = stackalloc char[2];
			if (name[0] == '#' && name.Length > 1)
			{
				var digits = name[1..];
				var hex = digits[0] is 'x' or 'X';
				if (hex)
				{
					digits = digits[1..];
				}

				if (!digits.IsEmpty && int.TryParse(digits, hex ? System.Globalization.NumberStyles.AllowHexSpecifier : System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var value))
				{
					var rune = value != 0 && Rune.IsValid(value) ? new Rune(value) : Rune.ReplacementChar;
					output.Append(decoded[..rune.EncodeToUtf16(decoded)]);
					return semicolon + 1;
				}
			}
			else if (!name.ContainsAnyExcept(WordCharsAndDigits))
			{
				var written = Parsing.EntityCodes.Decode(string.Concat("#", name, ";"));
				if (written.Length > 0 && written[0] != '#')
				{
					output.Append(written);
					return semicolon + 1;
				}
			}
		}

		output.Append('&');
		return 1;
	}

	private static readonly System.Buffers.SearchValues<char> WordCharsAndDigits =
		System.Buffers.SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789");

	private static bool IsWordChar(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';
}
