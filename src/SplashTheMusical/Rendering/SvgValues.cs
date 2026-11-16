using System.Buffers;
using System.Collections.Frozen;
using System.Text;

namespace SplashTheMusical.Rendering;

/// <summary>
/// The allowlists and value grammars <see cref="SvgWriter"/> and <see cref="SvgSanitizer"/> share. The writer's names are
/// a subset of the sanitizer's, so everything the writer emits passes the sanitizer.
/// </summary>
internal static class SvgValues
{
	public const string SvgNamespace = "http://www.w3.org/2000/svg";

	public const string XLinkNamespace = "http://www.w3.org/1999/xlink";

	/// <summary>The document a sanitizer gives for input it cannot keep any of.</summary>
	public const string EmptySvg = "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>";

	private const int MaxValueLength = 512;

	/// <summary>The elements the sanitizer keeps.</summary>
	public static readonly FrozenSet<string> Elements = FrozenSet.Create(
		StringComparer.Ordinal,
		"svg", "g", "defs", "title", "desc", "rect", "circle", "ellipse", "polygon", "polyline", "line", "path", "text", "tspan", "marker", "image",
		"clipPath", "mask", "linearGradient", "radialGradient", "stop", "filter", "feGaussianBlur", "feOffset", "feBlend", "feFlood", "feComposite",
		"feMerge", "feMergeNode", "feDropShadow", "feColorMatrix", "feMorphology");

	/// <summary>The attributes without a namespace the sanitizer keeps, besides <c>data-*</c> and <c>aria-*</c>.</summary>
	public static readonly FrozenSet<string> Attributes = FrozenSet.Create(
		StringComparer.Ordinal,
		"id", "class", "transform", "role", "xmlns", "viewBox", "preserveAspectRatio", "width", "height", "x", "y", "cx", "cy", "r", "rx", "ry", "x1",
		"y1", "x2", "y2", "points", "d", "fill", "stroke", "stroke-width", "stroke-dasharray", "stroke-dashoffset", "stroke-linecap", "stroke-linejoin",
		"stroke-miterlimit", "stroke-opacity", "fill-opacity", "fill-rule", "clip-rule", "opacity", "font-family", "font-size", "font-weight",
		"font-style", "text-anchor", "dominant-baseline", "alignment-baseline", "baseline-shift", "text-decoration", "letter-spacing", "paint-order",
		"word-spacing", "direction", "unicode-bidi", "writing-mode", "color", "color-interpolation", "color-interpolation-filters", "marker-end",
		"marker-start", "marker-mid", "markerWidth", "markerHeight", "markerUnits", "refX", "refY", "orient", "offset", "stop-color", "stop-opacity",
		"gradientUnits", "gradientTransform", "spreadMethod", "fx", "fy", "fr", "in", "in2", "result", "stdDeviation", "dx", "dy", "mode",
		"flood-color", "flood-opacity", "operator", "k1", "k2", "k3", "k4", "type", "values", "filterUnits", "primitiveUnits", "clipPathUnits",
		"maskUnits", "maskContentUnits", "display", "visibility", "patternUnits", "patternContentUnits", "patternTransform");

	/// <summary>The elements <see cref="SvgWriter.StartElement"/> writes: the sanitizer's, less <c>image</c>.</summary>
	public static readonly FrozenSet<string> WriterElements = FrozenSet.Create(StringComparer.Ordinal, [.. Elements.Where(e => e != "image")]);

	/// <summary>The attributes <see cref="SvgWriter.WriteAttribute(string, string)"/> writes: the sanitizer's, less <c>xmlns</c>, which the writer adds to the root itself.</summary>
	public static readonly FrozenSet<string> WriterAttributes = FrozenSet.Create(StringComparer.Ordinal, [.. Attributes.Where(a => a != "xmlns")]);

	private static readonly FrozenSet<string> PaintAttributes = FrozenSet.Create(StringComparer.Ordinal, "color", "fill", "stroke", "stop-color", "flood-color");

	private static readonly FrozenSet<string> ReferenceAttributes = FrozenSet.Create(StringComparer.Ordinal, "marker-start", "marker-mid", "marker-end");

	private static readonly FrozenSet<string> ColorFunctions = FrozenSet.Create(
		StringComparer.OrdinalIgnoreCase, "rgb", "rgba", "hsl", "hsla", "hwb", "lab", "lch", "oklab", "oklch", "color");

	private static readonly SearchValues<char> ColorArgument = SearchValues.Create(
		"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789#%.,/+- \t\n\r\f\v");

	private static readonly SearchValues<char> HexDigits = SearchValues.Create("0123456789abcdefABCDEF");

