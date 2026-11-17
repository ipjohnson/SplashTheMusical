namespace SplashTheMusical.Rendering.Shapes;

// Upstream: packages/mermaid/src/rendering-util/rendering-elements/shapes/{cylinder,linedCylinder,tiltedCylinder,
// waveEdgedRectangle,multiWaveEdgedRectangle,linedWaveEdgedRect,taggedWaveEdgedRectangle,waveRectangle}.ts
internal static partial class ShapeDrawings
{
	public static ShapeGeometry Cylinder(ShapeNode node)
	{
		var nodePadding = node.Padding ?? 0;
		var labelPaddingX = node.IsNeo ? 24 : nodePadding;
		var labelPaddingY = node.IsNeo ? 24 : nodePadding;
		var originalWidth = node.Width ?? 0;
		var width = node.Width;
		var height = node.Height;
		if (Truthy(width))
		{
			width = Math.Max(width!.Value - labelPaddingY, 8);
		}

		if (Truthy(height))
		{
			var rx0 = originalWidth / 2;
			var ry0 = rx0 / (2.5 + originalWidth / 50);
			height = Math.Max(height!.Value - labelPaddingX - ry0 * 3, 8);
		}

		var label = Measure(node, width);
		var w = Math.Max(width ?? 0, label.Width) + labelPaddingY;
		var rx = w / 2;
		var ry = rx / (2.5 + w / 50);
		var h = Math.Max(height ?? 0, label.Height) + labelPaddingX + ry;
		var translate = new ShapePoint(-w / 2, -(h / 2 + ry));
		List<KeyValuePair<string, string>> offset = [new("label-offset-y", N(ry))];
		ShapeElement cylinder;
		IReadOnlyList<ShapeElement> elements;
		if (node.IsHandDrawn)
		{
			var outer = string.Join(' ', $"M{N(0)},{N(ry)}", $"M{N(w)},{N(ry)}", $"a{N(rx)},{N(ry)} 0,0,0 {N(-w)},0", $"l0,{N(h)}", $"a{N(rx)},{N(ry)} 0,0,0 {N(w)},0", $"l0,{N(-h)}");
			var inner = string.Join(' ', $"M{N(-w / 2)},{N(-h / 2)}", $"a{N(rx)},{N(ry)} 0,0,0 {N(w)},0");
			cylinder = RoughPath(outer, LabelContainer, translate) with { Attributes = offset };
			elements = [cylinder, RoughPath(inner)];
		}
		else
		{
			cylinder = Path(CylinderPath(w, h, rx, ry), OuterPath, translate) with { Attributes = offset };
			elements = [cylinder];
		}

		var labelTranslate = new ShapePoint(-(label.Width / 2) - label.OffsetX, -(label.Height / 2) + nodePadding / 1.5 - label.OffsetY);
		return Finish(elements, label, labelTranslate, ShapeIntersect.Cylinder(rx, ry));
	}

	/// <summary><c>createCylinderPathD</c> at the origin: the top ellipse, the sides and the bottom arc.</summary>
	private static string CylinderPath(double w, double h, double rx, double ry) =>
		string.Join(' ', $"M{N(0)},{N(ry)}", $"a{N(rx)},{N(ry)} 0,0,0 {N(w)},0", $"a{N(rx)},{N(ry)} 0,0,0 {N(-w)},0", $"l0,{N(h)}", $"a{N(rx)},{N(ry)} 0,0,0 {N(w)},0", $"l0,{N(-h)}");

	public static ShapeGeometry LinedCylinder(ShapeNode node)
	{
		var nodePadding = node.Padding ?? 0;
		var labelPaddingX = node.IsNeo ? 16 : nodePadding;
		var labelPaddingY = node.IsNeo ? 24 : nodePadding;
		var width = node.Width;
		var height = node.Height;
		if (Truthy(width) || Truthy(height))
		{
			var originalWidth = width ?? 0;
			width = Math.Max(originalWidth - labelPaddingX, 10);
			var rx0 = originalWidth / 2;
			var ry0 = rx0 / (2.5 + originalWidth / 50);
			height = Math.Max((height ?? 0) - labelPaddingY - ry0 * 3, 10);
		}

		var label = Measure(node, width);
		var w = OrElse(width, label.Width) + labelPaddingX * 2;
		var rx = w / 2;
		var ry = rx / (2.5 + w / 50);
		var h = OrElse(height, label.Height) + ry + labelPaddingY * 2;
		var outerOffset = h * 0.1;
		var translate = new ShapePoint(-w / 2, -(h / 2 + ry));
		List<KeyValuePair<string, string>> offset = [new("label-offset-y", N(ry))];
		var rim = $" M{N(0)},{N(ry + outerOffset)} a{N(rx)},{N(ry)} 0,0,0 {N(w)},0";
		ShapeElement cylinder;
		IReadOnlyList<ShapeElement> elements;
		if (node.IsHandDrawn)
		{
			var outer = string.Join(' ', $"M{N(0)},{N(ry)}", $"M{N(w)},{N(ry)}", $"a{N(rx)},{N(ry)} 0,0,0 {N(-w)},0", $"l0,{N(h)}", $"a{N(rx)},{N(ry)} 0,0,0 {N(w)},0", $"l0,{N(-h)}") + rim;
			var inner = string.Join(' ', $"M{N(-w / 2)},{N(-h / 2)}", $"a{N(rx)},{N(ry)} 0,0,0 {N(w)},0");
			cylinder = RoughPath(outer, LabelContainer, translate) with { Attributes = offset };
			elements = [cylinder, RoughPath(inner, "line")];
		}
		else
		{
			cylinder = Path(CylinderPath(w, h, rx, ry) + rim, OuterPath, translate) with { Attributes = offset };
			elements = [cylinder];
		}

		var labelTranslate = new ShapePoint(-(label.Width / 2) - label.OffsetX, -(label.Height / 2) + ry - label.OffsetY);
		return Finish(elements, label, labelTranslate, ShapeIntersect.Cylinder(rx, ry));
	}

