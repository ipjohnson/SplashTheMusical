using System.Text;

namespace SplashTheMusical.Layout.Paths;

/// <summary>
/// Mermaid's <c>generateRoundedPath</c> in <c>rendering-elements/edges.js</c>: straight lines whose corners are cut and
/// joined by a quadratic curve. It writes numbers in full, as mermaid's template strings do.
/// </summary>
internal static class RoundedPath
{
	/// <summary>The radius <c>insertEdge</c> passes.</summary>
	public const double Radius = 5;

	private const double Epsilon = 1e-5;

	/// <summary>Appends the path, which is empty for fewer than two points.</summary>
	public static void Write(ReadOnlySpan<PathPoint> points, double radius, StringBuilder text)
	{
		if (points.Length < 2)
		{
			return;
		}

		var path = new PathWriter(text, digits: null);
		for (var i = 0; i < points.Length; i++)
		{
			var current = points[i];
			if (i == 0)
			{
				path.MoveTo(current.X, current.Y);
				continue;
			}

			if (i == points.Length - 1)
			{
				path.LineTo(current.X, current.Y);
				continue;
			}

			var previous = points[i - 1];
			var next = points[i + 1];
			var dx1 = current.X - previous.X;
			var dy1 = current.Y - previous.Y;
			var dx2 = next.X - current.X;
			var dy2 = next.Y - current.Y;
			var length1 = JsMath.Hypot(dx1, dy1);
			var length2 = JsMath.Hypot(dx2, dy2);
			if (length1 < Epsilon || length2 < Epsilon)
			{
				path.LineTo(current.X, current.Y);
				continue;
			}

			var nx1 = dx1 / length1;
			var ny1 = dy1 / length1;
			var nx2 = dx2 / length2;
			var ny2 = dy2 / length2;
			var dot = nx1 * nx2 + ny1 * ny2;
			var angle = JsMath.Acos(Math.Max(-1, Math.Min(1, dot)));
			if (angle < Epsilon || Math.Abs(Math.PI - angle) < Epsilon)
			{
				path.LineTo(current.X, current.Y);
				continue;
			}

			var cut = Math.Min(Math.Min(radius / JsMath.Sin(angle / 2), length1 / 2), length2 / 2);
			path.LineTo(current.X - nx1 * cut, current.Y - ny1 * cut);
			text.Append('Q');
			path.Number(current.X);
			text.Append(',');
			path.Number(current.Y);
			text.Append(' ');
			path.Number(current.X + nx2 * cut);
			text.Append(',');
			path.Number(current.Y + ny2 * cut);
		}
	}
}
