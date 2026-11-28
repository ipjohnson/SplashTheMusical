using System.Text;
using SplashTheMusical.Rendering;

namespace SplashTheMusical.Diagrams.C4;

/// <summary>
/// The C4 diagrams of <c>packages/mermaid/src/diagrams/c4</c>: the detector
/// <c>/^\s*C4Context|C4Container|C4Component|C4Dynamic|C4Deployment/</c> of <c>c4Detector.ts</c>, the parser of
/// <see cref="C4Parser"/>, the writer of <see cref="C4Writer"/>, the layout of <see cref="C4LayoutStep"/> and the
/// renderer of <see cref="C4Renderer"/>.
/// </summary>
public sealed class C4Module : DiagramModule<C4Document, C4Layout>
{
	private static readonly string[] AnywhereKeywords = ["C4Container", "C4Component", "C4Dynamic", "C4Deployment"];

	public override string DiagramType => "c4";

	/// <summary>Only <c>C4Context</c> is anchored to the start, so the other four keywords match anywhere.</summary>
	public override bool Detect(ReadOnlySpan<char> text)
	{
		if (text.StartsWith("C4Context", StringComparison.Ordinal))
		{
			return true;
		}

		foreach (var keyword in AnywhereKeywords)
		{
			if (text.Contains(keyword, StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}

	public override C4Document? Parse(ReadOnlySpan<char> source, DiagramParseContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return C4Parser.Parse(source, context);
	}

	public override void Write(C4Document document, StringBuilder output) => C4Writer.Write(document, output);

	/// <summary>
	/// <c>c4Renderer.ts</c>'s <c>draw</c>, whatever the <c>layout</c> config names. It refuses a relation whose end is no
	/// shape or boundary, or one that was not drawn, which <c>drawRels</c> throws on.
	/// </summary>
	public override C4Layout Layout(C4Document document, DiagramRenderContext context) => C4LayoutStep.Run(document, context);

	public override void Render(C4Document document, C4Layout layout, SvgWriter svg, DiagramRenderContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		C4Renderer.Render(layout, svg, context.SvgId);
	}
}
