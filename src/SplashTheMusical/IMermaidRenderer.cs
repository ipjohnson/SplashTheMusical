using System.Buffers;

namespace SplashTheMusical;

/// <summary>Draws a document as SVG.</summary>
/// <remarks>An implementation is stateless and thread safe.</remarks>
public interface IMermaidRenderer
{
	/// <summary>The SVG of <paramref name="document"/>.</summary>
	/// <exception cref="NotSupportedException">No diagram module draws the document's <see cref="MermaidDocument.DiagramType"/>.</exception>
	/// <exception cref="MermaidResourceLimitException">The SVG exceeds <see cref="ResourceLimits.MaxOutputLength"/>.</exception>
	string RenderSvg(MermaidDocument document, RenderOptions? options = null);

	/// <summary>Writes the SVG of <paramref name="document"/> to <paramref name="utf8Destination"/> as UTF-8.</summary>
	/// <inheritdoc cref="RenderSvg(MermaidDocument, RenderOptions?)"/>
	void RenderSvg(MermaidDocument document, IBufferWriter<byte> utf8Destination, RenderOptions? options = null);
}
