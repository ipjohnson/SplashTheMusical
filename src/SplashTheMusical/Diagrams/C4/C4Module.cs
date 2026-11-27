using System.Text;
using SplashTheMusical.Rendering;

namespace SplashTheMusical.Diagrams.C4;

/// <summary>
/// The C4 diagrams of <c>packages/mermaid/src/diagrams/c4</c>: the detector
/// <c>/^\s*C4Context|C4Container|C4Component|C4Dynamic|C4Deployment/</c> of <c>c4Detector.ts</c>, the parser of
/// <see cref="C4Parser"/> and the writer of <see cref="C4Writer"/>. The layout and the renderer draw nothing yet.
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
	/// Refuses a relation whose end is no shape or boundary, which <c>c4Renderer.ts</c>'s <c>drawRels</c> throws on.
	/// </summary>
	public override C4Layout Layout(C4Document document, DiagramRenderContext context)
	{
		ArgumentNullException.ThrowIfNull(document);
		if (C4Db.From(document).RelWithUnknownEnd() is { } rel)
		{
			var message = $"C4 rel \"{rel.StringOf("from")}\" -> \"{rel.StringOf("to")}\" references an unknown shape or boundary";
			throw new MermaidParseException(new MermaidDiagnostic(DiagnosticSeverity.Error, message, 0, 0));
		}

		return new();
	}

	public override void Render(C4Document document, C4Layout layout, SvgWriter svg, DiagramRenderContext context) { }
}

/// <summary>The layout of a C4 diagram. It has no parts yet.</summary>
public sealed class C4Layout() : DiagramLayout(0, 0);
