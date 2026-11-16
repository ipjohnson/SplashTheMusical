using System.Text;

namespace SplashTheMusical.Text;

/// <summary>
/// The default <see cref="ITextMeasurer"/>. It adds the advance widths and the kerning of the generated tables of
/// <see cref="FontTables"/>.
/// </summary>
/// <remarks>
/// It takes the first family of the CSS list it has a table for, and the generic sans-serif table when it has none. A
/// weight over 500 takes the bold face, and italic or oblique the italic face, as CSS font matching does for a family
/// with only those faces. A character outside the table is as wide as its script's fallback: none for marks and
/// format characters, one em for wide characters such as CJK and emoji, and the face's mean letter width for the rest.
/// </remarks>
public sealed class FontTableTextMeasurer : ITextMeasurer
{
	public TextMetrics Measure(ReadOnlySpan<char> text, in FontSpec font)
	{
		var face = FontTables.Face(FaceIndex(font));
		var units = 0L;
		var previous = '\0';
		for (var i = 0; i < text.Length; i++)
		{
			var c = text[i];
			var advance = face.Advance(c);
			if (advance >= 0)
			{
				units += advance + (previous == '\0' ? 0 : face.Kerning(previous, c));
				previous = c;
				continue;
			}

			previous = '\0';
			if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
			{
				units += Fallback(char.ConvertToUtf32(c, text[i + 1]), face);
				i++;
			}
			else
			{
				units += Fallback(c, face);
			}
		}

		var scale = font.Size / face.UnitsPerEm;
		return new TextMetrics(units * scale, face.Ascender * scale, -face.Descender * scale);
	}

	/// <summary>The index of the face of <see cref="FontTables.Face"/> that draws <paramref name="font"/>.</summary>
	internal static int FaceIndex(in FontSpec font) =>
		(4 * Family(font.Family)) + (font.Weight > 500 ? 1 : 0) + (font.Style == FontStyle.Normal ? 0 : 2);

	/// <summary>The index of the first family of a CSS <c>font-family</c> list that has a table, or the sans-serif family 0.</summary>
	internal static int Family(ReadOnlySpan<char> families)
	{
		foreach (var range in families.Split(','))
		{
			var name = families[range].Trim();
			if (name.Length >= 2 && (name[0] is '"' or '\'') && name[^1] == name[0])
			{
				name = name[1..^1].Trim();
			}

			var family = FontTables.FindFamily(name);
			if (family >= 0)
			{
				return family;
			}
		}

		return 0;
	}

	private static int Fallback(int codePoint, FontFace face)
	{
		var rune = new Rune(codePoint);
		switch (Rune.GetUnicodeCategory(rune))
		{
			case System.Globalization.UnicodeCategory.NonSpacingMark:
			case System.Globalization.UnicodeCategory.EnclosingMark:
			case System.Globalization.UnicodeCategory.Format:
			case System.Globalization.UnicodeCategory.Control:
				return 0;
		}

		return IsWide(codePoint) ? face.UnitsPerEm : face.FallbackAdvance;
	}

	/// <summary>True for the East Asian wide and fullwidth blocks and the emoji blocks.</summary>
	private static bool IsWide(int c) =>
		c is (>= 0x1100 and <= 0x115F) // Hangul Jamo initials
			or (>= 0x2E80 and <= 0x303E) // CJK radicals, punctuation
			or (>= 0x3041 and <= 0x33FF) // kana, CJK compatibility
			or (>= 0x3400 and <= 0x4DBF) // CJK extension A
			or (>= 0x4E00 and <= 0x9FFF) // CJK unified ideographs
			or (>= 0xA000 and <= 0xA4CF) // Yi
			or (>= 0xAC00 and <= 0xD7A3) // Hangul syllables
			or (>= 0xF900 and <= 0xFAFF) // CJK compatibility ideographs
			or (>= 0xFE30 and <= 0xFE4F) // CJK compatibility forms
			or (>= 0xFF00 and <= 0xFF60) // fullwidth forms
			or (>= 0xFFE0 and <= 0xFFE6)
			or (>= 0x2600 and <= 0x27BF) // miscellaneous symbols, dingbats
			or (>= 0x1F000 and <= 0x1FAFF) // emoji
			or (>= 0x20000 and <= 0x3FFFD); // CJK extensions B and later
}
