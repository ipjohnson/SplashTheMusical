namespace SplashTheMusical.Diagrams.Sequence;

/// <summary>
/// A sequence diagram: the statements <c>sequenceDiagram.jison</c> parses, in source order, with the statements of a
/// <c>box</c> and of each block inside it. mermaid.js's <c>SequenceDB</c> applies them in that order, and the order of
/// participants, messages and activations is what it draws. <see cref="SequenceDb"/> applies them as the db does. Text
/// keeps entity codes such as <c>#quot;</c> as written, and the <c>wrap:</c> or <c>nowrap:</c> at its start is
/// <see cref="SequenceMessage.Wrap"/> and the other <c>Wrap</c> properties.
/// </summary>
public sealed class SequenceDocument : MermaidDocument
{
	public override string DiagramType => "sequence";

	/// <summary>The text of the last <c>title</c> statement, as the db's <c>setDiagramTitle</c> gets it, or <c>null</c>.</summary>
	public string? DiagramTitle { get; set; }

	public List<SequenceStatement> Statements { get; init; } = [];
}

/// <summary>One statement of a sequence diagram's body.</summary>
public abstract class SequenceStatement;

/// <summary>The keyword that declares a participant, which sets the type it is drawn as.</summary>
public enum SequenceParticipantKind
{
	/// <summary><c>participant</c>, drawn as a box.</summary>
	Participant,

	/// <summary><c>actor</c>, drawn as a stick figure.</summary>
	Actor,
}

/// <summary>
/// <c>participant id</c> or <c>actor id</c>, with an optional <c>@{ … }</c> and <c>as description</c>. With
/// <see cref="Created"/> it is <c>create participant id</c>, which the next message must go to.
/// </summary>
public sealed class SequenceParticipant : SequenceStatement
{
	public SequenceParticipant(string id, SequenceParticipantKind kind = SequenceParticipantKind.Participant)
	{
		ArgumentException.ThrowIfNullOrEmpty(id);
		Id = id;
		Kind = kind;
	}

	public string Id { get; }

	public SequenceParticipantKind Kind { get; }

	/// <summary>Whether the statement starts with <c>create</c>.</summary>
	public bool Created { get; init; }

	/// <summary>The text after <c>as</c>, or <c>null</c> when there is none.</summary>
	public string? Description { get; init; }

	/// <summary><c>true</c> for <c>wrap:</c> before <see cref="Description"/>, <c>false</c> for <c>nowrap:</c>.</summary>
	public bool? Wrap { get; init; }

	/// <summary>
	/// The YAML between <c>@{</c> and <c>}</c>, without the space around it, or <c>null</c> when there is none. Its
	/// <c>type</c> sets the participant's type, and its <c>alias</c> the description when there is no other.
	/// </summary>
	public string? Metadata { get; init; }
}

/// <summary><c>destroy id</c>: the next message must go to or come from the participant.</summary>
public sealed class SequenceDestroy : SequenceStatement
{
	public SequenceDestroy(string id)
	{
		ArgumentException.ThrowIfNullOrEmpty(id);
		Id = id;
	}

	public string Id { get; }
}

/// <summary>
/// <c>box color title</c> … <c>end</c>: a group of participants. The db's <c>parseBoxData</c> takes the first word,
/// or an <c>rgb()</c>, <c>rgba()</c>, <c>hsl()</c> or <c>hsla()</c>, as the colour when the browser reads it as one, and
/// otherwise the colour is <c>transparent</c> and the whole line is the title.
/// </summary>
public sealed class SequenceBox : SequenceStatement
{
	/// <summary>The colour, as written.</summary>
	public string Color { get; init; } = "transparent";

	/// <summary>The title, or <c>null</c> when there is none.</summary>
	public string? Title { get; init; }

	/// <summary><c>true</c> for <c>wrap:</c> before <see cref="Title"/>, <c>false</c> for <c>nowrap:</c>.</summary>
	public bool? Wrap { get; init; }

	/// <summary>The statements inside the box: participants that are not created, and <see cref="SequenceDestroy"/>.</summary>
	public List<SequenceStatement> Statements { get; init; } = [];
}

/// <summary>
/// The arrows of a message, with the values of the db's <c>LINETYPE</c>. Each comment is the arrow's text.
/// </summary>
public enum SequenceArrow
{
	/// <summary><c>-&gt;&gt;</c>.</summary>
	Solid = 0,

	/// <summary><c>--&gt;&gt;</c>.</summary>
	Dotted = 1,

	/// <summary><c>-x</c>.</summary>
	SolidCross = 3,

	/// <summary><c>--x</c>.</summary>
	DottedCross = 4,

	/// <summary><c>-&gt;</c>.</summary>
	SolidOpen = 5,

