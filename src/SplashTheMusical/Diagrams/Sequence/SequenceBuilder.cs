namespace SplashTheMusical.Diagrams.Sequence;

/// <summary>
/// A short fluent way to build a <see cref="SequenceDocument"/> for the common case: participants, messages, notes and
/// <c>autonumber</c>. Each call adds the statement the source would have, in order. For boxes, blocks, activations and
/// the other statements, add them to <see cref="SequenceDocument.Statements"/>.
/// </summary>
/// <example>
/// <code>
/// var document = new SequenceBuilder()
///     .Actor("user", "User")
///     .Participant("api", "API", type: "boundary")
///     .Message("user", "api", "GET /orders")
///     .Message("api", "user", "200 OK", SequenceArrow.Dotted)
///     .Note("api", "cached", SequenceNotePlacement.RightOf)
///     .Build();
/// </code>
/// </example>
public sealed class SequenceBuilder
{
	private readonly SequenceDocument _document = new();

	/// <summary>Sets the <c>title</c> statement's text.</summary>
	public SequenceBuilder Title(string title)
	{
		_document.DiagramTitle = title;
		return this;
	}

	/// <summary>
	/// Adds <c>participant id</c>, with <c>as description</c> when it is not <c>null</c> and <c>@{ "type": … }</c> when
	/// <paramref name="type"/> is not <c>null</c>, such as <c>database</c> or <c>queue</c>.
	/// </summary>
	public SequenceBuilder Participant(string id, string? description = null, string? type = null) =>
		Add(new SequenceParticipant(id) { Description = description, Metadata = Metadata(type) });

	/// <summary>Adds <c>actor id</c>, with <c>as description</c> when it is not <c>null</c>.</summary>
	public SequenceBuilder Actor(string id, string? description = null) =>
		Add(new SequenceParticipant(id, SequenceParticipantKind.Actor) { Description = description });

	/// <summary>Adds <c>from arrow to: text</c>.</summary>
	public SequenceBuilder Message(
		string from,
		string to,
		string text,
		SequenceArrow arrow = SequenceArrow.Solid,
		SequenceMessageModifier modifier = SequenceMessageModifier.None) =>
		Add(new SequenceMessage(from, to, arrow) { Text = text, Modifier = modifier });

	/// <summary>Adds <c>Note placement actor: text</c>, or <c>Note over actor,secondActor: text</c>.</summary>
	public SequenceBuilder Note(string actor, string text, SequenceNotePlacement placement = SequenceNotePlacement.Over, string? secondActor = null) =>
		Add(new SequenceNote(placement, actor, secondActor) { Text = text });

	/// <summary>Adds <c>autonumber</c>, with a start and a step when they are not <c>null</c>.</summary>
	public SequenceBuilder Autonumber(double? start = null, double? step = null) =>
		Add(new SequenceAutonumber { Start = start, Step = step });

	public SequenceDocument Build() => _document;

	private SequenceBuilder Add(SequenceStatement statement)
	{
		_document.Statements.Add(statement);
		return this;
	}

	private static string? Metadata(string? type)
	{
		if (type is null)
		{
			return null;
		}

		if (type.AsSpan().ContainsAny("\"\\\n\r}"))
		{
			throw new ArgumentException($"The type {type} cannot be written in @{{ }}.", nameof(type));
		}

		return $"\"type\": \"{type}\"";
	}
}
