namespace SplashTheMusical.Diagrams.Er;

/// <summary>
/// An entity relationship diagram: the statements <c>erDiagram.jison</c> parses, in source order, with a subgraph's
/// statements inside it. mermaid.js's <c>ErDB</c> turns them into entities, relationships and subgraphs as the parser
/// reads them, and the order decides which entity comes first, which alias an entity keeps and whether a relationship
/// ends at an entity or a subgraph, so the model keeps the statements. <see cref="ErDb"/> runs them as the db does.
/// Text keeps entity codes such as <c>#quot;</c> as written.
/// </summary>
public sealed class ErDocument : MermaidDocument
{
	public override string DiagramType => "er";

	public List<ErStatement> Statements { get; init; } = [];
}

/// <summary>One statement of an entity relationship diagram's body or of a subgraph.</summary>
public abstract class ErStatement;

/// <summary>
/// An entity on its own: <c>NAME</c>, with an alias as <c>NAME[alias]</c>, classes as <c>NAME:::a,b</c> and
/// attributes as <c>NAME { type name }</c>.
/// </summary>
public sealed class ErEntity : ErStatement
{
	public ErEntity(string name)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);
		Name = name;
	}

	/// <summary>The name, without the quotes of a quoted name.</summary>
	public string Name { get; }

	/// <summary>The name between the brackets, or <c>null</c>. The db keeps the first alias an entity is given.</summary>
	public string? Alias { get; set; }

	/// <summary>The classes after <c>:::</c>, or <c>null</c>.</summary>
	public List<string>? Classes { get; set; }

	/// <summary>The attributes between the braces in source order, or <c>null</c> when there are no braces.</summary>
	public List<ErAttribute>? Attributes { get; set; }
}

/// <summary>An attribute of an entity: <c>type name</c>, then keys such as <c>PK, FK</c>, then a quoted comment.</summary>
public sealed class ErAttribute
{
	public ErAttribute(string type, string name)
	{
		ArgumentException.ThrowIfNullOrEmpty(type);
		ArgumentException.ThrowIfNullOrEmpty(name);
		Type = type;
		Name = name;
	}

	/// <summary>The type as written, with the <c>?</c> of an optional type.</summary>
	public string Type { get; }

	/// <summary>The name, without the backticks of a quoted name.</summary>
	public string Name { get; }

	/// <summary><c>PK</c>, <c>FK</c> and <c>UK</c> as written, in source order.</summary>
	public List<string> Keys { get; init; } = [];

	/// <summary>The comment without its quotes, or <c>null</c>.</summary>
	public string? Comment { get; set; }
}

/// <summary>The cardinality at one end of a relationship.</summary>
public enum ErCardinality
{
	/// <summary><c>||</c>, <c>one</c>, <c>only one</c> or <c>1</c>.</summary>
	OnlyOne,

	/// <summary><c>|o</c>, <c>o|</c>, <c>zero or one</c> or <c>one or zero</c>.</summary>
	ZeroOrOne,

	/// <summary><c>}|</c>, <c>|{</c>, <c>one or more</c>, <c>one or many</c>, <c>many(1)</c> or <c>1+</c>.</summary>
	OneOrMore,

	/// <summary><c>}o</c>, <c>o{</c>, <c>zero or more</c>, <c>zero or many</c>, <c>many(0)</c>, <c>many</c> or <c>0+</c>.</summary>
	ZeroOrMore,

	/// <summary><c>u</c>, before <c>.</c>, <c>-</c> or <c>|</c>.</summary>
	MdParent,
}

/// <summary>
/// <c>A ||--o{ B : role</c>: a relationship from <see cref="EntityA"/> to <see cref="EntityB"/>. Either end may carry
/// classes as <c>A:::a</c>. An end whose name is a subgraph's id by then ends at the subgraph.
/// </summary>
public sealed class ErRelationship : ErStatement
{
	public ErRelationship(string entityA, string entityB, string role)
	{
		ArgumentException.ThrowIfNullOrEmpty(entityA);
		ArgumentException.ThrowIfNullOrEmpty(entityB);
		ArgumentNullException.ThrowIfNull(role);
		EntityA = entityA;
		EntityB = entityB;
		Role = role;
	}

