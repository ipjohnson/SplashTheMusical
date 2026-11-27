using System.Buffers;
using System.Collections.Frozen;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Diagrams.State;
using SplashTheMusical.Parsing;
using SplashTheMusical.Rendering;

namespace SplashTheMusical.Diagrams.Sequence;

/// <summary>
/// The db's <c>parseBoxData</c>: the line after <c>box</c> read by <c>/^((?:rgba?|hsla?)\s*\(.*\)|\w*)(.*)$/</c>, the
/// first group as the colour when <c>CSS.supports('color', …)</c> takes it, and the rest, or the whole line when the
/// browser does not, as the title, without the <c>wrap:</c> or <c>nowrap:</c> at its start.
/// </summary>
internal static class SequenceBoxData
{
	public const string Transparent = "transparent";

	/// <summary>What JavaScript's <c>.</c> does not match.</summary>
	private static readonly SearchValues<char> LineTerminators = SearchValues.Create(['\n', '\r', (char)0x2028, (char)0x2029]);

	/// <summary>
	/// Reads <paramref name="line"/>. <paramref name="color"/> is empty when the colour is
	/// <see cref="Transparent"/> because the line gives none or one the browser refuses, and <paramref name="title"/> is
	/// empty when the box has no title.
	/// </summary>
	public static void Parse(ReadOnlySpan<char> line, out ReadOnlySpan<char> color, out ReadOnlySpan<char> title, out bool? wrap)
	{
		color = [];
		title = [];
		wrap = null;

		// The pattern fails at a line terminator, so both groups are undefined.
		if (line.ContainsAny(LineTerminators))
		{
			return;
		}

		var first = FunctionLength(line);
		if (first == 0)
		{
			while (first < line.Length && FlowchartText.IsWordChar(line[first]))
			{
				first++;
			}
		}

		var rest = line[first..];
		color = StateLexer.Trim(line[..first]);
		var hasTitle = !rest.IsEmpty;
		var titleText = StateLexer.Trim(rest);
		if (!color.IsEmpty && !SupportsColor(color))
		{
			color = [];
			hasTitle = true;
			titleText = StateLexer.Trim(line);
		}

		if (hasTitle)
		{
			title = SequenceText.ParseMessage(titleText, out wrap);
		}
	}

	/// <summary>The length of <c>(?:rgba?|hsla?)\s*\(.*\)</c> at the start of <paramref name="line"/>, or 0.</summary>
	private static int FunctionLength(ReadOnlySpan<char> line)
	{
		if (!line.StartsWith("rgb", StringComparison.Ordinal) && !line.StartsWith("hsl", StringComparison.Ordinal))
		{
			return 0;
		}

		var at = line.Length > 3 && line[3] == 'a' ? 4 : 3;
		while (true)
		{
			var open = at;
			while (open < line.Length && PreambleParser.IsSpace(line[open]))
			{
				open++;
			}

			if (open < line.Length && line[open] == '(')
			{
				var close = line.LastIndexOf(')');
				return close > open ? close + 1 : 0;
			}

			if (at == 4)
			{
				at = 3;
				continue;
			}

			return 0;
		}
	}

	/// <summary>Whether Chromium's <c>CSS.supports('color', …)</c> takes <paramref name="color"/>.</summary>
	internal static bool SupportsColor(ReadOnlySpan<char> color)
	{
		Span<char> lower = stackalloc char[32];
		if (color.Length <= lower.Length && color.IndexOf('(') < 0)
		{
			var length = color.ToLowerInvariant(lower);
			var name = lower[..length];
			return CssDeclaration.ColorNames.GetAlternateLookup<ReadOnlySpan<char>>().Contains(name)
				|| Keywords.GetAlternateLookup<ReadOnlySpan<char>>().Contains(name);
		}

		return IsColorFunction(color);
	}

	/// <summary>The colour keywords besides the names: <c>transparent</c>, <c>currentcolor</c>, the system colours and the CSS-wide keywords.</summary>
	private static readonly FrozenSet<string> Keywords = FrozenSet.Create(
		StringComparer.Ordinal,
		"transparent", "currentcolor", "inherit", "initial", "unset", "revert",
		"accentcolor", "accentcolortext", "activetext", "buttonborder", "buttonface", "buttontext", "canvas", "canvastext", "field",
		"fieldtext", "graytext", "highlight", "highlighttext", "linktext", "mark", "marktext", "selecteditem", "selecteditemtext",
		"visitedtext", "activeborder", "activecaption", "appworkspace", "background", "buttonhighlight", "buttonshadow", "captiontext",
		"inactiveborder", "inactivecaption", "inactivecaptiontext", "infobackground", "infotext", "menu", "menutext", "scrollbar",
		"threeddarkshadow", "threedface", "threedhighlight", "threedlightshadow", "threedshadow", "window", "windowframe", "windowtext");

