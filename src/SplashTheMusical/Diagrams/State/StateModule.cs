using System.Text;
using SplashTheMusical.Rendering;

namespace SplashTheMusical.Diagrams.State;

/// <summary>
/// The state diagram of <c>packages/mermaid/src/diagrams/state</c>: the detector <c>/^\s*stateDiagram/</c> of
/// <c>stateDetector-V2.ts</c>, which takes <c>stateDiagram</c> and <c>stateDiagram-v2</c>, the parser of
/// <see cref="StateParser"/> and the writer of <see cref="StateWriter"/>. <see cref="StateLayoutStep"/> lays it out with mermaid.js's
/// dagre renderer, the one flowchart uses, and <see cref="Flowchart.FlowchartRenderer"/> draws it.
/// </summary>
public sealed class StateModule : DiagramModule<StateDocument, StateLayout>
{
	public override string DiagramType => "stateDiagram";

	public override bool Detect(ReadOnlySpan<char> text) => text.StartsWith("stateDiagram", StringComparison.Ordinal);

	public override StateDocument? Parse(ReadOnlySpan<char> source, DiagramParseContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return StateParser.Parse(source, context);
	}

	public override void Write(StateDocument document, StringBuilder output) => StateWriter.Write(document, output);

	/// <summary>
	/// <c>stateRenderer-v3-unified.ts</c>'s <c>draw</c> with the dagre layout, whatever the <c>layout</c> config names:
	/// composite states and concurrent regions are compound nodes of the dagre port.
	/// </summary>
	public override StateLayout Layout(StateDocument document, DiagramRenderContext context)
	{
		ArgumentNullException.ThrowIfNull(document);
		ArgumentNullException.ThrowIfNull(context);
		return StateLayoutStep.Run(document, context);
	}

	public override void Render(StateDocument document, StateLayout layout, SvgWriter svg, DiagramRenderContext context)
	{
		ArgumentNullException.ThrowIfNull(layout);
		ArgumentNullException.ThrowIfNull(svg);
		ArgumentNullException.ThrowIfNull(context);
		Flowchart.FlowchartRenderer.Render(layout.Root, layout.Title, layout.Style, svg, context.SvgId);
	}
}
