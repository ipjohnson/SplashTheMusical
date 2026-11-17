using System.Globalization;

namespace SplashTheMusical.Rendering.Shapes;

/// <summary>
/// The tight box of the path data the shapes write: the commands M, L, H, V, A and Z in both cases. An arc counts
/// its end points and the extreme points of its ellipse that it passes, as the SVG spec's centre parameterization
/// gives them.
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
				case 'Z':
					(x, y) = (startX, startY);
					break;
				default:
					throw new FormatException("Unsupported path command '" + command + "' in " + data);
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

	private static double Wrap(double angle)
	{
		var wrapped = angle % (2 * Math.PI);
		return wrapped < 0 ? wrapped + 2 * Math.PI : wrapped;
	}

	private ref struct Reader(ReadOnlySpan<char> text)
	{
		private readonly ReadOnlySpan<char> _text = text;
		private int _at;

		/// <summary>Moves to the next command letter, or keeps the command when a number follows, as SVG repeats it.</summary>
		public bool Next(ref char command)
		{
			Skip();
			if (_at >= _text.Length)
			{
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

		private void Skip()
		{
			while (_at < _text.Length && (char.IsWhiteSpace(_text[_at]) || _text[_at] == ','))
			{
				_at++;
			}
		}
	}
}
