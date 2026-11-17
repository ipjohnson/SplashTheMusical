namespace SplashTheMusical.Rendering.Shapes;

// Upstream: packages/mermaid/src/rendering-util/rendering-elements/shapes/{curlyBraceLeft,curlyBraceRight,curlyBraces,
// bang,cloud,browser,bucket,console,folder,person}.ts
internal static partial class ShapeDrawings
{
	public static ShapeGeometry CurlyBraceLeft(ShapeNode node)
	{
		var label = Measure(node, node.Width);
		var nodePadding = node.Padding ?? 0;
		var w = label.Width + (node.IsNeo ? 18 : nodePadding);
		var h = label.Height + (node.IsNeo ? 12 : nodePadding);
		var radius = Math.Max(5, h * 0.1);
		var brace = LeftBrace(w, h, radius);
		var rect = LeftBraceBox(w, h, radius);
		var group = Group("text", RoughPath(Unclosed(brace)), BraceBox(rect)) with { Translate = new ShapePoint(radius, 0) };
		var labelTranslate = new ShapePoint(-w / 2 + radius - label.OffsetX, -h / 2 + nodePadding / 2 - label.OffsetY);
		return Finish(group, label, labelTranslate, ShapeIntersect.Polygon(rect));
	}

	public static ShapeGeometry CurlyBraceRight(ShapeNode node)
	{
		var label = Measure(node, node.Width);
		var nodePadding = node.Padding ?? 0;
		var w = label.Width + (node.IsNeo ? 18 * 2 : nodePadding);
		var h = label.Height + (node.IsNeo ? 12 * 2 : nodePadding);
		var radius = Math.Max(5, h * 0.1);
		List<ShapePoint> brace = [.. ShapePaths.CirclePoints(w / 2, -h / 2, radius, 20, -90, 0, negate: false), new(w / 2 + radius, -radius)];
		brace.AddRange(ShapePaths.CirclePoints(w / 2 + radius * 2, -radius, radius, 20, -180, -270, negate: false));
		brace.AddRange(ShapePaths.CirclePoints(w / 2 + radius * 2, radius, radius, 20, -90, -180, negate: false));
		brace.Add(new(w / 2 + radius, h / 2));
		brace.AddRange(ShapePaths.CirclePoints(w / 2, h / 2, radius, 20, 0, 90, negate: false));
		List<ShapePoint> rect = [new(-w / 2, -h / 2 - radius), new(w / 2, -h / 2 - radius)];
		rect.AddRange(brace);
		rect.Add(new(w / 2, h / 2 + radius));
		rect.Add(new(-w / 2, h / 2 + radius));
		var group = Group("text", RoughPath(Unclosed(brace)), BraceBox(rect)) with { Translate = new ShapePoint(-radius, 0) };
		var labelTranslate = new ShapePoint(-w / 2 + nodePadding / 2 - label.OffsetX, -h / 2 + nodePadding / 2 - label.OffsetY);
		return Finish(group, label, labelTranslate, ShapeIntersect.Polygon(rect));
	}

