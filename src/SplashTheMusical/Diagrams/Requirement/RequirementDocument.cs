namespace SplashTheMusical.Diagrams.Requirement;

/// <summary>
/// A requirement diagram: the statements <c>requirementDiagram.jison</c> parses, in source order. mermaid.js's
/// <c>RequirementDB</c> keeps the first requirement and the first element of each name, and a <c>style</c> or
/// <c>class</c> statement only reaches what is defined before it, so the model keeps the statements.
/// <see cref="RequirementDb"/> runs them as the db does. Text keeps entity codes such as <c>#quot;</c> as written.
/// </summary>
public sealed class RequirementDocument : MermaidDocument
{
	public override string DiagramType => "requirement";

	public List<RequirementStatement> Statements { get; init; } = [];
}

/// <summary>One statement of a requirement diagram's body.</summary>
public abstract class RequirementStatement;

/// <summary>The keyword of a requirement, which mermaid.js shows as its stereotype.</summary>
public enum RequirementKind
{
	/// <summary><c>requirement</c>.</summary>
	Requirement,

	/// <summary><c>functionalRequirement</c>.</summary>
	FunctionalRequirement,

	/// <summary><c>interfaceRequirement</c>.</summary>
	InterfaceRequirement,

	/// <summary><c>performanceRequirement</c>.</summary>
	PerformanceRequirement,

	/// <summary><c>physicalRequirement</c>.</summary>
	PhysicalRequirement,

	/// <summary><c>designConstraint</c>.</summary>
	DesignConstraint,
}

/// <summary>A requirement's <c>risk</c>.</summary>
public enum RequirementRisk
{
	Low,
	Medium,
	High,
}

/// <summary>A requirement's <c>verifyMethod</c>.</summary>
public enum RequirementVerifyMethod
{
	Analysis,
	Demonstration,
	Inspection,
	Test,
}

/// <summary>The type of a relationship between requirements and elements.</summary>
public enum RequirementRelationshipType
{
	Contains,
	Copies,
	Derives,
	Satisfies,
	Verifies,
	Refines,
	Traces,
}

/// <summary>
/// <c>requirement name { id: … text: … risk: … verifyMethod: … }</c>, with classes as <c>name:::a,b</c>. Each field
/// holds the first value the body gives it, the one the db keeps, or <c>null</c> when the body has none.
/// </summary>
public sealed class RequirementDefinition : RequirementStatement
{
	public RequirementDefinition(string name, RequirementKind kind = RequirementKind.Requirement)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);
		Name = name;
		Kind = kind;
	}

	/// <summary>The name, without the quotes of a quoted name.</summary>
	public string Name { get; }

	public RequirementKind Kind { get; }

	/// <summary>The <c>id</c>, without the quotes of a quoted value.</summary>
	public string? Id { get; set; }

	/// <summary>The <c>text</c>, without the quotes of a quoted value.</summary>
	public string? Text { get; set; }

	public RequirementRisk? Risk { get; set; }

	public RequirementVerifyMethod? VerifyMethod { get; set; }

	/// <summary>The classes after <c>:::</c>, or <c>null</c>.</summary>
	public List<string>? Classes { get; set; }
}

/// <summary>
/// <c>element name { type: … docRef: … }</c>, with classes as <c>name:::a,b</c>. Each field holds the first value the
/// body gives it, or <c>null</c>.
/// </summary>
public sealed class RequirementElement : RequirementStatement
{
	public RequirementElement(string name)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);
		Name = name;
	}

	/// <summary>The name, without the quotes of a quoted name.</summary>
	public string Name { get; }

	/// <summary>The <c>type</c>, without the quotes of a quoted value.</summary>
	public string? Type { get; set; }

	/// <summary>The <c>docRef</c>, without the quotes of a quoted value.</summary>
	public string? DocRef { get; set; }

	/// <summary>The classes after <c>:::</c>, or <c>null</c>.</summary>
	public List<string>? Classes { get; set; }
}

/// <summary>
/// <c>source - type -&gt; target</c>, or <c>target &lt;- type - source</c>. The ends need not name a requirement or an
/// element.
/// </summary>
public sealed class RequirementRelationship : RequirementStatement
{
	public RequirementRelationship(string source, string target, RequirementRelationshipType type)
	{
		ArgumentException.ThrowIfNullOrEmpty(source);
		ArgumentException.ThrowIfNullOrEmpty(target);
		Source = source;
		Target = target;
		Type = type;
	}

	public string Source { get; }

	public string Target { get; }

	public RequirementRelationshipType Type { get; }
}

/// <summary><c>classDef a,b styles</c>.</summary>
public sealed class RequirementClassDefinition : RequirementStatement
{
	public RequirementClassDefinition(List<string> names, List<string> styles)
	{
		ArgumentNullException.ThrowIfNull(names);
		ArgumentNullException.ThrowIfNull(styles);
		Names = names;
		Styles = styles;
	}

	public List<string> Names { get; }

	/// <summary>The comma-separated declarations, each with its spaces taken out, such as <c>fill:#f96</c>.</summary>
	public List<string> Styles { get; }
}

/// <summary><c>style a,b styles</c>: declarations for requirements and elements.</summary>
public sealed class RequirementStyle : RequirementStatement
{
	public RequirementStyle(List<string> ids, List<string> styles)
	{
		ArgumentNullException.ThrowIfNull(ids);
		ArgumentNullException.ThrowIfNull(styles);
		Ids = ids;
		Styles = styles;
	}

	public List<string> Ids { get; }

	/// <inheritdoc cref="RequirementClassDefinition.Styles"/>
	public List<string> Styles { get; }
}

/// <summary>
/// <c>class a,b c,d</c>, or <c>a:::c,d</c> on a line of its own: the classes <c>c</c> and <c>d</c> for the requirements
/// and elements <c>a</c> and <c>b</c>.
/// </summary>
public sealed class RequirementClassAssignment : RequirementStatement
{
	public RequirementClassAssignment(List<string> ids, List<string> classes)
	{
		ArgumentNullException.ThrowIfNull(ids);
		ArgumentNullException.ThrowIfNull(classes);
		Ids = ids;
		Classes = classes;
	}

	public List<string> Ids { get; }

	public List<string> Classes { get; }
}

/// <summary><c>direction TB</c>, <c>BT</c>, <c>RL</c> or <c>LR</c>. The last one sets the diagram's direction.</summary>
public sealed class RequirementDirection : RequirementStatement
{
	public RequirementDirection(string direction)
	{
		ArgumentException.ThrowIfNullOrEmpty(direction);
		Direction = direction;
	}

	public string Direction { get; }
}
