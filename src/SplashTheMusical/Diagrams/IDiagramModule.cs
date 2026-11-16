using System.Text;
using SplashTheMusical.Rendering;

namespace SplashTheMusical.Diagrams;

/// <summary>
/// One diagram type: a detector, a parser, a writer, a layout step and a renderer. <see cref="Mermaid.CoreModules"/>
/// lists STM's. A host passes more to the <see cref="Mermaid"/> constructor, as mermaid.js's
/// <c>registerExternalDiagrams</c> adds diagrams, and a module of a type the core has replaces the core's.
/// <see cref="DiagramModule{TDocument, TLayout}"/> implements it with typed methods.
/// </summary>
/// <remarks>An implementation is stateless and thread safe.</remarks>
public interface IDiagramModule
{
	/// <summary>The id of mermaid.js's detector for the type, such as <c>info</c>. It is the documents' <see cref="MermaidDocument.DiagramType"/>.</summary>
	string DiagramType { get; }

	/// <summary>
	/// Whether <paramref name="text"/> is a diagram of this type. It is asked only when no detector of mermaid.js's
	/// core claims the source, as mermaid.js asks the detectors of external diagrams after its own.
	/// </summary>
	/// <param name="text">The source from its first keyword, after the front matter and the directives and comments before it.</param>
	bool Detect(ReadOnlySpan<char> text);

	/// <summary>
	/// Parses the diagram's body, which starts at <see cref="DiagramParseContext.BodyStart"/> of <paramref name="source"/>.
	/// Returns <c>null</c> when the source is not valid, with an error added to <paramref name="context"/>. The caller
	/// sets the front matter and directives of the document.
	/// </summary>
	/// <exception cref="MermaidResourceLimitException">The source exceeds one of the context's limits.</exception>
	MermaidDocument? Parse(ReadOnlySpan<char> source, DiagramParseContext context);

	/// <summary>Appends the diagram's body to <paramref name="output"/>, each line ending in LF. The caller writes the front matter and directives.</summary>
	void Write(MermaidDocument document, StringBuilder output);

	/// <summary>Computes the size of the drawing and the place of its parts.</summary>
	DiagramLayout Layout(MermaidDocument document, DiagramRenderContext context);

	/// <summary>
	/// Draws <paramref name="document"/> with <paramref name="layout"/>, the result of <see cref="Layout"/>, inside the
	/// root <c>svg</c> element, after its stylesheet.
	/// </summary>
	void Render(MermaidDocument document, DiagramLayout layout, SvgWriter svg, DiagramRenderContext context);
}
