namespace SplashTheMusical.Text;

/// <summary>The measurer STM uses when the host supplies none.</summary>
public static class TextMeasurer
{
	public static ITextMeasurer Default { get; } = new FontTableTextMeasurer();
}
