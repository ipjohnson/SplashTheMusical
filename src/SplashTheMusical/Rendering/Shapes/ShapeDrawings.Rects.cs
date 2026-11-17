namespace SplashTheMusical.Rendering.Shapes;

// Upstream: packages/mermaid/src/rendering-util/rendering-elements/shapes/{drawRect,squareRect,roundedRect,datastore,
// text,note,stadium,shadedProcess,taggedRect,multiRect,dividedRect,windowPane,halfRoundedRectangle,curvedTrapezoid,
// bowTieRect}.ts
internal static partial class ShapeDrawings
{
	/// <summary><c>drawRect</c>: a rect around the label, at least the node's size, with an optional corner radius.</summary>
	private static ShapeGeometry DrawRect(ShapeNode node, double? radius, double labelPaddingX, double labelPaddingY)
	{
		var label = Measure(node, node.Width);
		var totalWidth = Math.Max(label.Width + labelPaddingX * 2, OrElse(node.Width, 0));
		var totalHeight = Math.Max(label.Height + labelPaddingY * 2, OrElse(node.Height, 0));
		var x = -totalWidth / 2;
		var y = -totalHeight / 2;
		var rx = Truthy(radius) ? radius : node.Radius;
		ShapeElement element;
		if (node.IsHandDrawn)
		{
			element = Truthy(rx)
				? RoughPath(ShapePaths.RoundedRect(x, y, totalWidth, totalHeight, rx!.Value), LabelContainer)
				: Rect(x, y, totalWidth, totalHeight, LabelContainer, rough: true);
		}
		else
		{
			element = new ShapeElement
			{
				Kind = ShapeElementKind.Rect,
				Class = LabelContainer,
				X = x,
				Y = y,
				Width = totalWidth,
				Height = totalHeight,
				Rx = rx ?? 0,
				Ry = rx ?? 0,
			};
		}

		return Finish(element, label, LabelHelperTranslate(label), ShapeIntersect.Rect);
	}

	public static ShapeGeometry SquareRect(ShapeNode node)
	{
		var nodePadding = node.Padding ?? 0;
		var labelPaddingX = node.IsNeo ? 16 : nodePadding * 2;
		var labelPaddingY = node.IsNeo ? 12 : nodePadding;
		return DrawRect(node, 0, node.LabelPaddingX ?? labelPaddingX, labelPaddingY);
	}

	public static ShapeGeometry RoundedRect(ShapeNode node) =>
		DrawRect(node, node.ThemeRadius ?? 5, node.Padding ?? 0, node.Padding ?? 0);

	/// <summary>The data store: the plain rect with a dash array that draws only its top and bottom edges.</summary>
	public static ShapeGeometry Datastore(ShapeNode node)
	{
		var labelPaddingX = node.LabelPaddingX ?? (node.Padding ?? 0) * 2;
		var labelPaddingY = node.LabelPaddingY ?? node.Padding ?? 0;
		var rect = DrawRect(node, 0, labelPaddingX, labelPaddingY);
		var element = rect.Elements[0];
		if (node.IsHandDrawn || element.Width <= 0 || element.Height <= 0)
		{
			return rect;
		}

		var dashed = element with { Attributes = [new("stroke-dasharray", N(element.Width) + " " + N(element.Height))] };
		return Finish(dashed, rect.Label, rect.LabelTranslate, rect.Intersect);
	}

	public static ShapeGeometry Text(ShapeNode node)
	{
		var label = Measure(node, node.Width);
		var totalWidth = Math.Max(label.Width + (node.Padding ?? 0), OrElse(node.Width, 0));
		var totalHeight = Math.Max(label.Height + (node.Padding ?? 0), OrElse(node.Height, 0));
		var rect = Rect(-totalWidth / 2, -totalHeight / 2, totalWidth, totalHeight, "text");
		return Finish(rect, label, LabelHelperTranslate(label), ShapeIntersect.Rect);
	}

