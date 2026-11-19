using SplashTheMusical.Diagrams.State;

namespace SplashTheMusical.Tests.Diagrams;

public class StateParserTests
{
	[Test]
	public void ReadsStatesTransitionsAndDescriptions()
	{
		var document = Parse("stateDiagram-v2\n  [*] --> Idle\n  Idle --> Busy : start\n  Busy : Working hard\n  Busy --> [*]\n");

		Verify(document.DiagramType == "stateDiagram");
		SequenceAssert.Equal(StateDump.Lines(document), "keyword stateDiagram-v2", "transition [*] -> Idle label=", "transition Idle -> Busy label=start", "state Busy description=Working hard", "transition Busy -> [*] label=");
	}

	[Test]
	public void TheVersionOneKeywordReadsTheSame()
	{
		var document = Parse("stateDiagram\nA --> B");

		SequenceAssert.Equal(StateDump.Lines(document), "keyword stateDiagram", "transition A -> B label=");
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

		SequenceAssert.Equal(StateDump.Lines(document), "keyword stateDiagram-v2", "block Active kind=Default description=The active state body=True", "  transition [*] -> Left label=", "  divider", "  transition [*] -> Right label=", "block Plain kind=Default description= body=True", "  block Inner kind=Default description= body=True", "    state X description=");
	}

	[Test]
	public void ReadsForksJoinsAndChoicesInBothForms()
	{
		var document = Parse("stateDiagram-v2\nstate f <<fork>>\nstate j <<join>>\nstate c <<choice>>\nstate f2 [[fork]]\n");

		SequenceAssert.Equal(StateDump.Lines(document).Skip(1), "block f kind=Fork description= body=False", "block j kind=Join description= body=False", "block c kind=Choice description= body=False", "block f2 kind=Fork description= body=False");
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
		SequenceAssert.Equal(notes.Select(n => (n.Position, n.Id)), (StateNotePosition.LeftOf, "A"), (StateNotePosition.RightOf, "A"));
		Verify(notes[0].Text == "on one line");
		Verify(notes[1].Text.StartsWith("first line\n", StringComparison.Ordinal));
		Verify(notes[1].Text.EndsWith("second line", StringComparison.Ordinal));
		var floating = document.Statements.OfType<StateFloatingNote>().ToList();
		Verify(floating.Count == 1);
		Verify(floating[0].Id == "N");
		Verify(floating[0].Text == "floating");
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

		SequenceAssert.Equal(StateDump.Lines(document).Skip(1), "direction LR", "classDef bad fill:#f00,color:white", "transition A:::bad -> B label=", "class A,B bad", "style B fill:#0f0", "hide empty description", "scale 350");
	}

	[Test]
	public void ReadsBothFormsOfClick()
	{
		var document = Parse("stateDiagram-v2\nA\nclick A \"https://example.com\" \"Go there\"\nclick A href \"https://example.org\"\n");

		SequenceAssert.Equal(document.Statements.OfType<StateClick>().Select(c => (c.State.Id, c.Url, c.Tooltip)), ("A", "https://example.com", "Go there"), ("A", "https://example.org", (string?)null));
	}

	[Test]
	public void ReadsAccessibilityTitleAndDescription()
	{
		var document = Parse("stateDiagram-v2\naccTitle: The title\naccDescr: The description\nA\n");

		Verify(document.AccessibilityTitle == "The title");
		Verify(document.AccessibilityDescription == "The description");
		SequenceAssert.Equal(StateDump.Lines(document).Skip(1), "state A description=");
	}

	[Test]
	public void KeepsEntityCodesAsWritten()
	{
		var document = Parse("stateDiagram-v2\nA : say #quot;hi#quot;\n");

		Verify(document.Statements.OfType<StateDeclaration>().Single().Description == "say #quot;hi#quot;");
	}

	[Test]
	public void InternsEachIdOnce()
	{
		var document = Parse("stateDiagram-v2\nA --> B\nB --> A\n");

		var transitions = document.Statements.OfType<StateTransition>().ToList();
		Verify(ReferenceEquals(transitions[1].To.Id, transitions[0].From.Id));
		Verify(ReferenceEquals(transitions[1].From.Id, transitions[0].To.Id));
	}

	[Test]
	public void AnUnclosedCompositeStateIsAParseError()
	{
		Verify(!Mermaid.Default.TryParse("stateDiagram-v2\nstate A {\nB\n", out _, out var diagnostics));

		Verify(diagnostics.Count == 1);
		Verify(diagnostics[0].Message.StartsWith("Parse error on line ", StringComparison.Ordinal));
	}

	[Test]
	public void ALexicalErrorRefusesTheSource()
	{
		Verify(!Mermaid.Default.TryParse("stateDiagram-v2\nA --> B\nA --> \"quoted\"\n", out _, out var diagnostics));

		Verify(diagnostics.Count == 1);
	}

	[Test]
	public void ACompositeStateWithASecondDescriptionIsRefusedAsTheDbRefusesIt()
	{
		Verify(!Mermaid.Default.TryParse("stateDiagram-v2\nstate \"Title\" as G {\nX\n}\nG : more\n", out _, out var diagnostics));

		Verify(diagnostics.Count == 1);
		Verify(diagnostics[0].Message == "Group nodes can only have label. Remove the additional description for node [G]");
	}

	private static StateDocument Parse(string source) => (StateDocument)Mermaid.Default.Parse(source);
}
