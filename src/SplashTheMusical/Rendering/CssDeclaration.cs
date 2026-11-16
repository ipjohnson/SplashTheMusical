using System.Buffers;
using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using SplashTheMusical.Parsing;

namespace SplashTheMusical.Rendering;

/// <summary>
/// One declaration of a rule, held as stylis writes it. Each factory checks the property against an allowlist and the
/// value against a grammar, and returns <c>null</c> for anything else, so a value cannot close the declaration or the
/// rule or reach a resource. The only <c>url()</c> a declaration can hold is a reference to an element of the same SVG,
/// written by <see cref="SvgWriter"/> from the SVG's id and <see cref="ReferenceSuffix"/>.
/// </summary>
internal sealed class CssDeclaration
{
	private CssDeclaration(string property, string value, string? referenceSuffix, string after, bool important)
	{
		Property = property;
		Value = value;
		ReferenceSuffix = referenceSuffix;
		After = after;
		Important = important;
	}

	public string Property { get; }

	/// <summary>The value, or the part of it before the reference when <see cref="ReferenceSuffix"/> is set.</summary>
	public string Value { get; }

	/// <summary>
	/// What follows the SVG's id in a <c>url(#id…)</c> reference, such as <c>-gradient</c>, or <c>null</c> when the
	/// value holds no reference.
	/// </summary>
	public string? ReferenceSuffix { get; }

	/// <summary>The part of the value after the reference.</summary>
	public string After { get; }

	public bool Important { get; }

	/// <summary>
	/// A declaration of mermaid's style templates: <paramref name="value"/> is the text a template puts after the
	/// property, as stylis reads it. The value may hold names, numbers with units, hex colours, strings, commas and the
	/// functions <c>rgb</c>, <c>rgba</c>, <c>hsl</c>, <c>hsla</c>, <c>drop-shadow</c> and <c>blur</c>. A <c>;</c> may
	/// only end it.
	/// </summary>
	public static CssDeclaration? FromTheme(string property, string value, bool important = false) =>
		Kind(property) is not null && TryThemeValue(value, out var text) ? new(property, text, null, string.Empty, important) : null;

	/// <summary>
	/// <c>${value}<paramref name="unit"/></c> in a template: the value as JavaScript's <c>String</c> writes it, and
	/// <c>undefined</c> when it is missing.
	/// </summary>
	public static CssDeclaration? FromTheme(string property, MermaidValue? value, string unit = "") =>
		FromTheme(property, (value is null ? "undefined" : PreambleParser.ToJsString(value)) + unit);

	/// <summary>
	/// <paramref name="before"/>, then <c>url(#id<paramref name="referenceSuffix"/>)</c> with the SVG's id, then
	/// <paramref name="after"/>. Each part outside the reference follows <see cref="FromTheme(string, string, bool)"/>.
	/// </summary>
	public static CssDeclaration? WithReference(string property, string before, string referenceSuffix, string after)
	{
		if (Kind(property) is null || referenceSuffix.Length == 0 || !IsNames(referenceSuffix)
			|| (!before.AsSpan().Trim().IsEmpty && (before.Contains(';', StringComparison.Ordinal) || !TryThemeValue(before, out _)))
			|| (!after.AsSpan().Trim().TrimEnd(';').IsEmpty && !TryThemeValue(after, out _)))
		{
			return null;
		}

		// The whole value goes through stylis's whitespace rule, with a placeholder for the reference.
		const string Placeholder = "url(#)";
		var builder = new StringBuilder();
		CssText.AppendStylis(builder, (before + Placeholder + after).AsSpan().Trim().TrimEnd(';'));
		var text = builder.ToString();
		var at = text.IndexOf(Placeholder, StringComparison.Ordinal);
		return new(property, text[..at], referenceSuffix, text[(at + Placeholder.Length)..], false);
	}