	/// <summary>
	/// <c>rgb()</c>, <c>rgba()</c>, <c>hsl()</c> or <c>hsla()</c> with CSS Color 4's legacy syntax, values separated by
	/// commas, or its modern syntax, values separated by space with an optional <c>/ alpha</c>.
	/// </summary>
	private static bool IsColorFunction(ReadOnlySpan<char> color)
	{
		var open = color.IndexOf('(');
		if (open < 3 || color[^1] != ')')
		{
			return false;
		}

		var name = color[..open];
		var hsl = name is "hsl" or "hsla";
		if (!hsl && name is not ("rgb" or "rgba"))
		{
			return false;
		}

		var inner = color[(open + 1)..^1];
		if (inner.IndexOfAny('(', ')') >= 0)
		{
			return false;
		}

		return inner.IndexOf(',') >= 0 ? IsLegacy(inner, hsl) : IsModern(inner, hsl);
	}

	private static bool IsLegacy(ReadOnlySpan<char> inner, bool hsl)
	{
		Span<Range> parts = stackalloc Range[5];
		var count = inner.Split(parts, ',');
		if (count is not (3 or 4))
		{
			return false;
		}

		var percentages = 0;
		for (var i = 0; i < count; i++)
		{
			var part = StateLexer.Trim(inner[parts[i]]);
			if (part.IsEmpty || part.IndexOfAny(' ', '\t', '/') >= 0)
			{
				return false;
			}

			var kind = Value(part);
			if (i == 3)
			{
				if (kind is not (ValueKind.Number or ValueKind.Percentage))
				{
					return false;
				}
			}
			else if (hsl)
			{
				if (i == 0 ? kind is not (ValueKind.Number or ValueKind.Angle) : kind != ValueKind.Percentage)
				{
					return false;
				}
			}
			else if (kind is ValueKind.Number or ValueKind.Percentage)
			{
				percentages += kind == ValueKind.Percentage ? 1 : 0;
			}
			else
			{
				return false;
			}
		}

		return hsl || percentages is 0 or 3;
	}

	private static bool IsModern(ReadOnlySpan<char> inner, bool hsl)
	{
		var values = 0;
		var slash = false;
		var alpha = 0;
		foreach (var range in inner.SplitAny(" \t\n\f/"))
		{
			var word = inner[range];
			var end = range.End.GetOffset(inner.Length);
			var isSlash = end < inner.Length && inner[end] == '/';
			if (!word.IsEmpty)
			{
				var kind = Value(word);
				if (slash)
				{
					if (alpha > 0 || kind is not (ValueKind.Number or ValueKind.Percentage or ValueKind.None))
					{
						return false;
					}

					alpha++;
				}
				else
				{
					var allowed = hsl && values == 0
						? kind is ValueKind.Number or ValueKind.Angle or ValueKind.None
						: kind is ValueKind.Number or ValueKind.Percentage or ValueKind.None;
					if (!allowed || values == 3)
					{
						return false;
					}

					values++;
				}
			}

			if (isSlash)
			{
				if (slash || values != 3)
				{
					return false;
				}

				slash = true;
			}
		}

		return values == 3 && (!slash || alpha == 1);
	}

	private enum ValueKind
	{
		Other,
		Number,
		Percentage,
		Angle,
		None,
	}

	/// <summary>A CSS <c>&lt;number&gt;</c>, <c>&lt;percentage&gt;</c>, <c>&lt;angle&gt;</c> or <c>none</c>.</summary>
	private static ValueKind Value(ReadOnlySpan<char> text)
	{
		if (text.Equals("none", StringComparison.OrdinalIgnoreCase))
		{
			return ValueKind.None;
		}

		var at = 0;
		if (at < text.Length && text[at] is '+' or '-')
		{
			at++;
		}

		var digits = 0;
		while (at < text.Length && char.IsAsciiDigit(text[at]))
		{
			at++;
			digits++;
		}

		if (at < text.Length && text[at] == '.')
		{
			var fraction = 0;
			at++;
			while (at < text.Length && char.IsAsciiDigit(text[at]))
			{
				at++;
				fraction++;
			}

			if (fraction == 0)
			{
				return ValueKind.Other;
			}

			digits += fraction;
		}

		if (digits == 0)
		{
			return ValueKind.Other;
		}

		if (at + 1 < text.Length && text[at] is 'e' or 'E' && (char.IsAsciiDigit(text[at + 1]) || (text[at + 1] is '+' or '-' && at + 2 < text.Length && char.IsAsciiDigit(text[at + 2]))))
		{
			at += text[at + 1] is '+' or '-' ? 2 : 1;
			while (at < text.Length && char.IsAsciiDigit(text[at]))
			{
				at++;
			}
		}

		var unit = text[at..];
		if (unit.IsEmpty)
		{
			return ValueKind.Number;
		}

		if (unit is "%")
		{
			return ValueKind.Percentage;
		}

		return unit.Equals("deg", StringComparison.OrdinalIgnoreCase) || unit.Equals("grad", StringComparison.OrdinalIgnoreCase)
			|| unit.Equals("rad", StringComparison.OrdinalIgnoreCase) || unit.Equals("turn", StringComparison.OrdinalIgnoreCase)
			? ValueKind.Angle
			: ValueKind.Other;
	}
}
