using System.Buffers;
using System.Collections.Frozen;
using System.Globalization;

namespace SplashTheMusical.Themes;

/// <summary>The channel changes of <see cref="Khroma.Adjust"/>. A channel left at 0 is not changed.</summary>
internal readonly record struct KhromaChannels(
	double H = 0, double S = 0, double L = 0, double R = 0, double G = 0, double B = 0, double A = 0);

/// <summary>
/// The functions of khroma 2.1.0 that mermaid's themes call, and mermaid's <c>mkBorder</c>. A result is formatted as
/// khroma formats it: <c>hsl()</c> after an HSL channel changes, <c>rgb()</c> or <c>rgba()</c> when a channel is not a
/// whole number or the alpha is below 1, and <c>#rrggbb</c> otherwise. A colour that does not change is returned as
/// written. A <c>double.NaN</c> amount is JavaScript's missing argument.
/// </summary>
internal static class Khroma
{
	public static string Adjust(string color, KhromaChannels channels)
	{
		var current = Parse(color);
		Span<(Channel Channel, double Value)> changes = stackalloc (Channel, double)[7];
		var count = 0;
		Add(ref current, changes, ref count, Channel.H, channels.H);
		Add(ref current, changes, ref count, Channel.S, channels.S);
		Add(ref current, changes, ref count, Channel.L, channels.L);
		Add(ref current, changes, ref count, Channel.R, channels.R);
		Add(ref current, changes, ref count, Channel.G, channels.G);
		Add(ref current, changes, ref count, Channel.B, channels.B);
		Add(ref current, changes, ref count, Channel.A, channels.A);

		var changed = Parse(color);
		foreach (var (channel, value) in changes[..count])
		{
			changed.Set(channel, Clamp(channel, value));
		}

		return changed.Stringify();

		static void Add(ref Channels current, Span<(Channel, double)> changes, ref int count, Channel channel, double amount)
		{
			if (amount != 0 && !double.IsNaN(amount))
			{
				changes[count++] = (channel, current.Get(channel) + amount);
			}
		}
	}

	public static string Darken(string color, double amount) => AdjustChannel(color, Channel.L, -amount);

	public static string Lighten(string color, double amount) => AdjustChannel(color, Channel.L, amount);

	public static string Invert(string color)
	{
		var inverse = Parse(color);
		inverse.Set(Channel.R, 255 - inverse.Get(Channel.R));
		inverse.Set(Channel.G, 255 - inverse.Get(Channel.G));
		inverse.Set(Channel.B, 255 - inverse.Get(Channel.B));
		return Mix(ref inverse, color, 100);
	}

	public static bool IsDark(string color) => !(Luminance(color) >= .5);

	public static string Rgba(string color, double alpha)
	{
		var channels = Parse(color);
		channels.Set(Channel.A, Clamp(Channel.A, alpha));
		return channels.Stringify();
	}

	public static string Rgba(double r, double g, double b = 0, double a = 1)
	{
		var channels = Channels.FromRgb(Clamp(Channel.R, r), Clamp(Channel.G, g), Clamp(Channel.B, b), Clamp(Channel.A, a), null);
		return channels.Stringify();
	}

	/// <summary>
	/// <c>fade</c> in mermaid's <c>diagrams/flowchart/styles.ts</c>: <c>rgba()</c> of the colour's red, green and blue
	/// channels with <paramref name="alpha"/>.
	/// </summary>
	public static string Fade(string color, double alpha)
	{
		var channels = Parse(color);
		return Rgba(channels.Get(Channel.R), channels.Get(Channel.G), channels.Get(Channel.B), alpha);
	}

	/// <summary>mermaid's <c>mkBorder</c> from <c>src/themes/theme-helpers.js</c>.</summary>
	public static string MkBorder(string color, bool darkMode) =>
		Adjust(color, darkMode ? new KhromaChannels(S: -40, L: 10) : new KhromaChannels(S: -40, L: -10));

	private static string AdjustChannel(string color, Channel channel, double amount)
	{
		var channels = Parse(color);
		var current = channels.Get(channel);
		var next = Clamp(channel, current + amount);
		if (current != next)
		{
			channels.Set(channel, next);
		}

		return channels.Stringify();
	}

