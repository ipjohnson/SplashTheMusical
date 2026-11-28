namespace SplashTheMusical.Diagrams.Sequence;

/// <summary>
/// The layout of a sequence diagram: where mermaid's sequence renderer puts each participant, box, message, note,
/// block, <c>rect</c> background and activation, and the canvas it sizes.
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
		IReadOnlyList<SequenceMessagePlace> messages,
		SequenceLayoutParts parts
	)
		: base(width, height)
	{
		ViewBox = viewBox;
		Look = look;
		Actors = actors;
		Boxes = boxes;
		Messages = messages;
		Notes = parts.Notes;
		Blocks = parts.Blocks;
		Backgrounds = parts.Backgrounds;
		Activations = parts.Activations;
	}

	/// <summary>The notes in the db's order.</summary>
	internal IReadOnlyList<SequenceNotePlace> Notes { get; }

	/// <summary>The blocks <c>drawLoop</c> draws, in the order their ends come.</summary>
	internal IReadOnlyList<SequenceBlockPlace> Blocks { get; }

	/// <summary>The <c>rect</c> blocks <c>drawBackgroundRect</c> draws, in the order their ends come.</summary>
	internal IReadOnlyList<SequenceBackgroundPlace> Backgrounds { get; }

	/// <summary>The activations <c>drawActivation</c> draws, in the order they end.</summary>
	internal IReadOnlyList<SequenceActivationPlace> Activations { get; }

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

/// <summary>The places of the parts that are not participants, boxes or messages.</summary>
internal sealed record SequenceLayoutParts(
	IReadOnlyList<SequenceNotePlace> Notes,
	IReadOnlyList<SequenceBlockPlace> Blocks,
	IReadOnlyList<SequenceBackgroundPlace> Backgrounds,
	IReadOnlyList<SequenceActivationPlace> Activations
);

/// <summary>
/// A note: its db id, the text of the db, the rectangle <c>drawNote</c> draws, its text lines, and the box of both.
/// </summary>
internal sealed record SequenceNotePlace(int Id, string Text, SequenceRect Rect, IReadOnlyList<SequenceTextLine> Lines, SequenceRect Bounds);

/// <summary>
/// A block <c>drawLoop</c> draws: the db id of the message that ends it, the <c>LINETYPE</c> and text of the message
/// that starts it, its frame, the dashed line of each branch, the label box in the corner and its text, the title
/// lines, the branch title lines, and the box of all of them.
/// </summary>
internal sealed record SequenceBlockPlace(
	int Id,
	string Kind,
	int StartType,
	string Text,
	SequenceRect Frame,
	IReadOnlyList<double> Sections,
	SequenceRect LabelBox,
	IReadOnlyList<SequenceTextLine> Label,
	IReadOnlyList<SequenceTextLine> Title,
	IReadOnlyList<SequenceTextLine> SectionTitles,
	SequenceRect Bounds
);

/// <summary>A <c>rect</c> block: its rectangle and the colour the source gave, or <c>null</c> for the theme's.</summary>
internal sealed record SequenceBackgroundPlace(SequenceRect Rect, string? Fill);

/// <summary>An activation: its participant, its rectangle, and the <c>activationN</c> class number, 0, 1 or 2.</summary>
internal sealed record SequenceActivationPlace(string Actor, SequenceRect Rect, int Level);
