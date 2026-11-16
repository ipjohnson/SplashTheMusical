using System.Text;
using SplashTheMusical.Rendering;

namespace SplashTheMusical.Diagrams;

/// <summary>An <see cref="IDiagramModule"/> for one document class and one layout class.</summary>
public abstract class DiagramModule<TDocument, TLayout> : IDiagramModule
	where TDocument : MermaidDocument
	where TLayout : DiagramLayout
{
	public abstract string DiagramType { get; }

	public abstract bool Detect(ReadOnlySpan<char> text);

	/// <inheritdoc cref="IDiagramModule.Parse"/>
	public abstract TDocument? Parse(ReadOnlySpan<char> source, DiagramParseContext context);

	/// <inheritdoc cref="IDiagramModule.Write"/>
	public abstract void Write(TDocument document, StringBuilder output);

	/// <inheritdoc cref="IDiagramModule.Layout"/>
	public abstract TLayout Layout(TDocument document, DiagramRenderContext context);

	/// <inheritdoc cref="IDiagramModule.Render"/>
	public abstract void Render(TDocument document, TLayout layout, SvgWriter svg, DiagramRenderContext context);

	MermaidDocument? IDiagramModule.Parse(ReadOnlySpan<char> source, DiagramParseContext context) => Parse(source, context);

	void IDiagramModule.Write(MermaidDocument document, StringBuilder output) => Write(Cast(document), output);

	DiagramLayout IDiagramModule.Layout(MermaidDocument document, DiagramRenderContext context) => Layout(Cast(document), context);

	void IDiagramModule.Render(MermaidDocument document, DiagramLayout layout, SvgWriter svg, DiagramRenderContext context) =>
		Render(
			Cast(document),
			layout as TLayout ?? throw new ArgumentException($"The layout is not one the {DiagramType} module's layout step makes.", nameof(layout)),
			svg,
			context);

	private TDocument Cast(MermaidDocument document) =>
		document as TDocument ?? throw new ArgumentException($"A {document.DiagramType} document is not one the {DiagramType} module reads.", nameof(document));
}
