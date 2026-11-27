using AwesomeAssertions;
using SplashTheMusical.Diagrams.Sequence;

namespace SplashTheMusical.Tests.Diagrams;

public class SequenceParserTests
{
	[Test]
	public void ReadsParticipantsWithTheirKindsDescriptionsAndMetadata()
	{
		var document = Parse("""
			sequenceDiagram
			  participant A as wrap:Alice the first
			  actor B
			  participant DB@{ "type": "database" } as User Database
			  participant Harry @{ type: queue }
			  create actor C
			  A->>C: hi
			""");

		var participants = document.Statements.OfType<SequenceParticipant>().ToList();
		participants.Select(p => (p.Id, p.Kind, p.Description, p.Wrap, p.Metadata, p.Created)).Should().Equal(
			("A", SequenceParticipantKind.Participant, "Alice the first", true, null, false),
			("B", SequenceParticipantKind.Actor, null, null, null, false),
			("DB", SequenceParticipantKind.Participant, "User Database", null, "\"type\": \"database\"", false),
			("Harry", SequenceParticipantKind.Participant, null, null, "type: queue", false),
			("C", SequenceParticipantKind.Actor, null, null, null, true));
		var db = SequenceDb.From(document);
		db.Actors.Select(a => (a.Name, a.Description, a.Type)).Should().Equal(
			("A", "Alice the first", "participant"),
			("B", "B", "actor"),
			("DB", "User Database", "database"),
			("Harry", "Harry", "queue"),
			("C", "C", "actor"));
		db.CreatedActors.Should().ContainKey("C");
	}

	[Test]
	public void ReadsEveryArrowAndModifier()
	{
		var arrows = SequenceLexer.Arrows.Select(a => a.Pattern).ToList();
		var source = "sequenceDiagram\n" + string.Join('\n', arrows.Select(a => $"A{a}B: {a}")) + "\nA->>+B: on\nB-->>-A: off\nA->>()B: c\nA()->>B: r\nA()->>()B: d\n";

		var messages = Parse(source).Statements.Cast<SequenceMessage>().ToList();

		messages.Take(arrows.Count).Select(m => (m.Arrow, m.Text)).Should().Equal(SequenceLexer.Arrows.Select(a => (a.Arrow, a.Pattern)));
		messages.Skip(arrows.Count).Select(m => (m.From, m.To, m.Modifier)).Should().Equal(
			("A", "B", SequenceMessageModifier.Activate),
			("B", "A", SequenceMessageModifier.Deactivate),
			("A", "B", SequenceMessageModifier.CentralConnection),
			("A", "B", SequenceMessageModifier.CentralConnectionReverse),
			("A", "B", SequenceMessageModifier.CentralConnectionDual));
	}

	[Test]
	public void ReadsNotesActivationsAutonumberAndMenus()
	{
		var document = Parse("""
			sequenceDiagram
			  autonumber 10 5
			  Note left of A: nowrap: one
			  note right of B: two
			  Note over A,B: three
			  activate A
			  deactivate A
			  autonumber off
			  link A: Wiki @ https://x.org
			  links A: {"Repo": "https://x.org/r"}
			  properties A: {"class": "internal"}
			  details A: menu-1
			""");

		var autonumber = (SequenceAutonumber)document.Statements[0];
		(autonumber.Start, autonumber.Step, autonumber.Visible).Should().Be((10d, 5d, true));
		document.Statements.OfType<SequenceNote>().Select(n => (n.Placement, n.Actor, n.SecondActor, n.Text, n.Wrap)).Should().Equal(
			(SequenceNotePlacement.LeftOf, "A", null, "one", false),
			(SequenceNotePlacement.RightOf, "B", null, "two", null),
			(SequenceNotePlacement.Over, "A", "B", "three", null));
		document.Statements.OfType<SequenceActivation>().Select(a => a.Active).Should().Equal(true, false);
		((SequenceAutonumber)document.Statements[6]).Visible.Should().BeFalse();
		document.Statements.OfType<SequenceActorData>().Select(d => (d.Kind, d.Text)).Should().Equal(
			(SequenceActorDataKind.Link, "Wiki @ https://x.org"),
			(SequenceActorDataKind.Links, "{\"Repo\": \"https://x.org/r\"}"),
			(SequenceActorDataKind.Properties, "{\"class\": \"internal\"}"),
			(SequenceActorDataKind.Details, "menu-1"));
	}

	[Test]
	public void ReadsBlocksWithTheirSections()
	{
		var document = Parse("""
			sequenceDiagram
			  alt is sick
			    A->>B: not good
			  else is well
			    A->>B: fine
			  else
			  end
			  par_over first
			    A->>B: x
			  and second
			    loop every minute
			      B->>A: y
			    end
			  end
			  critical connect
			  option timeout
			  end
			  rect rgb(0, 255, 0)
			    break when down
			      opt maybe
			      end
			    end
			  end
			""");

		var alt = (SequenceBlock)document.Statements[0];
		(alt.Kind, alt.Text).Should().Be((SequenceBlockKind.Alt, "is sick"));
		alt.Sections.Select(s => (s.Text, s.Statements.Count)).Should().Equal(("is well", 1), ("", 0));
		var parOver = (SequenceBlock)document.Statements[1];
		(parOver.Kind, ((SequenceBlock)parOver.Sections.Single().Statements.Single()).Kind).Should().Be((SequenceBlockKind.ParOver, SequenceBlockKind.Loop));
		((SequenceBlock)document.Statements[2]).Sections.Single().Text.Should().Be("timeout");
		var rect = (SequenceBlock)document.Statements[3];
		(rect.Kind, rect.Text).Should().Be((SequenceBlockKind.Rect, "rgb(0, 255, 0)"));
		var db = SequenceDb.From(document);
		db.Messages.Select(m => m.Type).Should().Equal(12, 0, 13, 0, 13, 14, 32, 0, 20, 10, 0, 11, 21, 27, 28, 29, 22, 30, 15, 16, 31, 23);
	}

