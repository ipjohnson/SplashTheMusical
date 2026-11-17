using System.Globalization;

namespace SplashTheMusical.Layout.Paths;

/// <summary>
/// Formats a double as JavaScript's <c>Number.prototype.toString</c> does, the shortest digits that round-trip. The
/// layout project cannot reference the core, so this is a copy of the core's <c>JsNumber</c>, and a test holds the two
/// to the same output.
/// </summary>
internal static class JsNumber
{
	/// <summary>The most characters <see cref="Format"/> writes.</summary>
	public const int MaxLength = 32;

	public static string ToString(double value)
	{
		Span<char> text = stackalloc char[MaxLength];
		return new string(text[..Format(value, text)]);
	}

	/// <summary>Writes <paramref name="value"/> to <paramref name="destination"/>, which holds at least <see cref="MaxLength"/> characters, and returns the count written.</summary>
	public static int Format(double value, Span<char> destination)
	{
		if (double.IsNaN(value))
		{
			return Copy("NaN", destination);
		}

		if (double.IsInfinity(value))
		{
			return Copy(value > 0 ? "Infinity" : "-Infinity", destination);
		}

		if (value == 0)
		{
			return Copy("0", destination);
		}

		// .NET's "R" gives the shortest round-trip digits, in its own layout, such as 1E+21 or 1E-07.
		Span<char> buffer = stackalloc char[MaxLength];
		Math.Abs(value).TryFormat(buffer, out var length, "R", CultureInfo.InvariantCulture);
		ReadOnlySpan<char> text = buffer[..length];
		var exponentAt = text.IndexOf('E');
		var exponent = exponentAt < 0 ? 0 : int.Parse(text[(exponentAt + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
		var mantissa = exponentAt < 0 ? text : text[..exponentAt];
		var dot = mantissa.IndexOf('.');
		Span<char> digitBuffer = stackalloc char[MaxLength];
		var count = 0;
		foreach (var c in mantissa)
		{
			if (c != '.')
			{
				digitBuffer[count++] = c;
			}
		}

		ReadOnlySpan<char> digits = digitBuffer[..count];
		var integerLength = dot < 0 ? mantissa.Length : dot;
		var leading = 0;
		while (leading < digits.Length - 1 && digits[leading] == '0')
		{
			leading++;
		}

		digits = digits[leading..].TrimEnd('0');

		// The value is 0.digits times 10 to the power n, as in ECMA-262's Number::toString.
		var n = integerLength - leading + exponent;
		var k = digits.Length;
		var at = 0;
		if (value < 0)
		{
			destination[at++] = '-';
		}

		if (k <= n && n <= 21)
		{
			at += Copy(digits, destination[at..]);
			destination.Slice(at, n - k).Fill('0');
			return at + n - k;
		}

		if (0 < n && n <= 21)
		{
			at += Copy(digits[..n], destination[at..]);
			destination[at++] = '.';
			return at + Copy(digits[n..], destination[at..]);
		}

		if (-6 < n && n <= 0)
		{
			destination[at++] = '0';
			destination[at++] = '.';
			destination.Slice(at, -n).Fill('0');
			at += -n;
			return at + Copy(digits, destination[at..]);
		}

		destination[at++] = digits[0];
		if (k > 1)
		{
			destination[at++] = '.';
			at += Copy(digits[1..], destination[at..]);
		}

		var power = n - 1;
		destination[at++] = 'e';
		destination[at++] = power < 0 ? '-' : '+';
		Math.Abs(power).TryFormat(destination[at..], out var written, provider: CultureInfo.InvariantCulture);
		return at + written;
	}

	private static int Copy(ReadOnlySpan<char> text, Span<char> destination)
	{
		text.CopyTo(destination);
		return text.Length;
	}
}
