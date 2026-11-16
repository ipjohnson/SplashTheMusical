using System.Globalization;
using System.Text;

namespace SplashTheMusical.Rendering;

/// <summary>The text rules the typed CSS shares: stylis's whitespace, CSS identifiers and Chromium's numbers.</summary>
internal static class CssText
{
	/// <summary>
	/// Appends <paramref name="text"/> with whitespace as stylis 4 writes it. Outside strings and parentheses a run of
	/// whitespace becomes one space, and none after <c>!+,/&gt;@~;{}:</c> or before <c>!+,/&gt;@~;{}</c>. Strings and
	/// parentheses are copied as they are. Leading and trailing whitespace is dropped.
	/// </summary>
	public static void AppendStylis(StringBuilder builder, ReadOnlySpan<char> text)
	{
		var start = builder.Length;
		var depth = 0;
		var quote = '\0';
		for (var i = 0; i < text.Length; i++)
		{
			var c = text[i];
			if (quote != '\0')
			{
				builder.Append(c);
				if (c == quote)
				{
					quote = '\0';
				}

				continue;
			}

			if (c is '"' or '\'')
			{
				quote = c;
				builder.Append(c);
				continue;
			}

			if (depth > 0)
			{
				builder.Append(c);
				depth += c switch
				{
					'(' => 1,
					')' => -1,
					_ => 0,
				};
				continue;
			}

			if (!IsWhitespace(c))
			{
				depth = c == '(' ? 1 : 0;
				builder.Append(c);
				continue;
			}

			var next = i + 1;
			while (next < text.Length && IsWhitespace(text[next]))
			{
				next++;
			}

			i = next - 1;
			if (builder.Length == start || next == text.Length)
			{
				continue;
			}

			var previous = builder[^1];
			if (previous is '!' or '+' or ',' or '/' or '>' or '@' or '~' or ';' or '{' or '}' or ':'
				|| text[next] is '!' or '+' or ',' or '/' or '>' or '@' or '~' or ';' or '{' or '}')
			{
				continue;
			}

			builder.Append(' ');
		}
	}

	public static bool IsWhitespace(char c) => c is ' ' or '\t' or '\n' or '\r' or '\f';

	/// <summary>
	/// Whether <paramref name="text"/> is a CSS identifier that needs no escape: it does not start with a digit, or with
	/// a hyphen and a digit, and holds only letters, digits, <c>-</c>, <c>_</c> and characters above U+007F.
	/// </summary>
	public static bool IsIdentifier(ReadOnlySpan<char> text)
	{
		if (text.IsEmpty || text is "-" || char.IsAsciiDigit(text[0]) || (text[0] == '-' && char.IsAsciiDigit(text[1])))
		{
			return false;
		}

		foreach (var c in text)
		{
			if (!IsNameChar(c))
			{
				return false;
			}
		}

		return true;
	}

	public static bool IsNameChar(char c) => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' || (c > '\u007f' && !char.IsWhiteSpace(c) && !char.IsControl(c));

	/// <summary>A number as Chromium serializes one in a declaration: six significant digits, no trailing zeros.</summary>
	public static string Number(double value) =>
		value == 0 ? "0" : value.ToString("G6", CultureInfo.InvariantCulture).Replace('E', 'e');
}