	public static ShapeGeometry Note(ShapeNode node)
	{
		var label = Measure(node, node.Width);
		var totalWidth = Math.Max(label.Width + (node.Padding ?? 0) * 2, node.Width ?? 0);
		var totalHeight = Math.Max(label.Height + (node.Padding ?? 0) * 2, node.Height ?? 0);
		var rect = Rect(-totalWidth / 2, -totalHeight / 2, totalWidth, totalHeight, OuterPath, rough: true);
		return Finish(rect, label, CenteredLabel(label), ShapeIntersect.Rect);
	}

	public static ShapeGeometry Stadium(ShapeNode node)
	{
		const int arcPointCount = 50;
		var nodePadding = node.Padding ?? 0;
		var paddingX = node.IsNeo ? 40 : nodePadding;
		var paddingY = node.IsNeo ? 24 : nodePadding;
		var label = Measure(node, node.Width);
		var h = label.Height + paddingY;
		var inscribedDiameter = h * Math.Cos(Math.PI / (2 * (arcPointCount - 1)));
		var capWidthAtLabel = Math.Sqrt(Math.Max(0, inscribedDiameter * inscribedDiameter - label.Height * label.Height));
		var w = Math.Max(
			Math.Max(label.Width + h / 4 + paddingX, 1.5 * h + (h - inscribedDiameter)),
			label.Width + paddingX + h - capWidthAtLabel);
		var radius = h / 2;
		List<ShapePoint> points = [new(-w / 2 + radius, -h / 2), new(w / 2 - radius, -h / 2)];
		points.AddRange(ShapePaths.CirclePoints(-w / 2 + radius, 0, radius, arcPointCount, 90, 270));
		points.Add(new(w / 2 - radius, h / 2));
		points.AddRange(ShapePaths.CirclePoints(w / 2 - radius, 0, radius, arcPointCount, 270, 450));
		return Finish(RoughPath(ShapePaths.FromPoints(points), OuterPath), label, LabelHelperTranslate(label), ShapeIntersect.Polygon(points));
	}

	public static ShapeGeometry ShadedProcess(ShapeNode node)
	{
		const double frameWidth = 8;
		var paddingX = node.IsNeo ? 16 : node.Padding ?? 0;
		var paddingY = node.IsNeo ? 12 : node.Padding ?? 0;
		var label = Measure(node, node.Width);
		var totalWidth = (node.Width ?? label.Width) + paddingX * 2 + (node.IsNeo ? frameWidth : frameWidth * 2);
		var totalHeight = (node.Height ?? label.Height) + paddingY * 2;
		var w = totalWidth - frameWidth;
		var h = totalHeight;
		var x = frameWidth - totalWidth / 2;
		var y = -totalHeight / 2;
		ShapePoint[] points =
		[
			new(x, y), new(x + w, y), new(x + w, y + h), new(x - frameWidth, y + h), new(x - frameWidth, y), new(x, y), new(x, y + h),
		];
		var labelTranslate = new ShapePoint(frameWidth / 2 - label.Width / 2 - label.OffsetX, -(label.Height / 2) - label.OffsetY);
		return Finish(RoughPolygon(points, OuterPath), label, labelTranslate, ShapeIntersect.Rect);
	}

	public static ShapeGeometry TaggedRect(ShapeNode node)
	{
		const double tagRatio = 0.2;
		var nodePadding = node.Padding ?? 0;
		var labelPaddingX = node.IsNeo ? 16 : nodePadding;
		var labelPaddingY = node.IsNeo ? 12 : nodePadding;
		var width = node.Width;
		var height = node.Height;
		if (Truthy(width) || Truthy(height))
		{
			height = Math.Max((height ?? 0) - labelPaddingY * 2, 10);
			width = Math.Max((width ?? 0) - labelPaddingX * 2 - tagRatio * (height.Value + labelPaddingY * 2), 10);
		}

		var label = Measure(node, width);
		var totalHeight = OrElse(height, label.Height) + labelPaddingY * 2;
		var tagWidth = tagRatio * totalHeight;
		var tagHeight = tagRatio * totalHeight;
		var totalWidth = OrElse(width, label.Width) + labelPaddingX * 2 + tagWidth;
		var w = totalWidth - tagWidth;
		var h = totalHeight;
		var x = -w / 2;
		var y = -h / 2;
		ShapePoint[] rectPoints = [new(x - tagWidth / 2, y), new(x + w + tagWidth / 2, y), new(x + w + tagWidth / 2, y + h), new(x - tagWidth / 2, y + h)];
		ShapePoint[] tagPoints = [new(x + w - tagWidth / 2, y + h), new(x + w + tagWidth / 2, y + h), new(x + w + tagWidth / 2, y + h - tagHeight)];
		var group = Group(OuterPath, RoughPath(ShapePaths.FromPoints(rectPoints)), RoughPath(ShapePaths.FromPoints(tagPoints)));
		return Finish(group, label, LabelHelperTranslate(label), ShapeIntersect.Polygon(rectPoints));
	}