	public static ShapeGeometry TiltedCylinder(ShapeNode node)
	{
		var labelPadding = node.IsNeo ? 12 : (node.Padding ?? 0) / 2;
		var originalHeight = node.Height ?? 0;
		var width = node.Width;
		var height = node.Height;
		if (Truthy(height))
		{
			height = Math.Max(height!.Value - labelPadding, 5);
		}

		if (Truthy(width))
		{
			var ry0 = originalHeight / 2;
			var rx0 = ry0 / (2.5 + originalHeight / 50);
			width = Math.Max(width!.Value - labelPadding - rx0 * 3, 10);
		}

		var label = Measure(node, width);
		var h = Math.Max(height ?? 0, label.Height) + labelPadding;
		var ry = h / 2;
		var rx = ry / (2.5 + h / 50);
		var w = Math.Max(width ?? 0, label.Width) + rx + labelPadding;
		var translate = new ShapePoint(-w / 2, h / 2);
		List<KeyValuePair<string, string>> offset = [new("label-offset-x", N(rx))];
		ShapeElement cylinder;
		IReadOnlyList<ShapeElement> elements;
		if (node.IsHandDrawn)
		{
			var outer = string.Join(' ', $"M{N(0)},{N(0)}", $"M{N(w)},{N(0)}", $"a{N(rx)},{N(ry)} 0,0,0 {N(0)},{N(-h)}", $"l{N(-w)},0", $"a{N(rx)},{N(ry)} 0,0,0 {N(0)},{N(h)}", $"l{N(w)},0");
			var inner = string.Join(' ', $"M{N(w / 2)},{N(-h / 2)}", $"a{N(rx)},{N(ry)} 0,0,0 0,{N(h)}");
			cylinder = RoughPath(outer, LabelContainer, translate) with { Attributes = offset };
			elements = [cylinder, RoughPath(inner)];
		}
		else
		{
			const string indent = "\n    ";
			var path = $"M{N(0)},{N(0)}"
				+ $"{indent}a{N(rx)},{N(ry)} 0,0,1 {N(0)},{N(-h)}"
				+ $"{indent}l{N(w)},{N(0)}"
				+ $"{indent}a{N(rx)},{N(ry)} 0,0,1 {N(0)},{N(h)}"
				+ $"{indent}M{N(w)},{N(-h)}"
				+ $"{indent}a{N(rx)},{N(ry)} 0,0,0 {N(0)},{N(h)}"
				+ $"{indent}l{N(-w)},{N(0)}";
			cylinder = Path(path, OuterPath, translate) with { Attributes = offset };
			elements = [cylinder];
		}

		var labelTranslate = new ShapePoint(-(label.Width / 2) - rx - label.OffsetX, -(label.Height / 2) - label.OffsetY);
		return Finish(elements, label, labelTranslate, ShapeIntersect.TiltedCylinder(rx, ry));
	}

