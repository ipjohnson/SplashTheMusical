// Upstream: packages/mermaid/src/rendering-util/rendering-elements/shapes/erBox.ts
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Rendering.Shapes;
using SplashTheMusical.Text;

namespace SplashTheMusical.Diagrams.Er;

/// <summary>The <c>er</c> config and theme values <c>erBox</c> reads.</summary>
/// <param name="DiagramPadding"><c>er.diagramPadding</c>, or 10.</param>
/// <param name="EntityPadding"><c>er.entityPadding</c>, or 6.</param>
/// <param name="MinEntityWidth"><c>er.minEntityWidth</c>.</param>
/// <param name="RowOdd">The theme's <c>rowOdd</c>, which fills the odd attribute rows.</param>
/// <param name="RowEven">The theme's <c>rowEven</c>, which fills the even attribute rows.</param>
/// <param name="NodeBorder">The theme's <c>nodeBorder</c>, which strokes the attribute rows.</param>
/// <param name="ScalePadding">
/// Whether the root <c>htmlLabels</c> is unset or false, which scales an entity's paddings by 1.25. The labels are
/// still HTML when it is unset.
/// </param>
/// <param name="ConfigFont">
/// The root <c>fontFamily</c> and <c>fontSize</c>, which <c>calculateTextWidth</c> measures a name in to decide whether
/// the entity takes <c>er.minEntityWidth</c>. The label itself is drawn in the theme's font.
/// </param>
internal sealed record ErBoxSettings(double DiagramPadding, double EntityPadding, double MinEntityWidth, string? RowOdd, string? RowEven, string? NodeBorder, bool ScalePadding, FontSpec ConfigFont);

/// <summary>
/// <c>erBox</c>: an entity without attributes is <c>drawRect</c>'s rectangle around its name, at least
/// <c>er.minEntityWidth</c> wide. An entity with attributes is its name over a row per attribute, with a column each
/// for the type, the name, the keys and the comment, and the keys and the comment columns only when one attribute has
/// them.
/// </summary>
/// <remarks>
/// Each text goes through <c>addText</c>, which gives <c>createText</c> a width 100 more than the text's, so it does not
/// wrap. The dividers mermaid.js draws as polygons 0.0001 thick are lines here.
/// </remarks>
internal static class ErBox
{
	public static void Draw(FlowchartMeasure measure, ErBoxSettings settings, FlowNode node)
	{
		var html = measure.NodeHtmlLabels;
		var padding = settings.DiagramPadding;
		var textPadding = settings.EntityPadding;
		var attributes = node.Attributes ?? [];
		if (attributes.Count == 0 && node.Label.Length > 0)
		{
			Plain(measure, settings, node, html, padding);
			return;
		}

		if (settings.ScalePadding)
		{
			padding *= 1.25;
			textPadding *= 1.25;
		}

		var name = Text(measure, node.Label, html);
		var nameHeight = name.Height + textPadding;
		var shapeWidth = name.Width;
		var cells = new List<(FlowchartLabelBox Type, FlowchartLabelBox Name, FlowchartLabelBox Keys, FlowchartLabelBox Comment)>(attributes.Count);
		var rows = new List<(double Y, double Height)>(attributes.Count);
		double maxType = 0, maxName = 0, maxKeys = 0, maxComment = 0, yOffset = 0;
		foreach (var attribute in attributes)
		{
			var type = Text(measure, attribute.Type, html);
			var attributeName = Text(measure, attribute.Name, html);
			var keys = Text(measure, string.Join(',', attribute.Keys), html);
			var comment = Text(measure, attribute.Comment ?? "", html);
			maxType = Math.Max(maxType, type.Width + padding);
			maxName = Math.Max(maxName, attributeName.Width + padding);
			maxKeys = Math.Max(maxKeys, keys.Width + padding);
			maxComment = Math.Max(maxComment, comment.Width + padding);
			shapeWidth = Math.Max(shapeWidth, Math.Max(Math.Max(type.Width, attributeName.Width), Math.Max(keys.Width, comment.Width)));
			var rowHeight = Math.Max(Math.Max(type.Height, attributeName.Height), Math.Max(keys.Height, comment.Height)) + textPadding;
			cells.Add((type, attributeName, keys, comment));
			rows.Add((yOffset, rowHeight));
			yOffset += rowHeight;
		}

		var sections = 4;
		var keysPresent = maxKeys > padding;
		if (!keysPresent)
		{
			maxKeys = 0;
			sections--;
		}

		var commentPresent = maxComment > padding;
		if (!commentPresent)
		{
			maxComment = 0;
			sections--;
		}

		var difference = name.Width + (padding * 2) - (maxType + maxName + maxKeys + maxComment);
		if (difference > 0)
		{
			maxType += difference / sections;
			maxName += difference / sections;
			if (maxKeys > 0)
			{
				maxKeys += difference / sections;
			}

			if (maxComment > 0)
			{
				maxComment += difference / sections;
			}
		}

		var maxWidth = maxType + maxName + maxKeys + maxComment;
		var w = Math.Max(shapeWidth + (padding * 2), maxWidth);
		var h = Math.Max(yOffset + nameHeight, 0);
		var x = -w / 2;
		var y = -h / 2;
		var labels = new List<(FlowchartLabelBox Box, double X, double Y)> { (name, -name.Width / 2, y + (textPadding / 2)) };
		var top = y + nameHeight + (textPadding / 2);
		for (var i = 0; i < cells.Count; i++)
		{
			var (type, attributeName, keys, comment) = cells[i];
			var rowY = rows[i].Y + top;
			var left = x + (padding / 2);
			labels.Add((type, left, rowY));
			labels.Add((attributeName, left + maxType, rowY));
			labels.Add((keys, left + maxType + maxName, rowY));
			labels.Add((comment, left + maxType + maxName + maxKeys, rowY));
		}

		var elements = new List<ShapeElement>
		{
			new() { Kind = ShapeElementKind.Rect, Class = "outer-path", Rough = true, X = x, Y = y, Width = w, Height = h },
		};
		for (var i = 0; i < rows.Count; i++)
		{
			var (rowY, rowHeight) = rows[i];
			var even = (i + 1) % 2 == 0 && rowY != 0;
			elements.Add(new ShapeElement
			{
				Kind = ShapeElementKind.Rect,
				Class = even ? "row-rect-even" : "row-rect-odd",
				Rough = true,
				X = x,
				Y = nameHeight + y + rowY,
				Width = w,
				Height = rowHeight,
				Attributes = Paints(even ? settings.RowEven : settings.RowOdd, settings.NodeBorder),
			});
		}

		var bottom = h + y;
		var nameLine = nameHeight + y;
		elements.Add(Divider(x, nameLine, w + x, nameLine));
		elements.Add(Divider(maxType + x, nameLine, maxType + x, bottom));
		if (keysPresent)
		{
			elements.Add(Divider(maxType + maxName + x, nameLine, maxType + maxName + x, bottom));
		}

		if (commentPresent)
		{
			elements.Add(Divider(maxType + maxName + maxKeys + x, nameLine, maxType + maxName + maxKeys + x, bottom));
		}

		elements.Add(Divider(x, nameLine, w + x, nameLine));
		var right = x + w;
		if (!html)
		{
			// Chromium's getBBox of the group takes in an empty SVG text at its origin, so the empty keys and comment
			// texts at the right of a row widen the node past the rectangle.
			foreach (var (box, labelX, _) in labels)
			{
				if (box.Lines.Count == 0 || box.Width == 0)
				{
					right = Math.Max(right, labelX);
				}
			}
		}

		var bounds = new ShapeBounds(x, y, right, y + h);
		node.Geometry = new ShapeGeometry
		{
			Width = right - x,
			Height = h,
			Bounds = bounds,
			Elements = elements,
			Intersect = ShapeIntersect.Rect,
		};
		node.Labels = labels;
		node.Width = right - x;
		node.Height = h;
	}

