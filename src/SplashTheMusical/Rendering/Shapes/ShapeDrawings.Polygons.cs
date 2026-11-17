namespace SplashTheMusical.Rendering.Shapes;

// Upstream: packages/mermaid/src/rendering-util/rendering-elements/shapes/{question,hexagon,leanLeft,leanRight,trapezoid,
// invertedTrapezoid,rectLeftInvArrow,card,slopedRect,trapezoidalPentagon,triangle,flippedTriangle,hourglass,
// lightningBolt,choice,subroutine}.ts
internal static partial class ShapeDrawings
{
	/// <summary>Polygon shapes drawn as a <c>polygon</c> in the classic look and as a roughjs path in the hand-drawn one.</summary>
	private static ShapeElement PolygonOrRough(ShapeNode node, double w, double h, ShapePoint[] points, string handDrawnPath) =>
		node.IsHandDrawn
			? RoughPath(handDrawnPath, translate: new ShapePoint(-w / 2, h / 2))
			: InsertPolygon(w, h, points);

	public static ShapeGeometry Question(ShapeNode node)
	{
		var label = Measure(node, node.Width);
		var w = label.Width + (node.Padding ?? 0);
		var h = label.Height + (node.Padding ?? 0);
		var s = w + h;
		const double adjustment = 0.5;
		ShapePoint[] points = [new(s / 2, 0), new(s, -s / 2), new(s / 2, -s), new(0, -s / 2)];
		var translate = new ShapePoint(-s / 2 + adjustment, s / 2);
		var element = node.IsHandDrawn
			? RoughPath(string.Join(' ', $"M{N(s / 2)},{N(0)}", $"L{N(s)},{N(-s / 2)}", $"L{N(s / 2)},{N(-s)}", $"L{N(0)},{N(-s / 2)}", "Z"), translate: translate)
			: new ShapeElement { Kind = ShapeElementKind.Polygon, Points = points, Class = "label-container", Translate = translate };
		return Finish(element, label, LabelHelperTranslate(label), ShapeIntersect.Polygon(points));
	}

	public static ShapeGeometry Hexagon(ShapeNode node)
	{
		var f = node.IsNeo ? 3.5 : 4;
		var nodePadding = node.Padding ?? 0;
		var labelPaddingX = node.IsNeo ? 70 : nodePadding;
		var labelPaddingY = node.IsNeo ? 32 : nodePadding;
		var width = node.Width;
		var height = node.Height;
		if (Truthy(width) || Truthy(height))
		{
			var m0 = (height ?? 0) / f;
			width = (width ?? 0) - 2 * m0 - labelPaddingY;
			height = (height ?? 0) - labelPaddingX;
		}

		var label = Measure(node, width);
		var h = OrElse(height, label.Height) + labelPaddingX;
		var m = h / f;
		var w = OrElse(width, label.Width) + 2 * m + labelPaddingY;
		ShapePoint[] points = [new(m, 0), new(w - m, 0), new(w, -h / 2), new(w - m, -h), new(m, -h), new(0, -h / 2)];
		var path = string.Join(' ', $"M{N(m)},{N(0)}", $"L{N(w - m)},{N(0)}", $"L{N(w)},{N(-h / 2)}", $"L{N(w - m)},{N(-h)}", $"L{N(m)},{N(-h)}", $"L{N(0)},{N(-h / 2)}", "Z");
		return Finish(PolygonOrRough(node, w, h, points, path), label, LabelHelperTranslate(label), ShapeIntersect.Polygon(points));
	}

	public static ShapeGeometry LeanLeft(ShapeNode node) => Lean(node, left: true);

	public static ShapeGeometry LeanRight(ShapeNode node) => Lean(node, left: false);

	private static ShapeGeometry Lean(ShapeNode node, bool left)
	{
		var nodePadding = node.Padding ?? 0;
		var labelPaddingX = node.IsNeo ? nodePadding * 2 : nodePadding;
		var label = Measure(node, node.Width);
		var h = Math.Max(label.Height + nodePadding, node.Height ?? 0);
		var w = Math.Max(label.Width + labelPaddingX, (node.Width ?? 0) - h);
		ShapePoint[] points = left
			? [new(0, 0), new(w + 3 * h / 6, 0), new(w, -h), new(-(3 * h) / 6, -h)]
			: [new(-3 * h / 6, 0), new(w, 0), new(w + 3 * h / 6, -h), new(0, -h)];
		return Finish(PolygonOrRough(node, w, h, points, ShapePaths.FromPoints(points)), label, LabelHelperTranslate(label), ShapeIntersect.Polygon(points));
	}

