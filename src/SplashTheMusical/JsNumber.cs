using System.Globalization;

namespace SplashTheMusical;

/// <summary>Formats a double as JavaScript's <c>Number.prototype.toString</c> does, the shortest digits that round-trip.</summary>
internal static class JsNumber
{
	public static string ToString(double value)
	{
		if (double.IsNaN(value))
		{
			return "NaN";
		}

		if (double.IsInfinity(value))
		{
			return value > 0 ? "Infinity" : "-Infinity";
		}

		if (value == 0)
		{
			return "0";
		}

		// .NET's "R" gives the shortest round-trip digits, in its own layout, such as 1E+21 or 1E-07.
		var text = Math.Abs(value).ToString("R", CultureInfo.InvariantCulture);
		var exponentAt = text.IndexOf('E', StringComparison.Ordinal);
		var exponent = exponentAt < 0 ? 0 : int.Parse(text.AsSpan(exponentAt + 1), CultureInfo.InvariantCulture);
		var mantissa = exponentAt < 0 ? text : text[..exponentAt];
		var dot = mantissa.IndexOf('.', StringComparison.Ordinal);
		var digits = dot < 0 ? mantissa : string.Concat(mantissa.AsSpan(0, dot), mantissa.AsSpan(dot + 1));
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
		var sign = value < 0 ? "-" : "";
		if (k <= n && n <= 21)
		{
			return sign + digits + new string('0', n - k);
		}

		if (0 < n && n <= 21)
		{
			return sign + digits[..n] + "." + digits[n..];
		}

		if (-6 < n && n <= 0)
		{
			return sign + "0." + new string('0', -n) + digits;
		}

		var power = n - 1;
		var powerText = (power < 0 ? "-" : "+") + Math.Abs(power).ToString(CultureInfo.InvariantCulture);
		return k == 1
			? sign + digits + "e" + powerText
			: sign + digits[..1] + "." + digits[1..] + "e" + powerText;
	}
}