	/// <summary>
	/// One declaration of a <c>classDef</c> or <c>style</c> statement, such as <c>fill:#f9f</c>, as Chromium serializes
	/// it after mermaid inserts it into a <c>CSSStyleSheet</c>: a hex colour becomes <c>rgb()</c> or <c>rgba()</c>, a
	/// keyword is lower-cased and a percentage of opacity becomes a number. A property outside the allowlist, a value
	/// the property does not take, and any value holding <c>;</c>, a brace, a parenthesis or <c>!</c> give <c>null</c>.
	/// </summary>
	public static CssDeclaration? FromSource(ReadOnlySpan<char> declaration, bool important)
	{
		var colon = declaration.IndexOf(':');
		if (colon < 0)
		{
			return null;
		}

		var name = declaration[..colon].Trim().ToString().ToLowerInvariant();
		var value = declaration[(colon + 1)..].Trim();
		if (Kind(name) is not { } kind || kind == PropertyKind.ThemeOnly || value.IsEmpty || value.ContainsAny(SourceForbidden))
		{
			return null;
		}

		var text = value.ContainsAny('"', '\'') && kind != PropertyKind.FontFamily
			? null
			: CssWideKeyword(value) ?? kind switch
			{
				PropertyKind.Paint => Paint(value, false),
				PropertyKind.PaintOrNone => Paint(value, true),
				PropertyKind.Length => Length(value, unitless: true, negative: false),
				PropertyKind.Offset => Length(value, unitless: true, negative: true),
				PropertyKind.FontSize => FontSize(value),
				PropertyKind.Alpha => Alpha(value),
				PropertyKind.Dasharray => Dasharray(value),
				PropertyKind.FontFamily => FontFamily(value),
				PropertyKind.FontWeight => FontWeight(value),
				_ => Keyword(name, value),
			};
		return text is null ? null : new(name, text, null, string.Empty, important);
	}

	private enum PropertyKind
	{
		Paint,
		PaintOrNone,
		Length,
		Offset,
		FontSize,
		Alpha,
		Dasharray,
		FontFamily,
		FontWeight,
		Keyword,

		/// <summary>Written only from mermaid's templates, never from the diagram source.</summary>
		ThemeOnly,
	}

	private static PropertyKind? Kind(string property) => property switch
	{
		"fill" or "stroke" => PropertyKind.PaintOrNone,
		"color" or "background-color" or "stop-color" => PropertyKind.Paint,
		"stroke-width" => PropertyKind.Length,
		"stroke-dashoffset" => PropertyKind.Offset,
		"font-size" => PropertyKind.FontSize,
		"opacity" or "fill-opacity" or "stroke-opacity" => PropertyKind.Alpha,
		"stroke-dasharray" => PropertyKind.Dasharray,
		"font-family" => PropertyKind.FontFamily,
		"font-weight" => PropertyKind.FontWeight,
		"font-style" or "stroke-linecap" or "stroke-linejoin" or "visibility" or "display" or "text-decoration" or "text-anchor" => PropertyKind.Keyword,
		"animation" or "filter" or "margin" or "--mermaid-font-family" or "--mermaid-alt-font-family" => PropertyKind.ThemeOnly,
		_ => null,
	};

	private static readonly SearchValues<char> SourceForbidden = SearchValues.Create(";{}()[]\\<>&!@/*`=");

	private static readonly FrozenSet<string> Functions = FrozenSet.Create(StringComparer.OrdinalIgnoreCase, "rgb", "rgba", "hsl", "hsla", "drop-shadow", "blur");

	private static bool IsNames(ReadOnlySpan<char> text)
	{
		foreach (var c in text)
		{
			if (!CssText.IsNameChar(c))
			{
				return false;
			}
		}

		return true;
	}

