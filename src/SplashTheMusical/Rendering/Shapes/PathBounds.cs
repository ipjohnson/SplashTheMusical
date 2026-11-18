using System.Globalization;

namespace SplashTheMusical.Rendering.Shapes;

/// <summary>
/// The tight box of the path data the shapes and the edges write: the commands M, L, H, V, C, Q, A and Z in both cases.
/// A cubic or a quadratic counts its end points and the points where its x or its y turns. An arc counts
/// its end points and the extreme points of its ellipse that it passes, as the SVG spec's centre parameterization
/// gives them. A number JavaScript writes as <c>NaN</c> or <c>Infinity</c> is an error in path data, and as a browser
/// draws a path up to its first error, the box is that of the commands before it.
/// </summary>
internal static class PathBounds
{
	public static ShapeBounds Measure(string data)
	{
		var reader = new Reader(data);
		var bounds = ShapeBounds.None;
		double x = 0, y = 0, startX = 0, startY = 0;
		var command = '\0';
		while (reader.Next(ref command))
		{
			var before = bounds;
			var relative = char.IsLower(command);
			switch (char.ToUpperInvariant(command))
			{
				case 'M':
					x = reader.Number() + (relative ? x : 0);
					y = reader.Number() + (relative ? y : 0);
					(startX, startY) = (x, y);
					bounds = bounds.Add(x, y);
					command = relative ? 'l' : 'L';
					break;
				case 'L':
					x = reader.Number() + (relative ? x : 0);
					y = reader.Number() + (relative ? y : 0);
					bounds = bounds.Add(x, y);
					break;
				case 'H':
					x = reader.Number() + (relative ? x : 0);
					bounds = bounds.Add(x, y);
					break;
				case 'V':
					y = reader.Number() + (relative ? y : 0);
					bounds = bounds.Add(x, y);
					break;
				case 'A':
					var rx = reader.Number();
					var ry = reader.Number();
					var rotation = reader.Number();
					var large = reader.Number() != 0;
					var sweep = reader.Number() != 0;
					var endX = reader.Number() + (relative ? x : 0);
					var endY = reader.Number() + (relative ? y : 0);
					bounds = Arc(bounds, x, y, rx, ry, rotation, large, sweep, endX, endY);
					(x, y) = (endX, endY);
					break;
				case 'C':
					var c1x = reader.Number() + (relative ? x : 0);
					var c1y = reader.Number() + (relative ? y : 0);
					var c2x = reader.Number() + (relative ? x : 0);
					var c2y = reader.Number() + (relative ? y : 0);
					var toX = reader.Number() + (relative ? x : 0);
					var toY = reader.Number() + (relative ? y : 0);
					bounds = Cubic(bounds, x, y, c1x, c1y, c2x, c2y, toX, toY);
					(x, y) = (toX, toY);
					break;
				case 'Q':
					var qx = reader.Number() + (relative ? x : 0);
					var qy = reader.Number() + (relative ? y : 0);
					var qToX = reader.Number() + (relative ? x : 0);
					var qToY = reader.Number() + (relative ? y : 0);
					// A quadratic is the cubic whose control points are two thirds of the way to its own.
					bounds = Cubic(bounds, x, y, x + (2.0 / 3 * (qx - x)), y + (2.0 / 3 * (qy - y)), qToX + (2.0 / 3 * (qx - qToX)), qToY + (2.0 / 3 * (qy - qToY)), qToX, qToY);
					(x, y) = (qToX, qToY);
					break;
				case 'Z':
					(x, y) = (startX, startY);
					break;
				default:
					throw new FormatException("Unsupported path command '" + command + "' in " + data);
			}

			if (reader.Failed)
			{
				return before;
			}
		}

		return bounds;
	}

	private static ShapeBounds Arc(ShapeBounds bounds, double x1, double y1, double rx, double ry, double rotation, bool large, bool sweep, double x2, double y2)
	{
		bounds = bounds.Add(x1, y1).Add(x2, y2);
		rx = Math.Abs(rx);
		ry = Math.Abs(ry);
		if (rx == 0 || ry == 0 || (x1 == x2 && y1 == y2))
		{
			return bounds;
		}

		var phi = rotation * Math.PI / 180;
		var cos = Math.Cos(phi);
		var sin = Math.Sin(phi);
		var dx = (x1 - x2) / 2;
		var dy = (y1 - y2) / 2;
		var x1p = cos * dx + sin * dy;
		var y1p = -sin * dx + cos * dy;
		var lambda = x1p * x1p / (rx * rx) + y1p * y1p / (ry * ry);
		if (lambda > 1)
		{
			rx *= Math.Sqrt(lambda);
			ry *= Math.Sqrt(lambda);
		}

		var numerator = rx * rx * ry * ry - rx * rx * y1p * y1p - ry * ry * x1p * x1p;
		var denominator = rx * rx * y1p * y1p + ry * ry * x1p * x1p;
		var coefficient = Math.Sqrt(Math.Max(0, numerator / denominator)) * (large == sweep ? -1 : 1);
		var cxp = coefficient * rx * y1p / ry;
		var cyp = -coefficient * ry * x1p / rx;
		var cx = cos * cxp - sin * cyp + (x1 + x2) / 2;
		var cy = sin * cxp + cos * cyp + (y1 + y2) / 2;
		var start = Math.Atan2((y1p - cyp) / ry, (x1p - cxp) / rx);
		var end = Math.Atan2((-y1p - cyp) / ry, (-x1p - cxp) / rx);
		var delta = end - start;
		if (sweep && delta < 0)
		{
			delta += 2 * Math.PI;
		}
		else if (!sweep && delta > 0)
		{
			delta -= 2 * Math.PI;
		}

		// The angles where dx/dt and dy/dt of the rotated ellipse are 0, each with its opposite.
		Span<double> extremes =
		[
			Math.Atan2(-ry * sin, rx * cos),
			Math.Atan2(ry * cos, rx * sin),
		];
		foreach (var extreme in extremes)
		{
			for (var half = 0; half < 2; half++)
			{
				var angle = extreme + half * Math.PI;
				var along = delta >= 0 ? Wrap(angle - start) : Wrap(start - angle);
				if (along <= Math.Abs(delta))
				{
					var px = cx + rx * cos * Math.Cos(angle) - ry * sin * Math.Sin(angle);
					var py = cy + rx * sin * Math.Cos(angle) + ry * cos * Math.Sin(angle);
					bounds = bounds.Add(px, py);
				}
			}
		}

		return bounds;
	}

