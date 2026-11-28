namespace SplashTheMusical.Diagrams.Sequence;

/// <summary>
/// The layout of a sequence diagram: where mermaid's sequence renderer puts each participant, box and message, and the
/// canvas it sizes. Blocks, notes and activations are not placed yet.
/// </summary>
public sealed class SequenceLayout : DiagramLayout
{
	internal SequenceLayout(
		double width,
		double height,
		SvgViewBox viewBox,
		string look,
		IReadOnlyList<SequenceActorPlace> actors,
		IReadOnlyList<SequenceBoxPlace> boxes,
		IReadOnlyList<SequenceMessagePlace> messages
	)
		: base(width, height)
	{
		ViewBox = viewBox;
		Look = look;
		Actors = actors;
		Boxes = boxes;
		Messages = messages;
	}

	/// <summary>The top-level <c>look</c> the renderer read.</summary>
	internal string Look { get; }

	/// <summary>The drawn participants, in the order of the db's actor keys.</summary>
	internal IReadOnlyList<SequenceActorPlace> Actors { get; }

	/// <summary>The boxes in the order <c>addActorRenderingData</c> adds them.</summary>
	internal IReadOnlyList<SequenceBoxPlace> Boxes { get; }

	/// <summary>The messages in the db's order.</summary>
	internal IReadOnlyList<SequenceMessagePlace> Messages { get; }
}

/// <summary>A rectangle in the SVG's user units.</summary>
internal readonly record struct SequenceRect(double X, double Y, double Width, double Height)
{
	public double Right => X + Width;

	public double Bottom => Y + Height;

	public static SequenceRect FromEdges(double left, double top, double right, double bottom) => new(left, top, right - left, bottom - top);

	public SequenceRect Union(SequenceRect other) =>
		FromEdges(Math.Min(X, other.X), Math.Min(Y, other.Y), Math.Max(Right, other.Right), Math.Max(Bottom, other.Bottom));
}

/// <summary>One line of a text element: its text, its <c>x</c> and <c>y</c>, and the box the browser gives it.</summary>
internal sealed record SequenceTextLine(string Text, double X, double Y, SequenceRect Box);

/// <summary>
/// A participant: its id, its type, its place, and the boxes of its top and bottom drawings. <see cref="Bottom"/> is
/// <c>null</c> when <c>mirrorActors</c> is off.
/// </summary>
internal sealed record SequenceActorPlace(
	string Name,
	string Type,
	double X,
	double Width,
	double Height,
	double StartY,
	double? StopY,
	SequenceActorDrawing Top,
	SequenceActorDrawing? Bottom
);

/// <summary>
/// The drawing of a participant at the top or the bottom. <see cref="Shape"/> is the box of what draws the type's
/// shape, and <see cref="Bounds"/> also holds the label.
/// </summary>
internal sealed record SequenceActorDrawing(SequenceRect Shape, SequenceRect Bounds, IReadOnlyList<SequenceTextLine> Label);

/// <summary>A box around participants: its title and the rectangle <c>drawBox</c> draws.</summary>
internal sealed record SequenceBoxPlace(string? Name, SequenceRect Rect, IReadOnlyList<SequenceTextLine> Label);

/// <summary>
/// A message: its db id, ends and <c>LINETYPE</c>, the line's ends and height, the box of its line or self loop, its
/// text lines, and its number when <c>autonumber</c> shows one.
/// </summary>
internal sealed record SequenceMessagePlace(
	int Id,
	string From,
	string To,
	int Type,
	double StartX,
	double StopX,
	double LineY,
	SequenceRect Line,
	IReadOnlyList<SequenceTextLine> Text,
	double? Number
);
