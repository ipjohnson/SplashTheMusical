using System.Text;

namespace SplashTheMusical.Layout.Paths;

/// <summary>
/// Writes the path of a curve through points. The d3 curves are ports of d3-shape 3.2.1's, run as its <c>line()</c>
/// runs them on points that are all defined, with numbers rounded to 3 decimals. Each port keeps d3's state and
/// arithmetic, and a line closes when it has one point because <c>line()</c> leaves the curve's <c>_line</c> at NaN.
/// </summary>
internal static class CurveWriter
{
	public static string ToString(Curve curve, ReadOnlySpan<PathPoint> points)
	{
		var text = new StringBuilder();
		Write(curve, points, text);
		return text.ToString();
	}

	/// <summary>Appends the path of <paramref name="curve"/> through <paramref name="points"/>, which is empty for no points.</summary>
	public static void Write(Curve curve, ReadOnlySpan<PathPoint> points, StringBuilder text)
	{
		if (curve == Curve.Rounded)
		{
			RoundedPath.Write(points, RoundedPath.Radius, text);
			return;
		}

		var path = new PathWriter(text);
		switch (curve)
		{
			case Curve.Basis:
				Run(new Basis(), points, ref path);
				break;
			case Curve.BumpX:
				Run(new Bump(horizontal: true), points, ref path);
				break;
			case Curve.BumpY:
				Run(new Bump(horizontal: false), points, ref path);
				break;
			case Curve.Cardinal:
				Run(new Cardinal(), points, ref path);
				break;
			case Curve.CatmullRom:
				Run(new CatmullRom(), points, ref path);
				break;
			case Curve.Linear:
				Run(new Linear(), points, ref path);
				break;
			case Curve.MonotoneX:
				Run(new Monotone(reflect: false), points, ref path);
				break;
			case Curve.MonotoneY:
				Run(new Monotone(reflect: true), points, ref path);
				break;
			case Curve.Natural:
				Natural(points, ref path);
				break;
			case Curve.Step:
				Run(new Step(0.5), points, ref path);
				break;
			case Curve.StepAfter:
				Run(new Step(1), points, ref path);
				break;
			case Curve.StepBefore:
				Run(new Step(0), points, ref path);
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(curve), curve, null);
		}
	}

	private interface ICurve
	{
		void LineStart();

		void Point(ref PathWriter path, double x, double y);

		void LineEnd(ref PathWriter path);
	}

	private static void Run<TCurve>(TCurve curve, ReadOnlySpan<PathPoint> points, ref PathWriter path)
		where TCurve : struct, ICurve
	{
		if (points.IsEmpty)
		{
			return;
		}

		curve.LineStart();
		foreach (var point in points)
		{
			curve.Point(ref path, point.X, point.Y);
		}

		curve.LineEnd(ref path);
	}

	// JavaScript's truthiness of a number.
	private static bool Truthy(double value) => value != 0 && !double.IsNaN(value);

	private struct Linear : ICurve
	{
		private int _point;

		public void LineStart() => _point = 0;

		public void Point(ref PathWriter path, double x, double y)
		{
			if (_point == 0)
			{
				_point = 1;
				path.MoveTo(x, y);
			}
			else
			{
				_point = 2;
				path.LineTo(x, y);
			}
		}

		public readonly void LineEnd(ref PathWriter path)
		{
			if (_point == 1)
			{
				path.ClosePath();
			}
		}
	}

	private struct Basis : ICurve
	{
		private double _x0, _x1, _y0, _y1;
		private int _point;

		public void LineStart()
		{
			_x0 = _x1 = _y0 = _y1 = double.NaN;
			_point = 0;
		}

		public readonly void LineEnd(ref PathWriter path)
		{
			switch (_point)
			{
				case 3:
					Segment(ref path, _x1, _y1);
					path.LineTo(_x1, _y1);
					break;
				case 2:
					path.LineTo(_x1, _y1);
					break;
			}

			if (_point == 1)
			{
				path.ClosePath();
			}
		}

		public void Point(ref PathWriter path, double x, double y)
		{
			switch (_point)
			{
				case 0:
					_point = 1;
					path.MoveTo(x, y);
					break;
				case 1:
					_point = 2;
					break;
				case 2:
					_point = 3;
					path.LineTo((5 * _x0 + _x1) / 6, (5 * _y0 + _y1) / 6);
					Segment(ref path, x, y);
					break;
				default:
					Segment(ref path, x, y);
					break;
			}

			_x0 = _x1;
			_x1 = x;
			_y0 = _y1;
			_y1 = y;
		}

		private readonly void Segment(ref PathWriter path, double x, double y) =>
			path.BezierCurveTo(
				(2 * _x0 + _x1) / 3,
				(2 * _y0 + _y1) / 3,
				(_x0 + 2 * _x1) / 3,
				(_y0 + 2 * _y1) / 3,
				(_x0 + 4 * _x1 + x) / 6,
				(_y0 + 4 * _y1 + y) / 6);
	}

	private struct Bump(bool horizontal) : ICurve
	{
		private double _x0, _y0;
		private int _point;

		public void LineStart() => _point = 0;

		public readonly void LineEnd(ref PathWriter path)
		{
			if (_point == 1)
			{
				path.ClosePath();
			}
		}

		public void Point(ref PathWriter path, double x, double y)
		{
			if (_point == 0)
			{
				_point = 1;
				path.MoveTo(x, y);
			}
			else
			{
				_point = 2;
				if (horizontal)
				{
					_x0 = (_x0 + x) / 2;
					path.BezierCurveTo(_x0, _y0, _x0, y, x, y);
				}
				else
				{
					_y0 = (_y0 + y) / 2;
					path.BezierCurveTo(_x0, _y0, x, _y0, x, y);
				}
			}

			_x0 = x;
			_y0 = y;
		}
	}

	// d3's curveCardinal at its default tension of 0.
	private struct Cardinal : ICurve
	{
		private const double K = (1 - 0.0) / 6;
		private double _x0, _x1, _x2, _y0, _y1, _y2;
		private int _point;

		public void LineStart()
		{
			_x0 = _x1 = _x2 = _y0 = _y1 = _y2 = double.NaN;
			_point = 0;
		}

		public readonly void LineEnd(ref PathWriter path)
		{
			switch (_point)
			{
				case 2:
					path.LineTo(_x2, _y2);
					break;
				case 3:
					Segment(ref path, _x1, _y1);
					break;
			}

			if (_point == 1)
			{
				path.ClosePath();
			}
		}

		public void Point(ref PathWriter path, double x, double y)
		{
			switch (_point)
			{
				case 0:
					_point = 1;
					path.MoveTo(x, y);
					break;
				case 1:
					_point = 2;
					_x1 = x;
					_y1 = y;
					break;
				case 2:
					_point = 3;
					Segment(ref path, x, y);
					break;
				default:
					Segment(ref path, x, y);
					break;
			}

			_x0 = _x1;
			_x1 = _x2;
			_x2 = x;
			_y0 = _y1;
			_y1 = _y2;
			_y2 = y;
		}

		private readonly void Segment(ref PathWriter path, double x, double y) =>
			path.BezierCurveTo(
				_x1 + K * (_x2 - _x0),
				_y1 + K * (_y2 - _y0),
				_x2 + K * (_x1 - x),
				_y2 + K * (_y1 - y),
				_x2,
				_y2);
	}

	// d3's curveCatmullRom at its default alpha of 0.5.
	private struct CatmullRom : ICurve
	{
		private const double Alpha = 0.5;
		private const double Epsilon = 1e-12;
		private double _x0, _x1, _x2, _y0, _y1, _y2;
		private double _l01A, _l12A, _l23A, _l01A2, _l12A2, _l23A2;
		private int _point;

		public void LineStart()
		{
			_x0 = _x1 = _x2 = _y0 = _y1 = _y2 = double.NaN;
			_l01A = _l12A = _l23A = _l01A2 = _l12A2 = _l23A2 = 0;
			_point = 0;
		}

		public void LineEnd(ref PathWriter path)
		{
			switch (_point)
			{
				case 2:
					path.LineTo(_x2, _y2);
					break;
				case 3:
					Point(ref path, _x2, _y2);
					break;
			}

			if (_point == 1)
			{
				path.ClosePath();
			}
		}

		public void Point(ref PathWriter path, double x, double y)
		{
			if (_point != 0)
			{
				var x23 = _x2 - x;
				var y23 = _y2 - y;
				_l23A2 = Math.Pow(x23 * x23 + y23 * y23, Alpha);
				_l23A = Math.Sqrt(_l23A2);
			}

			switch (_point)
			{
				case 0:
					_point = 1;
					path.MoveTo(x, y);
					break;
				case 1:
					_point = 2;
					break;
				case 2:
					_point = 3;
					Segment(ref path, x, y);
					break;
				default:
					Segment(ref path, x, y);
					break;
			}

			_l01A = _l12A;
			_l12A = _l23A;
			_l01A2 = _l12A2;
			_l12A2 = _l23A2;
			_x0 = _x1;
			_x1 = _x2;
			_x2 = x;
			_y0 = _y1;
			_y1 = _y2;
			_y2 = y;
		}

		private readonly void Segment(ref PathWriter path, double x, double y)
		{
			var x1 = _x1;
			var y1 = _y1;
			var x2 = _x2;
			var y2 = _y2;
			if (_l01A > Epsilon)
			{
				var a = 2 * _l01A2 + 3 * _l01A * _l12A + _l12A2;
				var n = 3 * _l01A * (_l01A + _l12A);
				x1 = (x1 * a - _x0 * _l12A2 + _x2 * _l01A2) / n;
				y1 = (y1 * a - _y0 * _l12A2 + _y2 * _l01A2) / n;
			}

			if (_l23A > Epsilon)
			{
				var b = 2 * _l23A2 + 3 * _l23A * _l12A + _l12A2;
				var m = 3 * _l23A * (_l23A + _l12A);
				x2 = (x2 * b + _x1 * _l23A2 - x * _l12A2) / m;
				y2 = (y2 * b + _y1 * _l23A2 - y * _l12A2) / m;
			}

			path.BezierCurveTo(x1, y1, x2, y2, _x2, _y2);
		}
	}

	// d3's curveMonotoneX, and curveMonotoneY when reflect is set, which swaps x and y on the way in and out.
	private struct Monotone(bool reflect) : ICurve
	{
		private double _x0, _x1, _y0, _y1, _t0;
		private int _point;

		public void LineStart()
		{
			_x0 = _x1 = _y0 = _y1 = _t0 = double.NaN;
			_point = 0;
		}

		public readonly void LineEnd(ref PathWriter path)
		{
			switch (_point)
			{
				case 2:
					LineTo(ref path, _x1, _y1);
					break;
				case 3:
					Hermite(ref path, _t0, Slope2(_t0));
					break;
			}

			if (_point == 1)
			{
				path.ClosePath();
			}
		}

		public void Point(ref PathWriter path, double x, double y)
		{
			if (reflect)
			{
				(x, y) = (y, x);
			}

			var t1 = double.NaN;
			if (x == _x1 && y == _y1)
			{
				return;
			}

			switch (_point)
			{
				case 0:
					_point = 1;
					if (reflect)
					{
						path.MoveTo(y, x);
					}
					else
					{
						path.MoveTo(x, y);
					}

					break;
				case 1:
					_point = 2;
					break;
				case 2:
					_point = 3;
					t1 = Slope3(x, y);
					Hermite(ref path, Slope2(t1), t1);
					break;
				default:
					t1 = Slope3(x, y);
					Hermite(ref path, _t0, t1);
					break;
			}

			_x0 = _x1;
			_x1 = x;
			_y0 = _y1;
			_y1 = y;
			_t0 = t1;
		}

		// The slope of the tangent at the middle point, after Steffen 1990.
		private readonly double Slope3(double x2, double y2)
		{
			var h0 = _x1 - _x0;
			var h1 = x2 - _x1;
			var s0 = (_y1 - _y0) / (Truthy(h0) ? h0 : h1 < 0 ? -0.0 : 0.0);
			var s1 = (y2 - _y1) / (Truthy(h1) ? h1 : h0 < 0 ? -0.0 : 0.0);
			var p = (s0 * h1 + s1 * h0) / (h0 + h1);
			var slope = (Sign(s0) + Sign(s1)) * Math.Min(Math.Min(Math.Abs(s0), Math.Abs(s1)), 0.5 * Math.Abs(p));
			return Truthy(slope) ? slope : 0;
		}

		private readonly double Slope2(double t)
		{
			var h = _x1 - _x0;
			return Truthy(h) ? (3 * (_y1 - _y0) / h - t) / 2 : t;
		}

		private static double Sign(double x) => x < 0 ? -1 : 1;

		private readonly void Hermite(ref PathWriter path, double t0, double t1)
		{
			var dx = (_x1 - _x0) / 3;
			if (reflect)
			{
				path.BezierCurveTo(_y0 + dx * t0, _x0 + dx, _y1 - dx * t1, _x1 - dx, _y1, _x1);
			}
			else
			{
				path.BezierCurveTo(_x0 + dx, _y0 + dx * t0, _x1 - dx, _y1 - dx * t1, _x1, _y1);
			}
		}

		private readonly void LineTo(ref PathWriter path, double x, double y)
		{
			if (reflect)
			{
				path.LineTo(y, x);
			}
			else
			{
				path.LineTo(x, y);
			}
		}
	}

	// d3's curveNatural, which needs all the points before it writes.
	private static void Natural(ReadOnlySpan<PathPoint> points, ref PathWriter path)
	{
		var n = points.Length;
		if (n == 0)
		{
			return;
		}

		path.MoveTo(points[0].X, points[0].Y);
		if (n == 2)
		{
			path.LineTo(points[1].X, points[1].Y);
		}
		else if (n > 2)
		{
			var x = new double[n];
			var y = new double[n];
			for (var i = 0; i < n; i++)
			{
				x[i] = points[i].X;
				y[i] = points[i].Y;
			}

			var (ax, bx) = ControlPoints(x);
			var (ay, by) = ControlPoints(y);
			for (int i0 = 0, i1 = 1; i1 < n; ++i0, ++i1)
			{
				path.BezierCurveTo(ax[i0], ay[i0], bx[i0], by[i0], x[i1], y[i1]);
			}
		}

		if (n == 1)
		{
			path.ClosePath();
		}
	}

	private static (double[] A, double[] B) ControlPoints(double[] x)
	{
		var n = x.Length - 1;
		var a = new double[n];
		var b = new double[n];
		var r = new double[n];
		a[0] = 0;
		b[0] = 2;
		r[0] = x[0] + 2 * x[1];
		for (var i = 1; i < n - 1; ++i)
		{
			a[i] = 1;
			b[i] = 4;
			r[i] = 4 * x[i] + 2 * x[i + 1];
		}

		a[n - 1] = 2;
		b[n - 1] = 7;
		r[n - 1] = 8 * x[n - 1] + x[n];
		for (var i = 1; i < n; ++i)
		{
			var m = a[i] / b[i - 1];
			b[i] -= m;
			r[i] -= m * r[i - 1];
		}

		a[n - 1] = r[n - 1] / b[n - 1];
		for (var i = n - 2; i >= 0; --i)
		{
			a[i] = (r[i] - a[i + 1]) / b[i];
		}

		b[n - 1] = (x[n] + a[n - 1]) / 2;
		for (var i = 0; i < n - 1; ++i)
		{
			b[i] = 2 * x[i + 1] - a[i + 1];
		}

		return (a, b);
	}

	// d3's curveStep, curveStepAfter and curveStepBefore, with t of 0.5, 1 and 0.
	private struct Step(double t) : ICurve
	{
		private double _x, _y;
		private int _point;

		public void LineStart()
		{
			_x = _y = double.NaN;
			_point = 0;
		}

		public readonly void LineEnd(ref PathWriter path)
		{
			if (0 < t && t < 1 && _point == 2)
			{
				path.LineTo(_x, _y);
			}

			if (_point == 1)
			{
				path.ClosePath();
			}
		}

		public void Point(ref PathWriter path, double x, double y)
		{
			if (_point == 0)
			{
				_point = 1;
				path.MoveTo(x, y);
			}
			else
			{
				_point = 2;
				if (t <= 0)
				{
					path.LineTo(_x, y);
					path.LineTo(x, y);
				}
				else
				{
					var x1 = _x * (1 - t) + x * t;
					path.LineTo(x1, _y);
					path.LineTo(x1, y);
				}
			}

			_x = x;
			_y = y;
		}
	}
}