	public static ShapeGeometry MultiRect(ShapeNode node)
	{
		var nodePadding = node.Padding ?? 0;
		var labelPaddingX = node.IsNeo ? 16 : nodePadding;
		var labelPaddingY = node.IsNeo ? 12 : nodePadding;
		var rectOffset = node.IsNeo ? 10 : 5;
		var width = node.Width;
		var height = node.Height;
		if (Truthy(width) || Truthy(height))
		{
			width = Math.Max((width ?? 0) - labelPaddingX * 2 - 2 * rectOffset, 10);
			height = Math.Max((height ?? 0) - labelPaddingY * 2 - 2 * rectOffset, 10);
		}

		var label = Measure(node, width);
		var totalWidth = OrElse(width, label.Width) + labelPaddingX * 2 + 2 * rectOffset;
		var totalHeight = OrElse(height, label.Height) + labelPaddingY * 2 + 2 * rectOffset;
		var w = totalWidth - 2 * rectOffset;
		var h = totalHeight - 2 * rectOffset;
		var x = -w / 2;
		var y = -h / 2;
		ShapePoint[] outer =
		[
			new(x - rectOffset, y + rectOffset),
			new(x - rectOffset, y + h + rectOffset),
			new(x + w - rectOffset, y + h + rectOffset),
			new(x + w - rectOffset, y + h),
			new(x + w, y + h),
			new(x + w, y + h - rectOffset),
			new(x + w + rectOffset, y + h - rectOffset),
			new(x + w + rectOffset, y - rectOffset),
			new(x + rectOffset, y - rectOffset),
			new(x + rectOffset, y),
			new(x, y),
			new(x, y + rectOffset),
		];
		ShapePoint[] inner =
		[
			new(x, y + rectOffset),
			new(x + w - rectOffset, y + rectOffset),
			new(x + w - rectOffset, y + h),
			new(x + w, y + h),
			new(x + w, y),
			new(x, y),
		];
		var group = Group(OuterPath, RoughPath(ShapePaths.FromPoints(outer)), RoughPath(ShapePaths.FromPoints(inner)));
		var labelTranslate = new ShapePoint(-(label.Width / 2) - rectOffset - label.OffsetX, -(label.Height / 2) + rectOffset - label.OffsetY);
		return Finish(group, label, labelTranslate, ShapeIntersect.Polygon(outer));
	}

	public static ShapeGeometry DividedRectangle(ShapeNode node)
	{
		var paddingX = node.IsNeo ? 16 : node.Padding ?? 0;
		var paddingY = node.IsNeo ? 16 : node.Padding ?? 0;
		var label = Measure(node, node.Width);
		var w = label.Width + paddingX;
		var h = label.Height + paddingY;
		var rectOffset = h * 0.2;
		var x = -w / 2;
		var y = -h / 2 - rectOffset / 2;
		ShapePoint[] points =
		[
			new(x, y + rectOffset), new(-x, y + rectOffset), new(-x, -y), new(x, -y), new(x, y), new(-x, y), new(-x, y + rectOffset),
		];
		var labelTranslate = new ShapePoint(x + (node.Padding ?? 0) / 2 - label.OffsetX, y + rectOffset + (node.Padding ?? 0) / 2 - label.OffsetY);
		return Finish(RoughPolygon(points, OuterPath), label, labelTranslate, ShapeIntersect.Rect);
	}