	// dart-sass's mix, as khroma ports it. The first colour is a parsed one, as invert passes it.
	private static string Mix(ref Channels color1, string color2, double weight)
	{
		var r1 = color1.Get(Channel.R);
		var g1 = color1.Get(Channel.G);
		var b1 = color1.Get(Channel.B);
		var a1 = color1.Get(Channel.A);
		var second = Parse(color2);
		var r2 = second.Get(Channel.R);
		var g2 = second.Get(Channel.G);
		var b2 = second.Get(Channel.B);
		var a2 = second.Get(Channel.A);
		var weightScale = weight / 100;
		var weightNormalized = (weightScale * 2) - 1;
		var alphaDelta = a1 - a2;
		var weight1Combined = (weightNormalized * alphaDelta) == -1
			? weightNormalized
			: (weightNormalized + alphaDelta) / (1 + weightNormalized * alphaDelta);
		var weight1 = (weight1Combined + 1) / 2;
		var weight2 = 1 - weight1;
		var r = (r1 * weight1) + (r2 * weight2);
		var g = (g1 * weight1) + (g2 * weight2);
		var b = (b1 * weight1) + (b2 * weight2);
		var a = (a1 * weightScale) + (a2 * (1 - weightScale));
		return Rgba(r, g, b, a);
	}

	private static double Luminance(string color)
	{
		var channels = Parse(color);
		var r = channels.Get(Channel.R);
		var g = channels.Get(Channel.G);
		var b = channels.Get(Channel.B);
		return Round(.2126 * ToLinear(r) + .7152 * ToLinear(g) + .0722 * ToLinear(b));
	}

	// khroma compares the 0 to 255 value with the threshold of the 0 to 1 value.
	private static double ToLinear(double c)
	{
		var n = c / 255;
		return c > .03928 ? Math.Pow((n + .055) / 1.055, 2.4) : n / 12.92;
	}

	private static double Clamp(Channel channel, double value) => channel switch
	{
		Channel.R or Channel.G or Channel.B => value >= 255 ? 255 : (value < 0 ? 0 : value),
		Channel.H => value % 360,
		Channel.S or Channel.L => value >= 100 ? 100 : (value < 0 ? 0 : value),
		_ => value >= 1 ? 1 : (value < 0 ? 0 : value),
	};

	// Math.round(number * 10000000000) / 10000000000.
	private static double Round(double number) => JsRound(number * 10000000000) / 10000000000;

	// JavaScript's Math.round, which rounds a half up.
	private static double JsRound(double value)
	{
		var floor = Math.Floor(value);
		return value - floor >= .5 ? floor + 1 : floor;
	}

	private static string Format(double value) => JsNumber.ToString(Round(value));

	private static Channels Parse(string color)
	{
		if (Hex(color, out var channels) || Rgb(color, out channels) || Hsl(color, out channels) || Keyword(color, out channels))
		{
			return channels;
		}

		throw new FormatException($"Unsupported color format: \"{color}\"");
	}

