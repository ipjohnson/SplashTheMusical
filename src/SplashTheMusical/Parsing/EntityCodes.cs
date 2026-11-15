using System.Buffers;
using System.Globalization;
using System.Text;

namespace SplashTheMusical.Parsing;

/// <summary>
/// Decodes Mermaid entity codes. mermaid.js's <c>encodeEntities</c> turns <c>#name;</c> into <c>&amp;name;</c> and
/// <c>#123;</c> into <c>&amp;#123;</c>, and the browser decodes them. A code that is not a known name or a valid code
/// point stays as written.
/// </summary>
internal static class EntityCodes
{
	private const int StackLimit = 256;

	private static readonly SearchValues<char> WordChars =
		SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_");

	public static string Decode(ReadOnlySpan<char> text)
	{
		if (!text.Contains('#'))
		{
			return text.ToString();
		}

		char[]? rented = null;
		var buffer = text.Length <= StackLimit ? stackalloc char[StackLimit] : (rented = ArrayPool<char>.Shared.Rent(text.Length));
		var result = buffer[..Decode(text, buffer)].ToString();
		if (rented is not null)
		{
			ArrayPool<char>.Shared.Return(rented);
		}

		return result;
	}

	/// <summary>Decodes into <paramref name="destination"/>, which is at least as long as <paramref name="text"/>.</summary>
	/// <returns>The number of characters written.</returns>
	public static int Decode(ReadOnlySpan<char> text, Span<char> destination)
	{
		var written = 0;
		int hash;
		while ((hash = text.IndexOf('#')) >= 0)
		{
			text[..hash].CopyTo(destination[written..]);
			written += hash;
			var rest = text[(hash + 1)..];
			var nameLength = rest.IndexOfAnyExcept(WordChars);
			if (nameLength > 0 && rest[nameLength] == ';' && TryDecode(rest[..nameLength], destination[written..], out var decoded))
			{
				written += decoded;
				text = rest[(nameLength + 1)..];
			}
			else
			{
				destination[written++] = '#';
				text = rest;
			}
		}

		text.CopyTo(destination[written..]);
		return written + text.Length;
	}

	private static bool TryDecode(ReadOnlySpan<char> name, Span<char> destination, out int written)
	{
		if (char.IsAsciiDigit(name[0]))
		{
			if (!name.ContainsAnyExceptInRange('0', '9'))
			{
				// The browser replaces a code that is out of range, a surrogate or zero with U+FFFD.
				var rune = int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
					&& value != 0 && Rune.IsValid(value) ? new Rune(value) : Rune.ReplacementChar;
				written = rune.EncodeToUtf16(destination);
				return true;
			}

			written = 0;
			return false;
		}

		var c = Named(name);
		if (c == '\0')
		{
			written = 0;
			return false;
		}

		destination[0] = c;
		written = 1;
		return true;
	}

	private static char Named(ReadOnlySpan<char> name) => name switch
	{
		"quot" => '"',
		"amp" => '&',
		"apos" => '\'',
		"lt" => '<',
		"gt" => '>',
		"nbsp" => ' ',
		"num" => '#',
		"semi" => ';',
		"colon" => ':',
		"comma" => ',',
		"period" => '.',
		"excl" => '!',
		"quest" => '?',
		"lpar" => '(',
		"rpar" => ')',
		"lsqb" or "lbrack" => '[',
		"rsqb" or "rbrack" => ']',
		"lcub" or "lbrace" => '{',
		"rcub" or "rbrace" => '}',
		"vert" or "verbar" => '|',
		"bsol" => '\\',
		"sol" => '/',
		"equals" => '=',
		"plus" => '+',
		"ast" => '*',
		"percnt" => '%',
		"dollar" => '$',
		"commat" => '@',
		"grave" => '`',
		"Hat" => '^',
		"lowbar" => '_',
		"copy" => '©',
		"reg" => '®',
		"trade" => '™',
		"deg" => '°',
		"times" => '×',
		"divide" => '÷',
		"middot" => '·',
		"bull" => '•',
		"hellip" => '…',
		"ndash" => '–',
		"mdash" => '—',
		"minus" => '−',
		"larr" => '←',
		"uarr" => '↑',
		"rarr" => '→',
		"darr" => '↓',
		"harr" => '↔',
		"infin" => '∞',
		"hearts" => '♥',
		_ => '\0',
	};
}