	public static ShapeGeometry Trapezoid(ShapeNode node)
	{
		var nodePadding = node.Padding ?? 0;
		var labelPaddingX = node.IsNeo ? nodePadding * 2 : nodePadding;
		var label = Measure(node, node.Width);
		var h = Math.Max(label.Height + nodePadding, node.Height ?? 0);
		var w = Math.Max(label.Width + labelPaddingX, (node.Width ?? 0) - h);
		ShapePoint[] points = [new(-3 * h / 6, 0), new(w + 3 * h / 6, 0), new(w, -h), new(0, -h)];
		return Finish(PolygonOrRough(node, w, h, points, ShapePaths.FromPoints(points)), label, LabelHelperTranslate(label), ShapeIntersect.Polygon(points));
	}

	public static ShapeGeometry InvTrapezoid(ShapeNode node)
	{
		var nodePadding = node.Padding ?? 0;
		var labelPaddingX = node.IsNeo ? nodePadding * 2 : nodePadding;
		var label = Measure(node, node.Width);
		var h = Math.Max(label.Height + nodePadding * 2, node.Height ?? 0);
		var w = Math.Max(label.Width + labelPaddingX * 2, (node.Width ?? 0) - h);
		ShapePoint[] points = [new(0, 0), new(w, 0), new(w + 3 * h / 6, -h), new(-3 * h / 6, -h)];
		return Finish(PolygonOrRough(node, w, h, points, ShapePaths.FromPoints(points)), label, LabelHelperTranslate(label), ShapeIntersect.Polygon(points));
	}

	public static ShapeGeometry RectLeftInvArrow(ShapeNode node)
	{
		var nodePadding = node.Padding ?? 0;
		var labelPaddingX = node.IsNeo ? 21 : nodePadding;
		var labelPaddingY = node.IsNeo ? 12 : nodePadding;
		var label = Measure(node, node.Width);
		var labelWidth = label.Width + (node.IsNeo ? labelPaddingX * 2 : labelPaddingX);
		var h = Math.Max(label.Height + (node.IsNeo ? labelPaddingY * 2 : labelPaddingY), node.Height ?? 0);
		var notchWidth = h / 4;
		var w = Math.Max(labelWidth, (node.Width ?? 0) - notchWidth);
		var x = -w / 2;
		var y = -h / 2;
		var notch = y / 2;
		ShapePoint[] points = [new(x + notch, y), new(x, 0), new(x + notch, -y), new(-x, -y), new(-x, y)];
		var element = RoughPath(ShapePaths.FromPoints(points), OuterPath, new ShapePoint(-notch / 2, 0));
		var labelTranslate = new ShapePoint(-notch / 2 - label.Width / 2 - label.OffsetX, -(label.Height / 2) - label.OffsetY);
		return Finish(element, label, labelTranslate, ShapeIntersect.Polygon(points));
	}

	public static ShapeGeometry Card(ShapeNode node)
	{
		const double notchSize = 12;
		var nodePadding = node.Padding ?? 0;
		var labelPaddingX = node.IsNeo ? 28 : nodePadding;
		var labelPaddingY = node.IsNeo ? 24 : nodePadding;
		var label = Measure(node, node.Width);
		var w = (node.Width ?? label.Width) + (node.IsNeo ? labelPaddingX * 2 : labelPaddingX + notchSize);
		var h = (node.Height ?? label.Height) + (node.IsNeo ? labelPaddingY * 2 : labelPaddingY);
		ShapePoint[] points = [new(notchSize, -h), new(w, -h), new(w, 0), new(0, 0), new(0, -h + notchSize), new(notchSize, -h)];
		return Finish(PolygonOrRough(node, w, h, points, ShapePaths.FromPoints(points)), label, LabelHelperTranslate(label), ShapeIntersect.Polygon(points));
	}

