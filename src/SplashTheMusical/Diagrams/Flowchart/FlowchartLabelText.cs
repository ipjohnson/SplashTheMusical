using System.Text;
using SplashTheMusical.Rendering.Shapes;
using SplashTheMusical.Text;

namespace SplashTheMusical.Diagrams.Flowchart;

/// <summary>A run of a label line drawn in one font: plain, or the strong and emphasis of a Markdown string.</summary>
internal readonly record struct LabelRun(string Text, bool Bold, bool Italic);

/// <summary>
/// A measured label: its lines, as <c>createText</c> wraps them, and the box mermaid.js measures for it. An HTML label's
/// box is the client rect of its <c>div</c>, one line of <c>line-height: 1.5</c> per line. An SVG label's lines are 1.1em
/// apart.
/// </summary>
internal sealed class FlowchartLabelBox
{
	public static FlowchartLabelBox Empty(bool html) => new([], 0, 0, 0, html);

	public FlowchartLabelBox(IReadOnlyList<IReadOnlyList<LabelRun>> lines, double width, double height, double lineHeight, bool html)
	{
		Lines = lines;
		Width = width;
		Height = height;
		LineHeight = lineHeight;
		Html = html;
	}

	public IReadOnlyList<IReadOnlyList<LabelRun>> Lines { get; }

	public double Width { get; }

	public double Height { get; }

	/// <summary>The distance between the baselines of two lines.</summary>
	public double LineHeight { get; }

	public bool Html { get; }

	/// <summary>The left of an SVG label's <c>getBBox</c>: its text is centred on x 0. It is 0 for an HTML label.</summary>
	public double X { get; init; }

	/// <summary>The top of an SVG label's <c>getBBox</c>, below the text's origin. It is 0 for an HTML label.</summary>
	public double Y { get; init; }

	/// <summary>
	/// Whether the renderer starts each line at the box's left rather than centring it, as a label with
	/// <c>text-align: left</c> or <c>text-anchor: start</c> shows.
	/// </summary>
	public bool AlignStart { get; set; }

	/// <summary>Whether the label shows any text, which is when the oracle finds a box for it.</summary>
	public bool HasText
	{
		get
		{
			foreach (var line in Lines)
			{
				foreach (var run in line)
				{
					if (!string.IsNullOrWhiteSpace(run.Text))
					{
						return true;
					}
				}
			}

			return false;
		}
	}

	public ShapeLabel ToShapeLabel() => new(Width, Height, Html, X, Y);
}

/// <summary>The font labels are measured in: the theme's <c>fontFamily</c> and <c>fontSize</c>.</summary>
internal sealed class FlowchartFont(ITextMeasurer measurer, string family, double size)
{
	public ITextMeasurer Measurer { get; } = measurer;

	public string Family { get; } = family;

	public double Size { get; } = size;

	public double Width(LabelRun run) =>
		Measurer.Measure(run.Text, new FontSpec(Family, Size, run.Bold ? 700 : 400, run.Italic ? FontStyle.Italic : FontStyle.Normal)).Width;

	public TextMetrics Metrics(string text) => Measurer.Measure(text, new FontSpec(Family, Size));
}

/// <summary>
/// The lines of a flowchart label and their measurement, after <c>createText</c> in <c>rendering-util/createText.ts</c>:
/// a line break is a newline, <c>\n</c> outside Markdown, or <c>&lt;br&gt;</c>. A Markdown string's <c>**strong**</c>
/// and <c>*emphasis*</c> are runs of their own. A line wider than the wrapping width is broken at its break
/// opportunities.
/// </summary>
internal static class FlowchartLabelText
{
	/// <summary>The unit Chromium lays text out in, 1/64 px, which a <c>div</c>'s width is rounded up to.</summary>
	private const double LayoutUnit = 64;

	private const char NoBreakSpace = (char)0xA0;

