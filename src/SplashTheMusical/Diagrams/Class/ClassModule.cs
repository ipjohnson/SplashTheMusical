using System.Text;
using SplashTheMusical.Rendering;

namespace SplashTheMusical.Diagrams.Class;

/// <summary>
/// The class diagram of <c>packages/mermaid/src/diagrams/class</c>: the detector <c>/^\s*classDiagram/</c> of
/// <c>classDetector-V2.ts</c>, which takes <c>classDiagram</c> and <c>classDiagram-v2</c>, the parser of
/// <see cref="ClassParser"/> and the writer of <see cref="ClassWriter"/>. <see cref="ClassLayoutStep"/> lays it out with
/// mermaid.js's dagre renderer, the one flowchart uses, and <see cref="Flowchart.FlowchartRenderer"/> draws it.
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

	/// <summary>
	/// <c>classRenderer-v3-unified.ts</c>'s <c>draw</c> with the dagre layout, whatever the <c>layout</c> config names:
	/// namespaces are compound nodes of the dagre port.
	/// </summary>
	public override ClassLayout Layout(ClassDocument document, DiagramRenderContext context)
	{
		ArgumentNullException.ThrowIfNull(document);
		ArgumentNullException.ThrowIfNull(context);
		return ClassLayoutStep.Run(document, context);
	}

	public override void Render(ClassDocument document, ClassLayout layout, SvgWriter svg, DiagramRenderContext context)
	{
		ArgumentNullException.ThrowIfNull(layout);
		ArgumentNullException.ThrowIfNull(svg);
		ArgumentNullException.ThrowIfNull(context);
		Flowchart.FlowchartRenderer.Render(layout.Root, layout.Title, layout.Style, svg, context.SvgId);
	}
}
