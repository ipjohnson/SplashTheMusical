using AwesomeAssertions;
using SplashTheMusical.Diagrams.Sequence;
using SplashTheMusical.Tests.Generation;

namespace SplashTheMusical.Tests.Diagrams;

public class SequenceWriterTests
{
	[Test]
	public void WritesOneStatementPerLine()
	{
		var document = new SequenceBuilder()
			.Title("Orders")
			.Actor("user", "User")
			.Participant("api", "API", type: "boundary")
			.Autonumber(10, 5)
			.Message("user", "api", "GET /orders", modifier: SequenceMessageModifier.Activate)
			.Message("api", "user", "200 OK", SequenceArrow.Dotted, SequenceMessageModifier.Deactivate)
			.Note("api", "cached", SequenceNotePlacement.RightOf)
			.Note("user", "both", secondActor: "api")
			.Build();

		new Mermaid().Write(document).Should().Be("""
			sequenceDiagram
			  title Orders
			  actor user as User
			  participant api@{ "type": "boundary" } as API
			  autonumber 10 5
			  user->>+api: GET /orders
			  api-->>-user: 200 OK
			  Note right of api: cached
			  Note over user,api: both

			""");
	}

	[Test]
	public void WritesBoxesAndBlocksIndented()
	{
		var document = new SequenceDocument
		{
			Statements =
			[
				new SequenceBox { Color = "Aqua", Title = "Team", Statements = [new SequenceParticipant("A"), new SequenceDestroy("B")] },
				new SequenceBlock(SequenceBlockKind.Alt)
				{
					Text = "ok",
					Wrap = true,
					Statements = [new SequenceMessage("A", "B") { Text = "yes" }],
					Sections = [new SequenceBlockSection { Text = "no", Statements = [new SequenceActivation("A")] }, new SequenceBlockSection()],
				},
				new SequenceBlock(SequenceBlockKind.Rect) { Text = "rgb(0, 0, 255)" },
			],
		};

		new Mermaid().Write(document).Should().Be("""
			sequenceDiagram
			  box Aqua Team
			    participant A
			    destroy B
			  end
			  alt wrap:ok
			    A->>B: yes
			  else no
			    activate A
			  else
			  end
			  rect rgb(0, 0, 255)
			  end

			""");
	}

	[Test]
	public void SeparatesAMinusFromTheArrowAroundIt()
	{
		var document = new SequenceDocument
		{
			Statements =
			[
				new SequenceMessage("A", "x1", SequenceArrow.Solid) { Modifier = SequenceMessageModifier.Deactivate },
				new SequenceMessage("A", "B", SequenceArrow.StickArrowTopReverse) { Modifier = SequenceMessageModifier.Deactivate },
				new SequenceMessage("A", "B", SequenceArrow.SolidCross) { Modifier = SequenceMessageModifier.CentralConnectionDual },
			],
		};

		var text = new Mermaid().Write(document);

		text.Should().Be("sequenceDiagram\n  A->>- x1:\n  A//- -B:\n  A()-x()B:\n");
	}

	[Test]
	public void WritesABoxWhoseColourTheBrowserRefusesAsTransparent()
	{
		var document = new SequenceDocument { Statements = [new SequenceBox { Title = "Group 1" }] };

		new Mermaid().Write(document).Should().Be("sequenceDiagram\n  box transparent Group 1\n  end\n");
	}

	[Test]
	[Arguments("end")]
	[Arguments("A:B")]
	[Arguments("A ")]
	[Arguments("1 x")]
	[Arguments("link x")]
	[Arguments("A%%B")]
	[Arguments("(A)")]
	public void RefusesAnIdTheLexerReadsAsSomethingElse(string id)
	{
		var document = new SequenceDocument { Statements = [new SequenceMessage(id, "B")] };

		var write = () => new Mermaid().Write(document);

		write.Should().Throw<ArgumentException>();
	}

	[Test]
	public void RefusesADashedIdBeforeAReverseArrow()
	{
		var document = new SequenceDocument { Statements = [new SequenceMessage("A-B", "C", SequenceArrow.SolidArrowTopReverse)] };

		var write = () => new Mermaid().Write(document);

		write.Should().Throw<ArgumentException>();
	}

	[Test]
	[Arguments("a # b")]
	[Arguments("a; b")]
	[Arguments("a\nb")]
	[Arguments(" a")]
	[Arguments("wrap: a")]
	public void RefusesTextThatDoesNotReadBackAsItself(string text)
	{
		var document = new SequenceDocument { Statements = [new SequenceMessage("A", "B") { Text = text }] };

		var write = () => new Mermaid().Write(document);

		write.Should().Throw<ArgumentException>();
	}

	[Test]
	public void RefusesABoxLineThatReadsBackAsAnotherColour()
	{
		var document = new SequenceDocument { Statements = [new SequenceBox { Color = "rgb(1, 2, 3)", Title = "(x)" }] };

		var write = () => new Mermaid().Write(document);

		write.Should().Throw<ArgumentException>();
	}

	[Test]
	public void RefusesAnIdWithSpaceBeforeAs()
	{
		var document = new SequenceDocument { Statements = [new SequenceParticipant("John Doe") { Description = "J" }] };

		var write = () => new Mermaid().Write(document);

		write.Should().Throw<ArgumentException>();
	}

	[Test]
	public void WritesWhatTheParserReadsBack()
	{
		const string Source = "sequenceDiagram\n  participant A as nowrap:Alice\n  A->>B: I #9829; you\n  Note over A: n\n  autonumber off\n";

		var mermaid = new Mermaid();
		var document = mermaid.Parse(Source);

		mermaid.Write(document).Should().Be(Source);
		DocumentDump.Lines(mermaid.Parse(mermaid.Write(document))).Should().Equal(DocumentDump.Lines(document));
	}
}