	// The grammar of a template value. Outside strings: names, numbers, # . % - + , _ whitespace, and parentheses after
	// an allowed function name, with / allowed inside them. A string holds no quote of its kind, backslash, newline, <, >
	// or &. Trailing semicolons end the value.
	private static bool TryThemeValue(string value, out string text)
	{
		text = string.Empty;
		var span = value.AsSpan().TrimEnd();
		while (span.Length > 0 && span[^1] == ';')
		{
			span = span[..^1].TrimEnd();
		}

		span = span.TrimStart();
		if (span.IsEmpty)
		{
			return false;
		}

		var depth = 0;
		for (var i = 0; i < span.Length; i++)
		{
			var c = span[i];
			if (c is '"' or '\'')
			{
				var end = span[(i + 1)..].IndexOfAny(c, '\\', '\n');
				if (end < 0 || span[i + 1 + end] != c || span.Slice(i + 1, end).ContainsAny('<', '>', '&') || span.Slice(i + 1, end).ContainsAny('\r', '\f'))
				{
					return false;
				}

				i += end + 1;
			}
			else if (c == '(')
			{
				var name = i;
				while (name > 0 && CssText.IsNameChar(span[name - 1]))
				{
					name--;
				}

				if (!Functions.GetAlternateLookup<ReadOnlySpan<char>>().Contains(span[name..i]))
				{
					return false;
				}

				depth++;
			}
			else if (c == ')')
			{
				if (--depth < 0)
				{
					return false;
				}
			}
			else if (!(CssText.IsNameChar(c) || CssText.IsWhitespace(c) || c is '#' or '.' or '%' or '+' or ',' || (c == '/' && depth > 0)))
			{
				return false;
			}
		}

		if (depth != 0)
		{
			return false;
		}

		var builder = new StringBuilder(span.Length);
		CssText.AppendStylis(builder, span);
		text = builder.ToString();
		return true;
	}

	private static string? CssWideKeyword(ReadOnlySpan<char> value) =>
		value.Equals("inherit", StringComparison.OrdinalIgnoreCase) ? "inherit"
		: value.Equals("initial", StringComparison.OrdinalIgnoreCase) ? "initial"
		: value.Equals("unset", StringComparison.OrdinalIgnoreCase) ? "unset"
		: value.Equals("revert", StringComparison.OrdinalIgnoreCase) ? "revert"
		: null;

	private static string? Paint(ReadOnlySpan<char> value, bool none)
	{
		if (value[0] == '#')
		{
			return HexColor(value[1..]);
		}

		var lower = value.ToString().ToLowerInvariant();
		return (none && lower == "none") || lower is "transparent" or "currentcolor" || ColorNames.Contains(lower) ? lower : null;
	}

	// Chromium writes a hex colour as rgb(r, g, b), or as rgba(r, g, b, a) when it is not opaque. The alpha has two
	// decimals when they give back the same byte and three otherwise.
	private static string? HexColor(ReadOnlySpan<char> hex)
	{
		if (hex.Length is not (3 or 4 or 6 or 8) || !uint.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var bits))
		{
			return null;
		}

		var digit = hex.Length <= 4 ? 4 : 8;
		var count = hex.Length * 4 / digit;
		Span<int> channels = [0, 0, 0, 255];
		for (var i = 0; i < count; i++)
		{
			var channel = (int)((bits >> ((count - 1 - i) * digit)) & ((1u << digit) - 1));
			channels[i] = digit == 4 ? channel * 17 : channel;
		}

		if (channels[3] == 255)
		{
			return string.Create(CultureInfo.InvariantCulture, $"rgb({channels[0]}, {channels[1]}, {channels[2]})");
		}

		var alpha = Math.Round(channels[3] / 255.0, 2, MidpointRounding.AwayFromZero);
		if ((int)Math.Round(alpha * 255, MidpointRounding.AwayFromZero) != channels[3])
		{
			alpha = Math.Round(channels[3] / 255.0, 3, MidpointRounding.AwayFromZero);
		}

