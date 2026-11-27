namespace SplashTheMusical.Diagrams.C4;

/// <summary>
/// A C4 diagram: the header and the macro calls <c>c4Diagram.jison</c> parses, in source order, with each boundary's
/// calls inside it. mermaid.js's <c>c4Db.ts</c> reads a call's arguments by position, merges a second call of an alias
/// into the first, and lets <c>UpdateElementStyle</c> and <c>UpdateRelStyle</c> reach only what is defined before them,
/// so the model keeps the calls and their arguments as written. <see cref="C4Db"/> runs them as the db does. Text keeps
/// entity codes such as <c>#quot;</c> as written.
/// </summary>
public sealed class C4Document : MermaidDocument
{
	public override string DiagramType => "c4";

	/// <summary>The header keyword, which the db keeps as the diagram's <c>c4Type</c>.</summary>
	public C4DiagramKind Kind { get; set; }

	/// <summary>
	/// The db's title: the text after <c>title </c>, as the grammar takes it with <c>substring(6)</c>, or the trimmed text
	/// of <c>accTitle:</c>, which the grammar also hands to <c>setTitle</c>. The last one wins, and an empty one is
	/// <c>null</c>.
	/// </summary>
	public string? DiagramTitle { get; set; }

	public List<C4Statement> Statements { get; init; } = [];
}

/// <summary>The header of a C4 diagram.</summary>
public enum C4DiagramKind
{
	/// <summary><c>C4Context</c>.</summary>
	Context,

	/// <summary><c>C4Container</c>.</summary>
	Container,

	/// <summary><c>C4Component</c>.</summary>
	Component,

	/// <summary><c>C4Dynamic</c>, which numbers the relations' labels.</summary>
	Dynamic,

	/// <summary><c>C4Deployment</c>.</summary>
	Deployment,
}

/// <summary>
/// One argument of a macro call: a positional value, or a named one written <c>$name="value"</c>. The grammar trims
/// both the name and the value.
/// </summary>
public sealed class C4Argument
{
	/// <summary>A positional value. An empty value is written <c>""</c>.</summary>
	public C4Argument(string value)
	{
		ArgumentNullException.ThrowIfNull(value);
		Value = value;
	}

	/// <summary>A named value, <c>$name="value"</c>.</summary>
	public C4Argument(string name, string value)
	{
		ArgumentNullException.ThrowIfNull(name);
		ArgumentNullException.ThrowIfNull(value);
		Name = name;
		Value = value;
	}

	/// <summary>The name without its <c>$</c>, or <c>null</c> for a positional value.</summary>
	public string? Name { get; }

	public string Value { get; }
}

/// <summary>
/// A macro call, such as <c>Person(alias, "label")</c>. The db reads the arguments by position, and a named argument in
/// a position sets the field it names instead of the position's.
/// </summary>
public abstract class C4Statement
{
	protected C4Statement(List<C4Argument> arguments)
	{
		ArgumentNullException.ThrowIfNull(arguments);
		Arguments = arguments;
	}

	/// <summary>The arguments in the order written. The grammar needs at least one.</summary>
	public List<C4Argument> Arguments { get; }
}

/// <summary>
/// A person, system, container or component: <c>addPersonOrSystem</c>, <c>addContainer</c> or <c>addComponent</c>. The
/// arguments are the alias, the label, then the description for a person or a system, or the technology and the
/// description for a container or a component, then the sprite, the tags and the link.
/// </summary>
public sealed class C4Element : C4Statement
{
	public C4Element(C4ElementKind kind, List<C4Argument> arguments)
		: base(arguments) => Kind = kind;

	public C4ElementKind Kind { get; }
}

/// <summary>The macro of a <see cref="C4Element"/>.</summary>
public enum C4ElementKind
{
	/// <summary><c>Person</c>.</summary>
	Person,

	/// <summary><c>Person_Ext</c>.</summary>
	PersonExt,

	/// <summary><c>System</c>.</summary>
	System,

	/// <summary><c>SystemDb</c>.</summary>
	SystemDb,

	/// <summary><c>SystemQueue</c>.</summary>
	SystemQueue,

	/// <summary><c>System_Ext</c>.</summary>
	SystemExt,

	/// <summary><c>SystemDb_Ext</c>.</summary>
	SystemDbExt,

	/// <summary><c>SystemQueue_Ext</c>.</summary>
	SystemQueueExt,

	/// <summary><c>Container</c>.</summary>
	Container,

	/// <summary><c>ContainerDb</c>.</summary>
	ContainerDb,

	/// <summary><c>ContainerQueue</c>.</summary>
	ContainerQueue,

	/// <summary><c>Container_Ext</c>.</summary>
	ContainerExt,

	/// <summary><c>ContainerDb_Ext</c>.</summary>
	ContainerDbExt,

	/// <summary><c>ContainerQueue_Ext</c>.</summary>
	ContainerQueueExt,