	public static ShapeGeometry CurlyBraces(ShapeNode node)
	{
		var label = Measure(node, node.Width);
		var nodePadding = node.Padding ?? 0;
		var w = label.Width + (node.IsNeo ? 18 * 2 : nodePadding);
		var h = label.Height + (node.IsNeo ? 12 * 2 : nodePadding);
		var radius = Math.Max(5, h * 0.1);
		var left = LeftBrace(w, h, radius);
		List<ShapePoint> right = [.. ShapePaths.CirclePoints(-w / 2 + radius + radius / 2, -h / 2, radius, 20, -90, -180), new(w / 2 - radius / 2, radius)];
		right.AddRange(ShapePaths.CirclePoints(-w / 2 - radius / 2, -radius, radius, 20, 0, 90));
		right.AddRange(ShapePaths.CirclePoints(-w / 2 - radius / 2, radius, radius, 20, -90, 0));
		right.Add(new(w / 2 - radius / 2, -radius));
		right.AddRange(ShapePaths.CirclePoints(-w / 2 + radius + radius / 2, h / 2, radius, 30, -180, -270));
		List<ShapePoint> rect = [new(w / 2, -h / 2 - radius), new(-w / 2, -h / 2 - radius)];
		rect.AddRange(ShapePaths.CirclePoints(w / 2, -h / 2, radius, 20, -90, 0));
		rect.Add(new(-w / 2 - radius, -radius));
		rect.AddRange(ShapePaths.CirclePoints(w / 2 + radius * 2, -radius, radius, 20, -180, -270));
		rect.AddRange(ShapePaths.CirclePoints(w / 2 + radius * 2, radius, radius, 20, -90, -180));
		rect.Add(new(-w / 2 - radius, h / 2));
		rect.AddRange(ShapePaths.CirclePoints(w / 2, h / 2, radius, 20, 0, 90));
		rect.Add(new(-w / 2, h / 2 + radius));
		rect.Add(new(w / 2 - radius - radius / 2, h / 2 + radius));
		rect.AddRange(right);
		var group = Group("text", RoughPath(Unclosed(right)), RoughPath(Unclosed(left)), BraceBox(rect)) with
		{
			Translate = new ShapePoint(radius - radius / 4, 0),
		};
		var labelTranslate = new ShapePoint(-w / 2 + nodePadding / 2 - label.OffsetX, -h / 2 + nodePadding / 2 - label.OffsetY);
		return Finish(group, label, labelTranslate, ShapeIntersect.Polygon(rect));
	}

	/// <summary>The left brace of <c>curlyBraceLeft</c> and <c>curlyBraces</c>.</summary>
	private static List<ShapePoint> LeftBrace(double w, double h, double radius)
	{
		List<ShapePoint> points = [.. ShapePaths.CirclePoints(w / 2, -h / 2, radius, 30, -90, 0), new(-w / 2 - radius, radius)];
		points.AddRange(ShapePaths.CirclePoints(w / 2 + radius * 2, -radius, radius, 20, -180, -270));
		points.AddRange(ShapePaths.CirclePoints(w / 2 + radius * 2, radius, radius, 20, -90, -180));
		points.Add(new(-w / 2 - radius, -h / 2));
		points.AddRange(ShapePaths.CirclePoints(w / 2, h / 2, radius, 20, 0, 90));
		return points;
	}

	/// <summary>The invisible box of <c>curlyBraceLeft</c>, which edges meet.</summary>
	private static List<ShapePoint> LeftBraceBox(double w, double h, double radius)
	{
		List<ShapePoint> points = [new(w / 2, -h / 2 - radius), new(-w / 2, -h / 2 - radius)];
		points.AddRange(ShapePaths.CirclePoints(w / 2, -h / 2, radius, 20, -90, 0));
		points.Add(new(-w / 2 - radius, -radius));
		points.AddRange(ShapePaths.CirclePoints(w / 2 + w * 0.1, -radius, radius, 20, -180, -270));
		points.AddRange(ShapePaths.CirclePoints(w / 2 + w * 0.1, radius, radius, 20, -90, -180));
		points.Add(new(-w / 2 - radius, h / 2));
		points.AddRange(ShapePaths.CirclePoints(w / 2, h / 2, radius, 20, 0, 90));
		points.Add(new(-w / 2, h / 2 + radius));
		points.Add(new(w / 2, h / 2 + radius));
		return points;
	}

	private static ShapeElement BraceBox(IReadOnlyList<ShapePoint> points) =>
		RoughPath(ShapePaths.FromPoints(points)) with { Attributes = [new("stroke-opacity", "0")] };

	/// <summary>The path of the points with its <c>Z</c> taken out, as the braces' <c>replace('Z', '')</c> leaves it.</summary>
	private static string Unclosed(IReadOnlyList<ShapePoint> points) => ShapePaths.FromPoints(points)[..^1];

