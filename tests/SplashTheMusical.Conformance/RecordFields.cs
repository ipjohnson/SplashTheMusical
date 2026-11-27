using System.Globalization;
using System.Text;

namespace SplashTheMusical.Conformance;

/// <summary>
/// The fields of the oracle's tab-separated layout files, <c>.dagre.txt</c> and <c>.elk.txt</c>. <c>tools/oracle/README.md</c>
/// describes their escapes and numbers.
/// </summary>
internal static class RecordFields
{
	/// <summary>The field, unescaped. A trailing empty field is dropped from the line, so a missing one is empty.</summary>
	public static string Field(string[] fields, int index) => index < fields.Length ? Unescape(fields[index]) : "";

	/// <summary>The field, or null when it is empty.</summary>
	public static string? Text(string[] fields, int index)
	{
		var text = Field(fields, index);
		return text.Length == 0 ? null : text;
	}

	/// <summary>A number written by JavaScript's <c>String</c>, or null when the field is empty.</summary>
	public static double? Number(string[] fields, int index) => Text(fields, index) is { } text ? ParseNumber(text) : null;

	public static double ParseNumber(string text) => text switch
	{
		"NaN" => double.NaN,
		"Infinity" => double.PositiveInfinity,
		"-Infinity" => double.NegativeInfinity,
		_ => double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture),
	};

	public static List<(double X, double Y)> Points(string text)
	{
		var points = new List<(double X, double Y)>();
		foreach (var pair in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
		{
			var comma = pair.IndexOf(',', StringComparison.Ordinal);
			points.Add((ParseNumber(pair[..comma]), ParseNumber(pair[(comma + 1)..])));
		}

		return points;
	}

	/// <summary>
	/// A field that lists ids separated by spaces, each unescaped. An id's own space is written <c>\s</c>, which no other
	/// field holds.
	/// </summary>
	public static List<string> Ids(string[] fields, int index) =>
		index < fields.Length ? [.. fields[index].Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Unescape)] : [];

	/// <summary>A point written as <c>x,y</c>, or null when the field is empty.</summary>
	public static (double X, double Y)? Point(string[] fields, int index) =>
		Text(fields, index) is { } text ? Points(text)[0] : null;

	public static string Unescape(string text)
	{
		if (!text.Contains('\\', StringComparison.Ordinal))
		{
			return text;
		}

		var builder = new StringBuilder(text.Length);
		for (var i = 0; i < text.Length; i++)
		{
			if (text[i] == '\\' && i + 1 < text.Length)
			{
				i++;
				builder.Append(text[i] switch
				{
					't' => '\t',
					'n' => '\n',
					'r' => '\r',
					's' => ' ',
					_ => text[i],
				});
			}
			else
			{
				builder.Append(text[i]);
			}
		}

		return builder.ToString();
	}
}
