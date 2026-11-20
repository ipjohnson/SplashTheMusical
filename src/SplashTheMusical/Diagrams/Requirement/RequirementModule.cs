using System.Text;
using SplashTheMusical.Rendering;

namespace SplashTheMusical.Diagrams.Requirement;

/// <summary>
/// The requirement diagram of <c>packages/mermaid/src/diagrams/requirement</c>: the detector
/// <c>/^\s*requirement(Diagram)?/</c> of <c>requirementDetector.ts</c>, the parser of <see cref="RequirementParser"/>
/// and the writer of <see cref="RequirementWriter"/>. The layout and the renderer draw nothing yet.
/// </summary>
public sealed class RequirementModule : DiagramModule<RequirementDocument, RequirementLayout>
{
	public override string DiagramType => "requirement";

	public override bool Detect(ReadOnlySpan<char> text) => text.StartsWith("requirement", StringComparison.Ordinal);

	public override RequirementDocument? Parse(ReadOnlySpan<char> source, DiagramParseContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return RequirementParser.Parse(source, context);
	}

	public override void Write(RequirementDocument document, StringBuilder output) => RequirementWriter.Write(document, output);

	public override RequirementLayout Layout(RequirementDocument document, DiagramRenderContext context) => new();

	public override void Render(RequirementDocument document, RequirementLayout layout, SvgWriter svg, DiagramRenderContext context) { }
}

/// <summary>The layout of a requirement diagram. It has no parts yet.</summary>
public sealed class RequirementLayout() : DiagramLayout(0, 0);
