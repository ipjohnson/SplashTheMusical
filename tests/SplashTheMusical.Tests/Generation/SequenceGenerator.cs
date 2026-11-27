using CsCheck;
using SplashTheMusical.Diagrams.Sequence;

namespace SplashTheMusical.Tests.Generation;

/// <summary>
/// Random sequence diagrams in the form <see cref="SequenceParser"/> gives them: participants and actors with
/// descriptions, wrap settings and <c>@{ }</c> types, <c>create</c> and <c>destroy</c>, boxes with named and
/// functional colours, messages of every arrow with every modifier, notes of each placement, activations,
/// <c>autonumber</c> in each form, the <c>links</c>, <c>link</c>, <c>properties</c> and <c>details</c> statements, and
/// every block with its sections, nested two deep. Ids hold spaces, <c>-</c>, digits and non-ASCII letters. Text holds
/// entity codes, quotes, brackets and HTML line breaks.
/// </summary>
/// <remarks>
/// <see cref="Repaired"/> changes what mermaid.js's db refuses: a <c>create</c> of a participant that exists, a
/// created or destroyed participant whose next message does not involve it, a deactivation of an inactive participant,
/// and a participant in a second box. Text holds no <c>#</c> or <c>;</c> outside an entity code, which would end it, and
/// no <c>%</c>, which would start a comment.
/// </remarks>
public static class SequenceGenerator
{
	private static readonly string[] Ids = ["Alice", "Bob", "a1", "x_y", "A-B", "api.v2", "1", "Ünïcödé", "日本語", "John Doe"];

	/// <summary>Ids that the <c>ID</c> rules read before <c>as</c> and <c>@{</c>, which take no space.</summary>
	private static readonly string[] BareIds = ["Alice", "Bob", "a1", "x_y", "A-B", "api.v2", "Ünïcödé", "日本語"];

	private static readonly string[] Metadata =
	[
		"\"type\": \"database\"", "\"type\" : \"collections\"", "type: queue", "\"type\": \"boundary\", \"alias\": \"Edge\"", "\"alias\": \"Svc\"",
		"\"type\": \"entity\"", "\"type\": \"control\"", "\"type\": \"actor\"",
	];

	private static readonly string[] Colors = ["transparent", "red", "Aqua", "rgb(200,220,255)", "rgba(0, 0, 0, 0.1)", "hsl(120, 50%, 50%)"];

	private static readonly string[] RectColors = ["", "rgb(200, 150, 255)", "rgba(0, 0, 255, .1)", "aqua"];

	private static readonly string[] Data =
	[
		"{\"Dashboard\": \"https://x.org/a\", \"Wiki\": \"https://x.org/b\"}", "Dashboard @ https://x.org/a", "details-1", "{\"class\": \"internal\"}",
	];

	private static readonly double[] Starts = [1, 10, 0.5, 1.25, 100];

	private static readonly double[] Steps = [1, 2, 0.1, 0.25];

	private static readonly Gen<string> Id = Gen.OneOf(Ids);

	private static readonly Gen<string> BareId = Gen.OneOf(BareIds);

	/// <summary>One line of text that <c>TXT</c> and <c>restOfLine</c> read to its end.</summary>
	private static readonly Gen<string> Text = GenText.Line.Select(WithoutBareHashes);

	private static readonly Gen<bool?> Wrap = Gen.Frequency((3, Gen.Const((bool?)null)), (1, Gen.Const((bool?)true)), (1, Gen.Const((bool?)false)));

	private static readonly Gen<SequenceStatement> Participant =
		Gen.Select(
			Gen.Bool,
			Gen.Bool,
			Text.OrNull(),
			Wrap,
			Gen.OneOf(Metadata).OrNull(),
			Gen.Int[0, 3],
			(actor, created, description, wrap, metadata, idIndex) =>
			{
				var bare = description is not null || metadata is not null;
				var id = bare ? BareIds[idIndex % BareIds.Length] : Ids[idIndex % Ids.Length];
				return (SequenceStatement)new SequenceParticipant(id, actor ? SequenceParticipantKind.Actor : SequenceParticipantKind.Participant)
				{
					Created = created,
					Description = description,
					Wrap = description is null ? null : wrap,
					Metadata = metadata,
				};
			});