	/// <summary><c>--&gt;</c>.</summary>
	DottedOpen = 6,

	/// <summary><c>-)</c>.</summary>
	SolidPoint = 24,

	/// <summary><c>--)</c>.</summary>
	DottedPoint = 25,

	/// <summary><c>&lt;&lt;-&gt;&gt;</c>.</summary>
	BidirectionalSolid = 33,

	/// <summary><c>&lt;&lt;--&gt;&gt;</c>.</summary>
	BidirectionalDotted = 34,

	/// <summary><c>-|\</c>.</summary>
	SolidTop = 41,

	/// <summary><c>-|/</c>.</summary>
	SolidBottom = 42,

	/// <summary><c>-\\</c>.</summary>
	StickTop = 43,

	/// <summary><c>-//</c>.</summary>
	StickBottom = 44,

	/// <summary><c>/|-</c>.</summary>
	SolidArrowTopReverse = 45,

	/// <summary><c>\|-</c>.</summary>
	SolidArrowBottomReverse = 46,

	/// <summary><c>//-</c>.</summary>
	StickArrowTopReverse = 47,

	/// <summary><c>\\-</c>.</summary>
	StickArrowBottomReverse = 48,

	/// <summary><c>--|\</c>.</summary>
	SolidTopDotted = 51,

	/// <summary><c>--|/</c>.</summary>
	SolidBottomDotted = 52,

	/// <summary><c>--\\</c>.</summary>
	StickTopDotted = 53,

	/// <summary><c>--//</c>.</summary>
	StickBottomDotted = 54,

	/// <summary><c>/|--</c>.</summary>
	SolidArrowTopReverseDotted = 55,

	/// <summary><c>\|--</c>.</summary>
	SolidArrowBottomReverseDotted = 56,

	/// <summary><c>//--</c>.</summary>
	StickArrowTopReverseDotted = 57,

	/// <summary><c>\\--</c>.</summary>
	StickArrowBottomReverseDotted = 58,
}

/// <summary>What a message does besides itself, from the <c>+</c>, <c>-</c> or <c>()</c> around its arrow.</summary>
public enum SequenceMessageModifier
{
	None,

	/// <summary><c>A-&gt;&gt;+B</c>: activates the receiver.</summary>
	Activate,

	/// <summary><c>A-&gt;&gt;-B</c>: deactivates the sender.</summary>
	Deactivate,

	/// <summary><c>A-&gt;&gt;()B</c>: a central connection at the receiver.</summary>
	CentralConnection,

	/// <summary><c>A()-&gt;&gt;B</c>: a central connection at the sender.</summary>
	CentralConnectionReverse,

	/// <summary><c>A()-&gt;&gt;()B</c>: central connections at both ends.</summary>
	CentralConnectionDual,
}

/// <summary><c>from arrow to: text</c>.</summary>
public sealed class SequenceMessage : SequenceStatement
{
	public SequenceMessage(string from, string to, SequenceArrow arrow = SequenceArrow.Solid)
	{
		ArgumentException.ThrowIfNullOrEmpty(from);
		ArgumentException.ThrowIfNullOrEmpty(to);
		From = from;
		To = to;
		Arrow = arrow;
	}

	public string From { get; }

	public string To { get; }

	public SequenceArrow Arrow { get; }

	public SequenceMessageModifier Modifier { get; init; }

	public string Text { get; init; } = "";

	/// <summary><c>true</c> for <c>wrap:</c> before <see cref="Text"/>, <c>false</c> for <c>nowrap:</c>.</summary>
	public bool? Wrap { get; init; }
}

/// <summary>Where a note is, with the values of the db's <c>PLACEMENT</c>.</summary>
public enum SequenceNotePlacement
{
	/// <summary><c>left of</c>.</summary>
	LeftOf,

	/// <summary><c>right of</c>.</summary>
	RightOf,

	/// <summary><c>over</c>.</summary>
	Over,
}

/// <summary><c>note left of a: text</c>, <c>note right of a: text</c>, <c>note over a: text</c> or <c>note over a,b: text</c>.</summary>
public sealed class SequenceNote : SequenceStatement
{
	public SequenceNote(SequenceNotePlacement placement, string actor, string? secondActor = null)
	{
		ArgumentException.ThrowIfNullOrEmpty(actor);
		if (secondActor is not null && placement != SequenceNotePlacement.Over)
		{
			throw new ArgumentException("Only a note over participants has a second participant.", nameof(secondActor));
		}

		Placement = placement;
		Actor = actor;
		SecondActor = secondActor;
	}

	public SequenceNotePlacement Placement { get; }

	public string Actor { get; }