	public string EntityA { get; }

	public string EntityB { get; }

	/// <summary>The label, without its quotes.</summary>
	public string Role { get; }

	/// <summary>The cardinality written next to <see cref="EntityA"/>, which mermaid.js's <c>relSpec</c> calls <c>cardB</c>.</summary>
	public ErCardinality CardinalityA { get; set; }

	/// <summary>The cardinality written next to <see cref="EntityB"/>, which mermaid.js's <c>relSpec</c> calls <c>cardA</c>.</summary>
	public ErCardinality CardinalityB { get; set; }

	/// <summary>Whether the line is <c>--</c> or <c>to</c>, rather than <c>..</c>, <c>.-</c>, <c>-.</c> or <c>optionally to</c>.</summary>
	public bool Identifying { get; set; } = true;

	/// <summary>The classes after <c>:::</c> on <see cref="EntityA"/>, or <c>null</c>.</summary>
	public List<string>? ClassesA { get; set; }

	/// <summary>The classes after <c>:::</c> on <see cref="EntityB"/>, or <c>null</c>.</summary>
	public List<string>? ClassesB { get; set; }
}

/// <summary><c>classDef a,b styles</c>.</summary>
public sealed class ErClassDefinition : ErStatement
{
	public ErClassDefinition(List<string> names, List<string> styles)
	{
		ArgumentNullException.ThrowIfNull(names);
		ArgumentNullException.ThrowIfNull(styles);
		Names = names;
		Styles = styles;
	}

	public List<string> Names { get; }

	/// <summary>The comma-separated declarations, each with its space taken out, such as <c>fill:#f96</c>.</summary>
	public List<string> Styles { get; }
}

/// <summary><c>style a,b styles</c>: declarations for entities and subgraphs.</summary>
public sealed class ErStyle : ErStatement
{
	public ErStyle(List<string> ids, List<string> styles)
	{
		ArgumentNullException.ThrowIfNull(ids);
		ArgumentNullException.ThrowIfNull(styles);
		Ids = ids;
		Styles = styles;
	}

	public List<string> Ids { get; }

	/// <inheritdoc cref="ErClassDefinition.Styles"/>
	public List<string> Styles { get; }
}

/// <summary><c>class a,b c,d</c>: the classes <c>c</c> and <c>d</c> for the entities and subgraphs <c>a</c> and <c>b</c>.</summary>
public sealed class ErClassAssignment : ErStatement
{
	public ErClassAssignment(List<string> ids, List<string> classes)
	{
		ArgumentNullException.ThrowIfNull(ids);
		ArgumentNullException.ThrowIfNull(classes);
		Ids = ids;
		Classes = classes;
	}

	public List<string> Ids { get; }

	public List<string> Classes { get; }
}

/// <summary><c>direction TB</c>, <c>BT</c>, <c>RL</c> or <c>LR</c>: of the diagram, or of the subgraph it is in.</summary>
public sealed class ErDirection : ErStatement
{
	public ErDirection(string direction)
	{
		ArgumentException.ThrowIfNullOrEmpty(direction);
		Direction = direction;
	}

	public string Direction { get; }
}

/// <summary>
/// <c>subgraph id</c> or <c>subgraph id [title]</c>, then statements up to <c>end</c>. Its members are the entities its
/// statements name and the subgraphs inside it, less those an earlier subgraph holds.
/// </summary>
public sealed class ErSubgraph : ErStatement
{
	public ErSubgraph(string id)
	{
		ArgumentException.ThrowIfNullOrEmpty(id);
		Id = id;
	}

	public string Id { get; }

	/// <summary>The names between the brackets, joined with a space, or <c>null</c>, when the title is the id.</summary>
	public string? Title { get; set; }

	public List<ErStatement> Statements { get; init; } = [];
}
