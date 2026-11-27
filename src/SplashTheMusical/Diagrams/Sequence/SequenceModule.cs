using System.Text;
using SplashTheMusical.Rendering;

namespace SplashTheMusical.Diagrams.Sequence;

/// <summary>
/// The sequence diagram of <c>packages/mermaid/src/diagrams/sequence</c>: the detector <c>/^\s*sequenceDiagram/</c> of
/// <c>sequenceDetector.ts</c>, the parser of <see cref="SequenceParser"/> and the writer of
/// <see cref="SequenceWriter"/>. The layout and the renderer draw nothing yet.
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

	public override SequenceLayout Layout(SequenceDocument document, DiagramRenderContext context) => new();

	public override void Render(SequenceDocument document, SequenceLayout layout, SvgWriter svg, DiagramRenderContext context) { }
}

/// <summary>The layout of a sequence diagram. It has no parts yet.</summary>
public sealed class SequenceLayout() : DiagramLayout(0, 0);