	[Test]
	[Arguments("box Aqua Group A", "Aqua", "Group A")]
	[Arguments("box Group 1", "transparent", "Group 1")]
	[Arguments("box rgb(34, 56, 0) Group1", "rgb(34, 56, 0)", "Group1")]
	[Arguments("box rgb (34, 56, 0) G", "transparent", "rgb (34, 56, 0) G")]
	[Arguments("box hsl(120 50% 50% / 0.5)", "hsl(120 50% 50% / 0.5)", null)]
	[Arguments("box", "transparent", null)]
	public void ReadsTheBoxColourAsTheBrowserDoes(string line, string color, string? title)
	{
		var document = Parse($"sequenceDiagram\n{line}\nparticipant A\nend\n");

		var box = (SequenceBox)document.Statements.Single();
		(box.Color, box.Title).Should().Be((color, title));
		SequenceDb.From(document).Boxes.Single().ActorKeys.Should().Equal("A");
	}

	[Test]
	public void ReadsTitlesAccessibilityAndEntityCodes()
	{
		var document = Parse("sequenceDiagram\ntitle: The #quot;flow#quot;\naccTitle: Access\naccDescr {\n  line one\n  line two\n}\nA->>B: I #9829; you\n");

		document.DiagramTitle.Should().Be("The #quot;flow#quot;");
		document.AccessibilityTitle.Should().Be("Access");
		document.AccessibilityDescription.Should().Be("line one\n  line two");
		((SequenceMessage)document.Statements.Single()).Text.Should().Be("I #9829; you");
	}

	[Test]
	public void ReadsKeywordsWithoutCaseAndSemicolonsAsLineEnds()
	{
		var document = Parse("sequenceDiagram\nPARTICIPANT A\nA->>B: x; B->>A: y\n");

		document.Statements.OfType<SequenceMessage>().Select(m => m.Text).Should().Equal("x", "y");
		((SequenceParticipant)document.Statements[0]).Id.Should().Be("A");
	}

	[Test]
	[Arguments("sequenceDiagram\nautonumber 10.001\nA->>B: x\n")]
	[Arguments("sequenceDiagram\nA->>-B: x\n")]
	[Arguments("sequenceDiagram\nA->>+B: x\nB->>-A: y\nB->>-A: z\n")]
	[Arguments("sequenceDiagram\ndeactivate A\n")]
	[Arguments("sequenceDiagram\nA->>B: x\ncreate participant A\nB->>A: y\n")]
	[Arguments("sequenceDiagram\ncreate participant C\nA->>B: x\n")]
	[Arguments("sequenceDiagram\ndestroy B\nA->>C: x\n")]
	[Arguments("sequenceDiagram\nbox Aqua G1\nparticipant A\nend\nbox G2\nparticipant A\nend\n")]
	[Arguments("sequenceDiagram\nparticipant C@{ \"type\" \"control\" }\n")]
	[Arguments("sequenceDiagram\nparticipant E@{ \"type\": \"entity\"\nE ->> E: x\n")]
	[Arguments("sequenceDiagram\nA->>B\n")]
	[Arguments("sequenceDiagram\nloop x\nA->>B: y\n")]
	[Arguments("sequenceDiagram\nalt x\nA->>B: y\nand z\nend\n")]
	[Arguments("sequenceDiagram\nparticipant\n")]
	[Arguments("sequenceDiagram\nparticipant X asAAA:AAA\n")]
	[Arguments("sequenceDiagram\nend\n")]
	public void RefusesWhatMermaidRefuses(string source)
	{
		var mermaid = new Mermaid();

		mermaid.TryParse(source, out var document, out var diagnostics).Should().BeFalse();
		document.Should().BeNull();
		diagnostics.Should().ContainSingle(d => d.Severity == DiagnosticSeverity.Error);
	}

	[Test]
	[Arguments("sequenceDiagram\ndestroy B\nA->>B: x\n")]
	[Arguments("sequenceDiagram\ncreate participant C\nNote over C: x\nA->>C: x\n")]
	[Arguments("sequenceDiagram\nbox G1\nparticipant A\nend\nparticipant A\n")]
	[Arguments("sequenceDiagram\nactivate A\nA->>-B: x\n")]
	public void AcceptsWhatTheDbAllows(string source)
	{
		var mermaid = new Mermaid();

		mermaid.TryParse(source, out var document, out _).Should().BeTrue();
		document.Should().NotBeNull();
	}

	[Test]
	public void TheDbKeepsTheFirstTypeAndTheLastDescription()
	{
		var db = SequenceDb.From(Parse("""
			sequenceDiagram
			  A->>B: first
			  actor A
			  participant B as Bob
			  participant API@{ "type": "boundary", "alias": "Public API" }
			"""));

		db.Actors.Select(a => (a.Name, a.Description, a.Type)).Should().Equal(
			("A", "A", "participant"),
			("B", "Bob", "participant"),
			("API", "Public API", "boundary"));
	}

	private static SequenceDocument Parse(string source) => (SequenceDocument)new Mermaid().Parse(source);
}