	/// <summary>
	/// The label's lines of runs, before wrapping, with the text a browser shows for each. Without
	/// <paramref name="autoWrap"/>, <c>markdownToHTML</c> writes the spaces of an HTML Markdown label as <c>&amp;nbsp;</c>.
	/// An HTML line that starts with an icon and a space starts with an empty run for the icon, and the space after it,
	/// which <c>break-spaces</c> keeps when the label wraps.
	/// </summary>
	public static List<List<LabelRun>> Lines(string label, FlowchartLabelType type, bool html, bool autoWrap = true)
	{
		var lines = new List<List<LabelRun>>();
		var markdown = type == FlowchartLabelType.Markdown;
		foreach (var segment in Segments(label, markdown))
		{
			var runs = new List<LabelRun>();
			if (markdown)
			{
				foreach (var (text, bold, italic) in MarkdownRuns(segment.Trim(' ')))
				{
					var shown = html && !autoWrap ? text.Replace(' ', NoBreakSpace) : text;
					AddRun(runs, FlowchartLabels.Text(shown, FlowchartLabelType.Text, html), bold, italic);
				}
			}
			else
			{
				AddRun(runs, FlowchartLabels.Text(segment, type, html), false, false);
			}

			Trim(runs);
			if (html && runs.Count > 0 && StartsWithIconAndSpace(segment))
			{
				runs[0] = runs[0] with { Text = " " + runs[0].Text };
				runs.Insert(0, new LabelRun("", false, false));
			}

			lines.Add(runs);
		}

		while (lines.Count > 0 && lines[^1].Count == 0)
		{
			lines.RemoveAt(lines.Count - 1);
		}

		return lines;
	}

	/// <summary>
	/// Measures and wraps <paramref name="lines"/>. An HTML label is measured unwrapped first. When it reaches
	/// <paramref name="wrapWidth"/>, <c>addHtmlSpan</c> makes its <c>div</c> a table that wide with <c>break-spaces</c>,
	/// which grows to its widest word and wraps the lines at its width. One narrower than <paramref name="minWidth"/> is
	/// widened to it, which is the width of a label whose text draws nothing, such as a lone icon. The icon and image
	/// shapes pad the label's <c>p</c> by <paramref name="inset"/> on each side.
	/// </summary>
	public static FlowchartLabelBox Measure(List<List<LabelRun>> lines, FlowchartFont font, double wrapWidth, double minWidth, bool html, double inset = 0)
	{
		if (lines.Count == 0)
		{
			return html ? new FlowchartLabelBox([], minWidth, 0, 0, true) : FlowchartLabelBox.Empty(false);
		}

		var wrapped = new List<IReadOnlyList<LabelRun>>();
		if (html)
		{
			var unwrapped = 0.0;
			var minContent = 0.0;
			foreach (var line in lines)
			{
				// Unwrapped, the space after a leading icon is at the start of the line, where white space collapses away.
				var lineWidth = 2 * inset;
				for (var i = 0; i < line.Count; i++)
				{
					var run = i == 1 && line[0].Text.Length == 0 ? line[i] with { Text = line[i].Text.TrimStart(' ') } : line[i];
					lineWidth += Ceiling(font.Width(run));
				}

				unwrapped = Math.Max(unwrapped, lineWidth);

				// A piece's width is the distance between its ends, each placed on the line and rounded up.
				var start = 0.0;
				foreach (var word in Words(line, font))
				{
					var end = start + LineWidth(word.Runs, font) + word.Trailing;
					minContent = Math.Max(minContent, Ceiling(end) - Ceiling(start));
					start = end;
				}
			}

			var width = unwrapped;
			if (unwrapped >= wrapWidth)
			{
				width = Math.Max(wrapWidth, minContent + (2 * inset));
				foreach (var line in lines)
				{
					wrapped.AddRange(Wrap(line, font, width - (2 * inset)));
				}
			}
			else
			{
				wrapped.AddRange(lines);
			}

			var lineHeight = font.Size * 1.5;
			return new FlowchartLabelBox(wrapped, Math.Max(width, minWidth), (lineHeight * wrapped.Count) + (2 * inset), lineHeight, true);
		}

		var widest = 0.0;
		foreach (var line in lines)
		{
			foreach (var part in Wrap(line, font, wrapWidth))
			{
				widest = Math.Max(widest, LineWidth(part, font));
				wrapped.Add(part);
			}
		}

		var svgLineHeight = font.Size * 1.1;
		return new FlowchartLabelBox(wrapped, widest, svgLineHeight * wrapped.Count, svgLineHeight, false);
	}

