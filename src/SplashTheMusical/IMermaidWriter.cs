namespace SplashTheMusical;

/// <summary>Writes a document as Mermaid source, which parses back to the same document.</summary>
/// <remarks>An implementation is stateless and thread safe.</remarks>
public interface IMermaidWriter
{
	/// <summary>The <c>.mmd</c> text of <paramref name="document"/>: its front matter, its directives and its body.</summary>
	/// <exception cref="NotSupportedException">No diagram module writes the document's <see cref="MermaidDocument.DiagramType"/>.</exception>
	string Write(MermaidDocument document, WriteOptions? options = null);
}
