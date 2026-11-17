namespace SplashTheMusical.Rendering.Shapes;

/// <summary>mermaid.js's <c>look</c> config value, which changes the paddings of most shapes and how they are drawn.</summary>
internal enum ShapeLook
{
	/// <summary><c>classic</c>: plain SVG elements, or roughjs with roughness 0.</summary>
	Classic,

	/// <summary><c>neo</c>: the classic drawing with each shape's own paddings. The flowchart default in mermaid.js 12.3.0.</summary>
	Neo,

	/// <summary><c>handDrawn</c>: roughjs with roughness 0.7, seeded from <c>handDrawnSeed</c>.</summary>
	HandDrawn,
}
