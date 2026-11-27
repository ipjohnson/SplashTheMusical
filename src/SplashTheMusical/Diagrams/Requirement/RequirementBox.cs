// Upstream: packages/mermaid/src/rendering-util/rendering-elements/shapes/requirementBox.ts
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Rendering.Shapes;
using SplashTheMusical.Text;

namespace SplashTheMusical.Diagrams.Requirement;

/// <summary>The texts <c>requirementBox</c> draws for a requirement or an element.</summary>
/// <param name="Type">The first line, such as <c>&amp;lt;&amp;lt;Requirement&amp;gt;&amp;gt;</c>, as the shape writes it.</param>
/// <param name="Name">The name, drawn in bold.</param>
/// <param name="Body">
/// The lines under the divider, each empty when the requirement or element lacks it: a requirement's id, text, risk and
/// verify method, or an element's type and document reference.
/// </param>
/// <param name="ConfigFont">The root <c>fontFamily</c> and <c>fontSize</c>, which <c>calculateTextWidth</c> measures a text in.</param>
internal sealed record RequirementBoxContent(string Type, string Name, IReadOnlyList<string> Body, FontSpec ConfigFont);

/// <summary>
/// <c>requirementBox</c>: the type and the name centred over the body lines, each through <c>addText</c>, in a rectangle
/// 20 wider and 20 taller than the box of the texts, with a divider under the name when the body has a line that counts
/// towards its height.
/// </summary>
/// <remarks>
/// <c>addText</c> gives <c>createText</c> a width 50 more than <c>calculateTextWidth</c> of the text as written, entity
/// codes and all, so a line does not wrap. The divider mermaid.js draws in the neo look as a polygon 0.001 thick is a line
/// here.
/// </remarks>
internal static class RequirementBox
{
	private const double Padding = 20;
	private const double Gap = 20;

	/// <summary>The height an SVG label's box gets on top of its text's, so it is as tall as an HTML label.</summary>
	private const double SvgExtraHeight = 6;

	public static void Draw(FlowchartMeasure measure, RequirementBoxContent content, FlowNode node)
	{
		var html = measure.NodeHtmlLabels;

		// styles2String puts a font-weight of the node's styles in the label style every text is drawn with.
		var boldStyle = IsBold(FlowchartStyles.StyleValue(node.CssCompiledStyles, node.CssStyles, "font-weight"));
		var texts = new List<(FlowchartLabelBox Box, double X, double Y)>(2 + content.Body.Count);
		var bounds = ShapeBounds.None;

		// addText: the label at translate(-width / 2, -height / 2 + yOffset), and its height, or 0 for an empty text.
		double Add(string text, double yOffset, bool bold)
		{
			if (text.Length == 0)
			{
				return 0;
			}

			var box = Text(measure, content.ConfigFont, text, bold || boldStyle, html);
			var height = html ? box.Height : box.Height + SvgExtraHeight;
			var x = -box.Width / 2;
			var y = (-height / 2) + yOffset;
			texts.Add((box, x, y));
			var top = y + (html ? 0 : box.Y);
			bounds = bounds.Union(new ShapeBounds(x, top, x + box.Width, top + box.Height));
			return height;
		}

		var typeHeight = Add(content.Type, 0, false);
		var accumulated = typeHeight;
		var nameHeight = Add(content.Name, accumulated, true);
		accumulated += nameHeight + Gap;
		for (var i = 0; i < content.Body.Count; i++)
		{
			var height = Add(content.Body[i], accumulated, false);

			// The last line of a requirement's and of an element's body does not count towards the height.
			if (i < content.Body.Count - 1)
			{
				accumulated += height;
			}
		}

		var totalWidth = (bounds.IsEmpty ? 0 : bounds.Width) + Padding;
		var totalHeight = (bounds.IsEmpty ? 0 : bounds.Height) + Padding;
		var x0 = -totalWidth / 2;
		var y0 = -totalHeight / 2;
		var labels = new List<(FlowchartLabelBox Box, double X, double Y)>(texts.Count);
		for (var i = 0; i < texts.Count; i++)
		{
			var (box, x, y) = texts[i];

			// The body lines all start at the same x, which an HTML label's div shows whatever its text-align.
			box.AlignStart = i >= 2;
			labels.Add((box, i < 2 ? x : x0 + (Padding / 2), y - (totalHeight / 2) + Padding));
		}

		// roughjs gives its paths userNodeOverrides' stroke width, which the neo look's stylesheet overrides with its own.
		var strokeWidth = measure.Look == "classic" ? StrokeWidth(node) : null;
		var elements = new List<ShapeElement>
		{
			new() { Kind = ShapeElementKind.Rect, Class = "basic label-container outer-path", Rough = true, X = x0, Y = y0, Width = totalWidth, Height = totalHeight, Attributes = strokeWidth },
		};
		if (accumulated > typeHeight + nameHeight + Gap)
		{
			var lineY = y0 + typeHeight + nameHeight + Gap;
			var line = new ShapeElement { Kind = ShapeElementKind.Line, Rough = true, X = x0, Y = lineY, Width = x0 + totalWidth, Height = lineY, Attributes = strokeWidth };
			elements.Add(new ShapeElement { Kind = ShapeElementKind.Group, Class = "divider", Children = [line] });
		}

		node.Geometry = new ShapeGeometry
		{
			Width = totalWidth,
			Height = totalHeight,
			Bounds = new ShapeBounds(x0, y0, x0 + totalWidth, y0 + totalHeight),
			Elements = elements,
			Intersect = ShapeIntersect.Rect,
		};
		node.Labels = labels;
		node.Width = totalWidth;
		node.Height = totalHeight;
	}