	public static ShapeGeometry WaveEdgedRectangle(ShapeNode node)
	{
		var nodePadding = node.Padding ?? 0;
		var labelPaddingX = node.IsNeo ? 16 : nodePadding;
		var labelPaddingY = node.IsNeo ? 12 : nodePadding;
		var adjustFinalHeight = true;
		var width = node.Width;
		var height = node.Height;
		if (Truthy(width) || Truthy(height))
		{
			adjustFinalHeight = false;
			width = Math.Max((width ?? 0) - labelPaddingX * 2, 10);
			height = Math.Max((height ?? 0) - labelPaddingY * 2, 10);
		}

		var label = Measure(node, width);
		var w = OrElse(width, label.Width) + labelPaddingX * 2;
		var h = OrElse(height, label.Height) + labelPaddingY * 2;
		var waveAmplitude = node.IsNeo ? h / 4 : h / 8;
		var finalH = h + (adjustFinalHeight ? waveAmplitude : -waveAmplitude);
		var widthDifference = 14 - w;
		var extraW = widthDifference > 0 ? widthDifference / 2 : 0;
		List<ShapePoint> points = [new(-w / 2 - extraW, finalH / 2)];
		points.AddRange(ShapePaths.SineWave(-w / 2 - extraW, finalH / 2, w / 2 + extraW, finalH / 2, waveAmplitude, 0.8));
		points.Add(new(w / 2 + extraW, -finalH / 2));
		points.Add(new(-w / 2 - extraW, -finalH / 2));
		var element = RoughPath(ShapePaths.FromPoints(points), OuterPath, new ShapePoint(0, -waveAmplitude / 2));
		var labelTranslate = new ShapePoint(-w / 2 + nodePadding - label.OffsetX, -h / 2 + nodePadding - waveAmplitude - label.OffsetY);
		return Finish(element, label, labelTranslate, ShapeIntersect.Polygon(points));
	}

	public static ShapeGeometry MultiWaveEdgedRectangle(ShapeNode node)
	{
		var label = Measure(node, node.Width);
		var nodePadding = node.Padding ?? 0;
		var labelPaddingX = node.IsNeo ? 16 : nodePadding;
		var labelPaddingY = node.IsNeo ? 12 : nodePadding;
		var adjustFinalHeight = true;
		var width = node.Width;
		var height = node.Height;
		if (Truthy(width) || Truthy(height))
		{
			adjustFinalHeight = false;
			width = (width ?? 0) - labelPaddingX * 2;
			height = (height ?? 0) - labelPaddingY * 3;
		}

		var w = Math.Max(label.Width, width ?? 0) + labelPaddingX * 2;
		var h = Math.Max(label.Height, height ?? 0) + labelPaddingY * 3;
		var waveAmplitude = node.IsNeo ? h / 4 : h / 8;
		var finalH = h + (adjustFinalHeight ? waveAmplitude / 2 : -waveAmplitude / 2);
		var x = -w / 2;
		var y = -finalH / 2;
		const double rectOffset = 10;
		var wave = ShapePaths.SineWave(x - rectOffset, y + finalH + rectOffset, x + w - rectOffset, y + finalH + rectOffset, waveAmplitude, 0.8);
		var last = wave[^1];
		List<ShapePoint> outer = [new(x - rectOffset, y + rectOffset), new(x - rectOffset, y + finalH + rectOffset)];
		outer.AddRange(wave);
		outer.AddRange(
		[
			new(x + w - rectOffset, last.Y - rectOffset),
			new(x + w, last.Y - rectOffset),
			new(x + w, last.Y - 2 * rectOffset),
			new(x + w + rectOffset, last.Y - 2 * rectOffset),
			new(x + w + rectOffset, y - rectOffset),
			new(x + rectOffset, y - rectOffset),
			new(x + rectOffset, y),
			new(x, y),
			new(x, y + rectOffset),
		]);
		ShapePoint[] inner =
		[
			new(x, y + rectOffset),
			new(x + w - rectOffset, y + rectOffset),
			new(x + w - rectOffset, last.Y - rectOffset),
			new(x + w, last.Y - rectOffset),
			new(x + w, y),
			new(x, y),
		];
		var group = Group(OuterPath, RoughPath(ShapePaths.FromPoints(outer)), RoughPath(ShapePaths.FromPoints(inner))) with
		{
			Translate = new ShapePoint(0, -waveAmplitude / 2),
		};
		var labelTranslate = new ShapePoint(-(label.Width / 2) - rectOffset - label.OffsetX, -(label.Height / 2) + rectOffset - waveAmplitude - label.OffsetY);
		return Finish(group, label, labelTranslate, ShapeIntersect.Polygon(outer));
	}