	/// <summary>
	/// <c>createText</c>'s SVG label, from the lines of words of <see cref="FlowchartLabels.SvgLines"/>, and the
	/// <c>getBBox</c> mermaid.js measures for it. <c>createFormattedText</c> keeps a line no wider than
	/// <paramref name="wrapWidth"/> and splits a wider one with <c>splitLineToFitWidth</c>. Each word is a <c>tspan</c>
	/// after the first with a space before it, and Chromium rounds the width of each up to 1/64 px. The lines are 1.1em
	/// apart, and a line's box is the font's ascent and descent, each rounded to a pixel. An edge label's background
	/// (<paramref name="background"/>) adds 2 on each side, and <c>withMinWidth</c> widens a node's to
	/// <paramref name="minWidth"/>. The box's lines hold the words with their spaces.
	/// </summary>
	public static FlowchartLabelBox MeasureSvg(List<List<LabelRun>> lines, FlowchartFont font, double wrapWidth, double minWidth, bool background)
	{
		var wrapped = new List<IReadOnlyList<LabelRun>>();
		foreach (var line in lines)
		{
			if (SvgLineWidth(line, font) <= wrapWidth)
			{
				wrapped.Add(Spaced(line));
			}
			else
			{
				foreach (var part in SplitLineToFitWidth(line, font, wrapWidth))
				{
					wrapped.Add(Spaced(part));
				}
			}
		}

		var lineHeight = font.Size * 1.1;
		var first = -1;
		var last = -1;
		var width = 0.0;
		for (var i = 0; i < wrapped.Count; i++)
		{
			var lineWidth = 0.0;
			foreach (var word in wrapped[i])
			{
				lineWidth += Math.Ceiling(font.Width(word) * LayoutUnit) / LayoutUnit;
			}

			if (wrapped[i].Count > 0)
			{
				first = first < 0 ? i : first;
				last = i;
			}

			width = Math.Max(width, lineWidth);
		}

		// Line i's baseline is at (1.1i + 1)em: createTspan's y of (1.1i - 0.1)em and dy of 1.1em.
		var height = 0.0;
		var top = 0.0;
		if (first >= 0)
		{
			var metrics = font.Metrics("");
			var ascent = Math.Floor(metrics.Ascent + 0.5);
			top = (first * lineHeight) + font.Size - ascent;
			height = ((last - first) * lineHeight) + ascent + Math.Floor(metrics.Descent + 0.5);
		}

		if (background)
		{
			width += 4;
			height += 4;
			top -= 2;
		}

		width = Math.Max(width, minWidth);
		return new FlowchartLabelBox(wrapped, width, height, lineHeight, false) { X = -width / 2, Y = top };
	}

	/// <summary>The words with a space before each but the first, as <c>updateTextContentAndStyles</c> writes them.</summary>
	private static List<LabelRun> Spaced(List<LabelRun> words)
	{
		var spaced = new List<LabelRun>(words.Count);
		for (var i = 0; i < words.Count; i++)
		{
			spaced.Add(i == 0 ? words[i] : words[i] with { Text = " " + words[i].Text });
		}

		return spaced;
	}

	/// <summary><c>computeWidthOfText</c>: the length of the line's words with a space before each but the first.</summary>
	private static double SvgLineWidth(List<LabelRun> words, FlowchartFont font)
	{
		var width = 0.0;
		for (var i = 0; i < words.Count; i++)
		{
			width += font.Width(i == 0 ? words[i] : words[i] with { Text = " " + words[i].Text });
		}

		return width;
	}

	/// <summary>
	/// <c>splitLineToFitWidth</c>: the words go on a line while it fits, and a word too wide for a line of its own is
	/// split between graphemes, with at least one on each line.
	/// </summary>
	private static List<List<LabelRun>> SplitLineToFitWidth(List<LabelRun> line, FlowchartFont font, double width)
	{
		var words = new LinkedList<LabelRun>(line);
		var lines = new List<List<LabelRun>>();
		var current = new List<LabelRun>();
		while (words.First is { } node)
		{
			var next = node.Value;
			words.RemoveFirst();
			var candidate = new List<LabelRun>(current) { next };
			if (SvgLineWidth(candidate, font) <= width)
			{
				current = candidate;
				continue;
			}

			if (current.Count > 0)
			{
				lines.Add(current);
				_ = words.AddFirst(next);
			}
			else if (next.Text.Length > 0)
			{
				var (fits, rest) = SplitWordToFitWidth(next, font, width);
				lines.Add([fits]);
				if (rest.Text.Length > 0)
				{
					_ = words.AddFirst(rest);
				}
			}

			current = [];
		}

		if (current.Count > 0)
		{
			lines.Add(current);
		}

		return lines;
	}

