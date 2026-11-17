using System.Text;
using SplashTheMusical.Rendering;

namespace SplashTheMusical.Diagrams.Flowchart;

/// <summary>
/// The flowchart of <c>packages/mermaid/src/diagrams/flowchart</c>, under the detector id <c>flowchart-v2</c> for
/// <c>graph</c> and <c>flowchart</c>, or <c>flowchart-elk</c> for <c>flowchart-elk</c>. The parser is
/// <see cref="FlowchartParser"/>. The writer, the layout and the renderer are not ported yet: the layout is empty and
/// the renderer draws nothing.
/// </summary>
public sealed class FlowchartModule : DiagramModule<FlowchartDocument, FlowchartLayout>
{
	public const string FlowchartV2 = "flowchart-v2";
	public const string FlowchartElk = "flowchart-elk";

	/// <param name="diagramType"><see cref="FlowchartV2"/> or <see cref="FlowchartElk"/>.</param>
	public FlowchartModule(string diagramType = FlowchartV2)
	{
		if (diagramType is not FlowchartV2 and not FlowchartElk)
		{
			throw new ArgumentException($"A flowchart module's type is {FlowchartV2} or {FlowchartElk}, not '{diagramType}'.", nameof(diagramType));
		}

		DiagramType = diagramType;
	}

	public override string DiagramType { get; }

	/// <summary>mermaid.js's detectors: <c>/^\s*flowchart-elk/</c>, and <c>/^\s*(flowchart|graph)/</c> for <c>flowchart-v2</c>.</summary>
	public override bool Detect(ReadOnlySpan<char> text) =>
		text.StartsWith(FlowchartElk, StringComparison.Ordinal)
			? DiagramType == FlowchartElk
			: DiagramType == FlowchartV2 && (text.StartsWith("flowchart", StringComparison.Ordinal) || text.StartsWith("graph", StringComparison.Ordinal));

	public override FlowchartDocument? Parse(ReadOnlySpan<char> source, DiagramParseContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return FlowchartParser.Parse(source, context);
	}

	public override void Write(FlowchartDocument document, StringBuilder output) =>
		throw new NotSupportedException("STM cannot write a flowchart yet.");

	public override FlowchartLayout Layout(FlowchartDocument document, DiagramRenderContext context) => new();

	public override void Render(FlowchartDocument document, FlowchartLayout layout, SvgWriter svg, DiagramRenderContext context)
	{
	}
}

/// <summary>The layout of a flowchart. It is empty until the layout is ported.</summary>
public sealed class FlowchartLayout() : DiagramLayout(0);