	/// <summary><c>Component</c>.</summary>
	Component,

	/// <summary><c>ComponentDb</c>.</summary>
	ComponentDb,

	/// <summary><c>ComponentQueue</c>.</summary>
	ComponentQueue,

	/// <summary><c>Component_Ext</c>.</summary>
	ComponentExt,

	/// <summary><c>ComponentDb_Ext</c>.</summary>
	ComponentDbExt,

	/// <summary><c>ComponentQueue_Ext</c>.</summary>
	ComponentQueueExt,
}

/// <summary>
/// A boundary or a deployment node and the calls inside its braces: <c>addPersonOrSystemBoundary</c>,
/// <c>addContainerBoundary</c> or <c>addDeploymentNode</c>. The arguments are the alias, the label and the type, then the
/// tags and the link, and for a deployment node the type, the description, the sprite, the tags and the link.
/// <c>Enterprise_Boundary</c>, <c>System_Boundary</c> and <c>Container_Boundary</c> insert their type as the third
/// argument.
/// </summary>
public sealed class C4Boundary : C4Statement
{
	public C4Boundary(C4BoundaryKind kind, List<C4Argument> arguments)
		: base(arguments) => Kind = kind;

	public C4BoundaryKind Kind { get; }

	/// <summary>The calls inside the braces. The grammar needs at least one.</summary>
	public List<C4Statement> Statements { get; init; } = [];
}

/// <summary>The macro of a <see cref="C4Boundary"/>.</summary>
public enum C4BoundaryKind
{
	/// <summary><c>Boundary</c>.</summary>
	Boundary,

	/// <summary><c>Enterprise_Boundary</c>.</summary>
	EnterpriseBoundary,

	/// <summary><c>System_Boundary</c>.</summary>
	SystemBoundary,

	/// <summary><c>Container_Boundary</c>.</summary>
	ContainerBoundary,

	/// <summary><c>Deployment_Node</c>.</summary>
	DeploymentNode,

	/// <summary><c>Node</c>, which the lexer reads as <c>Deployment_Node</c>.</summary>
	Node,

	/// <summary><c>Node_L</c>.</summary>
	NodeL,

	/// <summary><c>Node_R</c>.</summary>
	NodeR,
}

/// <summary>
/// A relation, <c>addRel</c>: the arguments are the source, the target, the label, the technology, the description, the
/// sprite, the tags and the link. <c>RelIndex</c> takes an index first, which the grammar drops. The ends are kept as
/// written for every macro, <c>Rel_Back</c> too.
/// </summary>
public sealed class C4Relation : C4Statement
{
	public C4Relation(C4RelationKind kind, List<C4Argument> arguments)
		: base(arguments) => Kind = kind;

	public C4RelationKind Kind { get; }
}

/// <summary>The macro of a <see cref="C4Relation"/>.</summary>
public enum C4RelationKind
{
	/// <summary><c>Rel</c>.</summary>
	Rel,

	/// <summary><c>BiRel</c>.</summary>
	BiRel,

	/// <summary><c>Rel_Up</c>.</summary>
	RelUp,

	/// <summary><c>Rel_U</c>.</summary>
	RelU,

	/// <summary><c>Rel_Down</c>.</summary>
	RelDown,

	/// <summary><c>Rel_D</c>.</summary>
	RelD,

	/// <summary><c>Rel_Left</c>.</summary>
	RelLeft,

	/// <summary><c>Rel_L</c>.</summary>
	RelL,

	/// <summary><c>Rel_Right</c>.</summary>
	RelRight,

	/// <summary><c>Rel_R</c>.</summary>
	RelR,

	/// <summary><c>Rel_Back</c>.</summary>
	RelBack,

	/// <summary><c>RelIndex</c>, whose first argument the grammar drops.</summary>
	RelIndex,
}

/// <summary>
/// <c>UpdateElementStyle(alias, …)</c>, <c>UpdateRelStyle(from, to, …)</c> or <c>UpdateLayoutConfig(…)</c>. The
/// arguments are the db's: an element's background, font and border colours, shadowing, shape, sprite, technology,
/// legend text and legend sprite; a relation's text and line colours and offsets; the shapes and boundaries per row.
/// </summary>
public sealed class C4Update : C4Statement
{
	public C4Update(C4UpdateKind kind, List<C4Argument> arguments)
		: base(arguments) => Kind = kind;

	public C4UpdateKind Kind { get; }
}

/// <summary>The macro of a <see cref="C4Update"/>.</summary>
public enum C4UpdateKind
{
	/// <summary><c>UpdateElementStyle</c>.</summary>
	ElementStyle,

	/// <summary><c>UpdateRelStyle</c>.</summary>
	RelStyle,

	/// <summary><c>UpdateLayoutConfig</c>.</summary>
	LayoutConfig,
}
