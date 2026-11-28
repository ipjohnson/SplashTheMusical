// Upstream: packages/mermaid/src/rendering-util/rendering-elements/shapes/composite.ts
// Upstream: packages/mermaid/src/rendering-util/rendering-elements/shapes/blockArrow.ts
using SplashTheMusical.Rendering.Shapes;

namespace SplashTheMusical.Diagrams.Block;

/// <summary>
/// The two shapes of mermaid.js's <c>shapes</c> map only the block diagram draws: <c>composite</c>, the frame of a
/// nested block, and <c>block_arrow</c>. Each is sized from its label until the grid has placed it, and from the size
/// the grid gave it after.
/// </summary>
internal static class BlockShapes
{
	private static readonly string[] DirectionOrder = ["right", "left", "up", "down"];

	/// <summary>
	/// <c>composite</c>: a rect of the label's size plus the padding, or of the node's own size once it is
	/// <paramref name="positioned"/>, with the label where <c>labelHelper</c> puts it.
	/// </summary>
	public static ShapeGeometry Composite(ShapeNode node, bool positioned)
	{
		var label = node.Label.Measure(node.Width, false);
		var nodePadding = node.Padding ?? 0;
		var halfPadding = nodePadding / 2;
		var totalWidth = positioned ? node.Width ?? 0 : label.Width + nodePadding;
		var totalHeight = positioned ? node.Height ?? 0 : label.Height + nodePadding;
		var x = positioned ? -totalWidth / 2 : (-label.Width / 2) - halfPadding;
		var y = positioned ? -totalHeight / 2 : (-label.Height / 2) - halfPadding;
		var rect = new ShapeElement
		{
			Kind = ShapeElementKind.Rect,
			Class = "basic cluster composite label-container",
			X = x,
			Y = y,
			Width = totalWidth,
			Height = totalHeight,
			Rx = node.Radius ?? 0,
			Ry = node.Radius ?? 0,
		};
		return Finish(rect, label, ShapeIntersect.Rect);
	}

	/// <summary>
	/// <c>block_arrow</c>: the polygon of <see cref="ArrowPoints"/> around the label, as wide as the label and the two
	/// arrowheads, or as wide as the node when it spans more than one column and the grid made it wider.
	/// </summary>
	public static ShapeGeometry BlockArrow(ShapeNode node, IReadOnlyList<string> directions, bool positioned, int widthInColumns)
	{
		var label = node.Label.Measure(node.Width, false);
		var nodePadding = node.Padding ?? 0;
		var height = label.Height + (2 * nodePadding);
		var midpoint = height / 2;
		var naturalWidth = label.Width + (2 * midpoint) + nodePadding;
		var nodeWidth = node.Width ?? 0;
		var spanning = positioned && widthInColumns > 1 && nodeWidth > naturalWidth;
		var width = spanning ? nodeWidth : naturalWidth;
		var points = ArrowPoints(directions, label.Width, label.Height, nodePadding, width);
		var polygon = new ShapeElement
		{
			Kind = ShapeElementKind.Polygon,
			Class = "label-container",
			Points = points,
			Translate = new ShapePoint(-width / 2, height / 2),
		};
		return Finish(polygon, label, ShapeIntersect.Polygon(points));
	}

