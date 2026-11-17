namespace SplashTheMusical.Rendering.Shapes;

/// <summary>
/// The box <c>labelHelper</c> measures for a node's label. An HTML label's box is the client rect of its <c>div</c>.
/// An SVG label's box is the <c>getBBox</c> of its <c>text</c>, which is centred on x 0, so its <see cref="X"/> and
/// <see cref="Y"/> are not 0.
/// </summary>
internal readonly record struct ShapeLabel(double Width, double Height, bool Html, double X = 0, double Y = 0)
{
	/// <summary>The box of an empty label.</summary>
	public static ShapeLabel Empty(bool html) => new(0, 0, html);

	/// <summary>
	/// <c>bbox.x - (bbox.left ?? 0)</c>, which shapes subtract when they place the label. It is 0 for an HTML label,
	/// whose client rect has a <c>left</c>.
	/// </summary>
	public double OffsetX => Html ? 0 : X;

	/// <summary><c>bbox.y - (bbox.top ?? 0)</c>.</summary>
	public double OffsetY => Html ? 0 : Y;
}

/// <summary>Measures a node's label as <c>labelHelper</c> does, once the shape has set the node's width.</summary>
internal interface IShapeLabelMeasurer
{
	/// <summary>
	/// The label's box. <paramref name="nodeWidth"/> is <c>node.width</c> when the shape calls <c>labelHelper</c>: a
	/// width wraps the label at it and turns off the minimum label width. <paramref name="ignoreMinWidth"/> turns off
	/// the minimum width too, for the circles.
	/// </summary>
	ShapeLabel Measure(double? nodeWidth, bool ignoreMinWidth);
}
