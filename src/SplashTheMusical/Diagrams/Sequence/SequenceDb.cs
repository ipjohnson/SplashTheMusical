using System.Text.Json;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Diagrams.State;
using SplashTheMusical.Parsing;

namespace SplashTheMusical.Diagrams.Sequence;

/// <summary>The db's <c>LINETYPE</c> values of what is not an arrow.</summary>
internal static class SequenceLineType
{
	public const int Note = 2;
	public const int LoopStart = 10;
	public const int LoopEnd = 11;
	public const int AltStart = 12;
	public const int AltElse = 13;
	public const int AltEnd = 14;
	public const int OptStart = 15;
	public const int OptEnd = 16;
	public const int ActiveStart = 17;
	public const int ActiveEnd = 18;
	public const int ParStart = 19;
	public const int ParAnd = 20;
	public const int ParEnd = 21;
	public const int RectStart = 22;
	public const int RectEnd = 23;
	public const int Autonumber = 26;
	public const int CriticalStart = 27;
	public const int CriticalOption = 28;
	public const int CriticalEnd = 29;
	public const int BreakStart = 30;
	public const int BreakEnd = 31;
	public const int ParOverStart = 32;
	public const int CentralConnection = 59;
	public const int CentralConnectionReverse = 60;
	public const int CentralConnectionDual = 61;
}

/// <summary>A participant of the db: its <c>Actor</c>.</summary>
internal sealed class SequenceDbActor(string name, string description, bool wrap, string type)
{
	public string Name { get; } = name;

	public string Description { get; } = description;

	public bool Wrap { get; } = wrap;

	/// <summary>mermaid's participant type: <c>participant</c>, <c>actor</c>, or what <c>@{ type }</c> gives.</summary>
	public string Type { get; } = type;

	public SequenceDbBox? Box { get; set; }

	/// <summary>The <c>prevActor</c> of the db when this record was set: the id added before it, which may be its own.</summary>
	public string? PrevActor { get; init; }

	/// <summary>The id of the next record set after this one, or <c>null</c>.</summary>
	public string? NextActor { get; set; }

	/// <summary>The labels of the actor's <c>links</c> menu, in JavaScript's key order.</summary>
	public List<string> Links { get; } = [];

	/// <summary><c>insertLinks</c> of one label: array indexes come first in numeric order, then other labels as added.</summary>
	public void AddLink(string label)
	{
		if (Links.Contains(label))
		{
			return;
		}

		if (!IsArrayIndex(label, out var index))
		{
			Links.Add(label);
			return;
		}

		var at = 0;
		while (at < Links.Count && IsArrayIndex(Links[at], out var other) && other < index)
		{
			at++;
		}

		Links.Insert(at, label);
	}

	private static bool IsArrayIndex(string key, out uint index)
	{
		index = 0;
		return key.Length > 0 && (key.Length == 1 || key[0] != '0') && key.All(char.IsAsciiDigit)
			&& uint.TryParse(key, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out index)
			&& index != uint.MaxValue;
	}
}

/// <summary>A box of the db: its <c>Box</c>.</summary>
internal sealed class SequenceDbBox(string? name, bool wrap, string fill)
{
	public string? Name { get; } = name;

	public bool Wrap { get; } = wrap;

	public string Fill { get; } = fill;

	/// <summary>The ids added inside the box, in order, with an id once for each statement that added it.</summary>
	public List<string> ActorKeys { get; } = [];
}

/// <summary>A message of the db: a signal, a note, an activation, a block's start, divider or end, or <c>autonumber</c>.</summary>
internal sealed class SequenceDbMessage
{
	public required int Id { get; init; }

	public string? From { get; init; }

	public string? To { get; init; }

	public string Text { get; init; } = "";

	public bool Wrap { get; init; }

	/// <summary>The <c>LINETYPE</c>: a <see cref="SequenceArrow"/> or one of <see cref="SequenceLineType"/>.</summary>
	public required int Type { get; init; }

	public bool Activate { get; init; }

	public SequenceNotePlacement? Placement { get; init; }

	/// <summary>The message's <c>centralConnection</c>: 0, or one of the <c>CENTRAL_CONNECTION</c> types.</summary>
	public int CentralConnection { get; init; }

	/// <summary>An <c>autonumber</c>'s start, or <c>null</c>.</summary>
	public double? Start { get; init; }