	public static ShapeGeometry Bang(ShapeNode node)
	{
		var label = Measure(node, node.Width);
		var halfPadding = (node.Padding ?? 0) / 2;
		var w = label.Width + 10 * halfPadding;
		var h = label.Height + 8 * halfPadding;
		var r = 0.15 * w;
		var ew = Math.Max(w, label.Width + 20);
		var eh = Math.Max(h, label.Height + 20);
		const string indent = "\n    ";
		var a = $"a{N(r)},{N(r)} 1 0,0 ";
		var small = $"a{N(r * 0.8)},{N(r * 0.8)} 1 0,0 ";
		var path = "M0 0 "
			+ $"{indent}{a}{N(ew * 0.25)},{N(-1 * eh * 0.1)}"
			+ $"{indent}{a}{N(ew * 0.25)},{N(0)}"
			+ $"{indent}{a}{N(ew * 0.25)},{N(0)}"
			+ $"{indent}{a}{N(ew * 0.25)},{N(eh * 0.1)}"
			+ $"{indent}{a}{N(ew * 0.15)},{N(eh * 0.33)}"
			+ $"{indent}{small}0,{N(eh * 0.34)}"
			+ $"{indent}{a}{N(-1 * ew * 0.15)},{N(eh * 0.33)}"
			+ $"{indent}{a}{N(-1 * ew * 0.25)},{N(eh * 0.15)}"
			+ $"{indent}{a}{N(-1 * ew * 0.25)},0"
			+ $"{indent}{a}{N(-1 * ew * 0.25)},0"
			+ $"{indent}{a}{N(-1 * ew * 0.25)},{N(-1 * eh * 0.15)}"
			+ $"{indent}{a}{N(-1 * ew * 0.1)},{N(-1 * eh * 0.33)}"
			+ $"{indent}{small}0,{N(-1 * eh * 0.34)}"
			+ $"{indent}{a}{N(ew * 0.1)},{N(-1 * eh * 0.33)}"
			+ "\n  H0 V0 Z";
		var translate = new ShapePoint(-ew / 2, -eh / 2);
		var element = node.IsHandDrawn ? RoughPath(path, translate: translate) : Path(path, LabelContainer, translate);
		return Finish(element, label, new ShapePoint(-label.Width / 2, -label.Height / 2), ShapeIntersect.Rect);
	}

	public static ShapeGeometry Cloud(ShapeNode node)
	{
		var label = Measure(node, node.Width);
		var halfPadding = (node.Padding ?? 0) / 2;
		var w = label.Width + 2 * halfPadding;
		var h = label.Height + 2 * halfPadding;
		var r1 = 0.15 * w;
		var r2 = 0.25 * w;
		var r3 = 0.35 * w;
		var r4 = 0.2 * w;
		const string indent = "\n    ";
		var path = "M0 0 "
			+ $"{indent}a{N(r1)},{N(r1)} 0 0,1 {N(w * 0.25)},{N(-1 * w * 0.1)}"
			+ $"{indent}a{N(r3)},{N(r3)} 1 0,1 {N(w * 0.4)},{N(-1 * w * 0.1)}"
			+ $"{indent}a{N(r2)},{N(r2)} 1 0,1 {N(w * 0.35)},{N(w * 0.2)}"
			+ $"{indent}a{N(r1)},{N(r1)} 1 0,1 {N(w * 0.15)},{N(h * 0.35)}"
			+ $"{indent}a{N(r4)},{N(r4)} 1 0,1 {N(-1 * w * 0.15)},{N(h * 0.65)}"
			+ $"{indent}a{N(r2)},{N(r1)} 1 0,1 {N(-1 * w * 0.25)},{N(w * 0.15)}"
			+ $"{indent}a{N(r3)},{N(r3)} 1 0,1 {N(-1 * w * 0.5)},0"
			+ $"{indent}a{N(r1)},{N(r1)} 1 0,1 {N(-1 * w * 0.25)},{N(-1 * w * 0.15)}"
			+ $"{indent}a{N(r1)},{N(r1)} 1 0,1 {N(-1 * w * 0.1)},{N(-1 * h * 0.35)}"
			+ $"{indent}a{N(r4)},{N(r4)} 1 0,1 {N(w * 0.1)},{N(-1 * h * 0.65)}"
			+ "\n  H0 V0 Z";
		var translate = new ShapePoint(-w / 2, -h / 2);
		var element = node.IsHandDrawn ? RoughPath(path, translate: translate) : Path(path, LabelContainer, translate);
		return Finish(element, label, new ShapePoint(-label.Width / 2, -label.Height / 2), ShapeIntersect.Rect);
	}

