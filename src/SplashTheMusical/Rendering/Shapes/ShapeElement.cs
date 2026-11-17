namespace SplashTheMusical.Rendering.Shapes;

/// <summary>The SVG element a shape draws, or the roughjs call that draws it.</summary>
internal enum ShapeElementKind
{
	/// <summary>A <c>rect</c>, or roughjs's <c>rectangle</c>.</summary>
	Rect,

	/// <summary>A <c>polygon</c>, or roughjs's <c>polygon</c>.</summary>
	Polygon,

	/// <summary>A <c>path</c>, or roughjs's <c>path</c>.</summary>
	Path,

	/// <summary>A <c>circle</c>, or roughjs's <c>circle</c>, whose third argument is the diameter.</summary>
	Circle,

	Ellipse,

	/// <summary>A <c>line</c>, or roughjs's <c>line</c>.</summary>
	Line,

	/// <summary>A <c>text</c>, such as the console's prompt glyph.</summary>
	Text,

	/// <summary>A <c>g</c> that holds <see cref="ShapeElement.Children"/>.</summary>
	Group,
}

/// <summary>
/// One element of a shape's outline, in the shape's own space, where the node's centre is the origin. When
/// <see cref="Rough"/> is set, mermaid.js draws it through roughjs, whose output is a <c>g</c> of paths.
/// </summary>
internal sealed record ShapeElement
{
	public required ShapeElementKind Kind { get; init; }

	/// <summary>The <c>class</c> attribute, or null when mermaid.js sets none.</summary>
	public string? Class { get; init; }

	public bool Rough { get; init; }

	/// <summary>The <c>translate</c> of the element's <c>transform</c>, or null when it has none.</summary>
	public ShapePoint? Translate { get; init; }

	/// <summary>A path's <c>d</c>, as mermaid.js writes it.</summary>
	public string? PathData { get; init; }

	/// <summary>A polygon's points.</summary>
	public IReadOnlyList<ShapePoint>? Points { get; init; }

	/// <summary>A rect's corner, a circle's or an ellipse's centre, a line's start or a text's anchor.</summary>
	public double X { get; init; }

	/// <inheritdoc cref="X"/>
	public double Y { get; init; }

	/// <summary>A rect's width, or a line's end x.</summary>
	public double Width { get; init; }

	/// <summary>A rect's height, or a line's end y.</summary>
	public double Height { get; init; }

	/// <summary>A rect's corner radius, a circle's radius or an ellipse's x radius.</summary>
	public double Rx { get; init; }

	/// <summary>A rect's corner radius or an ellipse's y radius.</summary>
	public double Ry { get; init; }

	/// <summary>A text's content.</summary>
	public string? Text { get; init; }

	public IReadOnlyList<ShapeElement>? Children { get; init; }

	/// <summary>Other attributes mermaid.js sets to fixed or computed values, such as <c>stroke-opacity</c> or <c>stroke-dasharray</c>.</summary>
	public IReadOnlyList<KeyValuePair<string, string>>? Attributes { get; init; }

	/// <summary>
	/// The element's <c>getBBox</c>: its box in its own space, before its <see cref="Translate"/>. A group's box is the
	/// union of its children's boxes after their translates. A text adds nothing, because the only one lies inside its
	/// shape's body.
	/// </summary>
	public ShapeBounds Bounds()
	{
		switch (Kind)
		{
			case ShapeElementKind.Rect:
				return new ShapeBounds(X, Y, X + Width, Y + Height);
			case ShapeElementKind.Polygon:
				return ShapeBounds.Of(Points!);
			case ShapeElementKind.Path:
				return PathBounds.Measure(PathData!);
			case ShapeElementKind.Circle when Rough:
				return RoughEllipse.Bounds(X, Y, Rx * 2, Rx * 2);
			case ShapeElementKind.Circle:
				return new ShapeBounds(X - Rx, Y - Rx, X + Rx, Y + Rx);
			case ShapeElementKind.Ellipse:
				return new ShapeBounds(X - Rx, Y - Ry, X + Rx, Y + Ry);
			case ShapeElementKind.Line:
				return new ShapeBounds(Math.Min(X, Width), Math.Min(Y, Height), Math.Max(X, Width), Math.Max(Y, Height));
			case ShapeElementKind.Group:
				var bounds = ShapeBounds.None;
				foreach (var child in Children!)
				{
					bounds = bounds.Union(child.Bounds().Offset(child.Translate));
				}

				return bounds;
			default:
				return ShapeBounds.None;
		}
	}
}

/// <summary>An axis-aligned box by its edges. <see cref="None"/> is empty, and a union with it is the other box.</summary>
internal readonly record struct ShapeBounds(double MinX, double MinY, double MaxX, double MaxY)
{
	public static ShapeBounds None => new(double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.NegativeInfinity);

	public double Width => MaxX - MinX;

	public double Height => MaxY - MinY;

	public bool IsEmpty => MinX > MaxX;

	public static ShapeBounds Of(IEnumerable<ShapePoint> points)
	{
		var bounds = None;
		foreach (var point in points)
		{
			bounds = bounds.Add(point.X, point.Y);
		}

		return bounds;
	}

	public ShapeBounds Add(double x, double y) => new(Math.Min(MinX, x), Math.Min(MinY, y), Math.Max(MaxX, x), Math.Max(MaxY, y));

	public ShapeBounds Union(ShapeBounds other) =>
		other.IsEmpty ? this
		: IsEmpty ? other
		: new(Math.Min(MinX, other.MinX), Math.Min(MinY, other.MinY), Math.Max(MaxX, other.MaxX), Math.Max(MaxY, other.MaxY));

	public ShapeBounds Offset(ShapePoint? translate) =>
		translate is { } t && !IsEmpty ? new(MinX + t.X, MinY + t.Y, MaxX + t.X, MaxY + t.Y) : this;
}
