using System.Text;
using SplashTheMusical.Rendering;

namespace SplashTheMusical.Diagrams.Block;

/// <summary>
/// The block diagram of <c>packages/mermaid/src/diagrams/block</c>: the detector <c>/^\s*block(-beta)?/</c> of
/// <c>blockDetector.ts</c>, the parser of <see cref="BlockParser"/> and the writer of <see cref="BlockWriter"/>.
/// <see cref="BlockLayoutStep"/> places the blocks on mermaid.js's grid of columns, whatever the <c>layout</c> config
/// names, and <see cref="Flowchart.FlowchartRenderer"/> draws them with the shapes of the flowchart.
/// </summary>
public sealed class BlockModule : DiagramModule<BlockDocument, BlockLayout>
{
	public override string DiagramType => "block";

	public override bool Detect(ReadOnlySpan<char> text) => text.StartsWith("block", StringComparison.Ordinal);

	public override BlockDocument? Parse(ReadOnlySpan<char> source, DiagramParseContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return BlockParser.Parse(source, context);
	}

	public override void Write(BlockDocument document, StringBuilder output) => BlockWriter.Write(document, output);

	/// <summary>
	/// <c>blockRenderer.ts</c>'s <c>draw</c>. It fails where <c>layout.ts</c>'s <c>calculateBlockPosition</c> fails: on
	/// a block with children whose last <c>columns</c> statement is <c>columns 0</c>.
	/// </summary>
	public override BlockLayout Layout(BlockDocument document, DiagramRenderContext context)
	{
		ArgumentNullException.ThrowIfNull(document);
		ArgumentNullException.ThrowIfNull(context);
		return BlockLayoutStep.Run(document, context);
	}

	public override void Render(BlockDocument document, BlockLayout layout, SvgWriter svg, DiagramRenderContext context)
	{
		ArgumentNullException.ThrowIfNull(layout);
		ArgumentNullException.ThrowIfNull(svg);
		ArgumentNullException.ThrowIfNull(context);
		Flowchart.FlowchartRenderer.RenderBlock(layout.Root, layout.Style, svg, context.SvgId);
	}
}
