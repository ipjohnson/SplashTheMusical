using SplashTheMusical.Rendering;

namespace SplashTheMusical.Diagrams;

/// <summary>
/// The result of a module's layout step. It sizes the root <c>svg</c> element, as mermaid.js's
/// <c>configureSvgSize</c> and <c>setupGraphViewbox</c> do. A module derives from it to carry the places of its parts.
/// </summary>
public class DiagramLayout
{
	/// <param name="width">The <c>width</c> of the root element.</param>
	/// <param name="height">The <c>height</c> of the root element, or <c>null</c> to leave it to the browser.</param>
	public DiagramLayout(double width, double? height = null)
	{
		Width = width;
		Height = height;
	}

	public double Width { get; }

	public double? Height { get; }

	/// <summary>The <c>viewBox</c> of the root element, or <c>null</c> for none.</summary>
	public SvgViewBox? ViewBox { get; init; }

	/// <summary>The rules of the diagram type's own <c>styles</c> function, which the stylesheet puts after the shared rules.</summary>
	internal virtual IReadOnlyList<CssRule> DiagramRules => [];

	/// <summary>The classes the diagram defines with <c>classDef</c>, as its database's <c>getClasses</c> returns them.</summary>
	internal virtual IReadOnlyList<StyleClass> Classes => [];

	/// <summary>
	/// Rules for single elements, which stand in for the <c>style</c> attributes mermaid.js writes. They go last in the
	/// stylesheet.
	/// </summary>
	internal virtual IReadOnlyList<CssRule> ElementRules => [];
}

public readonly record struct SvgViewBox(double X, double Y, double Width, double Height);
