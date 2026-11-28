using SplashTheMusical.Rendering;

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
		SequenceLayoutParts parts,
		SequenceStyling styling
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
		Title = parts.Title;
		DiagramRules = styling.Rules;
		ElementRules = styling.ElementRules;
		Theme = styling.Theme;
		RectFill = styling.RectFill;
		ActorBorder = styling.ActorBorder;
		ActorBackground = styling.ActorBackground;
	}

	internal override IReadOnlyList<CssRule> DiagramRules { get; }

	/// <summary>The rules of the palette colours the colour themes give each participant and activation.</summary>
	internal override IReadOnlyList<CssRule> ElementRules { get; }

	/// <summary>The diagram's title and its <c>x</c>, or <c>null</c> when it has none.</summary>
	internal SequenceTitlePlace? Title { get; }

	/// <summary>The sequence config the layout read, whose fonts, alignments and margins the renderer writes.</summary>
	internal SequenceConf Conf { get; init; } = null!;

	/// <summary>The config's <c>theme</c>, which picks the drop shadow's colour under <c>neo</c>.</summary>
	internal string Theme { get; }

	/// <summary>The fill of a <c>rect</c> block without a colour: <c>rectBkgColor</c>, <c>actorBkg</c> or grey.</summary>
	internal string? RectFill { get; }

	/// <summary>The <c>actorBorder</c> theme variable, which the icon participants set on their group.</summary>
	internal string? ActorBorder { get; }

	/// <summary>The <c>actorBkg</c> theme variable, which <c>control</c> sets on its group.</summary>
	internal string? ActorBackground { get; }

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

/// <summary>
/// One line of a text element: its text, its <c>x</c> and <c>y</c>, and the box the browser gives it. A line of
/// <c>byTspan</c> is at <see cref="Y"/> once its tspan's <see cref="Dy"/> is added.
/// </summary>
internal sealed record SequenceTextLine(string Text, double X, double Y, SequenceRect Box)
{
	public double Dy { get; init; }
}

/// <summary>
/// A participant: its id, its type, its place, and the boxes of its top and bottom drawings. <see cref="Bottom"/> is
/// <c>null</c> when <c>mirrorActors</c> is off. The lifeline runs from <see cref="LifelineStartY"/>, where the type's
/// drawing puts it, to <see cref="LifelineEndY"/>, where <c>fixLifeLineHeights</c> ends it. <see cref="PaletteClass"/> is
/// the class of the colour theme's palette rule for the participant, or <c>null</c>.
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
)
{
	public double LifelineStartY { get; init; }

	public double LifelineEndY { get; init; }

	public string? PaletteClass { get; init; }
}

/// <summary>The diagram's title, at <c>x</c> and <c>y = -25</c>.</summary>
internal sealed record SequenceTitlePlace(string Text, double X);

/// <summary>The stylesheet rules of a sequence diagram and the theme values its renderer writes as attributes.</summary>
internal sealed record SequenceStyling(
	IReadOnlyList<CssRule> Rules,
	IReadOnlyList<CssRule> ElementRules,
	string Theme,
	string? RectFill,
	string? ActorBorder,
	string? ActorBackground
);

/// <summary>
/// The drawing of a participant at the top or the bottom. <see cref="Shape"/> is the box of what draws the type's
/// shape, and <see cref="Bounds"/> also holds the label.
/// </summary>
internal sealed record SequenceActorDrawing(SequenceRect Shape, SequenceRect Bounds, IReadOnlyList<SequenceTextLine> Label);

/// <summary>A box around participants: its title, the rectangle <c>drawBox</c> draws, and the colour the source gave it.</summary>
internal sealed record SequenceBoxPlace(string? Name, SequenceRect Rect, IReadOnlyList<SequenceTextLine> Label)
{
	public string Fill { get; init; } = "transparent";
}

/// <summary>
/// A message: its db id, ends and <c>LINETYPE</c>, the line's ends and height, the box of its line or self loop, its
/// text lines, and its number when <c>autonumber</c> shows one.
/// </summary>
/// <remarks>
/// <see cref="X1"/> and <see cref="X2"/> are the line's ends once <c>drawMessage</c> has made room for the number. A
/// message to its own participant is the path <see cref="SelfPath"/>. The number's circle and text are at
/// <see cref="NumberX"/>, and <see cref="CentralCircles"/> holds the <c>cx</c> of each circle of a central connection.
/// </remarks>
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
)
{
	public double X1 { get; init; }

	public double X2 { get; init; }

	public string? SelfPath { get; init; }

	public double NumberX { get; init; }

	public IReadOnlyList<double> CentralCircles { get; init; } = [];
}

/// <summary>The places of the parts that are not participants, boxes or messages.</summary>
internal sealed record SequenceLayoutParts(
	IReadOnlyList<SequenceNotePlace> Notes,
	IReadOnlyList<SequenceBlockPlace> Blocks,
	IReadOnlyList<SequenceBackgroundPlace> Backgrounds,
	IReadOnlyList<SequenceActivationPlace> Activations,
	SequenceTitlePlace? Title = null
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

/// <summary>
/// An activation: its participant, its rectangle, and the <c>activationN</c> class number, 0, 1 or 2. Its group is
/// added when the message <see cref="StartId"/> starts it, and <see cref="PaletteClass"/> is the class of the colour
/// theme's palette rule, or <c>null</c>.
/// </summary>
internal sealed record SequenceActivationPlace(string Actor, SequenceRect Rect, int Level)
{
	public int StartId { get; init; }

	public string? PaletteClass { get; init; }
}