	/// <summary>An <c>autonumber</c>'s step, or <c>null</c>.</summary>
	public double? Step { get; init; }

	/// <summary>Whether an <c>autonumber</c> shows the numbers.</summary>
	public bool Visible { get; init; }
}

/// <summary>
/// The port of <c>SequenceDB</c>'s <c>apply</c> in <c>sequenceDb.ts</c>: the participants, boxes and messages it builds
/// from a document's statements. The parser has already refused what <c>apply</c> throws on. The <c>links</c>,
/// <c>link</c>, <c>properties</c> and <c>details</c> statements only add their participant, because their menus are
/// not drawn.
/// </summary>
internal sealed class SequenceDb
{
	private readonly Dictionary<string, int> _actorIndex = new(StringComparer.Ordinal);
	private readonly bool _autoWrap;
	private SequenceDbBox? _currentBox;
	private string? _prevActor;

	private SequenceDb(bool autoWrap) => _autoWrap = autoWrap;

	/// <summary>The participants in the order the db's <c>Map</c> holds them, which is the order each was first added.</summary>
	public List<SequenceDbActor> Actors { get; } = [];

	public List<SequenceDbBox> Boxes { get; } = [];

	public List<SequenceDbMessage> Messages { get; } = [];

	/// <summary>The created participants, each with the number of messages before its <c>create</c>.</summary>
	public Dictionary<string, int> CreatedActors { get; } = new(StringComparer.Ordinal);

	/// <summary>The destroyed participants, each with the number of messages before its <c>destroy</c>.</summary>
	public Dictionary<string, int> DestroyedActors { get; } = new(StringComparer.Ordinal);

	/// <summary>The db of <paramref name="document"/>. <paramref name="autoWrap"/> is the db's <c>autoWrap</c>, from <c>sequence.wrap</c>.</summary>
	public static SequenceDb From(SequenceDocument document, bool autoWrap = false)
	{
		ArgumentNullException.ThrowIfNull(document);
		var db = new SequenceDb(autoWrap);
		db.Apply(document.Statements);
		return db;
	}

	public SequenceDbActor? Actor(string id) => _actorIndex.TryGetValue(id, out var index) ? Actors[index] : null;

	private void Apply(List<SequenceStatement> statements)
	{
		foreach (var statement in statements)
		{
			switch (statement)
			{
				case SequenceParticipant participant:
					AddActor(
						participant.Id,
						participant.Description is { } text ? (text, participant.Wrap) : null,
						participant.Kind == SequenceParticipantKind.Actor ? "actor" : "participant",
						participant.Metadata);
					if (participant.Created)
					{
						CreatedActors[participant.Id] = Messages.Count;
					}

					break;
				case SequenceDestroy destroy:
					DestroyedActors[destroy.Id] = Messages.Count;
					break;
				case SequenceBox box:
					_currentBox = new SequenceDbBox(box.Title, box.Wrap ?? _autoWrap, box.Color);
					Boxes.Add(_currentBox);
					Apply(box.Statements);
					_currentBox = null;
					break;
				case SequenceMessage message:
					ApplyMessage(message);
					break;
				case SequenceNote note:
					AddActor(note.Actor);
					var second = note.SecondActor ?? note.Actor;
					if (note.SecondActor is not null)
					{
						AddActor(second);
					}

					Add(note.Actor, note.Placement == SequenceNotePlacement.Over ? second : note.Actor, note.Text, note.Wrap, SequenceLineType.Note, placement: note.Placement);
					break;
				case SequenceActivation activation:
					Add(activation.Id, null, "", null, activation.Active ? SequenceLineType.ActiveStart : SequenceLineType.ActiveEnd);
					break;
				case SequenceAutonumber autonumber:
					Messages.Add(new SequenceDbMessage
					{
						Id = Messages.Count,
						Type = SequenceLineType.Autonumber,
						Start = autonumber.Start,
						Step = autonumber.Start is null ? null : autonumber.Step ?? 1,
						Visible = autonumber.Visible,
					});
					break;
				case SequenceActorData data:
					AddActor(data.Actor);
					AddLinks(data);
					break;
				case SequenceBlock block:
					ApplyBlock(block);
					break;
			}
		}
	}

