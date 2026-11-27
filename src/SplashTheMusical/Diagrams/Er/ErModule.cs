using System.Text;
using SplashTheMusical.Rendering;

namespace SplashTheMusical.Diagrams.Er;

/// <summary>
/// The entity relationship diagram of <c>packages/mermaid/src/diagrams/er</c>: the detector <c>/^\s*erDiagram/</c> of
/// <c>erDetector.ts</c>, the parser of <see cref="ErParser"/> and the writer of <see cref="ErWriter"/>. <see cref="ErLayoutStep"/>
/// lays it out with mermaid.js's dagre renderer, the one flowchart uses, and <see cref="Flowchart.FlowchartRenderer"/>
/// draws it.
/// </summary>
public sealed class ErModule : DiagramModule<ErDocument, ErLayout>
{
	public override string DiagramType => "er";

	public override bool Detect(ReadOnlySpan<char> text) => text.StartsWith("erDiagram", StringComparison.Ordinal);

	public override ErDocument? Parse(ReadOnlySpan<char> source, DiagramParseContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return ErParser.Parse(source, context);
	}

	public override void Write(ErDocument document, StringBuilder output) => ErWriter.Write(document, output);

	/// <summary>
	/// <c>erRenderer-unified.ts</c>'s <c>draw</c> with the dagre layout, whatever the <c>layout</c> config names:
	/// subgraphs are compound nodes of the dagre port.
	/// </summary>
	public override ErLayout Layout(ErDocument document, DiagramRenderContext context)
	{
		ArgumentNullException.ThrowIfNull(document);
		ArgumentNullException.ThrowIfNull(context);
		return ErLayoutStep.Run(document, context);
	}

	public override void Render(ErDocument document, ErLayout layout, SvgWriter svg, DiagramRenderContext context)
	{
		ArgumentNullException.ThrowIfNull(layout);
		ArgumentNullException.ThrowIfNull(svg);
		ArgumentNullException.ThrowIfNull(context);
		Flowchart.FlowchartRenderer.Render(layout.Root, layout.Title, layout.Style, svg, context.SvgId);
	}
}