	private static readonly SearchValues<char> Base64 = SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/");

	/// <summary>Whether the value of the attribute <paramref name="name"/> fits its grammar. Attributes without one take any value.</summary>
	public static bool IsAllowedValue(string name, ReadOnlySpan<char> value) =>
		PaintAttributes.Contains(name) ? IsAllowedPaint(value)
		: !ReferenceAttributes.Contains(name) || IsAllowedLocalReference(value);

	/// <summary>Whether <paramref name="name"/> is a <c>data-*</c> or <c>aria-*</c> attribute name of letters, digits, <c>-</c>, <c>_</c> and <c>.</c>.</summary>
	public static bool IsDataOrAriaName(ReadOnlySpan<char> name)
	{
		if (!(name.StartsWith("data-", StringComparison.Ordinal) || name.StartsWith("aria-", StringComparison.Ordinal)) || name.Length == 5)
		{
			return false;
		}

		foreach (var c in name[5..])
		{
			if (!(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'))
			{
				return false;
			}
		}

		return true;
	}

	/// <summary><c>none</c>, or <c>url(#id)</c> with an id of the same document.</summary>
	public static bool IsAllowedLocalReference(ReadOnlySpan<char> value) => value is "none" || IsLocalUrl(value);

	/// <summary>A colour, or <c>url(#id)</c> with an id of the same document.</summary>
	public static bool IsAllowedPaint(ReadOnlySpan<char> value)
	{
		var trimmed = value.Trim();
		return trimmed.Length is > 0 and <= MaxValueLength && (IsLocalUrl(trimmed) || IsHexColor(trimmed) || IsNamedColor(trimmed) || IsColorFunction(trimmed));
	}

	private static bool IsLocalUrl(ReadOnlySpan<char> value)
	{
		if (value.Length > MaxValueLength || !value.StartsWith("url(#", StringComparison.Ordinal) || !value.EndsWith(')') || value.Length == 6)
		{
			return false;
		}

		var id = value[5..^1];
		if (!(char.IsAsciiLetter(id[0]) || id[0] == '_'))
		{
			return false;
		}

		foreach (var c in id)
		{
			if (!(char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or ':' or '-'))
			{
				return false;
			}
		}

		return true;
	}

	private static bool IsHexColor(ReadOnlySpan<char> value) =>
		value.Length is 4 or 5 or 7 or 9 && value[0] == '#' && !value[1..].ContainsAnyExcept(HexDigits);

	private static bool IsNamedColor(ReadOnlySpan<char> value)
	{
		if (!char.IsAsciiLetter(value[0]))
		{
			return false;
		}

		foreach (var c in value)
		{
			if (!(char.IsAsciiLetter(c) || c == '-'))
			{
				return false;
			}
		}

		return true;
	}

	private static bool IsColorFunction(ReadOnlySpan<char> value)
	{
		var open = value.IndexOf('(');
		if (open <= 0 || value[^1] != ')')
		{
			return false;
		}

		var arguments = value[(open + 1)..^1];
		return !arguments.IsEmpty && !arguments.ContainsAnyExcept(ColorArgument) && ColorFunctions.GetAlternateLookup<ReadOnlySpan<char>>().Contains(value[..open]);
	}

	/// <summary>
	/// The image of an <c>href</c> the sanitizer keeps on an <c>image</c>: a base64 <c>data:image/png</c> URI whose bytes
	/// start with PNG's signature, or a base64 <c>data:image/svg+xml</c> URI whose SVG passes the sanitizer unchanged.
	/// </summary>
	public static (byte[] Bytes, bool IsPng)? DecodeImageDataUri(string value, Func<string, bool> isCleanSvg)
	{
		const string Png = "data:image/png;base64,";
		const string Svg = "data:image/svg+xml;base64,";
		var isPng = value.StartsWith(Png, StringComparison.Ordinal);
		if (!isPng && !value.StartsWith(Svg, StringComparison.Ordinal))
		{
			return null;
		}

		var payload = value.AsSpan(isPng ? Png.Length : Svg.Length);
		var data = payload.TrimEnd('=');
		if (payload.IsEmpty || payload.Length % 4 != 0 || payload.Length - data.Length > 2 || data.ContainsAnyExcept(Base64))
		{
			return null;
		}

		var bytes = Convert.FromBase64String(payload.ToString());
		if (isPng)
		{
			ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
			return bytes.AsSpan().StartsWith(signature) ? (bytes, true) : null;
		}

		string svg;
		try
		{
			svg = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
		}
		catch (DecoderFallbackException)
		{
			return null;
		}

		return isCleanSvg(svg) ? (bytes, false) : null;
	}
}