	private void ApplyMessage(SequenceMessage message)
	{
		AddActor(message.From);
		AddActor(message.To);
		var (activate, central) = message.Modifier switch
		{
			SequenceMessageModifier.Activate => (true, 0),
			SequenceMessageModifier.CentralConnection => (true, SequenceLineType.CentralConnection),
			SequenceMessageModifier.CentralConnectionReverse => (false, SequenceLineType.CentralConnectionReverse),
			SequenceMessageModifier.CentralConnectionDual => (true, SequenceLineType.CentralConnectionDual),
			_ => (false, 0),
		};
		Add(message.From, message.To, message.Text, message.Wrap, (int)message.Arrow, activate, central);
		switch (message.Modifier)
		{
			case SequenceMessageModifier.Activate:
				Add(message.To, null, "", null, SequenceLineType.ActiveStart);
				break;
			case SequenceMessageModifier.Deactivate:
				Add(message.From, null, "", null, SequenceLineType.ActiveEnd);
				break;
			case SequenceMessageModifier.CentralConnection:
				Add(message.To, null, "", null, SequenceLineType.CentralConnection);
				break;
			case SequenceMessageModifier.CentralConnectionReverse:
				Add(message.From, null, "", null, SequenceLineType.CentralConnectionReverse);
				break;
			case SequenceMessageModifier.CentralConnectionDual:
				Add(message.To, null, "", null, SequenceLineType.CentralConnection);
				Add(message.From, null, "", null, SequenceLineType.CentralConnectionReverse);
				break;
		}
	}

	private void ApplyBlock(SequenceBlock block)
	{
		var (start, divider, end) = block.Kind switch
		{
			SequenceBlockKind.Loop => (SequenceLineType.LoopStart, 0, SequenceLineType.LoopEnd),
			SequenceBlockKind.Rect => (SequenceLineType.RectStart, 0, SequenceLineType.RectEnd),
			SequenceBlockKind.Opt => (SequenceLineType.OptStart, 0, SequenceLineType.OptEnd),
			SequenceBlockKind.Alt => (SequenceLineType.AltStart, SequenceLineType.AltElse, SequenceLineType.AltEnd),
			SequenceBlockKind.Par => (SequenceLineType.ParStart, SequenceLineType.ParAnd, SequenceLineType.ParEnd),
			SequenceBlockKind.ParOver => (SequenceLineType.ParOverStart, SequenceLineType.ParAnd, SequenceLineType.ParEnd),
			SequenceBlockKind.Critical => (SequenceLineType.CriticalStart, SequenceLineType.CriticalOption, SequenceLineType.CriticalEnd),
			_ => (SequenceLineType.BreakStart, 0, SequenceLineType.BreakEnd),
		};
		Add(null, null, block.Text, block.Wrap, start);
		Apply(block.Statements);
		foreach (var section in block.Sections)
		{
			Add(null, null, section.Text, section.Wrap, divider);
			Apply(section.Statements);
		}

		Add(null, null, "", null, end);
	}

	private void Add(string? from, string? to, string text, bool? wrap, int type, bool activate = false, int central = 0, SequenceNotePlacement? placement = null) =>
		Messages.Add(new SequenceDbMessage
		{
			Id = Messages.Count,
			From = from,
			To = to,
			Text = text,
			Wrap = wrap ?? _autoWrap,
			Type = type,
			Activate = activate,
			CentralConnection = central,
			Placement = placement,
		});

	/// <summary>
	/// <c>addLinks</c> and <c>addALink</c>: the labels of a <c>links</c> object or a <c>link</c> line. Text that
	/// <c>JSON.parse</c> refuses adds none, as the db logs the error and goes on. <c>details</c> reads an element of the
	/// page, which the oracle's has not, so it adds none either.
	/// </summary>
	private void AddLinks(SequenceActorData data)
	{
		if (Actor(data.Actor) is not { } actor)
		{
			return;
		}

		var text = data.Text.Replace("&equals;", "=", StringComparison.Ordinal).Replace("&amp;", "&", StringComparison.Ordinal);
		if (data.Kind == SequenceActorDataKind.Link)
		{
			var sep = text.IndexOf('@', StringComparison.Ordinal);
			var end = sep - 1 < 0 ? Math.Max(0, text.Length + sep - 1) : sep - 1;
			actor.AddLink(StateLexer.Trim(text.AsSpan(0, Math.Min(end, text.Length))).ToString());
			return;
		}

		if (data.Kind != SequenceActorDataKind.Links)
		{
			return;
		}

		var labels = new List<string>();
		try
		{
			var reader = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(text), new JsonReaderOptions { MaxDepth = 64 });
			if (!reader.Read())
			{
				return;
			}

			switch (reader.TokenType)
			{
				case JsonTokenType.StartObject:
					while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
					{
						labels.Add(reader.GetString()!);
						reader.Skip();
					}

					break;
				case JsonTokenType.StartArray:
					var count = 0;
					while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
					{
						reader.Skip();
						labels.Add(JsNumber.ToString(count++));
					}

					break;
				case JsonTokenType.String:
					var length = reader.GetString()!.Length;
					for (var i = 0; i < length; i++)
					{
						labels.Add(JsNumber.ToString(i));
					}

					break;
				default:
					reader.Skip();
					break;
			}

			while (reader.Read())
			{
			}
		}
		catch (JsonException)
		{
			return;
		}

