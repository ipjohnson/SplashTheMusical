using AwesomeAssertions;
using SplashTheMusical.Diagrams.State;

namespace SplashTheMusical.Tests.Diagrams;

public class StateParserTests
{
	[Test]
	public void ReadsStatesTransitionsAndDescriptions()
	{
		var document = Parse("stateDiagram-v2\n  [*] --> Idle\n  Idle --> Busy : start\n  Busy : Working hard\n  Busy --> [*]\n");

		document.DiagramType.Should().Be("stateDiagram");
		StateDump.Lines(document).Should().Equal(
			"keyword stateDiagram-v2",
			"transition [*] -> Idle label=",
			"transition Idle -> Busy label=start",
			"state Busy description=Working hard",
			"transition Busy -> [*] label=");
	}

	[Test]
	public void TheVersionOneKeywordReadsTheSame()
	{
		var document = Parse("stateDiagram\nA --> B");

		StateDump.Lines(document).Should().Equal("keyword stateDiagram", "transition A -> B label=");
	}

	[Test]
	public void ReadsCompositeStatesAndTheirRegions()
	{
		var document = Parse("""
			stateDiagram-v2
			  state "The active state" as Active {
			    [*] --> Left
			    --
			    [*] --> Right
			  }
			  state Plain {
			    state Inner {
			      X
			    }
			  }
			""");

		StateDump.Lines(document).Should().Equal(
			"keyword stateDiagram-v2",
			"block Active kind=Default description=The active state body=True",
			"  transition [*] -> Left label=",
			"  divider",
			"  transition [*] -> Right label=",
			"block Plain kind=Default description= body=True",
			"  block Inner kind=Default description= body=True",
			"    state X description=");
	}

	[Test]
	public void ReadsForksJoinsAndChoicesInBothForms()
	{
		var document = Parse("stateDiagram-v2\nstate f <<fork>>\nstate j <<join>>\nstate c <<choice>>\nstate f2 [[fork]]\n");

		StateDump.Lines(document).Skip(1).Should().Equal(
			"block f kind=Fork description= body=False",
			"block j kind=Join description= body=False",
			"block c kind=Choice description= body=False",
			"block f2 kind=Fork description= body=False");
	}

	[Test]
	public void ReadsOneLineMultiLineAndFloatingNotes()
	{
		var document = Parse("""
			stateDiagram-v2
			  A
			  note left of A : on one line
			  note right of A
			    first line
			    second line
			  end note
			  note "floating" as N
			""");

		var notes = document.Statements.OfType<StateNote>().ToList();
		notes.Select(n => (n.Position, n.Id)).Should().Equal((StateNotePosition.LeftOf, "A"), (StateNotePosition.RightOf, "A"));
		notes[0].Text.Should().Be("on one line");
		notes[1].Text.Should().StartWith("first line\n").And.EndWith("second line");
		document.Statements.OfType<StateFloatingNote>().Should().ContainSingle().Which.Should().BeEquivalentTo(new { Id = "N", Text = "floating" });
	}

	[Test]
	public void ReadsClassesStylesDirectionScaleAndHide()
	{
		var document = Parse("""
			stateDiagram-v2
			  direction LR
			  classDef bad fill:#f00,color:white
			  A:::bad --> B
			  class A,B bad
			  style B fill:#0f0
			  hide empty description
			  scale 350 width
			""");

		StateDump.Lines(document).Skip(1).Should().Equal(
			"direction LR",
			"classDef bad fill:#f00,color:white",
			"transition A:::bad -> B label=",
			"class A,B bad",
			"style B fill:#0f0",
			"hide empty description",
			"scale 350");
	}

	[Test]
	public void ReadsBothFormsOfClick()
	{
		var document = Parse("stateDiagram-v2\nA\nclick A \"https://example.com\" \"Go there\"\nclick A href \"https://example.org\"\n");

		document.Statements.OfType<StateClick>().Select(c => (c.State.Id, c.Url, c.Tooltip)).Should().Equal(
			("A", "https://example.com", "Go there"),
			("A", "https://example.org", (string?)null));
	}

	[Test]
	public void ReadsAccessibilityTitleAndDescription()
	{
		var document = Parse("stateDiagram-v2\naccTitle: The title\naccDescr: The description\nA\n");

		document.AccessibilityTitle.Should().Be("The title");
		document.AccessibilityDescription.Should().Be("The description");
		StateDump.Lines(document).Skip(1).Should().Equal("state A description=");
	}

	[Test]
	public void KeepsEntityCodesAsWritten()
	{
		var document = Parse("stateDiagram-v2\nA : say #quot;hi#quot;\n");

		document.Statements.OfType<StateDeclaration>().Single().Description.Should().Be("say #quot;hi#quot;");
	}

	[Test]
	public void InternsEachIdOnce()
	{
		var document = Parse("stateDiagram-v2\nA --> B\nB --> A\n");

		var transitions = document.Statements.OfType<StateTransition>().ToList();
		transitions[1].To.Id.Should().BeSameAs(transitions[0].From.Id);
		transitions[1].From.Id.Should().BeSameAs(transitions[0].To.Id);
	}

	[Test]
	public void AnUnclosedCompositeStateIsAParseError()
	{
		Mermaid.Default.TryParse("stateDiagram-v2\nstate A {\nB\n", out _, out var diagnostics).Should().BeFalse();

		diagnostics.Should().ContainSingle().Which.Message.Should().StartWith("Parse error on line ");
	}

	[Test]
	public void ALexicalErrorRefusesTheSource()
	{
		Mermaid.Default.TryParse("stateDiagram-v2\nA --> B\nA --> \"quoted\"\n", out _, out var diagnostics).Should().BeFalse();

		diagnostics.Should().ContainSingle();
	}

	[Test]
	public void ACompositeStateWithASecondDescriptionIsRefusedAsTheDbRefusesIt()
	{
		Mermaid.Default.TryParse("stateDiagram-v2\nstate \"Title\" as G {\nX\n}\nG : more\n", out _, out var diagnostics).Should().BeFalse();

		diagnostics.Should().ContainSingle().Which.Message.Should()
			.Be("Group nodes can only have label. Remove the additional description for node [G]");
	}

	private static StateDocument Parse(string source) => (StateDocument)Mermaid.Default.Parse(source);
}