	/// <summary>A cubic Bézier's end points and the points where its x or its y turns.</summary>
	private static ShapeBounds Cubic(ShapeBounds bounds, double x0, double y0, double x1, double y1, double x2, double y2, double x3, double y3)
	{
		bounds = bounds.Add(x0, y0).Add(x3, y3);
		Span<double> turns = stackalloc double[4];
		var count = Turns(x0, x1, x2, x3, turns);
		count += Turns(y0, y1, y2, y3, turns[count..]);
		foreach (var t in turns[..count])
		{
			var u = 1 - t;
			var px = (u * u * u * x0) + (3 * u * u * t * x1) + (3 * u * t * t * x2) + (t * t * t * x3);
			var py = (u * u * u * y0) + (3 * u * u * t * y1) + (3 * u * t * t * y2) + (t * t * t * y3);
			bounds = bounds.Add(px, py);
		}

		return bounds;
	}

	/// <summary>The parameters in (0, 1) where the derivative of one coordinate of a cubic Bézier is 0.</summary>
	private static int Turns(double p0, double p1, double p2, double p3, Span<double> turns)
	{
		// B'(t) / 3 = a t² + b t + c
		var a = -p0 + (3 * p1) - (3 * p2) + p3;
		var b = 2 * (p0 - (2 * p1) + p2);
		var c = p1 - p0;
		var count = 0;
		if (Math.Abs(a) < 1e-12)
		{
			if (Math.Abs(b) > 1e-12)
			{
				count = Keep(-c / b, turns, count);
			}

			return count;
		}

		var discriminant = (b * b) - (4 * a * c);
		if (discriminant < 0)
		{
			return 0;
		}

		var root = Math.Sqrt(discriminant);
		count = Keep((-b + root) / (2 * a), turns, count);
		return Keep((-b - root) / (2 * a), turns, count);
	}

	private static int Keep(double t, Span<double> turns, int count)
	{
		if (t > 0 && t < 1)
		{
			turns[count++] = t;
		}

		return count;
	}

	private static double Wrap(double angle)
	{
		var wrapped = angle % (2 * Math.PI);
		return wrapped < 0 ? wrapped + 2 * Math.PI : wrapped;
	}

	private ref struct Reader(ReadOnlySpan<char> text)
	{
		private readonly ReadOnlySpan<char> _text = text;
		private int _at;

		/// <summary>Whether the reader has met a number that is not finite, which ends the path.</summary>
		public bool Failed { get; private set; }

		/// <summary>Moves to the next command letter, or keeps the command when a number follows, as SVG repeats it.</summary>
		public bool Next(ref char command)
		{
			Skip();
			if (_at >= _text.Length || Failed)
			{
				return false;
			}

			if (NotFinite())
			{
				Failed = true;
				return false;
			}

			if (char.IsAsciiLetter(_text[_at]) && _text[_at] is not ('e' or 'E'))
			{
				command = _text[_at++];
			}
			else if (command == '\0')
			{
				throw new FormatException("Path data starts without a command");
			}

			return true;
		}

		public double Number()
		{
			Skip();
			if (Failed || NotFinite())
			{
				Failed = true;
				return double.NaN;
			}

			var start = _at;
			if (_at < _text.Length && _text[_at] is '-' or '+')
			{
				_at++;
			}

			while (_at < _text.Length && (char.IsAsciiDigit(_text[_at]) || _text[_at] == '.'))
			{
				_at++;
			}

			if (_at < _text.Length && _text[_at] is 'e' or 'E')
			{
				_at++;
				if (_at < _text.Length && _text[_at] is '-' or '+')
				{
					_at++;
				}

				while (_at < _text.Length && char.IsAsciiDigit(_text[_at]))
				{
					_at++;
				}
			}

			return double.Parse(_text[start.._at], NumberStyles.Float, CultureInfo.InvariantCulture);
		}

		private readonly bool NotFinite()
		{
			var rest = _text[_at..];
			if (rest.Length > 0 && rest[0] is '-' or '+')
			{
				rest = rest[1..];
			}

			return rest.StartsWith("NaN") || rest.StartsWith("Infinity");
		}

		private void Skip()
		{
			while (_at < _text.Length && (char.IsWhiteSpace(_text[_at]) || _text[_at] == ','))
			{
				_at++;
			}
		}
	}
}
