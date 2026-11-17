namespace SplashTheMusical.Rendering.Shapes;

/// <summary>
/// The shapes of mermaid.js 12.3.0's <c>rendering-elements/shapes</c>, one method per drawing function, named after
/// it. Each reads what the function reads and returns what it leaves on the node: its size, its outline, its label
/// and its intersect. Styles are left to the renderer.
/// </summary>
internal static partial class ShapeDrawings
{
	private const string LabelContainer = "basic label-container";

	private const string OuterPath = "basic label-container outer-path";

	/// <summary>JavaScript's truth of a number field: defined, not 0 and not NaN.</summary>
	private static bool Truthy(double? value) => value is { } number && number != 0 && !double.IsNaN(number);

	/// <summary><c>value ? value : fallback</c>.</summary>
	private static double OrElse(double? value, double fallback) => Truthy(value) ? value!.Value : fallback;

	/// <summary><c>labelHelper</c>: the label's box, measured with the node's width as the shape set it.</summary>
	private static ShapeLabel Measure(ShapeNode node, double? width, bool ignoreMinWidth = false) =>
		node.Label.Measure(width, ignoreMinWidth);

	/// <summary>The translate <c>labelHelper</c> gives the label before a shape moves it.</summary>
	private static ShapePoint LabelHelperTranslate(ShapeLabel label, bool centerLabel = false) =>
		label.Html || centerLabel ? new ShapePoint(-label.Width / 2, -label.Height / 2) : new ShapePoint(0, -label.Height / 2);

	/// <summary>The translate most shapes give the label: its box centred on the node.</summary>
	private static ShapePoint CenteredLabel(ShapeLabel label, double dx = 0, double dy = 0) =>
		new(dx - label.Width / 2 - label.OffsetX, dy - label.Height / 2 - label.OffsetY);

	private static ShapeElement RoughPath(string data, string? cls = null, ShapePoint? translate = null) =>
		new() { Kind = ShapeElementKind.Path, PathData = data, Rough = true, Class = cls, Translate = translate };

	private static ShapeElement Path(string data, string? cls = null, ShapePoint? translate = null) =>
		new() { Kind = ShapeElementKind.Path, PathData = data, Class = cls, Translate = translate };

	private static ShapeElement RoughPolygon(IReadOnlyList<ShapePoint> points, string? cls = null) =>
		new() { Kind = ShapeElementKind.Polygon, Points = points, Rough = true, Class = cls };

	private static ShapeElement Group(string? cls, params ShapeElement[] children) =>
		new() { Kind = ShapeElementKind.Group, Class = cls, Children = children };

	private static ShapeElement Circle(double x, double y, double radius, string? cls = null, bool rough = false) =>
		new() { Kind = ShapeElementKind.Circle, X = x, Y = y, Rx = radius, Class = cls, Rough = rough };

	private static ShapeElement Rect(double x, double y, double width, double height, string? cls, double radius = 0, bool rough = false) =>
		new() { Kind = ShapeElementKind.Rect, X = x, Y = y, Width = width, Height = height, Rx = radius, Ry = radius, Class = cls, Rough = rough };

	/// <summary><c>insertPolygonShape</c>: a polygon of the points, moved left by half the width and down by half the height.</summary>
	private static ShapeElement InsertPolygon(double w, double h, IReadOnlyList<ShapePoint> points) =>
		new() { Kind = ShapeElementKind.Polygon, Points = points, Class = "label-container", Translate = new ShapePoint(-w / 2, h / 2) };

	private static ShapeGeometry Finish(ShapeElement element, ShapeLabel? label, ShapePoint labelTranslate, ShapeIntersect intersect) =>
		Finish([element], label, labelTranslate, intersect);

	/// <summary>
	/// The geometry, sized as <c>insertMeasuredNode</c> sizes the node after the shape: by the <c>getBBox</c> of the
	/// node's group, which holds the elements and the label. That replaces the size the shape gave the node, so the
	/// intersect reads it too.
	/// </summary>
	private static ShapeGeometry Finish(IReadOnlyList<ShapeElement> elements, ShapeLabel? label, ShapePoint labelTranslate, ShapeIntersect intersect)
	{
		var bounds = ShapeBounds.None;
		foreach (var element in elements)
		{
			bounds = bounds.Union(element.Bounds().Offset(element.Translate));
		}

		if (label is { Width: > 0 } or { Height: > 0 })
		{
			var box = label.Value;
			var x = labelTranslate.X + box.OffsetX;
			var y = labelTranslate.Y + box.OffsetY;
			bounds = bounds.Union(new ShapeBounds(x, y, x + box.Width, y + box.Height));
		}

		return new ShapeGeometry
		{
			Width = bounds.Width,
			Height = bounds.Height,
			Bounds = bounds,
			Elements = elements,
			Label = label,
			LabelTranslate = labelTranslate,
			Intersect = intersect,
		};
	}

	private static string N(double value) => ShapePaths.N(value);
}
