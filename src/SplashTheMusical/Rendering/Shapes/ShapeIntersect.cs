namespace SplashTheMusical.Rendering.Shapes;

/// <summary>
/// A shape's <c>node.intersect</c>: where the line from the node's centre to a point leaves the node. The node is
/// given by its centre and its size, as mermaid.js reads <c>node.x</c>, <c>node.y</c>, <c>node.width</c> and
/// <c>node.height</c> after layout. Ported from <c>rendering-elements/intersect</c>.
/// </summary>
internal abstract class ShapeIntersect
{
	public abstract ShapePoint Find(ShapePoint center, double width, double height, ShapePoint point);

	/// <summary><c>intersect.rect</c>.</summary>
	public static ShapeIntersect Rect { get; } = new RectIntersect();

	/// <summary><c>intersect.polygon</c> with the shape's points.</summary>
	public static ShapeIntersect Polygon(IReadOnlyList<ShapePoint> points) => new PolygonIntersect(points);

	/// <summary><c>intersect.circle</c>.</summary>
	public static ShapeIntersect Circle(double radius) => new EllipseIntersect(radius, radius);

	/// <summary><c>intersect.circle</c> with half the node's width, which the state start and end read when the edge is drawn.</summary>
	public static ShapeIntersect CircleOfWidth { get; } = new CircleOfWidthIntersect();

	/// <summary>The cylinder's intersect: the rect's, moved onto the ellipse of the top or the bottom.</summary>
	public static ShapeIntersect Cylinder(double rx, double ry) => new CylinderIntersect(rx, ry);

	/// <summary>The horizontal cylinder's intersect: the rect's, moved onto the ellipse of the left or the right end.</summary>
	public static ShapeIntersect TiltedCylinder(double rx, double ry) => new TiltedCylinderIntersect(rx, ry);

	internal static ShapePoint RectPoint(ShapePoint center, double width, double height, ShapePoint point)
	{
		var dx = point.X - center.X;
		var dy = point.Y - center.Y;
		var w = width / 2;
		var h = height / 2;
		double sx, sy;
		if (Math.Abs(dy) * w > Math.Abs(dx) * h)
		{
			if (dy < 0)
			{
				h = -h;
			}

			sx = dy == 0 ? 0 : h * dx / dy;
			sy = h;
		}
		else
		{
			if (dx < 0)
			{
				w = -w;
			}

			sx = w;
			sy = dx == 0 ? 0 : w * dy / dx;
		}

		return new ShapePoint(center.X + sx, center.Y + sy);
	}

	private sealed class RectIntersect : ShapeIntersect
	{
		public override ShapePoint Find(ShapePoint center, double width, double height, ShapePoint point) =>
			RectPoint(center, width, height, point);
	}

	/// <summary><c>intersectEllipse</c>.</summary>
	internal static ShapePoint EllipsePoint(ShapePoint center, double rx, double ry, ShapePoint point)
	{
		var px = center.X - point.X;
		var py = center.Y - point.Y;
		var det = Math.Sqrt(rx * rx * py * py + ry * ry * px * px);
		var dx = Math.Abs(rx * ry * px / det);
		if (point.X < center.X)
		{
			dx = -dx;
		}

		var dy = Math.Abs(rx * ry * py / det);
		if (point.Y < center.Y)
		{
			dy = -dy;
		}

		return new ShapePoint(center.X + dx, center.Y + dy);
	}

	private sealed class EllipseIntersect(double rx, double ry) : ShapeIntersect
	{
		public override ShapePoint Find(ShapePoint center, double width, double height, ShapePoint point) =>
			EllipsePoint(center, rx, ry, point);
	}

	private sealed class CircleOfWidthIntersect : ShapeIntersect
	{
		public override ShapePoint Find(ShapePoint center, double width, double height, ShapePoint point) =>
			EllipsePoint(center, width / 2, width / 2, point);
	}

