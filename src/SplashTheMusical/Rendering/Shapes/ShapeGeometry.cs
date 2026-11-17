namespace SplashTheMusical.Rendering.Shapes;

/// <summary>
/// What a shape's drawing function leaves behind: the node's size, which layout reads, the elements of its outline
/// in document order, where the label goes and how edges meet the outline.
/// </summary>
internal sealed class ShapeGeometry
{
	/// <summary><c>node.width</c> after the shape, mostly the <c>getBBox</c> of its outline.</summary>
	public required double Width { get; init; }

	/// <summary><c>node.height</c> after the shape.</summary>
	public required double Height { get; init; }

	/// <summary>The elements the shape inserts before its label, first child first.</summary>
	public required IReadOnlyList<ShapeElement> Elements { get; init; }

	/// <summary>The measured label, or null for a shape that draws none.</summary>
	public ShapeLabel? Label { get; init; }

	/// <summary>The <c>translate</c> of the label's <c>g</c>.</summary>
	public ShapePoint LabelTranslate { get; init; }

	public required ShapeIntersect Intersect { get; init; }

	/// <summary>Where the line from the node's centre to <paramref name="point"/> leaves the outline, for a node centred on <paramref name="center"/>.</summary>
	public ShapePoint IntersectFrom(ShapePoint center, ShapePoint point) => Intersect.Find(center, Width, Height, point);
}