	public static ShapeGeometry Browser(ShapeNode node)
	{
		const double barHeight = 18, radius = 12;
		var label = Measure(node, node.Width);
		var padding = node.Padding ?? 12;
		var w = Math.Max(Math.Max(label.Width + padding * 2, node.Width ?? 0), 90);
		var h = Math.Max(label.Height + padding * 2 + barHeight, node.Height ?? 0);
		var top = -h / 2;
		List<ShapeElement> children =
		[
			node.IsHandDrawn ? RoughPath(ShapePaths.RoundedRect(-w / 2, top, w, h, radius)) : Rect(-w / 2, top, w, h, null, radius),
			new ShapeElement { Kind = ShapeElementKind.Line, X = -w / 2, Y = top + barHeight, Width = w / 2, Height = top + barHeight },
		];
		for (var i = 0; i < 3; i++)
		{
			children.Add(Circle(-w / 2 + 12 + i * 9, top + barHeight / 2, 2.5));
		}

		children.Add(Rect(-w / 2 + 44, top + 4, Math.Max(w - 56, 10), barHeight - 8, "browser-address-bar", 3));
		var group = Group(LabelContainer, [.. children]);
		var bodyCenterY = top + barHeight + (h - barHeight) / 2;
		return Finish(group, label, CenteredLabel(label, 0, bodyCenterY), ShapeIntersect.Rect);
	}

	public static ShapeGeometry Bucket(ShapeNode node)
	{
		var label = Measure(node, node.Width);
		var padding = node.Padding ?? 12;
		var w = Math.Max(Math.Max(label.Width + padding * 2, node.Width ?? 0), 80);
		var rimRy = Math.Max(Math.Min(w * 0.08, 12), 5);
		var totalHeight = Math.Max(label.Height + padding * 2 + rimRy, node.Height ?? 0);
		var topY = -totalHeight / 2 + rimRy;
		var bottomY = totalHeight / 2;
		var bottomW = w * 0.72;
		var body = string.Join(
			' ',
			$"M{N(-w / 2)},{N(topY)}",
			$"L{N(-bottomW / 2)},{N(bottomY)}",
			$"A{N(bottomW / 2)},{N(rimRy)} 0 0 0 {N(bottomW / 2)},{N(bottomY)}",
			$"L{N(w / 2)},{N(topY)}",
			$"A{N(w / 2)},{N(rimRy)} 0 0 0 {N(-w / 2)},{N(topY)}",
			"Z");
		var rim = new ShapeElement { Kind = ShapeElementKind.Ellipse, X = 0, Y = topY, Rx = w / 2, Ry = rimRy };
		var group = Group(LabelContainer, node.IsHandDrawn ? RoughPath(body) : Path(body), rim);
		var bodyCenterY = topY + (bottomY - topY) / 2;
		const int segments = 12;
		List<ShapePoint> outline = [];
		for (var i = 0; i <= segments; i++)
		{
			var theta = Math.PI - i * Math.PI / segments;
			outline.Add(new ShapePoint(w / 2 * Math.Cos(theta), topY - rimRy * Math.Sin(theta)));
		}

		for (var i = segments; i >= 0; i--)
		{
			var theta = Math.PI - i * Math.PI / segments;
			outline.Add(new ShapePoint(bottomW / 2 * Math.Cos(theta), bottomY + rimRy * Math.Sin(theta)));
		}

		return Finish(group, label, CenteredLabel(label, 0, bodyCenterY), ShapeIntersect.Polygon(outline));
	}

	public static ShapeGeometry ConsoleWindow(ShapeNode node)
	{
		const double glyphBand = 20, radius = 12;
		var label = Measure(node, node.Width);
		var padding = node.Padding ?? 12;
		var w = Math.Max(Math.Max(label.Width + padding * 2, node.Width ?? 0), 90);
		var h = Math.Max(label.Height + padding * 2 + glyphBand, node.Height ?? 0);
		var top = -h / 2;
		var body = node.IsHandDrawn ? RoughPath(ShapePaths.RoundedRect(-w / 2, top, w, h, radius)) : Rect(-w / 2, top, w, h, null, radius);
		var glyph = new ShapeElement { Kind = ShapeElementKind.Text, X = -w / 2 + 12, Y = top + 16, Class = "console-glyph", Text = ">_" };
		var group = Group(LabelContainer, body, glyph);
		var bodyCenterY = top + glyphBand + (h - glyphBand) / 2;
		return Finish(group, label, CenteredLabel(label, 0, bodyCenterY), ShapeIntersect.Rect);
	}