	public static ShapeGeometry LinedWaveEdgedRect(ShapeNode node)
	{
		var nodePadding = node.Padding ?? 0;
		var labelPaddingX = node.IsNeo ? 16 : nodePadding;
		var labelPaddingY = node.IsNeo ? 12 : nodePadding;
		var width = node.Width;
		var height = node.Height;
		if (Truthy(width) || Truthy(height))
		{
			width = Math.Max((width ?? 0) * 10 / 11 - labelPaddingX * 2, 10);
			height = Math.Max((height ?? 0) - labelPaddingY * 2, 10);
		}

		var label = Measure(node, width);
		var w = OrElse(width, label.Width) + labelPaddingX * 2;
		var h = OrElse(height, label.Height) + labelPaddingY * 2;
		var waveAmplitude = node.IsNeo ? h / 4 : h / 8;
		var finalH = h + waveAmplitude;
		var left = -w / 2 - w / 2 * 0.1;
		var right = w / 2 + w / 2 * 0.1;
		List<ShapePoint> points = [new(left, -finalH / 2), new(left, finalH / 2)];
		points.AddRange(ShapePaths.SineWave(left, finalH / 2, right, finalH / 2, waveAmplitude, 0.8));
		points.AddRange(
		[
			new(right, -finalH / 2),
			new(left, -finalH / 2),
			new(-w / 2, -finalH / 2),
			new(-w / 2, finalH / 2 * 1.1),
			new(-w / 2, -finalH / 2),
		]);
		var element = RoughPolygon(points, OuterPath) with { Translate = new ShapePoint(0, -waveAmplitude / 2) };
		var labelTranslate = new ShapePoint(
			-w / 2 + nodePadding + w / 2 * 0.1 / 2 - label.OffsetX,
			-h / 2 + nodePadding - waveAmplitude - label.OffsetY);
		return Finish(element, label, labelTranslate, ShapeIntersect.Polygon(points));
	}

	public static ShapeGeometry TaggedWaveEdgedRectangle(ShapeNode node)
	{
		var nodePadding = node.Padding ?? 0;
		var label = Measure(node, node.Width);
		var w = Math.Max(label.Width + nodePadding * 2, node.Width ?? 0);
		var h = Math.Max(label.Height + nodePadding * 2, node.Height ?? 0);
		var waveAmplitude = h / 8;
		var tagWidth = 0.2 * w;
		var tagHeight = 0.2 * h;
		var finalH = h + waveAmplitude;
		var left = -w / 2 - w / 2 * 0.1;
		var right = w / 2 + w / 2 * 0.1;
		List<ShapePoint> points = [new(left, finalH / 2)];
		points.AddRange(ShapePaths.SineWave(left, finalH / 2, right, finalH / 2, waveAmplitude, 0.8));
		points.Add(new(right, -finalH / 2));
		points.Add(new(left, -finalH / 2));
		var x = -w / 2 + w / 2 * 0.1;
		var y = -finalH / 2 - tagHeight * 0.4;
		List<ShapePoint> tag = [new(x + w - tagWidth, (y + h) * 1.3), new(x + w, y + h - tagHeight), new(x + w, (y + h) * 0.9)];
		tag.AddRange(ShapePaths.SineWave(x + w, (y + h) * 1.25, x + w - tagWidth, (y + h) * 1.3, -h * 0.02, 0.5));
		var group = Group(OuterPath, RoughPath(ShapePaths.FromPoints(points)), RoughPath(ShapePaths.FromPoints(tag))) with
		{
			Translate = new ShapePoint(0, -waveAmplitude / 2),
		};
		var labelTranslate = new ShapePoint(-w / 2 + nodePadding - label.OffsetX, -h / 2 + nodePadding - waveAmplitude / 2 - label.OffsetY);
		return Finish(group, label, labelTranslate, ShapeIntersect.Polygon(points));
	}

	public static ShapeGeometry WaveRectangle(ShapeNode node)
	{
		var nodePadding = node.Padding ?? 0;
		var labelPaddingX = node.IsNeo ? 16 : nodePadding;
		var labelPaddingY = node.IsNeo ? 20 : nodePadding;
		var width = node.Width;
		var height = node.Height;
		if (Truthy(width) || Truthy(height))
		{
			width = Math.Max(width ?? 0, 20);
			height = Math.Max(height ?? 0, 10);
			var amplitude = Math.Min(height.Value * 0.2, height.Value / 4);
			height = Math.Ceiling(height.Value - labelPaddingY - amplitude * (20.0 / 9));
			width = width.Value - labelPaddingX * 2;
		}

		var label = Measure(node, width);
		var w = OrElse(width, label.Width) + labelPaddingX * 2;
		var h = OrElse(height, label.Height) + labelPaddingY;
		var waveAmplitude = h / 8;
		var finalH = h + waveAmplitude * 2;
		List<ShapePoint> points = [new(-w / 2, finalH / 2)];
		points.AddRange(ShapePaths.SineWave(-w / 2, finalH / 2, w / 2, finalH / 2, waveAmplitude, 1));
		points.Add(new(w / 2, -finalH / 2));
		points.AddRange(ShapePaths.SineWave(w / 2, -finalH / 2, -w / 2, -finalH / 2, waveAmplitude, -1));
		var element = RoughPath(ShapePaths.FromPoints(points), LabelContainer);
		return Finish(element, label, LabelHelperTranslate(label), ShapeIntersect.Polygon(points));
	}
}