	/// <summary><c>splitWordToFitWidth</c>: the longest start of the word that fits, at least one grapheme, and the rest.</summary>
	private static (LabelRun Fits, LabelRun Remainder) SplitWordToFitWidth(LabelRun word, FlowchartFont font, double width)
	{
		var used = 0;
		var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(word.Text);
		while (enumerator.MoveNext())
		{
			var end = enumerator.ElementIndex + enumerator.GetTextElement().Length;
			if (font.Width(word with { Text = word.Text[..end] }) > width)
			{
				break;
			}

			used = end;
		}

		if (used == 0)
		{
			used = System.Globalization.StringInfo.GetNextTextElementLength(word.Text);
		}

		return (word with { Text = word.Text[..used] }, word with { Text = word.Text[used..] });
	}

	public static double LineWidth(IReadOnlyList<LabelRun> line, FlowchartFont font)
	{
		var width = 0.0;
		foreach (var run in line)
		{
			width += font.Width(run);
		}

		return width;
	}

	/// <summary>
	/// The line split into lines no wider than <paramref name="width"/> at its break opportunities. A word wider than it
	/// has a line of its own.
	/// </summary>
	private static List<IReadOnlyList<LabelRun>> Wrap(List<LabelRun> line, FlowchartFont font, double width)
	{
		if (double.IsPositiveInfinity(width) || LineWidth(line, font) <= width)
		{
			return [line];
		}

		var result = new List<IReadOnlyList<LabelRun>>();
		var current = new List<LabelRun>();
		foreach (var word in Words(line, font))
		{
			var candidate = new List<LabelRun>(current);
			Append(candidate, word.Runs, word.Spaced && current.Count > 0);
			if (current.Count > 0 && LineWidth(candidate, font) + word.Trailing > width)
			{
				result.Add(current);
				current = [];
				Append(current, word.Runs, false);
			}
			else
			{
				current = candidate;
			}
		}

		if (current.Count > 0)
		{
			result.Add(current);
		}

		return result;
	}

	/// <summary>Adds the runs to the line, after a space when <paramref name="spaced"/>, joining runs of one style.</summary>
	private static void Append(List<LabelRun> line, List<LabelRun> runs, bool spaced)
	{
		for (var i = 0; i < runs.Count; i++)
		{
			var run = i == 0 && spaced ? runs[i] with { Text = " " + runs[i].Text } : runs[i];
			if (line.Count > 0 && line[^1].Text.Length > 0 && line[^1].Bold == run.Bold && line[^1].Italic == run.Italic)
			{
				line[^1] = line[^1] with { Text = line[^1].Text + run.Text };
			}
			else
			{
				line.Add(run);
			}
		}
	}

