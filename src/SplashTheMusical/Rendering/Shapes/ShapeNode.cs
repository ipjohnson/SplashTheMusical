namespace SplashTheMusical.Rendering.Shapes;

/// <summary>
/// What a shape reads from mermaid.js's <c>Node</c> and its render options. A null number is an undefined field.
/// <see cref="Width"/> and <see cref="Height"/> are the node's own size, which a flowchart node does not have.
/// </summary>
internal sealed class ShapeNode
{
	public required IShapeLabelMeasurer Label { get; init; }

	public ShapeLook Look { get; init; }

	/// <summary><c>node.padding</c>. A flowchart node has <c>flowchart.padding</c>, or 8 when it is 0.</summary>
	public double? Padding { get; init; }

	public double? Width { get; init; }

	public double? Height { get; init; }

	/// <summary><c>node.labelPaddingX</c>, which the plain rect and the data store use in place of their own.</summary>
	public double? LabelPaddingX { get; init; }

	/// <summary><c>node.labelPaddingY</c>, which the data store uses in place of its own.</summary>
	public double? LabelPaddingY { get; init; }

	/// <summary><c>node.rx</c> and <c>node.ry</c>, the corner radius of a rect without its own.</summary>
	public double? Radius { get; init; }

	/// <summary>The direction of the graph, such as <c>TB</c> or <c>LR</c>. The fork bar is upright in <c>LR</c> and <c>RL</c>.</summary>
	public string Direction { get; init; } = "TB";

	/// <summary><c>themeVariables.radius</c>, the rounded rect's corner radius.</summary>
	public double? ThemeRadius { get; init; }

	/// <summary>Whether labels are HTML: <c>node.useHtmlLabels</c> or the effective <c>htmlLabels</c>.</summary>
	public bool HtmlLabels { get; init; } = true;

	/// <summary><c>flowchart.htmlLabels</c> as the triangle reads it, which is false when unset.</summary>
	public bool FlowchartHtmlLabels { get; init; }

	internal bool IsNeo => Look == ShapeLook.Neo;

	internal bool IsHandDrawn => Look == ShapeLook.HandDrawn;
}
