using System.Buffers.Binary;

namespace SplashTheMusical.Text;

/// <summary>
/// The widths of one face of a font, in design units, over the static data of <see cref="FontTables"/>.
/// </summary>
/// <remarks>
/// The tables are bytes holding little-endian values, because only a byte span property compiles to a pointer into the
/// assembly's data with no allocation. The characters the face maps are runs: run <c>i</c> covers <c>RangeStarts[i]</c>
/// to <c>RangeEnds[i]</c>, and their advances start at <c>Advances[RangeOffsets[i]]</c>. Each of these is a
/// <see cref="ushort"/>. A kerned pair is a <see cref="uint"/>, the left character shifted 16 bits left over the right
/// one, sorted, and its kerning is the <see cref="short"/> at the same index.
/// </remarks>
internal readonly ref struct FontFace(
	int unitsPerEm,
	int ascender,
	int descender,
	int fallbackAdvance,
	ReadOnlySpan<byte> rangeStarts,
	ReadOnlySpan<byte> rangeEnds,
	ReadOnlySpan<byte> rangeOffsets,
	ReadOnlySpan<byte> advances,
	ReadOnlySpan<byte> kerningPairs,
	ReadOnlySpan<byte> kerningValues
)
{
	public int UnitsPerEm { get; } = unitsPerEm;

	/// <summary>The ascender of <c>hhea</c>.</summary>
	public int Ascender { get; } = ascender;

	/// <summary>The descender of <c>hhea</c>. It is negative.</summary>
	public int Descender { get; } = descender;

	/// <summary>The width of a letter of a script the face does not cover: the mean advance of a to z.</summary>
	public int FallbackAdvance { get; } = fallbackAdvance;

	private readonly ReadOnlySpan<byte> _rangeStarts = rangeStarts;
	private readonly ReadOnlySpan<byte> _rangeEnds = rangeEnds;
	private readonly ReadOnlySpan<byte> _rangeOffsets = rangeOffsets;
	private readonly ReadOnlySpan<byte> _advances = advances;
	private readonly ReadOnlySpan<byte> _kerningPairs = kerningPairs;
	private readonly ReadOnlySpan<byte> _kerningValues = kerningValues;

	/// <summary>The advance of <paramref name="c"/>, or -1 when the face does not map it.</summary>
	public int Advance(char c)
	{
		// The first run holds printable ASCII in every Latin font, so most characters are found without a search.
		var first = U16(_rangeStarts, 0);
		if ((uint)(c - first) <= (uint)(U16(_rangeEnds, 0) - first))
		{
			return U16(_advances, c - first);
		}

		var lo = 0;
		var hi = (_rangeStarts.Length / 2) - 1;
		while (lo <= hi)
		{
			var mid = (lo + hi) >>> 1;
			if (c < U16(_rangeStarts, mid))
			{
				hi = mid - 1;
			}
			else if (c > U16(_rangeEnds, mid))
			{
				lo = mid + 1;
			}
			else
			{
				return U16(_advances, U16(_rangeOffsets, mid) + (c - U16(_rangeStarts, mid)));
			}
		}

		return -1;
	}

	/// <summary>The kerning between <paramref name="left"/> and <paramref name="right"/>, or 0.</summary>
	public int Kerning(char left, char right)
	{
		var pair = ((uint)left << 16) | right;
		var lo = 0;
		var hi = (_kerningPairs.Length / 4) - 1;
		while (lo <= hi)
		{
			var mid = (lo + hi) >>> 1;
			var value = BinaryPrimitives.ReadUInt32LittleEndian(_kerningPairs[(4 * mid)..]);
			if (pair < value)
			{
				hi = mid - 1;
			}
			else if (pair > value)
			{
				lo = mid + 1;
			}
			else
			{
				return BinaryPrimitives.ReadInt16LittleEndian(_kerningValues[(2 * mid)..]);
			}
		}

		return 0;
	}

	private static int U16(ReadOnlySpan<byte> table, int index) => BinaryPrimitives.ReadUInt16LittleEndian(table[(2 * index)..]);
}