	// ^#((?:[a-f0-9]{2}){2,4}|[a-f0-9]{3})$ with the i flag.
	private static bool Hex(string color, out Channels channels)
	{
		channels = default;
		var length = color.Length - 1;
		if (color.Length == 0 || color[0] != '#' || length is not (3 or 4 or 6 or 8) || color.AsSpan(1).ContainsAnyExcept(HexDigits))
		{
			return false;
		}

		var dec = uint.Parse(color.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
		var hasAlpha = length % 4 == 0;
		var isFullLength = length > 4;
		var multiplier = isFullLength ? 1u : 17u;
		var bits = isFullLength ? 8 : 4;
		var bitsOffset = hasAlpha ? 0 : -1;
		var mask = isFullLength ? 255u : 15u;
		channels = Channels.FromRgb(
			((dec >> (bits * (bitsOffset + 3))) & mask) * multiplier,
			((dec >> (bits * (bitsOffset + 2))) & mask) * multiplier,
			((dec >> (bits * (bitsOffset + 1))) & mask) * multiplier,
			hasAlpha ? (double)((dec & mask) * multiplier) / 255 : 1,
			color);
		return true;
	}

	private static readonly SearchValues<char> HexDigits = SearchValues.Create("0123456789abcdefABCDEF");

	// rgba?\( r sep g sep b [ (,|/) +?a ] \), each number with an optional %, with the i flag.
	private static bool Rgb(string color, out Channels channels)
	{
		channels = default;
		var text = color.AsSpan();
		var at = 0;
		if (!Name(text, ref at, "rgb")
			|| !Number(text, ref at, false, out var r) || !Percent(text, ref at, out var rPercent) || !Separator(text, ref at)
			|| !Number(text, ref at, false, out var g) || !Percent(text, ref at, out var gPercent) || !Separator(text, ref at)
			|| !Number(text, ref at, false, out var b) || !Percent(text, ref at, out var bPercent)
			|| !Alpha(text, ref at, false, out var a))
		{
			return false;
		}

		channels = Channels.FromRgb(
			Clamp(Channel.R, rPercent ? r * 2.55 : r),
			Clamp(Channel.G, gPercent ? g * 2.55 : g),
			Clamp(Channel.B, bPercent ? b * 2.55 : b),
			a,
			color);
		return true;
	}

	// hsla?\( h[deg|grad|rad|turn] sep s% sep l% [ (,|/) +?a ] \), with the i flag.
	private static bool Hsl(string color, out Channels channels)
	{
		channels = default;
		var text = color.AsSpan();
		var at = 0;
		if (!Name(text, ref at, "hsl") || !Number(text, ref at, true, out var h))
		{
			return false;
		}

		// The unit is matched without case, and only a lowercase one converts the hue.
		var unit = text[at..];
		foreach (var (name, factor) in HueUnits)
		{
			if (unit.StartsWith(name, StringComparison.OrdinalIgnoreCase))
			{
				if (unit.StartsWith(name, StringComparison.Ordinal))
				{
					h = factor(h);
				}

				at += name.Length;
				break;
			}
		}

		if (!Separator(text, ref at)
			|| !Number(text, ref at, true, out var s) || !Percent(text, ref at, out var sPercent) || !sPercent || !Separator(text, ref at)
			|| !Number(text, ref at, true, out var l) || !Percent(text, ref at, out var lPercent) || !lPercent
			|| !Alpha(text, ref at, true, out var a))
		{
			return false;
		}

		channels = Channels.FromHsl(Clamp(Channel.H, h), Clamp(Channel.S, s), Clamp(Channel.L, l), a, color);
		return true;
	}

	private static readonly (string Name, Func<double, double> Factor)[] HueUnits =
	[
		("deg", h => h),
		("grad", h => h * .9),
		("rad", h => h * 180 / Math.PI),
		("turn", h => h * 360),
	];

	// The name, an optional a, a ( and the spaces after it.
	private static bool Name(ReadOnlySpan<char> text, ref int at, string name)
	{
		if (!text.StartsWith(name, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		at = name.Length;
		if (at < text.Length && (text[at] == 'a' || text[at] == 'A'))
		{
			at++;
		}

		if (at >= text.Length || text[at] != '(')
		{
			return false;
		}

		at++;
		SkipSpaces(text, ref at);
		return true;
	}

	// -?(?:\d+(?:\.\d+)?|(?:\.\d+)) then e\d+, or e-?\d+ when the exponent may be negative.
	private static bool Number(ReadOnlySpan<char> text, ref int at, bool negativeExponent, out double value)
	{
		value = 0;
		var start = at;
		var i = at;
		if (i < text.Length && text[i] == '-')
		{
			i++;
		}

		var integer = Digits(text, i);
		i += integer;
		if (i + 1 < text.Length && text[i] == '.' && char.IsAsciiDigit(text[i + 1]))
		{
			i += 1 + Digits(text, i + 1);
		}
		else if (integer == 0)
		{
			return false;
		}

		if (i < text.Length && (text[i] == 'e' || text[i] == 'E'))
		{
			var e = i + 1;
			if (negativeExponent && e < text.Length && text[e] == '-')
			{
				e++;
			}

			var exponent = Digits(text, e);
			if (exponent > 0)
			{
				i = e + exponent;
			}
		}

		value = double.Parse(text[start..i], NumberStyles.Float, CultureInfo.InvariantCulture);
		at = i;
		return true;
	}

	private static int Digits(ReadOnlySpan<char> text, int at)
	{
		var i = at;
		while (i < text.Length && char.IsAsciiDigit(text[i]))
		{
			i++;
		}

		return i - at;
	}

	private static bool Percent(ReadOnlySpan<char> text, ref int at, out bool percent)
	{
		percent = at < text.Length && text[at] == '%';
		if (percent)
		{
			at++;
		}

		return true;
	}

	// \s*?(?:,|\s)\s*?: spaces with at most one comma, and at least one of either.
	private static bool Separator(ReadOnlySpan<char> text, ref int at)
	{
		var start = at;
		SkipSpaces(text, ref at);
		if (at < text.Length && text[at] == ',')
		{
			at++;
			SkipSpaces(text, ref at);
			return true;
		}

		return at > start;
	}

	// (?:\s*?(?:,|\/)\s*?\+?NUMBER(%?))?\s*?\)$, the alpha 1 when it is left out.
	private static bool Alpha(ReadOnlySpan<char> text, ref int at, bool negativeExponent, out double alpha)
	{
		alpha = 1;
		SkipSpaces(text, ref at);
		if (at < text.Length && (text[at] == ',' || text[at] == '/'))
		{
			at++;
			SkipSpaces(text, ref at);
			if (at < text.Length && text[at] == '+')
			{
				at++;
			}

			if (!Number(text, ref at, negativeExponent, out alpha) || !Percent(text, ref at, out var percent))
			{
				return false;
			}

			alpha = Clamp(Channel.A, percent ? alpha / 100 : alpha);
			SkipSpaces(text, ref at);
		}

		return at == text.Length - 1 && text[at] == ')';
	}

	private static void SkipSpaces(ReadOnlySpan<char> text, ref int at)
	{
		while (at < text.Length && IsJsSpace(text[at]))
		{
			at++;
		}
	}

	// JavaScript's \s: char.IsWhiteSpace without U+0085, and with U+FEFF.
	private static bool IsJsSpace(char c) => c == '﻿' || (c != '\u0085' && char.IsWhiteSpace(c));

	private static bool Keyword(string color, out Channels channels)
	{
		channels = default;
		Span<char> lower = stackalloc char[24];
		if (color.Length > lower.Length)
		{
			return false;
		}

		// JavaScript's toLowerCase. Of the characters outside ASCII, only the Kelvin sign lowers to an ASCII letter.
		for (var i = 0; i < color.Length; i++)
		{
			var c = color[i];
			if (c == 'K')
			{
				c = 'k';
			}
			else if (c > '\u007F')
			{
				return false;
			}

			lower[i] = char.ToLowerInvariant(c);
		}

		return Keywords.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(lower[..color.Length], out var hex)
			&& Hex(hex, out channels);
	}

	// khroma's table, which has cyanaqua where CSS has cyan.
	private static readonly FrozenDictionary<string, string> Keywords = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		["aliceblue"] = "#f0f8ff",
		["antiquewhite"] = "#faebd7",
		["aqua"] = "#00ffff",
		["aquamarine"] = "#7fffd4",
		["azure"] = "#f0ffff",
		["beige"] = "#f5f5dc",
		["bisque"] = "#ffe4c4",
		["black"] = "#000000",
		["blanchedalmond"] = "#ffebcd",
		["blue"] = "#0000ff",
		["blueviolet"] = "#8a2be2",
		["brown"] = "#a52a2a",
		["burlywood"] = "#deb887",
		["cadetblue"] = "#5f9ea0",
		["chartreuse"] = "#7fff00",
		["chocolate"] = "#d2691e",
		["coral"] = "#ff7f50",
		["cornflowerblue"] = "#6495ed",
		["cornsilk"] = "#fff8dc",
		["crimson"] = "#dc143c",
		["cyanaqua"] = "#00ffff",
		["darkblue"] = "#00008b",
		["darkcyan"] = "#008b8b",
		["darkgoldenrod"] = "#b8860b",
		["darkgray"] = "#a9a9a9",
		["darkgreen"] = "#006400",
		["darkgrey"] = "#a9a9a9",
		["darkkhaki"] = "#bdb76b",
		["darkmagenta"] = "#8b008b",
		["darkolivegreen"] = "#556b2f",
		["darkorange"] = "#ff8c00",
		["darkorchid"] = "#9932cc",
		["darkred"] = "#8b0000",
		["darksalmon"] = "#e9967a",
		["darkseagreen"] = "#8fbc8f",
		["darkslateblue"] = "#483d8b",
		["darkslategray"] = "#2f4f4f",
		["darkslategrey"] = "#2f4f4f",
		["darkturquoise"] = "#00ced1",
		["darkviolet"] = "#9400d3",
		["deeppink"] = "#ff1493",
		["deepskyblue"] = "#00bfff",
		["dimgray"] = "#696969",
		["dimgrey"] = "#696969",
		["dodgerblue"] = "#1e90ff",
		["firebrick"] = "#b22222",
		["floralwhite"] = "#fffaf0",
		["forestgreen"] = "#228b22",
		["fuchsia"] = "#ff00ff",
		["gainsboro"] = "#dcdcdc",
		["ghostwhite"] = "#f8f8ff",
		["gold"] = "#ffd700",
		["goldenrod"] = "#daa520",
		["gray"] = "#808080",
		["green"] = "#008000",
		["greenyellow"] = "#adff2f",
		["grey"] = "#808080",
		["honeydew"] = "#f0fff0",
		["hotpink"] = "#ff69b4",
		["indianred"] = "#cd5c5c",
		["indigo"] = "#4b0082",
		["ivory"] = "#fffff0",
		["khaki"] = "#f0e68c",
		["lavender"] = "#e6e6fa",
		["lavenderblush"] = "#fff0f5",
		["lawngreen"] = "#7cfc00",
		["lemonchiffon"] = "#fffacd",
		["lightblue"] = "#add8e6",
		["lightcoral"] = "#f08080",
		["lightcyan"] = "#e0ffff",
		["lightgoldenrodyellow"] = "#fafad2",
		["lightgray"] = "#d3d3d3",
		["lightgreen"] = "#90ee90",
		["lightgrey"] = "#d3d3d3",
		["lightpink"] = "#ffb6c1",
		["lightsalmon"] = "#ffa07a",
		["lightseagreen"] = "#20b2aa",
		["lightskyblue"] = "#87cefa",
		["lightslategray"] = "#778899",
		["lightslategrey"] = "#778899",
		["lightsteelblue"] = "#b0c4de",
		["lightyellow"] = "#ffffe0",
		["lime"] = "#00ff00",
		["limegreen"] = "#32cd32",
		["linen"] = "#faf0e6",
		["magenta"] = "#ff00ff",
		["maroon"] = "#800000",
		["mediumaquamarine"] = "#66cdaa",
		["mediumblue"] = "#0000cd",
		["mediumorchid"] = "#ba55d3",
		["mediumpurple"] = "#9370db",
		["mediumseagreen"] = "#3cb371",
		["mediumslateblue"] = "#7b68ee",
		["mediumspringgreen"] = "#00fa9a",
		["mediumturquoise"] = "#48d1cc",
		["mediumvioletred"] = "#c71585",
		["midnightblue"] = "#191970",
		["mintcream"] = "#f5fffa",
		["mistyrose"] = "#ffe4e1",
		["moccasin"] = "#ffe4b5",
		["navajowhite"] = "#ffdead",
		["navy"] = "#000080",
		["oldlace"] = "#fdf5e6",
		["olive"] = "#808000",
		["olivedrab"] = "#6b8e23",
		["orange"] = "#ffa500",
		["orangered"] = "#ff4500",
		["orchid"] = "#da70d6",
		["palegoldenrod"] = "#eee8aa",
		["palegreen"] = "#98fb98",
		["paleturquoise"] = "#afeeee",
		["palevioletred"] = "#db7093",
		["papayawhip"] = "#ffefd5",
		["peachpuff"] = "#ffdab9",
		["peru"] = "#cd853f",
		["pink"] = "#ffc0cb",
		["plum"] = "#dda0dd",
		["powderblue"] = "#b0e0e6",
		["purple"] = "#800080",
		["rebeccapurple"] = "#663399",
		["red"] = "#ff0000",
		["rosybrown"] = "#bc8f8f",
		["royalblue"] = "#4169e1",
		["saddlebrown"] = "#8b4513",
		["salmon"] = "#fa8072",
		["sandybrown"] = "#f4a460",
		["seagreen"] = "#2e8b57",
		["seashell"] = "#fff5ee",
		["sienna"] = "#a0522d",
		["silver"] = "#c0c0c0",
		["skyblue"] = "#87ceeb",
		["slateblue"] = "#6a5acd",
		["slategray"] = "#708090",
		["slategrey"] = "#708090",
		["snow"] = "#fffafa",
		["springgreen"] = "#00ff7f",
		["tan"] = "#d2b48c",
		["teal"] = "#008080",
		["thistle"] = "#d8bfd8",
		["transparent"] = "#00000000",
		["turquoise"] = "#40e0d0",
		["violet"] = "#ee82ee",
		["wheat"] = "#f5deb3",
		["white"] = "#ffffff",
		["whitesmoke"] = "#f5f5f5",
		["yellow"] = "#ffff00",
		["yellowgreen"] = "#9acd32",
	}.ToFrozenDictionary(StringComparer.Ordinal);

	private enum Channel
	{
		R,
		G,
		B,
		H,
		S,
		L,
		A,
	}

	private enum ChannelType
	{
		All,
		Rgb,
		Hsl,
	}

	/// <summary>
	/// khroma's Channels: a colour parsed as RGB or as HSL, whose other channels are computed when read. A channel that
	/// was never set is NaN, as JavaScript's undefined is in arithmetic.
	/// </summary>
	private struct Channels
	{
		private double _r = double.NaN;
		private double _g = double.NaN;
		private double _b = double.NaN;
		private double _h = double.NaN;
		private double _s = double.NaN;
		private double _l = double.NaN;
		private double _a;
		private bool _hasR;
		private bool _hasG;
		private bool _hasB;
		private bool _hasH;
		private bool _hasS;
		private bool _hasL;
		private ChannelType _type;
		private bool _changed;
		private readonly string? _color;

		private Channels(double a, string? color)
		{
			_a = a;
			_color = color;
		}

		public static Channels FromRgb(double r, double g, double b, double a, string? color) => new(a, color)
		{
			_r = r,
			_g = g,
			_b = b,
			_hasR = true,
			_hasG = true,
			_hasB = true,
		};

		public static Channels FromHsl(double h, double s, double l, double a, string color) => new(a, color)
		{
			_h = h,
			_s = s,
			_l = l,
			_hasH = true,
			_hasS = true,
			_hasL = true,
		};

		public double Get(Channel channel)
		{
			switch (channel)
			{
				case Channel.R or Channel.G or Channel.B:
					if (_type != ChannelType.Hsl && Has(channel))
					{
						return Value(channel);
					}

					EnsureHsl();
					return HslToRgb(channel);
				case Channel.H or Channel.S or Channel.L:
					if (_type != ChannelType.Rgb && Has(channel))
					{
						return Value(channel);
					}

					EnsureRgb();
					return RgbToHsl(channel);
				default:
					return _a;
			}
		}

		public void Set(Channel channel, double value)
		{
			if (channel != Channel.A)
			{
				var type = channel is Channel.R or Channel.G or Channel.B ? ChannelType.Rgb : ChannelType.Hsl;
				if (_type != ChannelType.All && _type != type)
				{
					throw new InvalidOperationException("Cannot change both RGB and HSL channels at the same time");
				}

				_type = type;
			}

			_changed = true;
			Store(channel, value);
		}

		public string Stringify()
		{
			if (!_changed && !string.IsNullOrEmpty(_color))
			{
				return _color;
			}

			if (_type == ChannelType.Hsl || !_hasR)
			{
				var h = Get(Channel.H);
				var s = Get(Channel.S);
				var l = Get(Channel.L);
				return _a < 1
					? $"hsla({Format(h)}, {Format(s)}%, {Format(l)}%, {JsNumber.ToString(_a)})"
					: $"hsl({Format(h)}, {Format(s)}%, {Format(l)}%)";
			}

			if (_a < 1 || !double.IsInteger(Get(Channel.R)) || !double.IsInteger(Get(Channel.G)) || !double.IsInteger(Get(Channel.B)))
			{
				var r = Get(Channel.R);
				var g = Get(Channel.G);
				var b = Get(Channel.B);
				return _a < 1
					? $"rgba({Format(r)}, {Format(g)}, {Format(b)}, {Format(_a)})"
					: $"rgb({Format(r)}, {Format(g)}, {Format(b)})";
			}

			return $"#{HexByte(Get(Channel.R))}{HexByte(Get(Channel.G))}{HexByte(Get(Channel.B))}";
		}

		// khroma's DEC2HEX table, which has the bytes 0 to 255.
		private static string HexByte(double value)
		{
			var rounded = JsRound(value);
			return rounded is >= 0 and <= 255 ? ((int)rounded).ToString("x2", CultureInfo.InvariantCulture) : "undefined";
		}

		private readonly bool Has(Channel channel) => channel switch
		{
			Channel.R => _hasR,
			Channel.G => _hasG,
			Channel.B => _hasB,
			Channel.H => _hasH,
			Channel.S => _hasS,
			_ => _hasL,
		};

		private readonly double Value(Channel channel) => channel switch
		{
			Channel.R => _r,
			Channel.G => _g,
			Channel.B => _b,
			Channel.H => _h,
			Channel.S => _s,
			Channel.L => _l,
			_ => _a,
		};

		private void Store(Channel channel, double value)
		{
			switch (channel)
			{
				case Channel.R:
					_r = value;
					_hasR = true;
					break;
				case Channel.G:
					_g = value;
					_hasG = true;
					break;
				case Channel.B:
					_b = value;
					_hasB = true;
					break;
				case Channel.H:
					_h = value;
					_hasH = true;
					break;
				case Channel.S:
					_s = value;
					_hasS = true;
					break;
				case Channel.L:
					_l = value;
					_hasL = true;
					break;
				default:
					_a = value;
					break;
			}
		}

		private void EnsureHsl()
		{
			foreach (var channel in (ReadOnlySpan<Channel>)[Channel.H, Channel.S, Channel.L])
			{
				if (!Has(channel))
				{
					Store(channel, RgbToHsl(channel));
				}
			}
		}

		private void EnsureRgb()
		{
			foreach (var channel in (ReadOnlySpan<Channel>)[Channel.R, Channel.G, Channel.B])
			{
				if (!Has(channel))
				{
					Store(channel, HslToRgb(channel));
				}
			}
		}

		private readonly double HslToRgb(Channel channel)
		{
			if (_s == 0 || double.IsNaN(_s))
			{
				return _l * 2.55;
			}

			var h = _h / 360;
			var s = _s / 100;
			var l = _l / 100;
			var q = (l < .5) ? l * (1 + s) : (l + s) - (l * s);
			var p = 2 * l - q;
			return channel switch
			{
				Channel.R => HueToRgb(p, q, h + 1.0 / 3) * 255,
				Channel.G => HueToRgb(p, q, h) * 255,
				_ => HueToRgb(p, q, h - 1.0 / 3) * 255,
			};
		}

		private static double HueToRgb(double p, double q, double t)
		{
			if (t < 0)
			{
				t += 1;
			}

			if (t > 1)
			{
				t -= 1;
			}

			if (t < 1.0 / 6)
			{
				return p + (q - p) * 6 * t;
			}

			if (t < 1.0 / 2)
			{
				return q;
			}

			if (t < 2.0 / 3)
			{
				return p + (q - p) * (2.0 / 3 - t) * 6;
			}

			return p;
		}

		private readonly double RgbToHsl(Channel channel)
		{
			var r = _r / 255;
			var g = _g / 255;
			var b = _b / 255;
			var max = Math.Max(r, Math.Max(g, b));
			var min = Math.Min(r, Math.Min(g, b));
			var l = (max + min) / 2;
			if (channel == Channel.L)
			{
				return l * 100;
			}

			if (max == min)
			{
				return 0;
			}

			var d = max - min;
			var s = (l > .5) ? d / (2 - max - min) : d / (max + min);
			if (channel == Channel.S)
			{
				return s * 100;
			}

			if (max == r)
			{
				return ((g - b) / d + (g < b ? 6 : 0)) * 60;
			}

			if (max == g)
			{
				return ((b - r) / d + 2) * 60;
			}

			return max == b ? ((r - g) / d + 4) * 60 : -1;
		}
	}
}
