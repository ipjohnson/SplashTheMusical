using System.Text;
using SplashTheMusical.Rendering;

namespace SplashTheMusical.Diagrams.State;

/// <summary>
/// The state diagram of <c>packages/mermaid/src/diagrams/state</c>: the detector <c>/^\s*stateDiagram/</c> of
/// <c>stateDetector-V2.ts</c>, which takes <c>stateDiagram</c> and <c>stateDiagram-v2</c>, the parser of
/// <see cref="StateParser"/> and the writer of <see cref="StateWriter"/>. The layout and the renderer draw nothing
/// yet.
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

	public override StateLayout Layout(StateDocument document, DiagramRenderContext context) => new();

	public override void Render(StateDocument document, StateLayout layout, SvgWriter svg, DiagramRenderContext context)
	{
	}
}

/// <summary>The layout of a state diagram, which is empty until the layout step is ported.</summary>
public sealed class StateLayout() : DiagramLayout(0);