	public static ShapeGeometry Folder(ShapeNode node)
	{
		var label = Measure(node, node.Width);
		var padding = node.Padding ?? 12;
		var w = Math.Max(Math.Max(label.Width + padding * 2, node.Width ?? 0), 90);
		var contentHeight = label.Height + padding * 2;
		var tabHeight = Math.Max(Math.Min(contentHeight * 0.16, 14), 8);
		var totalHeight = Math.Max(contentHeight + tabHeight, node.Height ?? 0);
		var bodyHeight = totalHeight - tabHeight;
		var tabWidth = Math.Max(w * 0.38, 28);
		var top = -totalHeight / 2;
		ShapePoint[] points =
		[
			new(-w / 2, top),
			new(-w / 2 + tabWidth, top),
			new(-w / 2 + tabWidth, top + tabHeight),
			new(w / 2, top + tabHeight),
			new(w / 2, totalHeight / 2),
			new(-w / 2, totalHeight / 2),
		];
		var path = ShapePaths.FromPoints(points);
		var element = node.IsHandDrawn ? RoughPath(path, LabelContainer) : Path(path, LabelContainer);
		return Finish(element, label, CenteredLabel(label, 0, top + tabHeight + bodyHeight / 2), ShapeIntersect.Polygon(points));
	}

	public static ShapeGeometry Person(ShapeNode node)
	{
		var label = Measure(node, node.Width);
		var padding = node.Padding ?? 20;
		var w = Math.Max(Math.Max(label.Width + padding * 2, node.Width ?? 0), 100);
		var headRadius = Math.Min(Math.Max(w * 0.23, 16), 56);
		var overlap = headRadius * 0.27;
		var bodyHeight = Math.Max(label.Height + padding * 2, Truthy(node.Height) ? node.Height!.Value - (2 * headRadius - overlap) : 0);
		var bodyRadius = Math.Min(w * 0.177, bodyHeight * 0.45);
		var totalHeight = bodyHeight + 2 * headRadius - overlap;
		var top = -totalHeight / 2;
		var bodyTop = top + 2 * headRadius - overlap;
		var group = node.IsHandDrawn
			? Group(LabelContainer, RoughPath(ShapePaths.RoundedRect(-w / 2, bodyTop, w, bodyHeight, bodyRadius)), Circle(0, top + headRadius, headRadius, rough: true))
			: Group(LabelContainer, Rect(-w / 2, bodyTop, w, bodyHeight, null, bodyRadius), Circle(0, top + headRadius, headRadius));
		var headCenterY = top + headRadius;
		var phi = Math.Asin(Math.Min(1, (bodyTop - headCenterY) / headRadius)) * 180 / Math.PI;
		List<ShapePoint> outline = [.. ShapePaths.CirclePoints(0, -headCenterY, headRadius, 24, 180 + phi, -phi)];
		outline.AddRange(ShapePaths.CirclePoints(-(-w / 2 + bodyRadius), -(bodyTop + bodyRadius), bodyRadius, 12, 90, 0));
		outline.AddRange(ShapePaths.CirclePoints(-(-w / 2 + bodyRadius), -(totalHeight / 2 - bodyRadius), bodyRadius, 12, 360, 270));
		outline.AddRange(ShapePaths.CirclePoints(-(w / 2 - bodyRadius), -(totalHeight / 2 - bodyRadius), bodyRadius, 12, 270, 180));
		outline.AddRange(ShapePaths.CirclePoints(-(w / 2 - bodyRadius), -(bodyTop + bodyRadius), bodyRadius, 12, 180, 90));
		return Finish(group, label, CenteredLabel(label, 0, bodyTop + bodyHeight / 2), ShapeIntersect.Polygon(outline));
	}
}