		return string.Create(CultureInfo.InvariantCulture, $"rgba({channels[0]}, {channels[1]}, {channels[2]}, {CssText.Number(alpha)})");
	}

	// A number with an optional unit, split at the first letter or %.
	private static bool TryNumber(ReadOnlySpan<char> token, out double number, out string unit)
	{
		var end = 0;
		while (end < token.Length && (char.IsAsciiDigit(token[end]) || token[end] is '.' or '+' or '-'
			|| (token[end] is 'e' or 'E' && end + 1 < token.Length && (char.IsAsciiDigit(token[end + 1]) || token[end + 1] is '+' or '-'))))
		{
			end++;
		}

		unit = token[end..].ToString().ToLowerInvariant();
		return double.TryParse(token[..end], NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out number)
			&& double.IsFinite(number);
	}

	private static readonly FrozenSet<string> Units = FrozenSet.Create(
		StringComparer.Ordinal, "px", "em", "rem", "ex", "ch", "pt", "pc", "in", "cm", "mm", "q", "vw", "vh", "vmin", "vmax", "%");

	private static string? Length(ReadOnlySpan<char> value, bool unitless, bool negative)
	{
		if (!TryNumber(value, out var number, out var unit) || (number < 0 && !negative) || (unit.Length == 0 ? !unitless : !Units.Contains(unit)))
		{
			return null;
		}

		return CssText.Number(number) + unit;
	}

	private static string? FontSize(ReadOnlySpan<char> value)
	{
		var lower = value.ToString().ToLowerInvariant();
		if (lower is "xx-small" or "x-small" or "small" or "medium" or "large" or "x-large" or "xx-large" or "xxx-large" or "smaller" or "larger")
		{
			return lower;
		}

		return TryNumber(value, out var number, out _) && number == 0 ? "0px" : Length(value, unitless: false, negative: false);
	}

	private static string? Alpha(ReadOnlySpan<char> value)
	{
		if (!TryNumber(value, out var number, out var unit) || unit is not ("" or "%"))
		{
			return null;
		}

		return CssText.Number(unit.Length == 0 ? number : number / 100);
	}

	private static string? Dasharray(ReadOnlySpan<char> value)
	{
		if (value.Equals("none", StringComparison.OrdinalIgnoreCase))
		{
			return "none";
		}

		var builder = new StringBuilder();
		foreach (var range in value.SplitAny(" \t\n\r\f,"))
		{
			if (value[range].IsEmpty)
			{
				continue;
			}

			if (Length(value[range], unitless: true, negative: false) is not { } length)
			{
				return null;
			}

			builder.Append(builder.Length == 0 ? string.Empty : ",").Append(length);
		}

		return builder.Length == 0 ? null : builder.ToString();
	}

	private static string? FontWeight(ReadOnlySpan<char> value)
	{
		var lower = value.ToString().ToLowerInvariant();
		if (lower is "normal" or "bold" or "bolder" or "lighter")
		{
			return lower;
		}

		return TryNumber(value, out var number, out var unit) && unit.Length == 0 && number is >= 1 and <= 1000 ? CssText.Number(number) : null;
	}

	private static readonly FrozenSet<string> GenericFamilies = FrozenSet.Create(
		StringComparer.Ordinal,
		"serif", "sans-serif", "monospace", "cursive", "fantasy", "system-ui", "ui-serif", "ui-sans-serif", "ui-monospace", "ui-rounded", "math", "emoji", "fangsong");

	// Chromium writes a quoted family in double quotes, a run of names as one quoted family and a generic family in lower
	// case. stylis then drops the spaces after the commas.
	private static string? FontFamily(ReadOnlySpan<char> value)
	{
		var builder = new StringBuilder();
		foreach (var range in value.Split(','))
		{
			var family = value[range].Trim();
			if (family.IsEmpty)
			{
				return null;
			}

			builder.Append(builder.Length == 0 ? string.Empty : ",");
			if (family[0] is '"' or '\'')
			{
				if (family.Length < 2 || family[^1] != family[0] || family[1..^1].ContainsAny('"', '\'') || family[1..^1].ContainsAny('\n', '\r', '\f'))
				{
					return null;
				}

				builder.Append('"').Append(family[1..^1]).Append('"');
				continue;
			}

			var names = 0;
			var words = new StringBuilder();
			foreach (var word in family.SplitAny(" \t\n\r\f"))
			{
				if (family[word].IsEmpty)
				{
					continue;
				}

				if (!CssText.IsIdentifier(family[word]))
				{
					return null;
				}

				words.Append(names++ == 0 ? string.Empty : " ").Append(family[word]);
			}

			var lower = words.ToString().ToLowerInvariant();
			if (names == 1 && GenericFamilies.Contains(lower))
			{
				builder.Append(lower);
			}
			else if (names == 1 && lower is "inherit" or "initial" or "unset" or "revert" or "default")
			{
				return null;
			}
			else
			{
				builder.Append(names == 1 ? words.ToString() : "\"" + words + "\"");
			}
		}

		return builder.ToString();
	}

	private static string? Keyword(string property, ReadOnlySpan<char> value)
	{
		var lower = value.ToString().ToLowerInvariant();
		var allowed = property switch
		{
			"font-style" => lower is "normal" or "italic" or "oblique",
			"stroke-linecap" => lower is "butt" or "round" or "square",
			"stroke-linejoin" => lower is "miter" or "round" or "bevel" or "arcs" or "miter-clip",
			"visibility" => lower is "visible" or "hidden" or "collapse",
			"display" => lower is "none" or "inline" or "block" or "inline-block" or "flex" or "inline-flex" or "grid" or "inline-grid" or "contents",
			"text-decoration" => lower is "none" or "underline" or "overline" or "line-through",
			"text-anchor" => lower is "start" or "middle" or "end",
			_ => false,
		};
		return allowed ? lower : null;
	}

	// The 148 colour names of CSS Color 4.
	private static readonly FrozenSet<string> ColorNames = FrozenSet.Create(
		StringComparer.Ordinal,
		"aliceblue", "antiquewhite", "aqua", "aquamarine", "azure", "beige", "bisque", "black", "blanchedalmond", "blue", "blueviolet", "brown",
		"burlywood", "cadetblue", "chartreuse", "chocolate", "coral", "cornflowerblue", "cornsilk", "crimson", "cyan", "darkblue", "darkcyan",
		"darkgoldenrod", "darkgray", "darkgreen", "darkgrey", "darkkhaki", "darkmagenta", "darkolivegreen", "darkorange", "darkorchid", "darkred",
		"darksalmon", "darkseagreen", "darkslateblue", "darkslategray", "darkslategrey", "darkturquoise", "darkviolet", "deeppink", "deepskyblue",
		"dimgray", "dimgrey", "dodgerblue", "firebrick", "floralwhite", "forestgreen", "fuchsia", "gainsboro", "ghostwhite", "gold", "goldenrod",
		"gray", "green", "greenyellow", "grey", "honeydew", "hotpink", "indianred", "indigo", "ivory", "khaki", "lavender", "lavenderblush",
		"lawngreen", "lemonchiffon", "lightblue", "lightcoral", "lightcyan", "lightgoldenrodyellow", "lightgray", "lightgreen", "lightgrey",
		"lightpink", "lightsalmon", "lightseagreen", "lightskyblue", "lightslategray", "lightslategrey", "lightsteelblue", "lightyellow", "lime",
		"limegreen", "linen", "magenta", "maroon", "mediumaquamarine", "mediumblue", "mediumorchid", "mediumpurple", "mediumseagreen",
		"mediumslateblue", "mediumspringgreen", "mediumturquoise", "mediumvioletred", "midnightblue", "mintcream", "mistyrose", "moccasin",
		"navajowhite", "navy", "oldlace", "olive", "olivedrab", "orange", "orangered", "orchid", "palegoldenrod", "palegreen", "paleturquoise",
		"palevioletred", "papayawhip", "peachpuff", "peru", "pink", "plum", "powderblue", "purple", "rebeccapurple", "red", "rosybrown",
		"royalblue", "saddlebrown", "salmon", "sandybrown", "seagreen", "seashell", "sienna", "silver", "skyblue", "slateblue", "slategray",
		"slategrey", "snow", "springgreen", "steelblue", "tan", "teal", "thistle", "tomato", "turquoise", "violet", "wheat", "white",
		"whitesmoke", "yellow", "yellowgreen");
}
