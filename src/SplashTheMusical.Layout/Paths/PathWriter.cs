using System.Text;

namespace SplashTheMusical.Layout.Paths;

/// <summary>A point of an edge or a line.</summary>
internal readonly record struct PathPoint(double X, double Y);

/// <summary>
/// Writes SVG path commands as d3-path 3.1's <c>Path</c> does. A writer with <c>digits</c> rounds each number to that
/// many decimals with JavaScript's <c>Math.round</c>, as d3-shape's line does with its default of 3. A writer without
/// writes each number in full.
/// </summary>
internal struct PathWriter(StringBuilder text, int? digits = PathWriter.LineDigits)
{
	/// <summary>The digits d3-shape's <c>line()</c> gives its path.</summary>
	public const int LineDigits = 3;

	private readonly double _scale = digits is { } d ? Math.Pow(10, d) : double.NaN;
	private bool _hasSubpath;

	public readonly StringBuilder Text => text;

	public void MoveTo(double x, double y)
	{
		_hasSubpath = true;
		Command('M', x, y);
	}

	public void LineTo(double x, double y) => Command('L', x, y);

	public void BezierCurveTo(double x1, double y1, double x2, double y2, double x, double y)
	{
		Command('C', x1, y1);
		text.Append(',');
		Number(x2);
		text.Append(',');
		Number(y2);
		text.Append(',');
		Number(x);
		text.Append(',');
		Number(y);
	}

	public readonly void ClosePath()
	{
		if (_hasSubpath)
		{
			text.Append('Z');
		}
	}

	/// <summary>Writes <paramref name="value"/> with the writer's rounding.</summary>
	public readonly void Number(double value)
	{
		if (!double.IsNaN(_scale))
		{
			value = JsMath.Round(value * _scale) / _scale;
		}

		Span<char> buffer = stackalloc char[JsNumber.MaxLength];
		text.Append(buffer[..JsNumber.Format(value, buffer)]);
	}

	private readonly void Command(char command, double x, double y)
	{
		text.Append(command);
		Number(x);
		text.Append(',');
		Number(y);
	}
}