	public static ShapeGeometry SlopedRect(ShapeNode node)
	{
		var nodePadding = node.Padding ?? 0;
		var labelPaddingX = node.IsNeo ? 16 : nodePadding;
		var labelPaddingY = node.IsNeo ? 12 : nodePadding;
		var width = node.Width;
		var height = node.Height;
		if (Truthy(width) || Truthy(height))
		{
			width = Math.Max((width ?? 0) - labelPaddingX * 2, 10);
			height = Math.Max((height ?? 0) / 1.5 - labelPaddingY * 2, 10);
		}

		var label = Measure(node, width);
		var w = OrElse(width, label.Width) + labelPaddingX * 2;
		var h = (OrElse(height, label.Height) + labelPaddingY * 2) * 1.5 / 1.5;
		var x = -w / 2;
		var y = -h / 2;
		ShapePoint[] points = [new(x, y), new(x, y + h), new(x + w, y + h), new(x + w, y - h / 2)];
		var element = RoughPath(ShapePaths.FromPoints(points), "basic label-container  outer-path", new ShapePoint(0, h / 4));
		var labelTranslate = new ShapePoint(-w / 2 + nodePadding - label.OffsetX, -h / 4 + nodePadding - label.OffsetY);
		return Finish(element, label, labelTranslate, ShapeIntersect.Polygon(points));
	}

	public static ShapeGeometry TrapezoidalPentagon(ShapeNode node)
	{
		var nodePadding = node.Padding ?? 0;
		var labelPaddingX = node.IsNeo ? 16 : nodePadding;
		var labelPaddingY = node.IsNeo ? 12 : nodePadding;
		var width = node.Width;
		var height = node.Height;
		if (Truthy(width) || Truthy(height))
		{
			height = Math.Max((height ?? 0) - labelPaddingY * 2, 5);
			width = Math.Max((width ?? 0) - labelPaddingX * 2, 15);
		}

		var label = Measure(node, width);
		var w = OrElse(width, label.Width) + labelPaddingX * 2;
		var h = OrElse(height, label.Height) + labelPaddingY * 2;
		ShapePoint[] points =
		[
			new(-w / 2 * 0.8, -h / 2),
			new(w / 2 * 0.8, -h / 2),
			new(w / 2, -h / 2 * 0.6),
			new(w / 2, h / 2),
			new(-w / 2, h / 2),
			new(-w / 2, -h / 2 * 0.6),
		];
		return Finish(RoughPath(ShapePaths.FromPoints(points), OuterPath), label, LabelHelperTranslate(label), ShapeIntersect.Polygon(points));
	}

	public static ShapeGeometry Triangle(ShapeNode node)
	{
		var nodePadding = node.Padding ?? 0;
		var labelPaddingX = node.IsNeo ? nodePadding * 2 : nodePadding;
		var width = node.Width;
		var height = node.Height;
		if (Truthy(width) || Truthy(height))
		{
			width = Math.Max(((width ?? 0) - labelPaddingX) / 2, 10);
			height = Math.Max(height ?? 0, 10);
		}

		var label = Measure(node, width);
		var w = OrElse(width, label.Width) + labelPaddingX;
		var h = OrElse(height, w + label.Height);
		ShapePoint[] points = [new(0, 0), new(h, 0), new(h / 2, -h)];
		var element = RoughPath(ShapePaths.FromPoints(points), "outer-path", new ShapePoint(-h / 2, h / 2));
		var labelTranslate = new ShapePoint(
			-label.Width / 2 - label.OffsetX,
			h / 2 - (label.Height + nodePadding / (node.FlowchartHtmlLabels ? 2 : 1) - label.OffsetY));
		return Finish(element, label, labelTranslate, ShapeIntersect.Polygon(points));
	}