	/// <summary>The participant after the <c>,</c> of <c>note over a,b</c>, or <c>null</c>.</summary>
	public string? SecondActor { get; }

	public string Text { get; init; } = "";

	/// <summary><c>true</c> for <c>wrap:</c> before <see cref="Text"/>, <c>false</c> for <c>nowrap:</c>.</summary>
	public bool? Wrap { get; init; }
}

/// <summary><c>activate id</c> or <c>deactivate id</c>.</summary>
public sealed class SequenceActivation : SequenceStatement
{
	public SequenceActivation(string id, bool active = true)
	{
		ArgumentException.ThrowIfNullOrEmpty(id);
		Id = id;
		Active = active;
	}

	public string Id { get; }

	/// <summary><c>true</c> for <c>activate</c>, <c>false</c> for <c>deactivate</c>.</summary>
	public bool Active { get; }
}

/// <summary><c>autonumber</c>, <c>autonumber start</c>, <c>autonumber start step</c> or <c>autonumber off</c>.</summary>
public sealed class SequenceAutonumber : SequenceStatement
{
	/// <summary>The first number, or <c>null</c> when the statement gives none.</summary>
	public double? Start { get; init; }

	/// <summary>The step, or <c>null</c> when the statement gives none. Only a statement with a start has one.</summary>
	public double? Step { get; init; }

	/// <summary><c>false</c> for <c>autonumber off</c>.</summary>
	public bool Visible { get; init; } = true;
}

/// <summary>The keyword of a <see cref="SequenceActorData"/> statement.</summary>
public enum SequenceActorDataKind
{
	/// <summary><c>links id: {"label": "url"}</c>, the db's <c>addLinks</c>.</summary>
	Links,

	/// <summary><c>link id: label @ url</c>, the db's <c>addALink</c>.</summary>
	Link,

	/// <summary><c>properties id: {"key": "value"}</c>, the db's <c>addProperties</c>.</summary>
	Properties,

	/// <summary><c>details id: element-id</c>, the db's <c>addDetails</c>.</summary>
	Details,
}

/// <summary>
/// A <c>links</c>, <c>link</c>, <c>properties</c> or <c>details</c> statement, which gives a participant's menu of links
/// and its properties. They are DOM popups, which STM does not draw.
/// </summary>
public sealed class SequenceActorData : SequenceStatement
{
	public SequenceActorData(SequenceActorDataKind kind, string actor, string text)
	{
		ArgumentException.ThrowIfNullOrEmpty(actor);
		ArgumentNullException.ThrowIfNull(text);
		Kind = kind;
		Actor = actor;
		Text = text;
	}

	public SequenceActorDataKind Kind { get; }

	public string Actor { get; }

	/// <summary>The text after the <c>:</c>, without the <c>wrap:</c> or <c>nowrap:</c> the db takes out.</summary>
	public string Text { get; }
}

/// <summary>The keyword of a <see cref="SequenceBlock"/>.</summary>
public enum SequenceBlockKind
{
	Loop,
	Rect,
	Opt,
	Alt,
	Par,

	/// <summary><c>par_over</c>.</summary>
	ParOver,
	Critical,
	Break,
}

/// <summary>
/// <c>loop</c>, <c>rect</c>, <c>opt</c>, <c>alt</c>, <c>par</c>, <c>par_over</c>, <c>critical</c> or <c>break</c>, its
/// text, its statements, and for <c>alt</c>, <c>par</c>, <c>par_over</c> and <c>critical</c> the sections after each
/// <c>else</c>, <c>and</c> or <c>option</c>, up to <c>end</c>. The text of a <c>rect</c> is its colour.
/// </summary>
public sealed class SequenceBlock : SequenceStatement
{
	public SequenceBlock(SequenceBlockKind kind) => Kind = kind;

	public SequenceBlockKind Kind { get; }

	public string Text { get; init; } = "";

	/// <summary><c>true</c> for <c>wrap:</c> before <see cref="Text"/>, <c>false</c> for <c>nowrap:</c>.</summary>
	public bool? Wrap { get; init; }

	public List<SequenceStatement> Statements { get; init; } = [];

	/// <summary>The <c>else</c>, <c>and</c> or <c>option</c> sections, in order.</summary>
	public List<SequenceBlockSection> Sections { get; init; } = [];
}

/// <summary>An <c>else</c>, <c>and</c> or <c>option</c> section of a <see cref="SequenceBlock"/>.</summary>
public sealed class SequenceBlockSection
{
	public string Text { get; init; } = "";

	/// <summary><c>true</c> for <c>wrap:</c> before <see cref="Text"/>, <c>false</c> for <c>nowrap:</c>.</summary>
	public bool? Wrap { get; init; }

	public List<SequenceStatement> Statements { get; init; } = [];
}