	/// <summary>
	/// <c>drawRect</c> with no corner radius and the name padded by <c>er.diagramPadding</c> across and 1.5 times it down.
	/// The name wraps at <c>er.minEntityWidth</c> when the entity takes it, and at the wrapping width otherwise.
	/// </summary>
	private static void Plain(FlowchartMeasure measure, ErBoxSettings settings, FlowNode node, bool html, double padding)
	{
		var paddingY = padding * 1.5;
		var minimum = MermaidText.CalculateTextWidth(node.Label, settings.ConfigFont, measure.Font.Measurer) + (padding * 2) < settings.MinEntityWidth
			? settings.MinEntityWidth
			: 0;
		var label = measure.Text(node.Label, node.LabelType, minimum > 0 ? minimum : measure.WrappingWidth, html);
		var w = Math.Max(label.Width + (padding * 2), minimum);
		var h = label.Height + (paddingY * 2);
		var x = -w / 2;
		var y = -h / 2;
		node.Geometry = new ShapeGeometry
		{
			Width = w,
			Height = h,
			Bounds = new ShapeBounds(x, y, x + w, y + h),
			Elements = [new ShapeElement { Kind = ShapeElementKind.Rect, Class = "basic label-container", X = x, Y = y, Width = w, Height = h }],
			Intersect = ShapeIntersect.Rect,
		};
		node.Labels = [(label, -label.Width / 2, -label.Height / 2)];
		node.Width = w;
		node.Height = h;
	}

	private static FlowchartLabelBox Text(FlowchartMeasure measure, string text, bool html) =>
		text.Length == 0
			? FlowchartLabelBox.Empty(html)
			: measure.Text(text, FlowchartLabelType.Markdown, double.PositiveInfinity, html);

	private static ShapeElement Divider(double x1, double y1, double x2, double y2) =>
		new() { Kind = ShapeElementKind.Line, Class = "divider", X = x1, Y = y1, Width = x2, Height = y2 };

	private static List<KeyValuePair<string, string>>? Paints(string? fill, string? stroke)
	{
		var paints = new List<KeyValuePair<string, string>>(2);
		if (fill is not null && Rendering.SvgValues.IsAllowedPaint(fill))
		{
			paints.Add(new("fill", fill));
		}

		if (stroke is not null && Rendering.SvgValues.IsAllowedPaint(stroke))
		{
			paints.Add(new("stroke", stroke));
		}

		return paints.Count > 0 ? paints : null;
	}
}