	public static ShapeGeometry FlippedTriangle(ShapeNode node)
	{
		var nodePadding = node.Padding ?? 0;
		var labelPaddingX = node.IsNeo ? nodePadding * 2 : nodePadding;
		var width = node.Width;
		var height = node.Height;
		if (Truthy(width) || Truthy(height))
		{
			height = Math.Max(height ?? 0, 10);
			width = Math.Max((width ?? 0) - labelPaddingX - labelPaddingX / 2, 10);
		}

		var label = Measure(node, width);
		var w = OrElse(width, label.Width) + labelPaddingX;
		var h = OrElse(height, w + label.Height);
		ShapePoint[] points = [new(0, -h), new(h, -h), new(h / 2, 0)];
		var element = RoughPath(ShapePaths.FromPoints(points), "outer-path", new ShapePoint(-h / 2, h / 2));
		var labelTranslate = new ShapePoint(-label.Width / 2 - label.OffsetX, -h / 2 + nodePadding / 2 + label.OffsetY);
		return Finish(element, label, labelTranslate, ShapeIntersect.Polygon(points));
	}

	public static ShapeGeometry Hourglass(ShapeNode node)
	{
		var label = ShapeLabel.Empty(node.HtmlLabels);
		var w = Math.Max(30, node.Width ?? 0);
		var h = Math.Max(30, node.Height ?? 0);
		ShapePoint[] points = [new(0, 0), new(w, 0), new(0, h), new(w, h)];
		var element = RoughPath(ShapePaths.FromPoints(points), OuterPath, new ShapePoint(-w / 2, -h / 2));
		return Finish(element, label, LabelHelperTranslate(label), ShapeIntersect.Polygon(points));
	}

	public static ShapeGeometry LightningBolt(ShapeNode node)
	{
		var width = Math.Max(35, node.Width ?? 0);
		var height = Math.Max(35, node.Height ?? 0);
		const double gap = 7;
		ShapePoint[] points =
		[
			new(width, 0),
			new(0, height + gap / 2),
			new(width - 2 * gap, height + gap / 2),
			new(0, 2 * height),
			new(width, height - gap / 2),
			new(2 * gap, height - gap / 2),
		];
		var element = RoughPath(ShapePaths.FromPoints(points), "outer-path", new ShapePoint(-width / 2, -height));
		return Finish(element, null, default, ShapeIntersect.Polygon(points));
	}

	public static ShapeGeometry Choice(ShapeNode node)
	{
		var s = Math.Max(28, node.Width ?? 0);
		ShapePoint[] points = [new(0, s / 2), new(s / 2, 0), new(0, -s / 2), new(-s / 2, 0)];
		return Finish(RoughPath(ShapePaths.FromPoints(points)), null, default, ShapeIntersect.Polygon(points));
	}

	public static ShapeGeometry Subroutine(ShapeNode node)
	{
		const double frameWidth = 8;
		var nodePadding = node.Padding ?? 8;
		var labelPaddingX = node.IsNeo ? 28 : nodePadding;
		var labelPaddingY = node.IsNeo ? 12 : nodePadding;
		var label = Measure(node, node.Width);
		var totalWidth = Math.Max(label.Width + 2 * frameWidth + labelPaddingX, node.Width ?? 0);
		var totalHeight = Math.Max(label.Height + labelPaddingY, node.Height ?? 0);
		var w = totalWidth - 2 * frameWidth;
		var h = totalHeight;
		var x = -totalWidth / 2;
		var y = -totalHeight / 2;
		ShapePoint[] points =
		[
			new(0, 0), new(w, 0), new(w, -h), new(0, -h), new(0, 0),
			new(-8, 0), new(w + 8, 0), new(w + 8, -h), new(-8, -h), new(-8, 0),
		];
		if (node.IsHandDrawn)
		{
			var rect = Rect(x, y, w + 16, h, LabelContainer, rough: true);
			var l1 = new ShapeElement { Kind = ShapeElementKind.Line, Rough = true, X = x + frameWidth, Y = y, Width = x + frameWidth, Height = y + h };
			var l2 = new ShapeElement { Kind = ShapeElementKind.Line, Rough = true, X = x + frameWidth + w, Y = y, Width = x + frameWidth + w, Height = y + h };
			return Finish([rect, l2, l1], label, LabelHelperTranslate(label), ShapeIntersect.Polygon(points));
		}

		return Finish(InsertPolygon(w, h, points), label, LabelHelperTranslate(label), ShapeIntersect.Polygon(points));
	}
}