	public static ShapeGeometry WindowPane(ShapeNode node)
	{
		const double rectOffset = 10;
		var paddingX = node.IsNeo ? 16 : node.Padding ?? 0;
		var paddingY = node.IsNeo ? 12 : node.Padding ?? 0;
		var width = node.Width;
		var height = node.Height;
		if (Truthy(width) || Truthy(height))
		{
			width = Math.Max((width ?? 0) - paddingX * 2 - rectOffset, 10);
			height = Math.Max((height ?? 0) - paddingY * 2 - rectOffset, 10);
		}

		var label = Measure(node, width);
		var w = OrElse(width, label.Width) + paddingX * 2;
		var h = OrElse(height, label.Height) + paddingY * 2;
		var x = -w / 2;
		var y = -h / 2;
		ShapePoint[] outer = [new(x - rectOffset, y - rectOffset), new(x - rectOffset, y + h), new(x + w, y + h), new(x + w, y - rectOffset)];
		const string indent = "\n                ";
		var path = $"M{N(x - rectOffset)},{N(y - rectOffset)} L{N(x + w)},{N(y - rectOffset)} L{N(x + w)},{N(y + h)} L{N(x - rectOffset)},{N(y + h)} L{N(x - rectOffset)},{N(y - rectOffset)}"
			+ $"{indent}M{N(x - rectOffset)},{N(y)} L{N(x + w)},{N(y)}"
			+ $"{indent}M{N(x)},{N(y - rectOffset)} L{N(x)},{N(y + h)}";
		var element = RoughPath(path, OuterPath, new ShapePoint(rectOffset / 2, rectOffset / 2));
		var labelTranslate = new ShapePoint(-(label.Width / 2) + rectOffset / 2 - label.OffsetX, -(label.Height / 2) + rectOffset / 2 - label.OffsetY);
		return Finish(element, label, labelTranslate, ShapeIntersect.Polygon(outer));
	}

	public static ShapeGeometry HalfRoundedRectangle(ShapeNode node)
	{
		const double minWidth = 15, minHeight = 10;
		var paddingX = node.IsNeo ? 16 : node.Padding ?? 0;
		var paddingY = node.IsNeo ? 12 : node.Padding ?? 0;
		var width = node.Width;
		var height = node.Height;
		if (Truthy(width) || Truthy(height))
		{
			height = Math.Max((height ?? 0) - paddingY * 2, minHeight);
			width = Math.Max((width ?? 0) - paddingX * 2, minWidth);
		}

		var label = Measure(node, width);
		var h = OrElse(height, Math.Max(minHeight, label.Height)) + paddingY * 2;
		var radius = h / 2;
		var capClearance = radius - Math.Sqrt(Math.Max(0, radius * radius - (label.Height / 2) * (label.Height / 2)));
		var w = OrElse(width, Math.Max(minWidth, label.Width) + capClearance * 2) + paddingX * 2;
		List<ShapePoint> points = [new(-w / 2, -h / 2), new(w / 2 - radius, -h / 2)];
		points.AddRange(ShapePaths.CirclePoints(-w / 2 + radius, 0, radius, 50, 90, 270));
		points.Add(new(w / 2 - radius, h / 2));
		points.Add(new(-w / 2, h / 2));
		return Finish(RoughPath(ShapePaths.FromPoints(points), OuterPath), label, LabelHelperTranslate(label), ShapeIntersect.Polygon(points));
	}

	public static ShapeGeometry CurvedTrapezoid(ShapeNode node)
	{
		const double minWidth = 20, minHeight = 5;
		var nodePadding = node.Padding ?? 0;
		var labelPaddingX = node.IsNeo ? 16 : nodePadding;
		var labelPaddingY = node.IsNeo ? 12 : nodePadding;
		var label = Measure(node, node.Width);
		var h = Math.Max(Math.Max(minHeight, label.Height + labelPaddingY * 2), node.Height ?? 0);
		var radius = h / 2;
		var capClearance = radius - Math.Sqrt(Math.Max(0, radius * radius - (label.Height / 2) * (label.Height / 2)));
		var sideClearance = Math.Max(h / 4, capClearance);
		var w = Math.Max(
			Math.Max(minWidth, label.Width + labelPaddingX * 2 + sideClearance * 2),
			Math.Max((label.Width + labelPaddingX * 2) * 1.25, node.Width ?? 0));
		var rw = w - radius;
		var tw = h / 4;
		List<ShapePoint> points = [new(rw, 0), new(tw, 0), new(0, h / 2), new(tw, h), new(rw, h)];
		points.AddRange(ShapePaths.CirclePoints(-rw, -h / 2, radius, 50, 270, 90));
		var element = RoughPath(ShapePaths.FromPoints(points), OuterPath, new ShapePoint(-w / 2, -h / 2));
		return Finish(element, label, LabelHelperTranslate(label), ShapeIntersect.Polygon(points));
	}

