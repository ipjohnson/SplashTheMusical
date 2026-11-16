using System.Text;

namespace SplashTheMusical.Text;

/// <summary>The size mermaid.js's <c>calculateTextDimensions</c> gives a text, in whole pixels.</summary>
/// <param name="Width">The width of the widest line.</param>
/// <param name="Height">The sum of the line heights.</param>
/// <param name="LineHeight">The height of the tallest line.</param>
public readonly record struct TextDimensions(double Width, double Height, double LineHeight);

/// <summary>
/// mermaid.js's line breaking and wrapping, from <c>diagrams/common/common.ts</c> and <c>utils.ts</c>. A line break is
/// a <c>br</c> tag: <c>&lt;br&gt;</c>, <c>&lt;br/&gt;</c>, <c>&lt;/br&gt;</c> and their variants with spaces before the
/// slash or the closing bracket, in any case.
/// </summary>
/// <remarks>Each method measures with <see cref="TextMeasurer.Default"/> unless the host passes its own measurer.</remarks>
public static class MermaidText
{
	/// <summary>mermaid's <c>wrapLabel</c> separator.</summary>
	public const string LineBreak = "<br/>";

	/// <summary>mermaid's <c>hasBreaks</c>: true when the text holds a <c>br</c> tag.</summary>
	public static bool HasBreaks(ReadOnlySpan<char> text)
	{
		for (var i = text.IndexOf('<'); i >= 0; i = Next(text, i + 1))
		{
			if (BreakLength(text[i..]) > 0)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>mermaid's <c>splitBreaks</c>: the lines between the <c>br</c> tags of the text.</summary>
	public static LineEnumerator SplitBreaks(ReadOnlySpan<char> text) => new(text);

	/// <summary>
	/// mermaid's <c>calculateTextDimensions</c>. It measures each line in the generic <c>sans-serif</c> family and in the
	/// font's family. It takes the <c>sans-serif</c> size only when it is larger in width, height and line height, and
	/// rounds each line to whole pixels.
	/// </summary>
	public static TextDimensions CalculateTextDimensions(ReadOnlySpan<char> text, in FontSpec font, ITextMeasurer? measurer = null)
	{
		if (text.IsEmpty)
		{
			return default;
		}

		measurer ??= TextMeasurer.Default;
		var sansSerif = Dimensions(text, font with { Family = "sans-serif" }, measurer);
		var family = Dimensions(text, font, measurer);
		return sansSerif.Height > family.Height && sansSerif.Width > family.Width && sansSerif.LineHeight > family.LineHeight
			? sansSerif
			: family;
	}

	/// <summary>mermaid's <c>calculateTextWidth</c>: the width of <see cref="CalculateTextDimensions"/>.</summary>
	public static double CalculateTextWidth(ReadOnlySpan<char> text, in FontSpec font, ITextMeasurer? measurer = null) =>
		CalculateTextDimensions(text, font, measurer).Width;

	/// <summary>
	/// mermaid's <c>wrapLabel</c>. It breaks the label at spaces into lines narrower than <paramref name="maxWidth"/>, and
	/// hyphenates a word wider than that with <see cref="BreakString"/>. A label that already holds a <c>br</c> tag is
	/// returned as it is.
	/// </summary>
	public static string WrapLabel(string label, double maxWidth, in FontSpec font, ITextMeasurer? measurer = null, string joinWith = LineBreak)
	{
		if (label.Length == 0 || HasBreaks(label))
		{
			return label;
		}

		var words = label.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		var completed = new List<string>();
		var nextLine = "";
		foreach (var word in words)
		{
			var wordLength = CalculateTextWidth(word + " ", font, measurer);
			var nextLineLength = CalculateTextWidth(nextLine, font, measurer);
			if (wordLength > maxWidth)
			{
				var (hyphenated, remaining) = BreakString(word, maxWidth, font, measurer);
				completed.Add(nextLine);
				completed.AddRange(hyphenated);
				nextLine = remaining;
			}
			else if (nextLineLength + wordLength >= maxWidth)
			{
				completed.Add(nextLine);
				nextLine = word;
			}
			else
			{
				nextLine = nextLine.Length == 0 ? word : nextLine + " " + word;
			}
		}

		completed.Add(nextLine);
		return string.Join(joinWith, completed.Where(line => line.Length > 0));
	}

	/// <summary>
	/// mermaid's <c>breakString</c>. It adds the word's characters to a line until the line is at least
	/// <paramref name="maxWidth"/> wide, then ends the line with <paramref name="hyphen"/>, except at the word's end.
	/// </summary>
	/// <returns>The full lines, and the characters left over after them.</returns>
	public static (List<string> HyphenatedStrings, string RemainingWord) BreakString(
		string word,
		double maxWidth,
		in FontSpec font,
		ITextMeasurer? measurer = null,
		string hyphen = "-"
	)
	{
		var lines = new List<string>();
		var current = new StringBuilder();
		var end = 0;
		foreach (var character in word.EnumerateRunes())
		{
			current.Append(word.AsSpan(end, character.Utf16SequenceLength));
			end += character.Utf16SequenceLength;
			var nextLine = current.ToString();
			if (CalculateTextWidth(nextLine, font, measurer) >= maxWidth)
			{
				lines.Add(end == word.Length ? nextLine : nextLine + hyphen);
				current.Clear();
			}
		}

		return (lines, current.ToString());
	}

	private static TextDimensions Dimensions(ReadOnlySpan<char> text, in FontSpec font, ITextMeasurer measurer)
	{
		double width = 0;
		double height = 0;
		double lineHeight = 0;
		foreach (var line in SplitBreaks(text))
		{
			// mermaid draws an empty line as a zero width space, which has the font's height.
			var metrics = measurer.Measure(line, font);
			width = Math.Round(Math.Max(width, metrics.Width), MidpointRounding.AwayFromZero);
			var lineBox = Math.Round(metrics.Height, MidpointRounding.AwayFromZero);
			height += lineBox;
			lineHeight = Math.Round(Math.Max(lineHeight, lineBox), MidpointRounding.AwayFromZero);
		}

		return new TextDimensions(width, height, lineHeight);
	}

	private static int Next(ReadOnlySpan<char> text, int from)
	{
		var at = text[from..].IndexOf('<');
		return at < 0 ? -1 : from + at;
	}

	/// <summary>The length of the <c>br</c> tag at the start of <paramref name="text"/>, or 0 when it starts with none.</summary>
	private static int BreakLength(ReadOnlySpan<char> text)
	{
		var i = 1;
		if (i < text.Length && text[i] == '/')
		{
			i++;
		}

		if (i + 1 >= text.Length || (text[i] | 0x20) != 'b' || (text[i + 1] | 0x20) != 'r')
		{
			return 0;
		}

		i += 2;
		while (i < text.Length && IsRegexSpace(text[i]))
		{
			i++;
		}

		if (i < text.Length && text[i] == '/')
		{
			i++;
		}

		return i < text.Length && text[i] == '>' ? i + 1 : 0;
	}

	/// <summary>The characters of JavaScript's <c>\s</c>.</summary>
	private static bool IsRegexSpace(char c) => c == '﻿' || (c != '\u0085' && char.IsWhiteSpace(c));

	/// <summary>Enumerates the lines between the <c>br</c> tags of a text, without allocating.</summary>
	public ref struct LineEnumerator
	{
		private ReadOnlySpan<char> _rest;
		private bool _done;

		internal LineEnumerator(ReadOnlySpan<char> text)
		{
			_rest = text;
			Current = default;
		}

		public ReadOnlySpan<char> Current { get; private set; }

		public readonly LineEnumerator GetEnumerator() => this;

		public bool MoveNext()
		{
			if (_done)
			{
				return false;
			}

			for (var i = _rest.IndexOf('<'); i >= 0; i = Next(_rest, i + 1))
			{
				var length = BreakLength(_rest[i..]);
				if (length > 0)
				{
					Current = _rest[..i];
					_rest = _rest[(i + length)..];
					return true;
				}
			}

			Current = _rest;
			_done = true;
			return true;
		}
	}
}
