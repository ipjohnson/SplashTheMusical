namespace SplashTheMusical.Diagrams.Class;

/// <summary>
/// A class diagram: the statements <c>classDiagram.jison</c> parses, in source order, with a namespace's statements
/// inside it. mermaid.js's <c>ClassDB</c> turns them into classes, relations, notes and namespaces when it reads the
/// diagram, so the model keeps the statements, and <see cref="ClassData"/> builds what the db holds. Names are the
/// db's names: the tokens of a name joined as the grammar joins them, such as <c>List~int~</c> or <c>a.b</c>. Text keeps
/// entity codes such as <c>#quot;</c> as written.
/// </summary>
public sealed class ClassDocument : MermaidDocument
{
	public override string DiagramType => "classDiagram";

	/// <summary>The header keyword: <c>classDiagram</c> or <c>classDiagram-v2</c>. mermaid.js reads both the same way.</summary>
	public string Keyword { get; set; } = "classDiagram";

	public List<ClassStatement> Statements { get; init; } = [];
}

/// <summary>One statement of a class diagram's body or of a namespace.</summary>
public abstract class ClassStatement;

/// <summary>The end of a relation, as <c>relationType</c> names it, or <see cref="None"/> for a plain line end.</summary>
public enum ClassRelationEnd
{
	None,

	/// <summary><c>o</c>.</summary>
	Aggregation,

	/// <summary><c>&lt;|</c> or <c>|&gt;</c>.</summary>
	Extension,

	/// <summary><c>*</c>.</summary>
	Composition,

	/// <summary><c>&lt;</c> or <c>&gt;</c>.</summary>
	Dependency,

	/// <summary><c>()</c>.</summary>
	Lollipop,
}

/// <summary>The line of a relation.</summary>
public enum ClassLineStyle
{
	/// <summary><c>--</c>.</summary>
	Solid,

	/// <summary><c>..</c>.</summary>
	Dotted,
}

/// <summary>
/// A relation, <c>from "1" &lt;|-- "*" to : label</c>: the ends of the line, the cardinality strings and the label.
/// </summary>
public sealed class ClassRelation : ClassStatement
{
	public ClassRelation(string from, string to)
	{
		ArgumentException.ThrowIfNullOrEmpty(from);
		ArgumentException.ThrowIfNullOrEmpty(to);
		From = from;
		To = to;
	}

	public string From { get; }

	public string To { get; }

	/// <summary>The end drawn at <see cref="From"/>.</summary>
	public ClassRelationEnd FromEnd { get; set; }

	/// <summary>The end drawn at <see cref="To"/>.</summary>
	public ClassRelationEnd ToEnd { get; set; }

	public ClassLineStyle Line { get; set; }

	/// <summary>The quoted string before the line, or <c>null</c>. mermaid.js reads <c>none</c> as no cardinality.</summary>
	public string? FromCardinality { get; set; }

	/// <summary>The quoted string after the line, or <c>null</c>.</summary>
	public string? ToCardinality { get; set; }

	/// <summary>The text after the colon, trimmed, or <c>null</c>.</summary>
	public string? Label { get; set; }
}

/// <summary>
/// A <c>class</c> statement: <c>class name["label"]</c>, with <c>:::cssClass</c> or <c>&lt;&lt;annotation&gt;&gt;</c>
/// after it, and the members between <c>{ }</c>.
/// </summary>
public sealed class ClassDefinition : ClassStatement
{
	public ClassDefinition(string name)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);
		Name = name;
	}

	public string Name { get; }

	/// <summary>The quoted label of <c>["label"]</c>, or <c>null</c>.</summary>
	public string? Label { get; set; }

	/// <summary>The class of <c>:::cssClass</c>, or <c>null</c>. A definition has this or <see cref="Annotation"/>, not both.</summary>
	public string? CssClass { get; set; }

	/// <summary>The annotation of <c>&lt;&lt;annotation&gt;&gt;</c>, or <c>null</c>.</summary>
	public string? Annotation { get; set; }

	/// <summary>
	/// The lines between the braces that are not blank, each trimmed, or <c>null</c> when there are no braces. mermaid.js
	/// reads a line in <c>&lt;&lt; &gt;&gt;</c> as an annotation, a line with a <c>)</c> after its first character as a
	/// method, and any other line as an attribute.
	/// </summary>
	public List<string>? Members { get; set; }
}

/// <summary><c>name</c> on its own, which adds nothing, or <c>name : member</c>, which adds the class and the member.</summary>
public sealed class ClassMemberStatement : ClassStatement
{
	public ClassMemberStatement(string className)
	{
		ArgumentException.ThrowIfNullOrEmpty(className);
		ClassName = className;
	}

	public string ClassName { get; }

	/// <summary>The text after the colon, trimmed, or <c>null</c> for a name on its own.</summary>
	public string? Member { get; set; }
}

