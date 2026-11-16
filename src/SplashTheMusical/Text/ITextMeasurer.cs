namespace SplashTheMusical.Text;

/// <summary>
/// Measures text the way a browser lays it out on one line. A host that draws in a font the default measurer has no
/// table for supplies its own.
/// </summary>
/// <remarks>An implementation is stateless and thread safe.</remarks>
public interface ITextMeasurer
{
	/// <summary>Measures <paramref name="text"/> as one line, without line breaks, in <paramref name="font"/>.</summary>
	TextMetrics Measure(ReadOnlySpan<char> text, in FontSpec font);
}

/// <summary>The style of a font, as CSS <c>font-style</c>.</summary>
public enum FontStyle : byte
{
	Normal,
	Italic,
	Oblique,
}

/// <summary>A font as CSS describes it.</summary>
/// <param name="Family">A CSS <c>font-family</c> list, such as mermaid's <c>"trebuchet ms", verdana, arial, sans-serif</c>.</param>
/// <param name="Size">The font size in pixels.</param>
/// <param name="Weight">The CSS <c>font-weight</c>, from 1 to 1000. 400 is normal and 700 is bold.</param>
/// <param name="Style">The CSS <c>font-style</c>.</param>
public readonly record struct FontSpec(string Family, double Size, int Weight = 400, FontStyle Style = FontStyle.Normal);

/// <summary>The size of a line of text, in pixels.</summary>
/// <param name="Width">The sum of the advances, with kerning.</param>
/// <param name="Ascent">The distance from the baseline up to the top of the line box.</param>
/// <param name="Descent">The distance from the baseline down to the bottom of the line box, as a positive number.</param>
public readonly record struct TextMetrics(double Width, double Ascent, double Descent)
{
	/// <summary>The height of the line box.</summary>
	public double Height => Ascent + Descent;
}