	/// <summary>
	/// <c>getArrowPoints</c>: the outline for the set of directions, <c>x</c> read as right and left and <c>y</c> as up and
	/// down. A direction it does not know, such as <c>" right"</c> with the space the lexer leaves, adds nothing, and
	/// a set it has no outline for is a single point.
	/// </summary>
	internal static ShapePoint[] ArrowPoints(IReadOnlyList<string> directions, double labelWidth, double labelHeight, double nodePadding, double? totalWidth)
	{
		var set = new HashSet<string>(StringComparer.Ordinal);
		foreach (var direction in directions)
		{
			switch (direction)
			{
				case "x":
					set.Add("right");
					set.Add("left");
					break;
				case "y":
					set.Add("up");
					set.Add("down");
					break;
				default:
					set.Add(direction);
					break;
			}
		}

		var key = string.Join('|', DirectionOrder.Where(set.Contains));
		var p = nodePadding / 2;
		var h = labelHeight + (4 * p);
		var m = h / 2;
		var w = totalWidth ?? labelWidth + (2 * m) + (2 * p);
		return key switch
		{
			"right|left|up|down" =>
			[
				new(0, 0), new(m, 0), new(w / 2, 2 * p), new(w - m, 0), new(w, 0), new(w, -h / 3), new(w + (2 * p), -h / 2),
				new(w, -2 * h / 3), new(w, -h), new(w - m, -h), new(w / 2, -h - (2 * p)), new(m, -h), new(0, -h),
				new(0, -2 * h / 3), new(-2 * p, -h / 2), new(0, -h / 3),
			],
			"right|left|up" => [new(m, 0), new(w - m, 0), new(w, -h / 2), new(w - m, -h), new(m, -h), new(0, -h / 2)],
			"right|left|down" => [new(0, 0), new(m, -h), new(w - m, -h), new(w, 0)],
			"right|up|down" => [new(0, 0), new(w, -m), new(w, -h + m), new(0, -h)],
			"left|up|down" => [new(w, 0), new(0, -m), new(0, -h + m), new(w, -h)],
			"right|left" =>
			[
				new(m, 0), new(m, -p), new(w - m, -p), new(w - m, 0), new(w, -h / 2), new(w - m, -h), new(w - m, -h + p),
				new(m, -h + p), new(m, -h), new(0, -h / 2),
			],
			"up|down" =>
			[
				new(w / 2, 0), new(0, -p), new(m, -p), new(m, -h + p), new(0, -h + p), new(w / 2, -h), new(w, -h + p),
				new(w - m, -h + p), new(w - m, -p), new(w, -p),
			],
			"right|up" => [new(0, 0), new(w, -m), new(0, -h)],
			"right|down" => [new(0, 0), new(w, 0), new(0, -h)],
			"left|up" => [new(w, 0), new(0, -m), new(w, -h)],
			"left|down" => [new(w, 0), new(0, 0), new(w, -h)],
			"right" =>
			[
				new(m, -p), new(m, -p), new(w - m, -p), new(w - m, 0), new(w, -h / 2), new(w - m, -h), new(w - m, -h + p),
				new(m, -h + p), new(m, -h + p),
			],
			"left" => [new(m, 0), new(m, -p), new(w - m, -p), new(w - m, -h + p), new(m, -h + p), new(m, -h), new(0, -h / 2)],
			"up" => [new(m, -p), new(m, -h + p), new(0, -h + p), new(w / 2, -h), new(w, -h + p), new(w - m, -h + p), new(w - m, -p)],
			"down" => [new(w / 2, 0), new(0, -p), new(m, -p), new(m, -h + p), new(w - m, -h + p), new(w - m, -p), new(w, -p)],
			_ => [new(0, 0)],
		};
	}

	/// <summary>
	/// The geometry with the label at <c>labelHelper</c>'s translate, sized by the box of the outline and the label, as
	/// <c>getBBox</c> sizes the node's group.
	/// </summary>
	private static ShapeGeometry Finish(ShapeElement element, ShapeLabel label, ShapeIntersect intersect)
	{
		var translate = label.Html ? new ShapePoint(-label.Width / 2, -label.Height / 2) : new ShapePoint(0, -label.Height / 2);
		var bounds = element.Bounds().Offset(element.Translate);
		if (label.Width > 0 || label.Height > 0)
		{
			var x = translate.X + label.OffsetX;
			var y = translate.Y + label.OffsetY;
			bounds = bounds.Union(new ShapeBounds(x, y, x + label.Width, y + label.Height));
		}

		return new ShapeGeometry
		{
			Width = bounds.Width,
			Height = bounds.Height,
			Bounds = bounds,
			Elements = [element],
			Label = label,
			LabelTranslate = translate,
			Intersect = intersect,
		};
	}
}