	private sealed class PolygonIntersect(IReadOnlyList<ShapePoint> points) : ShapeIntersect
	{
		public override ShapePoint Find(ShapePoint center, double width, double height, ShapePoint point)
		{
			var minX = double.PositiveInfinity;
			var minY = double.PositiveInfinity;
			foreach (var entry in points)
			{
				minX = Math.Min(minX, entry.X);
				minY = Math.Min(minY, entry.Y);
			}

			var left = center.X - width / 2 - minX;
			var top = center.Y - height / 2 - minY;
			ShapePoint? nearest = null;
			var nearestDistance = double.PositiveInfinity;
			for (var i = 0; i < points.Count; i++)
			{
				var p1 = points[i];
				var p2 = points[i < points.Count - 1 ? i + 1 : 0];
				var found = Line(center, point, new ShapePoint(left + p1.X, top + p1.Y), new ShapePoint(left + p2.X, top + p2.Y));
				if (found is { } hit)
				{
					// mermaid sorts the hits by distance with a stable sort and takes the first, so the first of equals wins.
					var distance = Math.Sqrt((hit.X - point.X) * (hit.X - point.X) + (hit.Y - point.Y) * (hit.Y - point.Y));
					if (distance < nearestDistance)
					{
						nearest = hit;
						nearestDistance = distance;
					}
				}
			}

			return nearest ?? center;
		}

		/// <summary><c>intersectLine</c>: where segments p and q cross, or null when they do not.</summary>
		private static ShapePoint? Line(ShapePoint p1, ShapePoint p2, ShapePoint q1, ShapePoint q2)
		{
			var a1 = p2.Y - p1.Y;
			var b1 = p1.X - p2.X;
			var c1 = p2.X * p1.Y - p1.X * p2.Y;
			var r3 = a1 * q1.X + b1 * q1.Y + c1;
			var r4 = a1 * q2.X + b1 * q2.Y + c1;
			const double epsilon = 1e-6;
			if (r3 != 0 && r4 != 0 && r3 * r4 > 0)
			{
				return null;
			}

			var a2 = q2.Y - q1.Y;
			var b2 = q1.X - q2.X;
			var c2 = q2.X * q1.Y - q1.X * q2.Y;
			var r1 = a2 * p1.X + b2 * p1.Y + c2;
			var r2 = a2 * p2.X + b2 * p2.Y + c2;
			if (Math.Abs(r1) < epsilon && Math.Abs(r2) < epsilon && r1 * r2 > 0)
			{
				return null;
			}

			var denominator = a1 * b2 - a2 * b1;
			if (denominator == 0)
			{
				return null;
			}

			return new ShapePoint((b1 * c2 - b2 * c1) / denominator, (a2 * c1 - a1 * c2) / denominator);
		}
	}

	private sealed class CylinderIntersect(double rx, double ry) : ShapeIntersect
	{
		public override ShapePoint Find(ShapePoint center, double width, double height, ShapePoint point)
		{
			var pos = RectPoint(center, width, height, point);
			var x = pos.X - center.X;
			if (rx != 0
				&& (Math.Abs(x) < width / 2 || (Math.Abs(x) == width / 2 && Math.Abs(pos.Y - center.Y) > height / 2 - ry)))
			{
				var y = ry * ry * (1 - x * x / (rx * rx));
				if (y > 0)
				{
					y = Math.Sqrt(y);
				}

				y = ry - y;
				if (point.Y - center.Y > 0)
				{
					y = -y;
				}

				pos = pos with { Y = pos.Y + y };
			}

			return pos;
		}
	}

	private sealed class TiltedCylinderIntersect(double rx, double ry) : ShapeIntersect
	{
		public override ShapePoint Find(ShapePoint center, double width, double height, ShapePoint point)
		{
			var pos = RectPoint(center, width, height, point);
			var y = pos.Y - center.Y;
			if (ry != 0
				&& (Math.Abs(y) < height / 2 || (Math.Abs(y) == height / 2 && Math.Abs(pos.X - center.X) > width / 2 - rx)))
			{
				var x = rx * rx * (1 - y * y / (ry * ry));
				if (x != 0)
				{
					x = Math.Sqrt(Math.Abs(x));
				}

				x = rx - x;
				if (point.X - center.X > 0)
				{
					x = -x;
				}

				pos = pos with { X = pos.X + x };
			}

			return pos;
		}
	}
}