	/// <summary><c>userNodeOverrides</c>' <c>strokeWidth</c>: the node's <c>stroke-width</c> without <c>px</c>, or 1.3.</summary>
	private static List<KeyValuePair<string, string>> StrokeWidth(FlowNode node)
	{
		var width = FlowchartStyles.StyleValue(node.CssCompiledStyles, node.CssStyles, "stroke-width")?.Replace("px", "", StringComparison.Ordinal).Trim();
		return [new("stroke-width", width is { Length: > 0 } && double.TryParse(width, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _) ? width : "1.3")];
	}

	/// <summary>Whether a <c>font-weight</c> value draws a bold face: <c>bold</c>, <c>bolder</c> or a weight of 600 or more.</summary>
	private static bool IsBold(string? weight)
	{
		if (weight is null)
		{
			return false;
		}

		weight = weight.Replace("!important", "", StringComparison.Ordinal).Trim();
		return weight is "bold" or "bolder"
			|| (int.TryParse(weight, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number) && number >= 600);
	}

	/// <summary>
	/// <c>addText</c>'s <c>createText</c>: Markdown, at <c>calculateTextWidth</c> of the text and 50, in bold for the name.
	/// </summary>
	private static FlowchartLabelBox Text(FlowchartMeasure measure, FontSpec configFont, string text, bool bold, bool html)
	{
		var width = MermaidText.CalculateTextWidth(text, configFont, measure.Font.Measurer) + 50;
		var lines = html
			? FlowchartLabelText.Lines(text, FlowchartLabelType.Markdown, true, measure.MarkdownAutoWrap)
			: FlowchartLabels.SvgLines(text, FlowchartLabelType.Markdown);
		if (bold)
		{
			for (var i = 0; i < lines.Count; i++)
			{
				lines[i] = [.. lines[i].Select(run => run with { Bold = true })];
			}
		}

		return html
			? FlowchartLabelText.Measure(lines, measure.Font, width, 0, true)
			: FlowchartLabelText.MeasureSvg(lines, measure.Font, width, 0, false);
	}
}
