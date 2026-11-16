namespace SplashTheMusical.Icons;

/// <summary>
/// The icons a diagram can name, such as <c>logos:aws</c>, as mermaid's <c>registerIconPacks</c> provides them. STM
/// ships no icon packs, so a host that draws icons supplies a provider.
/// </summary>
/// <remarks>An implementation is stateless and thread safe.</remarks>
public interface IIconProvider
{
	/// <summary>The icon named <paramref name="name"/>, or <c>null</c> when there is none.</summary>
	/// <param name="name">The pack's prefix, a colon and the icon's name, as the diagram writes it.</param>
	MermaidIcon? GetIcon(string name);
}

/// <summary>An icon in the form of an Iconify icon set.</summary>
/// <param name="Body">The SVG content of the icon, without the <c>svg</c> element. The renderer sanitizes it.</param>
/// <param name="Width">The width of the icon's view box.</param>
/// <param name="Height">The height of the icon's view box.</param>
public sealed record MermaidIcon(string Body, double Width, double Height);

/// <summary>The provider STM uses when the host supplies none. It has no icons.</summary>
public sealed class NoIcons : IIconProvider
{
	private NoIcons()
	{
	}

	public static NoIcons Instance { get; } = new();

	public MermaidIcon? GetIcon(string name) => null;
}