		foreach (var label in labels)
		{
			actor.AddLink(label);
		}
	}

	/// <summary>The db's <c>addActor</c> for an id a message, note or menu names, with no description and no type.</summary>
	private void AddActor(string id) => AddActor(id, null, null, null);

	/// <summary>The db's <c>addActor</c>.</summary>
	private void AddActor(string id, (string Text, bool? Wrap)? description, string? type, string? metadata)
	{
		var assignedBox = _currentBox;
		var doc = Metadata(metadata);
		if (doc?["type"] is { } docType && Scalar(docType) is { } typeText)
		{
			type = typeText;
		}

		if (doc?["alias"] is { } alias && IsTruthy(alias) && Scalar(alias) is { } aliasText && (description is null || description.Value.Text == id))
		{
			description = (Restore(aliasText), description?.Wrap);
		}

		if (_actorIndex.TryGetValue(id, out var existing))
		{
			var old = Actors[existing];
			assignedBox = old.Box ?? _currentBox;
			old.Box = assignedBox;
			if (description is null)
			{
				return;
			}
		}

		if (type is null)
		{
			description = null;
		}

		var actor = new SequenceDbActor(id, description?.Text ?? id, description?.Wrap ?? _autoWrap, type ?? "participant")
		{
			Box = assignedBox,
			PrevActor = _prevActor,
		};
		if (_actorIndex.TryGetValue(id, out existing))
		{
			Actors[existing] = actor;
		}
		else
		{
			_actorIndex[id] = Actors.Count;
			Actors.Add(actor);
		}

		if (_prevActor is not null && Actor(_prevActor) is { } previous)
		{
			previous.NextActor = id;
		}

		_currentBox?.ActorKeys.Add(id);
		_prevActor = id;
	}

	/// <summary>
	/// The db's <c>yaml.load</c> of <c>@{ }</c> text: in braces when it is one line, and as a document when it spans
	/// lines, with mermaid.js's entity placeholders in it.
	/// </summary>
	private static MermaidValue? Metadata(string? metadata)
	{
		if (metadata is null)
		{
			return null;
		}

		var encoded = SequenceWriter.Encode(metadata);
		var yaml = encoded.Contains('\n', StringComparison.Ordinal) ? encoded + "\n" : "{\n" + encoded + "\n}";
		return YamlReader.Read(yaml, [], ResourceLimits.Unlimited) is { Kind: MermaidValueKind.Mapping } value ? value : null;
	}

	/// <summary>JavaScript's <c>String</c> of a scalar, or <c>null</c> for null and a collection.</summary>
	private static string? Scalar(MermaidValue value) => value.Kind switch
	{
		MermaidValueKind.String => value.String,
		MermaidValueKind.Number => JsNumber.ToString(value.Number),
		MermaidValueKind.Boolean => value.Boolean ? "true" : "false",
		_ => null,
	};

	private static bool IsTruthy(MermaidValue value) => value.Kind switch
	{
		MermaidValueKind.String => value.String.Length > 0,
		MermaidValueKind.Number => value.Number != 0 && !double.IsNaN(value.Number),
		MermaidValueKind.Boolean => value.Boolean,
		MermaidValueKind.Null => false,
		_ => true,
	};

	/// <summary>The text with the placeholders of <see cref="SequenceWriter.Encode"/> back as the characters they stand for.</summary>
	internal static string Restore(string text)
	{
		if (!FlowchartText.HasPlaceholder(text))
		{
			return text;
		}

		var output = new PooledList<char>(text.Length);
		try
		{
			FlowchartText.Restore(text, ref output);
			return output.AsSpan().ToString();
		}
		finally
		{
			output.Dispose();
		}
	}
}
