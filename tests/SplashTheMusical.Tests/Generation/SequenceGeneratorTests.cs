using AwesomeAssertions;
using CsCheck;
using SplashTheMusical.Diagrams.Sequence;

namespace SplashTheMusical.Tests.Generation;

/// <summary>The documents of <see cref="SequenceGenerator"/> reach every statement, arrow, modifier, block and placement.</summary>
public class SequenceGeneratorTests
{
	[Test]
	public void ReachesEveryStatementArrowModifierBlockAndPlacement()
	{
		var statements = new HashSet<string>(StringComparer.Ordinal);
		var arrows = new HashSet<SequenceArrow>();
		var modifiers = new HashSet<SequenceMessageModifier>();
		var blocks = new HashSet<SequenceBlockKind>();
		var placements = new HashSet<SequenceNotePlacement>();
		var data = new HashSet<SequenceActorDataKind>();
		var forms = new HashSet<string>(StringComparer.Ordinal);
		SequenceGenerator.Document.Sample(document =>
		{
			lock (statements)
			{
				Walk(document.Statements);
			}

			void Walk(List<SequenceStatement> list)
			{
				foreach (var statement in list)
				{
					statements.Add(statement.GetType().Name);
					switch (statement)
					{
						case SequenceParticipant participant:
							forms.Add(participant.Kind.ToString());
							forms.Add(participant.Created ? "created" : "declared");
							forms.Add(participant.Metadata is null ? "no metadata" : "metadata");
							forms.Add(participant.Description is null ? "no description" : "description");
							break;
						case SequenceMessage message:
							arrows.Add(message.Arrow);
							modifiers.Add(message.Modifier);
							forms.Add(message.Wrap switch { true => "wrap", false => "nowrap", null => "default wrap" });
							break;
						case SequenceNote note:
							placements.Add(note.Placement);
							forms.Add(note.SecondActor is null ? "note on one" : "note on two");
							break;
						case SequenceActorData actorData:
							data.Add(actorData.Kind);
							break;
						case SequenceAutonumber autonumber:
							forms.Add(!autonumber.Visible ? "autonumber off" : autonumber.Step is not null ? "autonumber step" : autonumber.Start is not null ? "autonumber start" : "autonumber");
							break;
						case SequenceBox box:
							Walk(box.Statements);
							break;
						case SequenceBlock block:
							blocks.Add(block.Kind);
							if (block.Sections.Count > 0)
							{
								forms.Add("sections");
							}

							Walk(block.Statements);
							foreach (var section in block.Sections)
							{
								Walk(section.Statements);
							}

							break;
					}
				}
			}
		}, iter: 2_000, threads: 1);

		statements.Should().BeEquivalentTo(
			nameof(SequenceParticipant), nameof(SequenceDestroy), nameof(SequenceBox), nameof(SequenceMessage), nameof(SequenceNote),
			nameof(SequenceActivation), nameof(SequenceAutonumber), nameof(SequenceActorData), nameof(SequenceBlock));
		arrows.Should().BeEquivalentTo(Enum.GetValues<SequenceArrow>());
		modifiers.Should().BeEquivalentTo(Enum.GetValues<SequenceMessageModifier>());
		blocks.Should().BeEquivalentTo(Enum.GetValues<SequenceBlockKind>());
		placements.Should().BeEquivalentTo(Enum.GetValues<SequenceNotePlacement>());
		data.Should().BeEquivalentTo(Enum.GetValues<SequenceActorDataKind>());
		forms.Should().Contain(
		[
			"Participant", "Actor", "created", "declared", "metadata", "description", "wrap", "nowrap", "note on one", "note on two",
			"autonumber", "autonumber off", "autonumber start", "autonumber step", "sections",
		]);
	}
}