/// <summary><c>&lt;&lt;annotation&gt;&gt; name</c>, for a class an earlier statement adds.</summary>
public sealed class ClassAnnotation : ClassStatement
{
	public ClassAnnotation(string annotation, string className)
	{
		ArgumentException.ThrowIfNullOrEmpty(annotation);
		ArgumentException.ThrowIfNullOrEmpty(className);
		Annotation = annotation;
		ClassName = className;
	}

	public string Annotation { get; }

	public string ClassName { get; }
}

/// <summary>
/// <c>namespace name["label"] { }</c>, which holds classes, notes and namespaces. A dotted name, such as <c>a.b</c>,
/// adds each namespace of the path.
/// </summary>
public sealed class ClassNamespace : ClassStatement
{
	public ClassNamespace(string name)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);
		Name = name;
	}

	/// <summary>The name as written, without the names of the namespaces around it.</summary>
	public string Name { get; }

	/// <summary>The quoted label of <c>["label"]</c>, or <c>null</c>.</summary>
	public string? Label { get; set; }

	/// <summary>The <see cref="ClassDefinition"/>, <see cref="ClassNote"/> and <see cref="ClassNamespace"/> statements inside it.</summary>
	public List<ClassStatement> Statements { get; init; } = [];
}

/// <summary><c>note "text"</c>, or <c>note for name "text"</c>.</summary>
public sealed class ClassNote : ClassStatement
{
	public ClassNote(string text)
	{
		ArgumentNullException.ThrowIfNull(text);
		Text = text;
	}

	public string Text { get; }

	/// <summary>The class of <c>note for</c>, or <c>null</c>.</summary>
	public string? ClassName { get; set; }
}

/// <summary>The forms of an interaction statement.</summary>
public enum ClassInteractionKind
{
	/// <summary><c>callback name "function" "tooltip"</c>.</summary>
	Callback,

	/// <summary><c>link name "url" "tooltip" target</c>.</summary>
	Link,

	/// <summary><c>click name call function(arguments) "tooltip"</c>.</summary>
	ClickCall,

	/// <summary><c>click name href "url" "tooltip" target</c>.</summary>
	ClickHref,
}

/// <summary>A <c>callback</c>, <c>link</c> or <c>click</c> statement.</summary>
public sealed class ClassInteraction : ClassStatement
{
	public ClassInteraction(ClassInteractionKind kind, string className, string target)
	{
		ArgumentException.ThrowIfNullOrEmpty(className);
		ArgumentNullException.ThrowIfNull(target);
		Kind = kind;
		ClassName = className;
		Target = target;
	}

	public ClassInteractionKind Kind { get; }

	public string ClassName { get; }

	/// <summary>The function of a callback, as written after <c>call</c> up to its <c>(</c>, or the URL of a link.</summary>
	public string Target { get; }

	/// <summary>The text between the parentheses of <c>call function(arguments)</c>, or <c>null</c> when it is blank.</summary>
	public string? Arguments { get; set; }

	public string? Tooltip { get; set; }

	/// <summary>The <c>_self</c>, <c>_blank</c>, <c>_parent</c> or <c>_top</c> of a link, or <c>null</c>.</summary>
	public string? LinkTarget { get; set; }
}

/// <summary><c>style id styles</c>.</summary>
public sealed class ClassStyle : ClassStatement
{
	public ClassStyle(string id, List<string> styles)
	{
		ArgumentException.ThrowIfNullOrEmpty(id);
		ArgumentNullException.ThrowIfNull(styles);
		Id = id;
		Styles = styles;
	}

	public string Id { get; }

	/// <summary>The styles between the commas the lexer reads as commas. A style's tokens are joined without the space between them.</summary>
	public List<string> Styles { get; }
}

/// <summary><c>classDef a,b styles</c>.</summary>
public sealed class ClassStyleDefinition : ClassStatement
{
	public ClassStyleDefinition(List<string> names, List<string> styles)
	{
		ArgumentNullException.ThrowIfNull(names);
		ArgumentNullException.ThrowIfNull(styles);
		Names = names;
		Styles = styles;
	}

	public List<string> Names { get; }

	/// <inheritdoc cref="ClassStyle.Styles"/>
	public List<string> Styles { get; }
}

/// <summary><c>cssClass "ids" className</c>, where the ids are separated by commas.</summary>
public sealed class ClassCssClass : ClassStatement
{
	public ClassCssClass(string ids, string className)
	{
		ArgumentNullException.ThrowIfNull(ids);
		ArgumentException.ThrowIfNullOrEmpty(className);
		Ids = ids;
		ClassName = className;
	}

	public string Ids { get; }

	public string ClassName { get; }
}

/// <summary><c>direction TB</c>, <c>BT</c>, <c>RL</c> or <c>LR</c>.</summary>
public sealed class ClassDirection : ClassStatement
{
	public ClassDirection(string direction)
	{
		ArgumentException.ThrowIfNullOrEmpty(direction);
		Direction = direction;
	}

	public string Direction { get; }
}
