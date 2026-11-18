namespace SplashTheMusical.Rendering.Shapes;

// Upstream: packages/mermaid/src/rendering-util/rendering-elements/shapes/{icon,iconCircle,iconRounded,iconSquare,
// imageSquare}.ts
internal static partial class ShapeDrawings
{
	/// <summary>The icon's picture, with no frame: the icon's square and the label above or below it.</summary>
	public static ShapeGeometry Icon(ShapeNode node)
	{
		var iconSize = IconSize(node);
		var label = Measure(node, Math.Max(iconSize, node.WrappingWidth ?? 0));
		var frame = Rect(-iconSize / 2, -iconSize / 2, iconSize, iconSize, null, rough: true) with { Attributes = Hidden };
		return Pictured(node, label, frame, iconSize, iconSize, iconSize);
	}

	/// <summary>The icon in a circle the square root of 2 times its size and 20 more on each side.</summary>
	public static ShapeGeometry IconCircle(ShapeNode node)
	{
		const double padding = 20;
		var iconSize = IconSize(node);
		var label = Measure(node, Math.Max(iconSize, node.WrappingWidth ?? 0));
		var diameter = iconSize * Math.Sqrt(2) + padding * 2;
		var circle = Circle(0, 0, diameter / 2, rough: true);
		return Pictured(node, label, circle, diameter, diameter, iconSize);
	}

	/// <summary>The icon in a square with corners of radius 5, the node's padding wider than the icon.</summary>
	public static ShapeGeometry IconRounded(ShapeNode node) => FramedIcon(node, 5);

	/// <summary>The icon in a square with corners of radius 0.1, the node's padding wider than the icon.</summary>
	public static ShapeGeometry IconSquare(ShapeNode node) => FramedIcon(node, 0.1);

	/// <summary>
	/// The image at its <c>w</c> and <c>h</c>, or its natural size. With a label, it is at least
	/// <c>flowchart.wrappingWidth</c> wide. <c>constraint: on</c> keeps the image's aspect ratio.
	/// </summary>
	public static ShapeGeometry ImageSquare(ShapeNode node)
	{
		var naturalWidth = node.ImageNaturalWidth;
		var naturalHeight = node.ImageNaturalHeight;
		var aspectRatio = naturalWidth / naturalHeight;
		var defaultWidth = node.WrappingWidth ?? 0;
		var rawWidth = Math.Max(node.HasLabel ? defaultWidth : 0, node.AssetWidth ?? naturalWidth);
		var constrained = node.Constraint == "on";
		var imageWidth = constrained && Truthy(node.AssetHeight) ? node.AssetHeight!.Value * aspectRatio : rawWidth;
		var imageHeight = constrained ? imageWidth / aspectRatio : node.AssetHeight ?? naturalHeight;
		var label = Measure(node, Math.Max(imageWidth, defaultWidth));
		var frame = Rect(-imageWidth / 2, -imageHeight / 2, imageWidth, imageHeight, null, rough: true);
		return Pictured(node, label, frame, imageWidth, imageHeight, null);
	}

	private static IReadOnlyList<KeyValuePair<string, string>> Hidden { get; } = [new("stroke-opacity", "0"), new("fill-opacity", "0")];

	private static double IconSize(ShapeNode node) => Math.Max(node.AssetHeight ?? 48, node.AssetWidth ?? 48);

	private static ShapeGeometry FramedIcon(ShapeNode node, double radius)
	{
		var iconSize = IconSize(node);
		var label = Measure(node, Math.Max(iconSize, node.WrappingWidth ?? 0));
		var size = iconSize + (node.Padding ?? 0);
		var frame = RoughPath(ShapePaths.RoundedRect(-size / 2, -size / 2, size, size, radius));
		return Pictured(node, label, frame, size, size, iconSize);
	}

	/// <summary>
	/// The picture of <paramref name="width"/> by <paramref name="height"/> with the label above it, for <c>pos: t</c>,
	/// or below it, 8 apart when the node has a label, inside an invisible box of both, which sizes the node.
	/// </summary>
	private static ShapeGeometry Pictured(ShapeNode node, ShapeLabel label, ShapeElement picture, double width, double height, double? iconSide)
	{
		var top = node.LabelPosition == "t";
		var labelPadding = node.HasLabel ? 8 : 0;
		var outerWidth = Math.Max(width, label.Width);
		var outerHeight = height + label.Height + labelPadding;
		var shift = label.Height / 2 + labelPadding / 2.0;
		var outer = Rect(-outerWidth / 2, -outerHeight / 2, outerWidth, outerHeight, null, rough: true) with { Attributes = Hidden };
		var labelTranslate = new ShapePoint(-label.Width / 2 - label.OffsetX, top ? -outerHeight / 2 : outerHeight / 2 - label.Height);
		var intersect = node.HasLabel ? ShapeIntersect.Polygon(PicturedOutline(top, label, width, height, outerHeight, labelPadding)) : ShapeIntersect.Rect;
		var centre = top ? shift : -shift;

		// An icon is iconSize square, centred on its frame. An image fills its frame.
		var (pictureWidth, pictureHeight) = iconSide is { } side ? (side, side) : (width, height);
		var geometry = Finish([picture with { Translate = new ShapePoint(0, centre) }, outer], label, labelTranslate, intersect);
		return new ShapeGeometry
		{
			Width = geometry.Width,
			Height = geometry.Height,
			Bounds = geometry.Bounds,
			Elements = geometry.Elements,
			Label = geometry.Label,
			LabelTranslate = geometry.LabelTranslate,
			Intersect = geometry.Intersect,
			Picture = new ShapeBounds(-pictureWidth / 2, centre - (pictureHeight / 2), pictureWidth / 2, centre + (pictureHeight / 2)),
		};
	}

	/// <summary>The outline of the picture and the label that the icon and image shapes intersect, as mermaid.js writes it.</summary>
	private static List<ShapePoint> PicturedOutline(bool top, ShapeLabel label, double width, double height, double nodeHeight, double labelPadding)
	{
		var half = nodeHeight / 2;
		if (top)
		{
			var below = -half + label.Height + labelPadding;
			return
			[
				new(-label.Width / 2, -half),
				new(label.Width / 2, -half),
				new(label.Width / 2, below),
				new(width / 2, below),
				new(width / 2, half),
				new(-width / 2, half),
				new(-width / 2, below),
				new(-label.Width / 2, below),
			];
		}

		var under = -half + height;
		return
		[
			new(-width / 2, -half),
			new(width / 2, -half),
			new(width / 2, under),
			new(label.Width / 2, under),
			new(label.Width / 2 / 2, half),
			new(-label.Width / 2, half),
			new(-label.Width / 2, under),
			new(-width / 2, under),
		];
	}
}