	private static readonly Gen<SequenceStatement> BoxMember = Gen.Frequency(
		(4, Participant.Select(p => (SequenceStatement)Uncreated((SequenceParticipant)p))),
		(1, Id.Select(id => (SequenceStatement)new SequenceDestroy(id))));

	private static readonly Gen<SequenceStatement> Box =
		Gen.Select(
			Gen.OneOf(Colors),
			Text.Select(t => GenText.Without(t, ")")).OrNull(),
			Wrap,
			BoxMember.List[0, 3],
			(color, title, wrap, members) => (SequenceStatement)new SequenceBox { Color = color, Title = title, Wrap = wrap, Statements = members });

	private static readonly Gen<SequenceStatement> Message =
		Gen.Select(
			Id,
			Id,
			Gen.OneOf(Enum.GetValues<SequenceArrow>()),
			Gen.OneOf(Enum.GetValues<SequenceMessageModifier>()),
			Text.Select(t => t).OrNull(),
			Wrap,
			(from, to, arrow, modifier, text, wrap) => (SequenceStatement)new SequenceMessage(ReadsBefore(from, arrow, modifier), to, arrow) { Modifier = modifier, Text = text ?? "", Wrap = wrap });

	/// <summary>
	/// The id, or <c>Alice</c> when the id has a <c>-</c> and <c>()</c> or a reverse arrow follows it, whose <c>(</c>,
	/// <c>/</c> or <c>\</c> the <c>ACTOR</c> rule would read as part of the id.
	/// </summary>
	private static string ReadsBefore(string from, SequenceArrow arrow, SequenceMessageModifier modifier) =>
		from.Contains('-', StringComparison.Ordinal)
		&& (modifier is SequenceMessageModifier.CentralConnectionReverse or SequenceMessageModifier.CentralConnectionDual
			|| arrow is >= SequenceArrow.SolidArrowTopReverse and <= SequenceArrow.StickArrowBottomReverse
				or >= SequenceArrow.SolidArrowTopReverseDotted and <= SequenceArrow.StickArrowBottomReverseDotted)
			? "Alice"
			: from;

	private static readonly Gen<SequenceStatement> Note =
		Gen.Select(
			Gen.OneOf(Enum.GetValues<SequenceNotePlacement>()),
			Id,
			Id.OrNull(),
			Text,
			Wrap,
			(placement, actor, second, text, wrap) => (SequenceStatement)new SequenceNote(placement, actor, placement == SequenceNotePlacement.Over ? second : null) { Text = text, Wrap = wrap });

	private static readonly Gen<SequenceStatement> Autonumber = Gen.Frequency(
		(1, Gen.Const((SequenceStatement)new SequenceAutonumber())),
		(1, Gen.Const((SequenceStatement)new SequenceAutonumber { Visible = false })),
		(1, Gen.OneOf(Starts).Select(start => (SequenceStatement)new SequenceAutonumber { Start = start })),
		(1, Gen.Select(Gen.OneOf(Starts), Gen.OneOf(Steps), (start, step) => (SequenceStatement)new SequenceAutonumber { Start = start, Step = step })));

	private static readonly Gen<SequenceStatement> ActorData =
		Gen.Select(Gen.OneOf(Enum.GetValues<SequenceActorDataKind>()), Id, Gen.OneOf(Data), (kind, actor, text) => (SequenceStatement)new SequenceActorData(kind, actor, text));

	private static readonly Gen<SequenceStatement> Leaf = Gen.Frequency(
		(3, Participant),
		(1, Id.Select(id => (SequenceStatement)new SequenceDestroy(id))),
		(1, Box),
		(8, Message),
		(3, Note),
		(2, Gen.Select(Id, Gen.Bool, (id, active) => (SequenceStatement)new SequenceActivation(id, active))),
		(1, Autonumber),
		(1, ActorData));

