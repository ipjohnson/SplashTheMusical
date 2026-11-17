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

	public ShapeLabel ToShapeLabel() => new(Width, Height, Html);
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
/// and <c>*emphasis*</c> are runs of their own. A line wider than the wrapping width is broken at spaces.
/// </summary>
internal static class FlowchartLabelText
{
	/// <summary>The unit Chromium lays text out in, 1/64 px, which a <c>div</c>'s width is rounded up to.</summary>
	private const double LayoutUnit = 64;

	/// <summary>The label's lines of runs, before wrapping, with the text a browser shows for each.</summary>
	public static List<List<LabelRun>> Lines(string label, FlowchartLabelType type, bool html)
	{
		var lines = new List<List<LabelRun>>();
		var markdown = type == FlowchartLabelType.Markdown;
		foreach (var segment in Segments(label, markdown))
		{
			var runs = new List<LabelRun>();
			if (markdown)
			{
				foreach (var (text, bold, italic) in MarkdownRuns(segment))
				{
					AddRun(runs, FlowchartLabels.Text(text, FlowchartLabelType.Text, html), bold, italic);
				}
			}
			else
			{
				AddRun(runs, FlowchartLabels.Text(segment, type, html), false, false);
			}

			Trim(runs);
			lines.Add(runs);
		}

		while (lines.Count > 0 && lines[^1].Count == 0)
		{
			lines.RemoveAt(lines.Count - 1);
		}

		return lines;
	}

	/// <summary>
	/// Measures and wraps <paramref name="lines"/>. An HTML label wider than <paramref name="wrapWidth"/> is that wide, and
	/// one narrower than <paramref name="minWidth"/> is widened to it, as <c>addHtmlSpan</c> styles its <c>div</c>.
	/// </summary>
	public static FlowchartLabelBox Measure(List<List<LabelRun>> lines, FlowchartFont font, double wrapWidth, double minWidth, bool html)
	{
		if (lines.Count == 0)
		{
			return FlowchartLabelBox.Empty(html);
		}

		var wrapped = new List<IReadOnlyList<LabelRun>>();
		var widest = 0.0;
		var overflows = false;
		foreach (var line in lines)
		{
			foreach (var part in Wrap(line, font, wrapWidth))
			{
				var width = LineWidth(part, font);
				overflows |= width > wrapWidth;
				widest = Math.Max(widest, html ? Math.Ceiling(width * LayoutUnit) / LayoutUnit : width);
				wrapped.Add(part);
			}
		}

		if (html)
		{
			var lineHeight = font.Size * 1.5;
			var width = overflows || wrapped.Count > lines.Count ? Math.Max(wrapWidth, widest) : widest;
			return new FlowchartLabelBox(wrapped, Math.Max(width, minWidth), lineHeight * wrapped.Count, lineHeight, true);
		}

		var svgLineHeight = font.Size * 1.1;
		return new FlowchartLabelBox(wrapped, widest, svgLineHeight * wrapped.Count, svgLineHeight, false);
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

	/// <summary>The line split into lines no wider than <paramref name="width"/>, at spaces. A word wider than it has a line of its own.</summary>
	private static List<IReadOnlyList<LabelRun>> Wrap(List<LabelRun> line, FlowchartFont font, double width)
	{
		if (double.IsPositiveInfinity(width) || LineWidth(line, font) <= width)
		{
			return [line];
		}

		// The words of the line, each a run of one style, with whether a space came before it.
		var words = new List<(LabelRun Run, bool Spaced)>();
		foreach (var run in line)
		{
			var text = run.Text.AsSpan();
			var spaced = text.Length > 0 && text[0] == ' ';
			foreach (var range in text.Split(' '))
			{
				var word = text[range];
				if (word.IsEmpty)
				{
					spaced = true;
					continue;
				}

				words.Add((run with { Text = word.ToString() }, spaced && words.Count > 0));
				spaced = true;
			}
		}

		var result = new List<IReadOnlyList<LabelRun>>();
		var current = new List<LabelRun>();
		foreach (var (run, spaced) in words)
		{
			var candidate = new List<LabelRun>(current);
			if (spaced && current.Count > 0)
			{
				candidate.Add(run with { Text = " " + run.Text });
			}
			else
			{
				candidate.Add(run);
			}

			if (current.Count > 0 && LineWidth(candidate, font) > width)
			{
				result.Add(current);
				current = [run];
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

	private static void AddRun(List<LabelRun> runs, string text, bool bold, bool italic)
	{
		var collapsed = Collapse(text);
		if (collapsed.Length > 0)
		{
			runs.Add(new LabelRun(collapsed, bold, italic));
		}
	}

	/// <summary>White space runs as one space, as HTML lays text out.</summary>
	private static string Collapse(string text)
	{
		var builder = new StringBuilder(text.Length);
		var space = false;
		foreach (var c in text)
		{
			if (char.IsWhiteSpace(c))
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
			var text = runs[0].Text.TrimStart();
			if (text.Length > 0)
			{
				runs[0] = runs[0] with { Text = text };
				break;
			}

			runs.RemoveAt(0);
		}

		while (runs.Count > 0)
		{
			var text = runs[^1].Text.TrimEnd();
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
				runs[i] = runs[i] with { Text = runs[i].Text.TrimStart() };
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
			start = i;
		}

		segments.Add(span[start..].ToString());
		return segments;
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
	/// strong text when it is two or more long, and emphasized text when it is one or three.
	/// </summary>
	private static List<(string Text, bool Bold, bool Italic)> MarkdownRuns(string line)
	{
		var runs = new List<(string, bool, bool)>();
		var text = line.AsSpan();
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

			var close = text[(i + run)..].IndexOf(text.Slice(i, run));
			if (close <= 0)
			{
				plain.Append(text.Slice(i, run));
				i += run - 1;
				continue;
			}

			if (plain.Length > 0)
			{
				runs.Add((plain.ToString(), false, false));
				plain.Clear();
			}

			runs.Add((text.Slice(i + run, close).ToString(), run >= 2, run is 1 or >= 3));
			i += run + close + run - 1;
		}

		if (plain.Length > 0)
		{
			runs.Add((plain.ToString(), false, false));
		}

		return runs;
	}
}
