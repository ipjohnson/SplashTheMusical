using System.Text;
using SplashTheMusical.Rendering;

namespace SplashTheMusical.Diagrams.Requirement;

/// <summary>
/// The requirement diagram of <c>packages/mermaid/src/diagrams/requirement</c>: the detector
/// <c>/^\s*requirement(Diagram)?/</c> of <c>requirementDetector.ts</c>, the parser of <see cref="RequirementParser"/>
/// and the writer of <see cref="RequirementWriter"/>. <see cref="RequirementLayoutStep"/> lays it out with mermaid.js's
/// dagre renderer, the one flowchart uses, and <see cref="Flowchart.FlowchartRenderer"/> draws it.
/// </summary>
public sealed class RequirementModule : DiagramModule<RequirementDocument, RequirementLayout>
{
	public override string DiagramType => "requirement";

	public override bool Detect(ReadOnlySpan<char> text) => text.StartsWith("requirement", StringComparison.Ordinal);

	public override RequirementDocument? Parse(ReadOnlySpan<char> source, DiagramParseContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return RequirementParser.Parse(source, context);
	}

	public override void Write(RequirementDocument document, StringBuilder output) => RequirementWriter.Write(document, output);

	/// <summary>
	/// <c>requirementRenderer.ts</c>'s <c>draw</c> with the dagre layout, whatever the <c>layout</c> config names.
	/// </summary>
	public override RequirementLayout Layout(RequirementDocument document, DiagramRenderContext context)
	{
		ArgumentNullException.ThrowIfNull(document);
		ArgumentNullException.ThrowIfNull(context);
		return RequirementLayoutStep.Run(document, context);
	}

	public override void Render(RequirementDocument document, RequirementLayout layout, SvgWriter svg, DiagramRenderContext context)
	{
		ArgumentNullException.ThrowIfNull(layout);
		ArgumentNullException.ThrowIfNull(svg);
		ArgumentNullException.ThrowIfNull(context);
		Flowchart.FlowchartRenderer.Render(layout.Root, layout.Title, layout.Style, svg, context.SvgId);
	}
}