	/// <summary>
	/// The pieces of the line between its break opportunities, after Unicode's line breaking rules as Chromium applies
	/// them: at a space, after a hyphen that follows other text and comes before no digit, between a full stop, comma,
	/// colon or semicolon and an opening bracket, after an en dash, around an em dash, and between CJK characters. Each piece has its runs, whether a space came before it and the width of the space after
	/// it. With <c>break-spaces</c> that space takes up room at the end of a line, so a piece fits only with it. An empty
	/// run is an icon, a piece of no width.
	/// </summary>
	private static List<Word> Words(List<LabelRun> line, FlowchartFont font)
	{
		var words = new List<Word>();
		Word? current = null;
		var spaced = false;
		var before = ' ';
		var beforeThat = ' ';
		var piece = new StringBuilder();
		foreach (var run in line)
		{
			if (run.Text.Length == 0)
			{
				current = new Word(spaced && words.Count > 0);
				current.Runs.Add(run);
				words.Add(current);
				spaced = false;
				continue;
			}

			foreach (var c in run.Text)
			{
				if (c == ' ')
				{
					Flush(run);
					if (current is not null && !spaced)
					{
						current.Trailing = font.Width(run with { Text = " " });
					}

					current = null;
					spaced = true;
				}
				else if ((current is not null || piece.Length > 0) && BreakBetween(beforeThat, before, c))
				{
					Flush(run);
					current = null;
				}

				if (c != ' ')
				{
					piece.Append(c);
				}

				beforeThat = before;
				before = c;
			}

			Flush(run);
		}

		return words;

		void Flush(LabelRun run)
		{
			if (piece.Length == 0)
			{
				return;
			}

			if (current is null)
			{
				current = new Word(spaced && words.Count > 0);
				words.Add(current);
				spaced = false;
			}

			current.Runs.Add(run with { Text = piece.ToString() });
			piece.Clear();
		}
	}

	/// <summary>Whether a line may break between <paramref name="before"/> and <paramref name="c"/>, neither a space.</summary>
	private static bool BreakBetween(char beforeThat, char before, char c) =>
		(before == '-' && beforeThat != ' ' && c != '-' && !char.IsAsciiDigit(c))
		|| (before is '.' or ',' or ':' or ';' && c is '(' or '[' or '{')
		|| before == EnDash
		|| (before == EmDash) != (c == EmDash)
		|| Ideographic(before)
		|| Ideographic(c);

	private const char EnDash = (char)0x2013;

	private const char EmDash = (char)0x2014;

	/// <summary>Whether the character is a CJK ideograph, kana or hangul syllable, which a line may break around.</summary>
	private static bool Ideographic(char c) => c is (>= (char)0x3040 and <= (char)0x30FF) or (>= (char)0x4E00 and <= (char)0x9FFF) or (>= (char)0xAC00 and <= (char)0xD7AF);

	/// <summary>Whether the line starts with <c>fa:fa-name</c> and a space, after its leading white space.</summary>
	private static bool StartsWithIconAndSpace(string segment)
	{
		var text = segment.AsSpan().TrimStart();
		var length = text.Length > 0 && text[0] == 'f' ? FlowchartLabels.IconLength(text, out _) : 0;
		return length > 0 && length < text.Length && char.IsWhiteSpace(text[length]);
	}

	/// <summary>A piece of a line that does not break, in one or more runs.</summary>
	private sealed class Word(bool spaced)
	{
		public List<LabelRun> Runs { get; } = [];

		public bool Spaced { get; } = spaced;

		public double Trailing { get; set; }
	}

	/// <summary>The width rounded up to Chromium's layout unit.</summary>
	private static double Ceiling(double width) => Math.Ceiling(width * LayoutUnit) / LayoutUnit;

	private static void AddRun(List<LabelRun> runs, string text, bool bold, bool italic)
	{
		var collapsed = Collapse(text);
		if (collapsed.Length > 0)
		{
			runs.Add(new LabelRun(collapsed, bold, italic));
		}
	}

	/// <summary>HTML's white space runs as one space, as HTML lays text out. A no-break space is not white space.</summary>
	private static string Collapse(string text)
	{
		var builder = new StringBuilder(text.Length);
		var space = false;
		foreach (var c in text)
		{
			if (c is ' ' or '\t' or '\n' or '\r' or '\f')
			{
				space = true;
				continue;
			}

			if (space)
			{
				builder.Append(' ');
				space = false;
			}

			builder.Append(c);
		}

		if (space)
		{
			builder.Append(' ');
		}

		return builder.ToString();
	}

	/// <summary>Takes the space off the line's ends and joins the runs' spaces.</summary>
	private static void Trim(List<LabelRun> runs)
	{
		while (runs.Count > 0)
		{
			var text = runs[0].Text.TrimStart(' ');
			if (text.Length > 0)
			{
				runs[0] = runs[0] with { Text = text };
				break;
			}

			runs.RemoveAt(0);
		}

		while (runs.Count > 0)
		{
			var text = runs[^1].Text.TrimEnd(' ');
			if (text.Length > 0)
			{
				runs[^1] = runs[^1] with { Text = text };
				break;
			}

			runs.RemoveAt(runs.Count - 1);
		}

		for (var i = 1; i < runs.Count; i++)
		{
			if (runs[i - 1].Text.EndsWith(' ') && runs[i].Text.StartsWith(' '))
			{
				runs[i] = runs[i] with { Text = runs[i].Text.TrimStart(' ') };
			}
		}
	}

