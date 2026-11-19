using System.Text;
using SplashTheMusical.Rendering;

namespace SplashTheMusical.Diagrams.Class;

/// <summary>
/// The class diagram of <c>packages/mermaid/src/diagrams/class</c>: the detector <c>/^\s*classDiagram/</c> of
/// <c>classDetector-V2.ts</c>, which takes <c>classDiagram</c> and <c>classDiagram-v2</c>, the parser of
/// <see cref="ClassParser"/> and the writer of <see cref="ClassWriter"/>. The layout and the renderer draw nothing
/// yet.
/// </summary>
public sealed class ClassModule : DiagramModule<ClassDocument, ClassLayout>
{
	public override string DiagramType => "classDiagram";

	public override bool Detect(ReadOnlySpan<char> text) => text.StartsWith("classDiagram", StringComparison.Ordinal);

	public override ClassDocument? Parse(ReadOnlySpan<char> source, DiagramParseContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return ClassParser.Parse(source, context);
	}

	public override void Write(ClassDocument document, StringBuilder output) => ClassWriter.Write(document, output);

	public override ClassLayout Layout(ClassDocument document, DiagramRenderContext context) => new();

	public override void Render(ClassDocument document, ClassLayout layout, SvgWriter svg, DiagramRenderContext context)
	{
	}
}

/// <summary>The layout of a class diagram, which is empty until the layout step is ported.</summary>
public sealed class ClassLayout() : DiagramLayout(0);
