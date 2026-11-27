using System.Text;
using SplashTheMusical.Rendering;

namespace SplashTheMusical.Diagrams.Block;

/// <summary>
/// The block diagram of <c>packages/mermaid/src/diagrams/block</c>: the detector <c>/^\s*block(-beta)?/</c> of
/// <c>blockDetector.ts</c>, the parser of <see cref="BlockParser"/> and the writer of <see cref="BlockWriter"/>. The
/// layout and the renderer draw nothing yet.
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
	/// Fails where <c>layout.ts</c>'s <c>calculateBlockPosition</c> fails: on a block with children whose last
	/// <c>columns</c> statement is <c>columns 0</c>.
	/// </summary>
	public override BlockLayout Layout(BlockDocument document, DiagramRenderContext context)
	{
		ArgumentNullException.ThrowIfNull(document);
		CheckColumns(BlockDb.From(document).Root);
		return new BlockLayout();
	}

	public override void Render(BlockDocument document, BlockLayout layout, SvgWriter svg, DiagramRenderContext context) { }

	private static void CheckColumns(BlockDbBlock block)
	{
		if (block.Children is not { Count: > 0 } children)
		{
			return;
		}

		if (block.Columns == 0)
		{
			throw new MermaidParseException(new MermaidDiagnostic(DiagnosticSeverity.Error, "Columns must be an integer !== 0.", 0, 0));
		}

		foreach (var child in children)
		{
			CheckColumns(child);
		}
	}
}

/// <summary>The layout of a block diagram. It has no parts yet.</summary>
public sealed class BlockLayout() : DiagramLayout(0, 0);