	private static readonly Gen<SequenceStatement> Statement = Statements(2);

	public static Gen<SequenceDocument> Document { get; } =
		Gen.Select(
			Text.OrNull(),
			GenText.Line.OrNull(),
			GenText.Lines.OrNull(),
			Statement.List[0, 10],
			(title, accTitle, accDescr, statements) => new SequenceDocument
			{
				DiagramTitle = title,
				AccessibilityTitle = accTitle,
				AccessibilityDescription = accDescr,
				Statements = Repaired(statements),
			});

	/// <summary>Statements with blocks nested up to <paramref name="depth"/> deep.</summary>
	private static Gen<SequenceStatement> Statements(int depth)
	{
		if (depth == 0)
		{
			return Leaf;
		}

		var inner = Statements(depth - 1).List[0, 3];
		var section = Gen.Select(Text.OrNull(), Wrap, inner, (text, wrap, statements) => new SequenceBlockSection { Text = text ?? "", Wrap = wrap, Statements = statements });
		var block = Gen.Select(
			Gen.OneOf(Enum.GetValues<SequenceBlockKind>()),
			Text.OrNull(),
			Gen.OneOf(RectColors),
			Wrap,
			inner,
			section.List[0, 2],
			(kind, text, color, wrap, statements, sections) => (SequenceStatement)new SequenceBlock(kind)
			{
				Text = kind == SequenceBlockKind.Rect ? color : text ?? "",
				Wrap = wrap,
				Statements = statements,
				Sections = kind is SequenceBlockKind.Alt or SequenceBlockKind.Par or SequenceBlockKind.ParOver or SequenceBlockKind.Critical ? sections : [],
			});
		return Gen.Frequency((6, Leaf), (1, block));
	}

	private static SequenceParticipant Uncreated(SequenceParticipant participant) =>
		participant.Created
			? new SequenceParticipant(participant.Id, participant.Kind) { Description = participant.Description, Wrap = participant.Wrap, Metadata = participant.Metadata }
			: participant;

	/// <summary>The text without a <c>#</c> or <c>;</c> that is not part of an entity code.</summary>
	private static string WithoutBareHashes(string text)
	{
		var output = new System.Text.StringBuilder(text.Length);
		for (var at = 0; at < text.Length; at++)
		{
			if (text[at] == '#')
			{
				var end = at + 1;
				while (end < text.Length && (char.IsAsciiLetterOrDigit(text[end]) || text[end] == '_'))
				{
					end++;
				}

				if (end > at + 1 && end < text.Length && text[end] == ';')
				{
					output.Append(text, at, end - at + 1);
					at = end;
				}

				continue;
			}

			if (text[at] != ';')
			{
				output.Append(text[at]);
			}
		}

		return GenText.Without(output.ToString(), "");
	}

	/// <summary>
	/// The statements as the db applies them, with what it throws on changed: a <c>create</c> of a participant that
	/// exists is a plain declaration, the next message after a <c>create</c> or <c>destroy</c> goes to its
	/// participant, a deactivation of an inactive participant activates it, and a participant in a second box is left
	/// out of it. An <c>activate</c>, <c>deactivate</c> or <c>destroy</c> of an id no earlier statement made a
	/// participant is left out, because mermaid.js's renderer throws on it.
	/// </summary>
	private static List<SequenceStatement> Repaired(List<SequenceStatement> statements)
	{
		var state = new DbState();
		return Repair(statements, state, box: -1);
	}

	private sealed class DbState
	{
		public Dictionary<string, int> Boxes { get; } = new(StringComparer.Ordinal);

		public Dictionary<string, int> Activations { get; } = new(StringComparer.Ordinal);

		public int BoxCount { get; set; }

		public string? LastCreated { get; set; }

