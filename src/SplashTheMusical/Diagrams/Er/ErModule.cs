using System.Text;
using SplashTheMusical.Rendering;

namespace SplashTheMusical.Diagrams.Er;

/// <summary>
/// The entity relationship diagram of <c>packages/mermaid/src/diagrams/er</c>: the detector <c>/^\s*erDiagram/</c> of
/// <c>erDetector.ts</c>, the parser of <see cref="ErParser"/> and the writer of <see cref="ErWriter"/>. The layout and
/// the renderer draw nothing yet.
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

	public override ErLayout Layout(ErDocument document, DiagramRenderContext context) => new();

	public override void Render(ErDocument document, ErLayout layout, SvgWriter svg, DiagramRenderContext context) { }
}

/// <summary>The layout of an entity relationship diagram. It has no parts yet.</summary>
public sealed class ErLayout() : DiagramLayout(0, 0);