	public static ShapeGeometry BowTieRect(ShapeNode node)
	{
		var nodePadding = node.Padding ?? 0;
		var labelPaddingX = node.IsNeo ? 16 : nodePadding;
		var labelPaddingY = node.IsNeo ? 12 : nodePadding;
		var label = Measure(node, node.Width);
		var totalHeight = OrElse(node.Height, label.Height) + labelPaddingY;
		var ry = totalHeight / 2;
		var rx = ry / (2.5 + totalHeight / 50);
		var sagitta = ArcSagitta(totalHeight, rx, ry);
		var totalWidth = OrElse(node.Width, label.Width) + labelPaddingX * 2 + sagitta;
		var w = totalWidth - sagitta;
		var h = totalHeight;
		List<ShapePoint> points = [new(w / 2, -h / 2), new(-w / 2, -h / 2)];
		points.AddRange(ArcPoints(-w / 2, -h / 2, -w / 2, h / 2, rx, ry, clockwise: false));
		points.Add(new(w / 2, h / 2));
		points.AddRange(ArcPoints(w / 2, h / 2, w / 2, -h / 2, rx, ry, clockwise: true));
		var element = RoughPath(ShapePaths.FromPoints(points), OuterPath, new ShapePoint(rx / 2, 0));
		return Finish(element, label, LabelHelperTranslate(label), ShapeIntersect.Polygon(points));
	}

	/// <summary>The bow tie's <c>generateArcPoints</c>: 20 points on the elliptical arc between two points.</summary>
	private static List<ShapePoint> ArcPoints(double x1, double y1, double x2, double y2, double rx, double ry, bool clockwise)
	{
		const int count = 20;
		var midX = (x1 + x2) / 2;
		var midY = (y1 + y2) / 2;
		var angle = Math.Atan2(y2 - y1, x2 - x1);
		var transformedX = (x2 - x1) / 2 / rx;
		var transformedY = (y2 - y1) / 2 / ry;
		var distance = Math.Sqrt(transformedX * transformedX + transformedY * transformedY);
		if (distance > 1)
		{
			throw new InvalidOperationException("The given radii are too small to create an arc between the points.");
		}

		var scaledCenterDistance = Math.Sqrt(1 - distance * distance);
		var centerX = midX + scaledCenterDistance * ry * Math.Sin(angle) * (clockwise ? -1 : 1);
		var centerY = midY - scaledCenterDistance * rx * Math.Cos(angle) * (clockwise ? -1 : 1);
		var startAngle = Math.Atan2((y1 - centerY) / ry, (x1 - centerX) / rx);
		var endAngle = Math.Atan2((y2 - centerY) / ry, (x2 - centerX) / rx);
		var range = endAngle - startAngle;
		if (clockwise && range < 0)
		{
			range += 2 * Math.PI;
		}

		if (!clockwise && range > 0)
		{
			range -= 2 * Math.PI;
		}

		var points = new List<ShapePoint>(count);
		for (var i = 0; i < count; i++)
		{
			var a = startAngle + (double)i / (count - 1) * range;
			points.Add(new ShapePoint(centerX + rx * Math.Cos(a), centerY + ry * Math.Sin(a)));
		}

		return points;
	}

	/// <summary><c>calculateArcSagitta</c>: the sagitta of a chord of the ellipse.</summary>
	private static double ArcSagitta(double chord, double rx, double ry)
	{
		var major = Math.Max(rx, ry);
		var minor = Math.Min(rx, ry);
		return minor * (1 - Math.Sqrt(1 - Math.Pow(chord / major / 2, 2)));
	}
}
