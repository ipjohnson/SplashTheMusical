namespace SplashTheMusical.Diagrams.State;

/// <summary>
/// A state diagram: the statements <c>stateDiagram.jison</c> parses, in source order, with a composite state's
/// statements inside it. mermaid.js's <c>StateDB</c> turns them into states and transitions when it reads the diagram,
/// so the model keeps the statements and not the states. Text keeps entity codes such as <c>#quot;</c> as written.
/// </summary>
public sealed class StateDocument : MermaidDocument
{
	public override string DiagramType => "stateDiagram";

	/// <summary>The header keyword: <c>stateDiagram</c> or <c>stateDiagram-v2</c>. mermaid.js reads both the same way.</summary>
	public string Keyword { get; set; } = "stateDiagram-v2";

	public List<StateStatement> Statements { get; init; } = [];
}

/// <summary>One statement of a state diagram's body or of a composite state.</summary>
public abstract class StateStatement;

/// <summary>
/// A state as a statement or a transition names it: an id, or <c>[*]</c> for the start or end of the document it is in,
/// and the class of <c>:::class</c>.
/// </summary>
public sealed class StateRef
{
	public const string StartOrEnd = "[*]";

	public StateRef(string id)
	{
		ArgumentException.ThrowIfNullOrEmpty(id);
		Id = id;
	}

	public string Id { get; }

	/// <summary>The class of <c>id:::class</c>, or <c>null</c>.</summary>
	public string? ClassName { get; set; }
}

/// <summary>A state named on its own, <c>A</c>, or with a description, <c>A : text</c>.</summary>
public sealed class StateDeclaration : StateStatement
{
	public StateDeclaration(StateRef state)
	{
		ArgumentNullException.ThrowIfNull(state);
		State = state;
	}

	public StateRef State { get; }

	/// <summary>The text after the colon, trimmed, or <c>null</c>.</summary>
	public string? Description { get; set; }
}

/// <summary>A transition, <c>A --&gt; B</c>, with the label of <c>A --&gt; B : label</c>.</summary>
public sealed class StateTransition : StateStatement
{
	public StateTransition(StateRef from, StateRef to)
	{
		ArgumentNullException.ThrowIfNull(from);
		ArgumentNullException.ThrowIfNull(to);
		From = from;
		To = to;
	}

	public StateRef From { get; }

	public StateRef To { get; }

	/// <summary>The text after the colon, trimmed, or <c>null</c>.</summary>
	public string? Label { get; set; }
}

/// <summary>What a <c>state</c> statement declares.</summary>
public enum StateKind
{
	/// <summary>A plain or composite state.</summary>
	Default,

	/// <summary><c>state id &lt;&lt;fork&gt;&gt;</c> or <c>state id [[fork]]</c>.</summary>
	Fork,

	/// <summary><c>state id &lt;&lt;join&gt;&gt;</c> or <c>state id [[join]]</c>.</summary>
	Join,

	/// <summary><c>state id &lt;&lt;choice&gt;&gt;</c> or <c>state id [[choice]]</c>.</summary>
	Choice,
}

/// <summary>
/// A statement that starts with <c>state</c>: <c>state id</c>, <c>state "description" as id</c>, either of them with
/// <c>{ }</c> around the statements of a composite state, or a fork, join or choice.
/// </summary>
/// <remarks>
/// mermaid.js adds nothing for <c>state id</c> without a description or braces. In <c>state "description" as id:more</c>
/// the id is the text before the first colon, and the text between the first and second colon is a second description.
/// </remarks>
public sealed class StateBlock : StateStatement
{
	public StateBlock(string id)
	{
		ArgumentNullException.ThrowIfNull(id);
		Id = id;
	}

	/// <summary>The id as written, trimmed. A fork, join or choice id is the text between <c>state</c> and its marker.</summary>
	public string Id { get; }

	public StateKind Kind { get; set; }

	/// <summary>The quoted description of <c>state "description" as id</c>, or <c>null</c>.</summary>
	public string? Description { get; set; }

	/// <summary>The statements between the braces of a composite state, or <c>null</c> when it has no braces.</summary>
	public List<StateStatement>? Statements { get; set; }
}

