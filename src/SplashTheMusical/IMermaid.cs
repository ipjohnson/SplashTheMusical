namespace SplashTheMusical;

/// <summary>Parses, writes and draws Mermaid diagrams.</summary>
/// <remarks>An implementation is stateless and thread safe.</remarks>
public interface IMermaid : IMermaidParser, IMermaidRenderer, IMermaidWriter
{
	/// <summary>Parses <paramref name="source"/> and returns its SVG.</summary>
	/// <exception cref="MermaidParseException">The source does not parse.</exception>
	/// <exception cref="MermaidResourceLimitException">The source or the SVG exceeds one of the <see cref="ResourceLimits"/>.</exception>
	string RenderSvg(ReadOnlySpan<char> source, RenderOptions? options = null);
}
