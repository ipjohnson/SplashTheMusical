using System.Buffers.Binary;

namespace SplashTheMusical.Upstream;

/// <summary>The metrics of a TrueType or OpenType font file that text measurement needs.</summary>
/// <param name="UnitsPerEm">The design units of one em, from <c>head</c>.</param>
/// <param name="Ascender">The ascender from <c>hhea</c>, in design units.</param>
/// <param name="Descender">The descender from <c>hhea</c>, in design units. It is negative.</param>
/// <param name="Advances">The advance width of each Basic Multilingual Plane character the <c>cmap</c> maps, in design units.</param>
/// <param name="Kerning">The horizontal kerning of each pair of those characters, in design units, from the <c>kern</c> table.</param>
public sealed record TrueTypeFont(
	int UnitsPerEm,
	int Ascender,
	int Descender,
	SortedDictionary<char, int> Advances,
	SortedDictionary<(char Left, char Right), int> Kerning
)
{
	public static TrueTypeFont Read(byte[] file)
	{
		var tables = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal);
		var count = U16(file, 4);
		for (var i = 0; i < count; i++)
		{
			var record = 12 + (16 * i);
			var tag = System.Text.Encoding.ASCII.GetString(file, record, 4);
			tables[tag] = file.AsMemory((int)U32(file, record + 8), (int)U32(file, record + 12));
		}

		var head = tables["head"].Span;
		var hhea = tables["hhea"].Span;
		var hmtx = tables["hmtx"].Span;
		var unitsPerEm = U16(head, 18);
		var ascender = S16(hhea, 4);
		var descender = S16(hhea, 6);
		var metricCount = U16(hhea, 34);

		var glyphs = CharacterGlyphs(tables["cmap"].Span);
		var advances = new SortedDictionary<char, int>();
		foreach (var (character, glyph) in glyphs)
		{
			// Glyphs past the last long metric share its advance.
			advances[character] = U16(hmtx, 4 * Math.Min(glyph, metricCount - 1));
		}

		var kerning = new SortedDictionary<(char, char), int>();
		if (tables.TryGetValue("kern", out var kern))
		{
			var pairs = GlyphKerning(kern.Span);
			var byGlyph = glyphs.GroupBy(pair => pair.Value).ToDictionary(group => group.Key, group => group.Select(pair => pair.Key).ToList());
			foreach (var ((left, right), value) in pairs)
			{
				if (byGlyph.TryGetValue(left, out var lefts) && byGlyph.TryGetValue(right, out var rights))
				{
					foreach (var l in lefts)
					{
						foreach (var r in rights)
						{
							kerning[(l, r)] = value;
						}
					}
				}
			}
		}

		return new TrueTypeFont(unitsPerEm, ascender, descender, advances, kerning);
	}

	/// <summary>The glyph of each BMP character, from the Windows Unicode BMP subtable, which is format 4.</summary>
	private static SortedDictionary<char, int> CharacterGlyphs(ReadOnlySpan<byte> cmap)
	{
		var count = U16(cmap, 2);
		for (var i = 0; i < count; i++)
		{
			var record = 4 + (8 * i);
			if (U16(cmap, record) == 3 && U16(cmap, record + 2) == 1)
			{
				return Format4(cmap[(int)U32(cmap, record + 4)..]);
			}
		}

		throw new InvalidDataException("The font has no Windows Unicode BMP cmap subtable.");
	}

	private static SortedDictionary<char, int> Format4(ReadOnlySpan<byte> table)
	{
		if (U16(table, 0) != 4)
		{
			throw new InvalidDataException("The Windows Unicode BMP cmap subtable is not format 4.");
		}

		var result = new SortedDictionary<char, int>();
		var segments = U16(table, 6) / 2;
		var ends = 14;
		var starts = ends + (2 * segments) + 2;
		var deltas = starts + (2 * segments);
		var rangeOffsets = deltas + (2 * segments);
		for (var s = 0; s < segments; s++)
		{
			var end = U16(table, ends + (2 * s));
			var start = U16(table, starts + (2 * s));
			var delta = S16(table, deltas + (2 * s));
			var rangeOffset = U16(table, rangeOffsets + (2 * s));
			for (var c = start; c <= end && c != 0xFFFF; c++)
			{
				int glyph;
				if (rangeOffset == 0)
				{
					glyph = (c + delta) & 0xFFFF;
				}
				else
				{
					var at = rangeOffsets + (2 * s) + rangeOffset + (2 * (c - start));
					glyph = U16(table, at);
					if (glyph != 0)
					{
						glyph = (glyph + delta) & 0xFFFF;
					}
				}

				if (glyph != 0)
				{
					result[(char)c] = glyph;
				}
			}
		}

		return result;
	}

	/// <summary>The horizontal pairs of the format 0 subtables of a version 0 <c>kern</c> table.</summary>
	private static Dictionary<(int, int), int> GlyphKerning(ReadOnlySpan<byte> kern)
	{
		var pairs = new Dictionary<(int, int), int>();
		if (U16(kern, 0) != 0)
		{
			return pairs;
		}

		var offset = 4;
		for (var t = U16(kern, 2); t > 0; t--)
		{
			var length = U16(kern, offset + 2);
			var coverage = U16(kern, offset + 4);
			if ((coverage >> 8) == 0 && (coverage & 0x7) == 0x1)
			{
				var count = U16(kern, offset + 6);
				for (var p = 0; p < count; p++)
				{
					var at = offset + 14 + (6 * p);
					pairs[(U16(kern, at), U16(kern, at + 2))] = S16(kern, at + 4);
				}
			}

			offset += length;
		}

		return pairs;
	}

	private static int U16(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);

	private static int S16(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadInt16BigEndian(data[offset..]);

	private static uint U32(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
}