/// <summary><c>--</c>, which splits a composite state into concurrent regions.</summary>
public sealed class StateDivider : StateStatement;

public enum StateNotePosition
{
	LeftOf,
	RightOf,
}

/// <summary>
/// <c>note left of id : text</c>, or the same with the text on the lines up to <c>end note</c>. A note on a state that no
/// statement has named adds the state.
/// </summary>
public sealed class StateNote : StateStatement
{
	public StateNote(StateNotePosition position, string id, string text)
	{
		ArgumentNullException.ThrowIfNull(id);
		ArgumentNullException.ThrowIfNull(text);
		Position = position;
		Id = id;
		Text = text;
	}

	public StateNotePosition Position { get; }

	public string Id { get; }

	/// <summary>The text, trimmed. Lines of a multi-line note are separated by <c>\n</c>.</summary>
	public string Text { get; }
}

/// <summary><c>note "text" as id</c>. mermaid.js parses it and draws nothing for it.</summary>
public sealed class StateFloatingNote : StateStatement
{
	public StateFloatingNote(string text, string id)
	{
		ArgumentNullException.ThrowIfNull(text);
		ArgumentNullException.ThrowIfNull(id);
		Text = text;
		Id = id;
	}

	public string Text { get; }

	public string Id { get; }
}

/// <summary><c>classDef name styles</c>.</summary>
public sealed class StateClassDefinition : StateStatement
{
	public StateClassDefinition(string name, string styles)
	{
		ArgumentNullException.ThrowIfNull(name);
		ArgumentNullException.ThrowIfNull(styles);
		Name = name;
		Styles = styles;
	}

	public string Name { get; }

	/// <summary>The declarations as written, such as <c>fill:#f96,color:white</c>, trimmed.</summary>
	public string Styles { get; }
}

/// <summary><c>style ids styles</c>: declarations for one or more comma-separated states.</summary>
public sealed class StateStyle : StateStatement
{
	public StateStyle(string ids, string styles)
	{
		ArgumentNullException.ThrowIfNull(ids);
		ArgumentNullException.ThrowIfNull(styles);
		Ids = ids;
		Styles = styles;
	}

	/// <summary>The ids as written, separated by commas.</summary>
	public string Ids { get; }

	public string Styles { get; }
}

/// <summary><c>class ids className</c>: a class for one or more comma-separated states.</summary>
public sealed class StateClassAssignment : StateStatement
{
	public StateClassAssignment(string ids, string className)
	{
		ArgumentNullException.ThrowIfNull(ids);
		ArgumentNullException.ThrowIfNull(className);
		Ids = ids;
		ClassName = className;
	}

	/// <summary>The ids as written, separated by commas.</summary>
	public string Ids { get; }

	public string ClassName { get; }
}

/// <summary><c>direction TB</c>, <c>BT</c>, <c>LR</c> or <c>RL</c>.</summary>
public sealed class StateDirection : StateStatement
{
	public StateDirection(string direction)
	{
		ArgumentNullException.ThrowIfNull(direction);
		Direction = direction;
	}

	public string Direction { get; }
}

/// <summary><c>hide empty description</c>. mermaid.js parses it and draws the same without it.</summary>
public sealed class StateHideEmptyDescription : StateStatement;

/// <summary><c>scale 350 width</c>. mermaid.js parses it and draws the same without it.</summary>
public sealed class StateScale : StateStatement
{
	public StateScale(string width)
	{
		ArgumentNullException.ThrowIfNull(width);
		Width = width;
	}

	/// <summary>The digits of the width.</summary>
	public string Width { get; }
}

/// <summary><c>click id "url" "tooltip"</c> or <c>click id href "url"</c>.</summary>
public sealed class StateClick : StateStatement
{
	public StateClick(StateRef state, string url)
	{
		ArgumentNullException.ThrowIfNull(state);
		ArgumentNullException.ThrowIfNull(url);
		State = state;
		Url = url;
	}

	public StateRef State { get; }

	/// <summary>The URL, without its quotes.</summary>
	public string Url { get; }

	/// <summary>The tooltip of the first form, without its quotes, or <c>null</c> for the <c>href</c> form.</summary>
	public string? Tooltip { get; set; }
}
