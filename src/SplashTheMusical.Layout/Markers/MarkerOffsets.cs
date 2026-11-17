using System.Collections.Immutable;

namespace SplashTheMusical.Layout.Markers;

/// <summary>How far an edge stops short of its node for each arrow type, from mermaid's <c>utils/lineWithOffset.ts</c>.</summary>
internal static class MarkerOffsets
{
	/// <summary><c>markerOffsets</c>, which shortens the line so the marker ends on the node outline.</summary>
	public static ImmutableArray<(string ArrowType, double Offset)> Line { get; } =
	[
		("aggregation", 17.25),
		("extension", 17.25),
		("composition", 17.25),
		("dependency", 6),
		("lollipop", 13.5),
		("arrow_point", 4),
		("arrow_barb", 0),
		("arrow_barb_neo", 5.5),
	];

	/// <summary><c>markerOffsets2</c>, the gaps the neo look leaves at each end of an edge's dash array.</summary>
	public static ImmutableArray<(string ArrowType, double Offset)> Dash { get; } =
	[
		("arrow_point", 4),
		("arrow_cross", 12.5),
		("arrow_circle", 12.5),
		("arrow_open_head", 4),
		("arrow_hollow_circle", 8),
	];

	/// <summary>The <see cref="Line"/> offset of <paramref name="arrowType"/>, or null when it has none.</summary>
	public static double? LineOffset(string? arrowType) => Find(Line, arrowType);

	/// <summary>The <see cref="Dash"/> offset of <paramref name="arrowType"/>, or null when it has none.</summary>
	public static double? DashOffset(string? arrowType) => Find(Dash, arrowType);

	private static double? Find(ImmutableArray<(string ArrowType, double Offset)> offsets, string? arrowType)
	{
		foreach (var (type, offset) in offsets)
		{
			if (string.Equals(type, arrowType, StringComparison.Ordinal))
			{
				return offset;
			}
		}

		return null;
	}
}