		public string? LastDestroyed { get; set; }
	}

	private static List<SequenceStatement> Repair(List<SequenceStatement> statements, DbState state, int box)
	{
		var repaired = new List<SequenceStatement>(statements.Count);
		foreach (var statement in statements)
		{
			switch (statement)
			{
				case SequenceParticipant participant:
					{
						var current = participant;
						if (current.Created && state.Boxes.ContainsKey(current.Id))
						{
							current = Uncreated(current);
						}

						if (box >= 0 && state.Boxes.TryGetValue(current.Id, out var other) && other >= 0 && other != box)
						{
							continue;
						}

						AddActor(state, current.Id, box);
						if (current.Created)
						{
							state.LastCreated = current.Id;
						}

						repaired.Add(current);
						break;
					}

				case SequenceDestroy destroy:
					if (!state.Boxes.ContainsKey(destroy.Id))
					{
						continue;
					}

					state.LastDestroyed = destroy.Id;
					repaired.Add(destroy);
					break;
				case SequenceBox group:
					{
						var index = state.BoxCount++;
						repaired.Add(new SequenceBox { Color = group.Color, Title = group.Title, Wrap = group.Wrap, Statements = Repair(group.Statements, state, index) });
						break;
					}

				case SequenceMessage message:
					{
						var to = message.To;
						var from = message.From;
						if (state.LastCreated is { } created)
						{
							to = created;
							state.LastCreated = null;
						}
						else if (state.LastDestroyed is { } destroyed)
						{
							if (to != destroyed && from != destroyed)
							{
								to = destroyed;
							}

							state.LastDestroyed = null;
						}

						AddActor(state, from, -1);
						AddActor(state, to, -1);
						var modifier = message.Modifier;
						if (modifier == SequenceMessageModifier.Activate)
						{
							state.Activations[to] = state.Activations.GetValueOrDefault(to) + 1;
						}
						else if (modifier == SequenceMessageModifier.Deactivate)
						{
							if (state.Activations.GetValueOrDefault(from) < 1)
							{
								modifier = SequenceMessageModifier.None;
							}
							else
							{
								state.Activations[from]--;
							}
						}

						repaired.Add(new SequenceMessage(from, to, message.Arrow) { Modifier = modifier, Text = message.Text, Wrap = message.Wrap });
						break;
					}

				case SequenceActivation activation:
					{
						if (!state.Boxes.ContainsKey(activation.Id))
						{
							continue;
						}

						var active = activation.Active || state.Activations.GetValueOrDefault(activation.Id) < 1;
						state.Activations[activation.Id] = state.Activations.GetValueOrDefault(activation.Id) + (active ? 1 : -1);
						repaired.Add(active == activation.Active ? activation : new SequenceActivation(activation.Id, active));
						break;
					}

				case SequenceNote note:
					AddActor(state, note.Actor, -1);
					if (note.SecondActor is { } second)
					{
						AddActor(state, second, -1);
					}

					repaired.Add(note);
					break;
				case SequenceActorData data:
					AddActor(state, data.Actor, -1);
					repaired.Add(data);
					break;
				case SequenceBlock block:
					{
						var body = Repair(block.Statements, state, -1);
						var sections = block.Sections
							.Select(s => new SequenceBlockSection { Text = s.Text, Wrap = s.Wrap, Statements = Repair(s.Statements, state, -1) })
							.ToList();
						repaired.Add(new SequenceBlock(block.Kind) { Text = block.Text, Wrap = block.Wrap, Statements = body, Sections = sections });
						break;
					}

				default:
					repaired.Add(statement);
					break;
			}
		}

		return repaired;
	}

	/// <summary>The db's <c>addActor</c>: a participant keeps its box, or takes the current one.</summary>
	private static void AddActor(DbState state, string id, int box)
	{
		if (!state.Boxes.TryGetValue(id, out var current) || current < 0)
		{
			state.Boxes[id] = box;
		}
	}
}
