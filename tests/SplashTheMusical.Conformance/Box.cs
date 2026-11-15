using System.Globalization;

namespace SplashTheMusical.Conformance;

/// <summary>A rectangle in the SVG's user units.</summary>
public readonly record struct Rect(double X, double Y, double Width, double Height)
{
	public double Right => X + Width;

	public double Bottom => Y + Height;

	public double CenterX => X + (Width / 2);

	public double CenterY => Y + (Height / 2);

	public Rect Translate(double dx, double dy) => this with { X = X + dx, Y = Y + dy };

	/// <summary>True when the two rectangles share more than <paramref name="margin"/> on both axes.</summary>
	public bool Overlaps(Rect other, double margin) =>
		X < other.Right - margin && Right > other.X + margin && Y < other.Bottom - margin && Bottom > other.Y + margin;

	/// <summary>True when <paramref name="other"/> lies inside this rectangle, give or take <paramref name="margin"/>.</summary>
	public bool Contains(Rect other, double margin) =>
		X <= other.X + margin && Y <= other.Y + margin && Right >= other.Right - margin && Bottom >= other.Bottom - margin;

	/// <summary>
	/// The area both share over the area either covers, after each side shorter than <paramref name="minimum"/> is
	/// widened to it about its center. A straight edge has no area, so it is compared as a thin strip.
	/// </summary>
	public double IntersectionOverUnion(Rect other, double minimum)
	{
		var a = Widen(this, minimum);
		var b = Widen(other, minimum);
		var width = Math.Max(0, Math.Min(a.Right, b.Right) - Math.Max(a.X, b.X));
		var height = Math.Max(0, Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Y, b.Y));
		var shared = width * height;
		return shared / ((a.Width * a.Height) + (b.Width * b.Height) - shared);
	}

	/// <summary>The rectangle of the canvas line of a boxes file: canvas, x, y, width and height.</summary>
	public static Rect? ParseCanvas(string? line)
	{
		if (line is null)
		{
			return null;
		}

		var fields = line.Split('\t');
		return fields.Length == 5 && fields[0] == "canvas" ? Parse(fields, 1) : null;
	}

	internal static Rect Parse(string[] fields, int start) =>
		new(Number(fields[start]), Number(fields[start + 1]), Number(fields[start + 2]), Number(fields[start + 3]));

	private static double Number(string text) => double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);

	private static Rect Widen(Rect r, double minimum)
	{
		var (x, width) = r.Width < minimum ? (r.CenterX - (minimum / 2), minimum) : (r.X, r.Width);
		var (y, height) = r.Height < minimum ? (r.CenterY - (minimum / 2), minimum) : (r.Y, r.Height);
		return new Rect(x, y, width, height);
	}
}

/// <summary>A box line of a boxes file: box, kind, name, x, y, width and height. tools/oracle/README.md describes it.</summary>
public sealed record Box(string Kind, string Name, Rect Rect, string Line)
{
	/// <summary>The kind and name, which pair a box with its counterpart.</summary>
	public string Identity => Kind + "\t" + Name;

	public static Box Parse(string line)
	{
		var fields = line.Split('\t');
		if (fields.Length != 7 || fields[0] != "box")
		{
			throw new FormatException($"{line} is not box, kind, name, x, y, width and height");
		}

		return new Box(fields[1], fields[2], Rect.Parse(fields, 3), line);
	}
}
