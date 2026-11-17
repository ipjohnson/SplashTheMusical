using System.Text;

namespace SplashTheMusical.Rendering.Shapes;

/// <summary>The point and path helpers of mermaid.js's <c>rendering-elements/shapes/util.ts</c>.</summary>
internal static class ShapePaths
{
	/// <summary><c>createPathFromPoints</c>: <c>M</c> to the first point, <c>L</c> to each other one, then <c>Z</c>.</summary>
	public static string FromPoints(IReadOnlyList<ShapePoint> points, bool close = true)
	{
		var text = new StringBuilder();
		for (var i = 0; i < points.Count; i++)
		{
			text.Append(i == 0 ? "M" : " L").Append(N(points[i].X)).Append(',').Append(N(points[i].Y));
		}

		if (close)
		{
			text.Append(points.Count == 0 ? "Z" : " Z");
		}

		return text.ToString();
	}

	/// <summary><c>generateFullSineWavePoints</c>: 51 points of a sine wave from one point to another.</summary>
	public static List<ShapePoint> SineWave(double x1, double y1, double x2, double y2, double amplitude, double cycles)
	{
		const int steps = 50;
		var points = new List<ShapePoint>(steps + 1);
		var deltaX = x2 - x1;
		var deltaY = y2 - y1;
		var cycleLength = deltaX / cycles;
		var frequency = 2 * Math.PI / cycleLength;
		var midY = y1 + deltaY / 2;
		for (var i = 0; i <= steps; i++)
		{
			var t = (double)i / steps;
			var x = x1 + t * deltaX;
			points.Add(new ShapePoint(x, midY + amplitude * Math.Sin(frequency * (x - x1))));
		}

		return points;
	}

	/// <summary>
	/// <c>generateCirclePoints</c>: points on a circle between two angles in degrees, negated. The curly braces' own
	/// copy has the same body, and the right brace's copy does not negate, which <paramref name="negate"/> covers.
	/// </summary>
	public static List<ShapePoint> CirclePoints(double centerX, double centerY, double radius, int count, double startAngle, double endAngle, bool negate = true)
	{
		var points = new List<ShapePoint>(count);
		var start = startAngle * Math.PI / 180;
		var end = endAngle * Math.PI / 180;
		var step = (end - start) / (count - 1);
		for (var i = 0; i < count; i++)
		{
			var angle = start + i * step;
			var x = centerX + radius * Math.Cos(angle);
			var y = centerY + radius * Math.Sin(angle);
			points.Add(negate ? new ShapePoint(-x, -y) : new ShapePoint(x, y));
		}

		return points;
	}

	/// <summary><c>createRoundedRectPathD</c>.</summary>
	public static string RoundedRect(double x, double y, double width, double height, double radius) =>
		Join(
			"M", x + radius, y,
			"H", x + width - radius,
			"A", radius, radius, 0, 0, 1, x + width, y + radius,
			"V", y + height - radius,
			"A", radius, radius, 0, 0, 1, x + width - radius, y + height,
			"H", x + radius,
			"A", radius, radius, 0, 0, 1, x, y + height - radius,
			"V", y + radius,
			"A", radius, radius, 0, 0, 1, x + radius, y,
			"Z");

	/// <summary>A JavaScript array of strings and numbers joined with spaces, as the path builders write them.</summary>
	public static string Join(params ReadOnlySpan<object> parts)
	{
		var text = new StringBuilder();
		foreach (var part in parts)
		{
			if (text.Length > 0)
			{
				text.Append(' ');
			}

			text.Append(part switch
			{
				double number => N(number),
				int number => N(number),
				_ => (string)part,
			});
		}

		return text.ToString();
	}

	/// <summary>A number as a JavaScript template literal writes it.</summary>
	public static string N(double value) => JsNumber.ToString(value);
}
