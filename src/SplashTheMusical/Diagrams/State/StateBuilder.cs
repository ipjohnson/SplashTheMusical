namespace SplashTheMusical.Diagrams.State;

/// <summary>
/// A short fluent way to build a <see cref="StateDocument"/> for the common case: states, transitions, composite states,
/// notes and classes. Each call adds the statement the source would have, in order. <c>[*]</c> is the start or end of
/// the document the call is in. For the rest of the model, add statements to <see cref="StateDocument.Statements"/>.
/// </summary>
/// <example>
/// <code>
/// var document = new StateBuilder()
///     .Transition("[*]", "Idle")
///     .Transition("Idle", "Busy", "start")
///     .Composite("Busy", "Working", busy => busy.Transition("[*]", "Step"))
///     .Build();
/// </code>
/// </example>
public sealed class StateBuilder
{
	private readonly List<StateStatement> _statements;
	private readonly StateDocument? _document;

	/// <param name="direction"><c>TB</c>, <c>BT</c>, <c>LR</c> or <c>RL</c>. A direction other than <c>TB</c> adds a <c>direction</c> statement.</param>
	public StateBuilder(string direction = "TB")
	{
		_document = new StateDocument();
		_statements = _document.Statements;
		if (direction != "TB")
		{
			_ = Direction(direction);
		}
	}

	private StateBuilder(List<StateStatement> statements) => _statements = statements;

	/// <summary>Adds <c>id</c>, or <c>id : description</c>.</summary>
	public StateBuilder State(string id, string? description = null)
	{
		_statements.Add(new StateDeclaration(Ref(id)) { Description = description });
		return this;
	}

	/// <summary>Adds <c>from --&gt; to</c>, or <c>from --&gt; to : label</c>.</summary>
	public StateBuilder Transition(string from, string to, string? label = null)
	{
		_statements.Add(new StateTransition(Ref(from), Ref(to)) { Label = label });
		return this;
	}

	/// <summary>
	/// Adds a composite state, <c>state id { }</c>, or <c>state "title" as id { }</c> with a title, whose statements
	/// <paramref name="body"/> adds.
	/// </summary>
	public StateBuilder Composite(string id, string? title, Action<StateBuilder> body)
	{
		ArgumentNullException.ThrowIfNull(body);
		var statements = new List<StateStatement>();
		body(new StateBuilder(statements));
		_statements.Add(new StateBlock(Id(id)) { Description = title, Statements = statements });
		return this;
	}

	/// <summary>Adds <c>--</c>, which starts a new concurrent region of the composite state the call is in.</summary>
	public StateBuilder Divider()
	{
		_statements.Add(new StateDivider());
		return this;
	}

	/// <summary>Adds <c>state id &lt;&lt;fork&gt;&gt;</c>.</summary>
	public StateBuilder Fork(string id) => Pseudostate(id, StateKind.Fork);

	/// <summary>Adds <c>state id &lt;&lt;join&gt;&gt;</c>.</summary>
	public StateBuilder Join(string id) => Pseudostate(id, StateKind.Join);

	/// <summary>Adds <c>state id &lt;&lt;choice&gt;&gt;</c>.</summary>
	public StateBuilder Choice(string id) => Pseudostate(id, StateKind.Choice);

	/// <summary>Adds a note beside a state.</summary>
	public StateBuilder Note(string id, string text, StateNotePosition position = StateNotePosition.RightOf)
	{
		_statements.Add(new StateNote(position, Id(id), text));
		return this;
	}

	/// <summary>Adds <c>classDef name styles</c>, with the declarations separated by commas.</summary>
	public StateBuilder ClassDef(string name, params string[] styles)
	{
		ArgumentNullException.ThrowIfNull(styles);
		_statements.Add(new StateClassDefinition(Id(name), string.Join(',', styles)));
		return this;
	}

	/// <summary>Adds <c>class ids className</c>.</summary>
	public StateBuilder Class(string className, params string[] ids)
	{
		ArgumentNullException.ThrowIfNull(ids);
		_statements.Add(new StateClassAssignment(string.Join(',', ids), Id(className)));
		return this;
	}

	/// <summary>Adds <c>style id styles</c>, with the declarations separated by commas.</summary>
	public StateBuilder Style(string id, params string[] styles)
	{
		ArgumentNullException.ThrowIfNull(styles);
		_statements.Add(new StateStyle(Id(id), string.Join(',', styles)));
		return this;
	}

	/// <summary>Adds <c>direction</c>, which sets the direction of the document the call is in.</summary>
	public StateBuilder Direction(string direction)
	{
		if (direction is not ("TB" or "BT" or "LR" or "RL"))
		{
			throw new ArgumentException($"A state diagram's direction is TB, BT, LR or RL, not '{direction}'.", nameof(direction));
		}

		_statements.Add(new StateDirection(direction));
		return this;
	}

	/// <summary>The document. Only the builder the constructor makes has one.</summary>
	public StateDocument Build() =>
		_document ?? throw new InvalidOperationException("The builder of a composite state's body has no document of its own.");

	private StateBuilder Pseudostate(string id, StateKind kind)
	{
		_statements.Add(new StateBlock(Id(id)) { Kind = kind });
		return this;
	}

	private static StateRef Ref(string id) => new(Id(id));

	private static string Id(string id)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		return id;
	}
}
