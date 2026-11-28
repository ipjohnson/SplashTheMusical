using System.Text;
using SplashTheMusical.Rendering;

namespace SplashTheMusical.Diagrams.Sequence;

/// <summary>
/// The sequence diagram of <c>packages/mermaid/src/diagrams/sequence</c>: the detector <c>/^\s*sequenceDiagram/</c> of
/// <c>sequenceDetector.ts</c>, the parser of <see cref="SequenceParser"/> and the writer of
/// <see cref="SequenceWriter"/>, the layout of <see cref="SequenceLayoutStep"/> and the renderer of
/// <see cref="SequenceRenderer"/>.
/// </summary>
public sealed class SequenceModule : DiagramModule<SequenceDocument, SequenceLayout>
{
	public override string DiagramType => "sequence";

	public override bool Detect(ReadOnlySpan<char> text) => text.StartsWith("sequenceDiagram", StringComparison.Ordinal);

	public override SequenceDocument? Parse(ReadOnlySpan<char> source, DiagramParseContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return SequenceParser.Parse(source, context);
	}

	public override void Write(SequenceDocument document, StringBuilder output) => SequenceWriter.Write(document, output);

	public override SequenceLayout Layout(SequenceDocument document, DiagramRenderContext context)
	{
		ArgumentNullException.ThrowIfNull(document);
		ArgumentNullException.ThrowIfNull(context);
		return SequenceLayoutStep.Run(document, context);
	}

	public override void Render(SequenceDocument document, SequenceLayout layout, SvgWriter svg, DiagramRenderContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		SequenceRenderer.Render(layout, svg, context.SvgId);
	}
}