	/// <summary>The label split at its line breaks.</summary>
	private static List<string> Segments(string label, bool markdown)
	{
		var segments = new List<string>();
		var span = label.AsSpan();
		var start = 0;
		var i = 0;
		while (i < span.Length)
		{
			var length = span[i] == '\n' ? 1
				: span[i] == '\\' && !markdown && i + 1 < span.Length && span[i + 1] == 'n' ? 2
				: span[i] == '<' ? BreakTagLength(span[i..])
				: 0;
			if (length == 0)
			{
				i++;
				continue;
			}

			segments.Add(span[start..i].ToString());
			i += length;
			if (markdown && length == 1)
			{
				// A blank line ends a Markdown paragraph, and the next paragraph starts on the next line.
				i += BlankLines(span[i..]);
			}

			start = i;
		}

		segments.Add(span[start..].ToString());
		return segments;
	}

	/// <summary>The length of the lines of white space at the start of <paramref name="text"/>, each with its newline.</summary>
	private static int BlankLines(ReadOnlySpan<char> text)
	{
		var length = 0;
		while (true)
		{
			var newline = text[length..].IndexOf('\n');
			if (newline < 0 || !text.Slice(length, newline).IsWhiteSpace())
			{
				return length;
			}

			length += newline + 1;
		}
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

	/// <summary>
	/// The runs of a line of inline Markdown: a run of <c>*</c> or <c>_</c> with a matching run later in the line wraps
	/// strong text when it is two or more long, and emphasized text when it is one or three, and the text inside it is
	/// read for runs again. A run of <c>_</c> between two letters or digits is text, as in <c>officer_of_firm</c>.
	/// </summary>
	public static List<(string Text, bool Bold, bool Italic)> MarkdownRuns(string line)
	{
		var runs = new List<(string, bool, bool)>();
		AddMarkdownRuns(line, false, false, runs);
		return runs;
	}

	private static void AddMarkdownRuns(ReadOnlySpan<char> text, bool bold, bool italic, List<(string Text, bool Bold, bool Italic)> runs)
	{
		var plain = new StringBuilder();
		for (var i = 0; i < text.Length; i++)
		{
			var c = text[i];
			if (c is not ('*' or '_'))
			{
				plain.Append(c);
				continue;
			}

			var run = 1;
			while (i + run < text.Length && text[i + run] == c)
			{
				run++;
			}

			var close = Intraword(text, i, run) ? -1 : CloseOf(text, i + run, run);
			if (close <= 0)
			{
				plain.Append(text.Slice(i, run));
				i += run - 1;
				continue;
			}

			if (plain.Length > 0)
			{
				runs.Add((plain.ToString(), bold, italic));
				plain.Clear();
			}

			AddMarkdownRuns(text.Slice(i + run, close), bold || run >= 2, italic || run is 1 or >= 3, runs);
			i += run + close + run - 1;
		}

		if (plain.Length > 0)
		{
			runs.Add((plain.ToString(), bold, italic));
		}
	}

	/// <summary>The offset from <paramref name="start"/> of the first run of the same <paramref name="run"/> characters that is not inside a word, or -1.</summary>
	private static int CloseOf(ReadOnlySpan<char> text, int start, int run)
	{
		var delimiter = text.Slice(start - run, run);
		for (var from = start; from < text.Length;)
		{
			var at = text[from..].IndexOf(delimiter);
			if (at < 0)
			{
				return -1;
			}

			if (!Intraword(text, from + at, run))
			{
				return from + at - start;
			}

			from += at + 1;
		}

		return -1;
	}

	private static bool Intraword(ReadOnlySpan<char> text, int at, int run) =>
		text[at] == '_' && at > 0 && at + run < text.Length && char.IsLetterOrDigit(text[at - 1]) && char.IsLetterOrDigit(text[at + run]);
}
