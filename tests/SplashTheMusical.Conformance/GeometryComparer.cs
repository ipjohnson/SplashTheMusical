using System.Globalization;

namespace SplashTheMusical.Conformance;

/// <summary>
/// L3: compares a provider's boxes with the oracle's after aligning the two canvases. It checks the canvas size, the
/// order of the box centers on each axis, the overlap of each box with its counterpart, and the overlaps
/// <see cref="OverlapLint"/> finds that mermaid.js does not have. Boxes pair by kind and name. A box only one side has
/// is an L2 difference.
/// </summary>
/// <remarks>
/// A difference's line is the oracle's line when it is missing and the provider's when it is extra: a canvas line, a
/// box line, an <c>order</c> line of axis, box, relation and box, or an <c>overlap</c> line of a lint finding.
/// </remarks>
public static class GeometryComparer
{
	/// <summary>Box centers closer than this on an axis, in user units, share a rank.</summary>
	public const double RankTolerance = 1;

	/// <summary>A box side shorter than this, in user units, is widened to it before the overlap is measured.</summary>
	public const double MinimumExtent = 10;

	/// <param name="type">The corpus type folder, which picks the lint roles.</param>
	public static IReadOnlyList<Difference> Compare(string type, CaseFacts oracle, CaseFacts actual, Tolerance tolerance)
	{
		var differences = new List<Difference>();
		var expectedCanvas = Rect.ParseCanvas(oracle.Canvas);
		var actualCanvas = Rect.ParseCanvas(actual.Canvas);
		if (expectedCanvas is not { } e || actualCanvas is not { } a)
		{
			if (oracle.Canvas != actual.Canvas)
			{
				Add(differences, oracle.Canvas, actual.Canvas);
			}

			return differences;
		}

		if (!Within(e.Width, a.Width, tolerance.Canvas) || !Within(e.Height, a.Height, tolerance.Canvas))
		{
			Add(differences, oracle.Canvas, actual.Canvas);
		}

		var expectedBoxes = Aligned(oracle.Boxes, e);
		var actualBoxes = Aligned(actual.Boxes, a);
		var pairs = expectedBoxes
			.Where(b => actualBoxes.ContainsKey(b.Key))
			.Select(b => (Expected: b.Value, Actual: actualBoxes[b.Key]))
			.OrderBy(p => p.Expected.Identity, StringComparer.Ordinal)
			.ToList();

		foreach (var (expected, found) in pairs)
		{
			if (expected.Rect.IntersectionOverUnion(found.Rect, MinimumExtent) < tolerance.Overlap)
			{
				Add(differences, expected.Line, found.Line);
			}
		}

		Order(differences, pairs, "x", r => r.CenterX);
		Order(differences, pairs, "y", r => r.CenterY);

		var origin = new Rect(0, 0, e.Width, e.Height);
		var expectedFindings = OverlapLint.Run(type, origin, expectedBoxes.Values);
		var actualFindings = OverlapLint.Run(type, origin with { Width = a.Width, Height = a.Height }, actualBoxes.Values);
		differences.AddRange(OverlapLint.Only(actualFindings, expectedFindings).Select(f => new Difference(Level.L3, false, f.Line)));

		return [.. differences.OrderBy(d => d.Line, StringComparer.Ordinal).ThenBy(d => !d.Missing)];
	}

	private static bool Within(double expected, double actual, double fraction) => Math.Abs(actual - expected) <= fraction * expected;

	// The boxes by identity, moved so the canvas starts at 0, 0. A repeated identity keeps its first box.
	private static Dictionary<string, Box> Aligned(IEnumerable<string> lines, Rect canvas)
	{
		var boxes = new Dictionary<string, Box>(StringComparer.Ordinal);
		foreach (var box in lines.Select(Box.Parse))
		{
			_ = boxes.TryAdd(box.Identity, box with { Rect = box.Rect.Translate(-canvas.X, -canvas.Y) });
		}

		return boxes;
	}

	// Every pair of boxes whose centers relate differently on the axis: before, in the same rank, or after.
	private static void Order(List<Difference> differences, List<(Box Expected, Box Actual)> pairs, string axis, Func<Rect, double> center)
	{
		for (var i = 0; i < pairs.Count; i++)
		{
			for (var j = i + 1; j < pairs.Count; j++)
			{
				var expected = Relation(center(pairs[i].Expected.Rect) - center(pairs[j].Expected.Rect));
				var actual = Relation(center(pairs[i].Actual.Rect) - center(pairs[j].Actual.Rect));
				if (expected != actual)
				{
					var (first, second) = (pairs[i].Expected.Identity, pairs[j].Expected.Identity);
					Add(
						differences,
						string.Create(CultureInfo.InvariantCulture, $"order\t{axis}\t{first}\t{expected}\t{second}"),
						string.Create(CultureInfo.InvariantCulture, $"order\t{axis}\t{first}\t{actual}\t{second}")
					);
				}
			}
		}
	}

	private static char Relation(double difference) =>
		difference < -RankTolerance ? '<'
		: difference > RankTolerance ? '>'
		: '=';

	private static void Add(List<Difference> differences, string? expected, string? actual)
	{
		if (expected is not null)
		{
			differences.Add(new Difference(Level.L3, true, expected));
		}

		if (actual is not null)
		{
			differences.Add(new Difference(Level.L3, false, actual));
		}
	}
}
