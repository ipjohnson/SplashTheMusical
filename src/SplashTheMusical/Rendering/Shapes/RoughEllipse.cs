namespace SplashTheMusical.Rendering.Shapes;

/// <summary>
/// The box of the curve roughjs 4.6.6 draws for an ellipse at roughness 0, as the classic look draws its circles:
/// <c>generateEllipseParams</c>, the roughness-0 branch of <c>_computeEllipsePoints</c> and <c>_curve</c>, with the
/// defaults <c>curveStepCount</c> 9 and <c>curveTightness</c> 0. The curve runs through points a quarter step apart, and
/// its Bézier segments bulge a little past the ellipse, so its box is slightly larger than the ellipse's.
/// </summary>
internal static class RoughEllipse
{
	private const double CurveStepCount = 9;

	public static ShapeBounds Bounds(double cx, double cy, double width, double height)
	{
		var psq = Math.Sqrt(Math.PI * 2 * Math.Sqrt((Math.Pow(width / 2, 2) + Math.Pow(height / 2, 2)) / 2));
		var stepCount = Math.Ceiling(Math.Max(CurveStepCount, CurveStepCount / Math.Sqrt(200) * psq));
		var increment = Math.PI * 2 / stepCount / 4;
		var rx = Math.Abs(width / 2);
		var ry = Math.Abs(height / 2);
		var points = new List<ShapePoint> { new(cx + rx * Math.Cos(-increment), cy + ry * Math.Sin(-increment)) };
		for (var angle = 0.0; angle <= Math.PI * 2; angle += increment)
		{
			points.Add(new ShapePoint(cx + rx * Math.Cos(angle), cy + ry * Math.Sin(angle)));
		}

		points.Add(new ShapePoint(cx + rx * Math.Cos(0), cy + ry * Math.Sin(0)));
		points.Add(new ShapePoint(cx + rx * Math.Cos(increment), cy + ry * Math.Sin(increment)));

		var bounds = ShapeBounds.None.Add(points[1].X, points[1].Y);
		for (var i = 1; i + 2 < points.Count; i++)
		{
			var p0 = points[i];
			var p1 = new ShapePoint(p0.X + (points[i + 1].X - points[i - 1].X) / 6, p0.Y + (points[i + 1].Y - points[i - 1].Y) / 6);
			var p3 = points[i + 1];
			var p2 = new ShapePoint(p3.X + (p0.X - points[i + 2].X) / 6, p3.Y + (p0.Y - points[i + 2].Y) / 6);
			bounds = CubicBounds(bounds, p0, p1, p2, p3);
		}

		return bounds;
	}

	/// <summary>Adds the end and the extreme points of a cubic Bézier segment to the box.</summary>
	private static ShapeBounds CubicBounds(ShapeBounds bounds, ShapePoint p0, ShapePoint p1, ShapePoint p2, ShapePoint p3)
	{
		bounds = bounds.Add(p3.X, p3.Y);
		Span<double> roots = stackalloc double[4];
		var count = Extremes(p0.X, p1.X, p2.X, p3.X, roots);
		count += Extremes(p0.Y, p1.Y, p2.Y, p3.Y, roots[count..]);
		foreach (var t in roots[..count])
		{
			var u = 1 - t;
			var x = u * u * u * p0.X + 3 * u * u * t * p1.X + 3 * u * t * t * p2.X + t * t * t * p3.X;
			var y = u * u * u * p0.Y + 3 * u * u * t * p1.Y + 3 * u * t * t * p2.Y + t * t * t * p3.Y;
			bounds = bounds.Add(x, y);
		}

		return bounds;
	}

	/// <summary>The parameters in (0, 1) where the derivative of one coordinate of the segment is 0.</summary>
	private static int Extremes(double a0, double a1, double a2, double a3, Span<double> roots)
	{
		var a = -a0 + 3 * a1 - 3 * a2 + a3;
		var b = 2 * (a0 - 2 * a1 + a2);
		var c = a1 - a0;
		var count = 0;
		if (Math.Abs(a) < 1e-12)
		{
			if (Math.Abs(b) > 1e-12)
			{
				count = Keep(-c / b, roots, count);
			}

			return count;
		}

		var discriminant = b * b - 4 * a * c;
		if (discriminant < 0)
		{
			return 0;
		}

		var root = Math.Sqrt(discriminant);
		count = Keep((-b + root) / (2 * a), roots, count);
		return Keep((-b - root) / (2 * a), roots, count);
	}

	private static int Keep(double t, Span<double> roots, int count)
	{
		if (t > 0 && t < 1)
		{
			roots[count++] = t;
		}

		return count;
	}
}
