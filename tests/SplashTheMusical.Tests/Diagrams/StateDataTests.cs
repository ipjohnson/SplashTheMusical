using SplashTheMusical.Diagrams.State;

namespace SplashTheMusical.Tests.Diagrams;

/// <summary>The port of <c>stateDb.ts</c>'s <c>docTranslator</c> and <c>extract</c>, and of <c>dataFetcher.ts</c>.</summary>
public class StateDataTests
{
	[Test]
	public void NamesTheStartAndEndAfterTheDocumentTheyAreIn()
	{
		var data = Data("stateDiagram-v2\n[*] --> A\nstate A {\n[*] --> B\nB --> [*]\n}\nA --> [*]\n");

		SequenceAssert.Equal(Nodes(data), "root_start stateStart parent= label=root_start", "A roundedWithTitle parent= label=A group TB", "A_start stateStart parent=A label=A_start", "B rect parent=A label=B", "A_end stateEnd parent=A label=A_end", "root_end stateEnd parent= label=root_end");
		SequenceAssert.Equal(Edges(data), "edge0 root_start -> A arrow_barb label=", "edge1 A_start -> B arrow_barb label=", "edge2 B -> A_end arrow_barb label=", "edge3 A -> root_end arrow_barb label=");
	}

	[Test]
	public void SplitsACompositeStateIntoRegionsAtItsDividers()
	{
		var data = Data("stateDiagram-v2\nstate P {\n[*] --> X\n--\n[*] --> Y\n--\nZ\n}\n");

		SequenceAssert.Equal(Nodes(data), "P roundedWithTitle parent= label=P group TB", "divider-id-1 divider parent=P label= group TB", "divider-id-1_start stateStart parent=divider-id-1 label=divider-id-1_start", "X rect parent=divider-id-1 label=X", "divider-id-2 divider parent=P label= group TB", "divider-id-2_start stateStart parent=divider-id-2 label=divider-id-2_start", "Y rect parent=divider-id-2 label=Y", "id-stm-3 divider parent=P label= group TB", "Z rect parent=id-stm-3 label=Z");
	}

	[Test]
	public void ATrailingDividerLeavesTheStatementsUnsplit()
	{
		// docTranslator replaces the doc only when the last region has statements.
		var data = Data("stateDiagram-v2\nstate P {\nX\n--\n}\n");

		SequenceAssert.Equal(Nodes(data), "P roundedWithTitle parent= label=P group TB", "X rect parent=P label=X", "divider-id-1 divider parent=P label=");
	}

	[Test]
	public void AStateWithTwoDescriptionsHasATitle()
	{
		var data = Data("stateDiagram-v2\nA : one\nB : first\nB : second\n");

		SequenceAssert.Equal(Nodes(data), "A rect parent= label=one", "B rectWithTitle parent= label=first|second");
	}

	[Test]
	public void ForksJoinsAndChoicesKeepTheirShape()
	{
		var data = Data("stateDiagram-v2\nstate f <<fork>>\nstate j <<join>>\nstate c <<choice>>\n");

		SequenceAssert.Equal(Nodes(data), "f fork parent= label=f", "j join parent= label=j", "c choice parent= label=c");
	}

	[Test]
	public void ANoteAddsAGroupANoteNodeAndAnEdge()
	{
		var data = Data("stateDiagram-v2\nA --> B\nnote left of A : left\nnote right of B : right\n");

		// A note's state keeps the place the transition gave it, and the note's group and node follow.
		SequenceAssert.Equal(Nodes(data), "A rect parent= label=A", "B rect parent= label=B", "A----parent noteGroup parent= label=left group", "A----note-1 note parent=A----parent label=left", "B----parent noteGroup parent= label=right group", "B----note-2 note parent=B----parent label=right");
		SequenceAssert.Equal(Edges(data), "edge0 A -> B arrow_barb label=", "A----note-1-A A----note-1 -> A  label= note", "B-B----note-2 B -> B----note-2  label= note");
	}

	[Test]
	public void TheNeoLookEndsTransitionsInTheNeoArrow()
	{
		var data = StateData.From(Parse("stateDiagram-v2\nA --> B : go\n"), "neo");

		SequenceAssert.Equal(Edges(data), "edge0 A -> B arrow_barb_neo label=go");
	}

	[Test]
	public void AGroupTakesTheLastDirectionOfItsOwnStatements()
	{
		var data = Data("stateDiagram-v2\ndirection LR\nstate G {\ndirection BT\nX\ndirection RL\n}\n");

		SequenceAssert.Equal(Nodes(data), "G roundedWithTitle parent= label=G group RL", "X rect parent=G label=X");
	}

	[Test]
	public void AGroupWithADescriptionBesideItsTitleIsAnError()
	{
		var document = new StateDocument();
		document.Statements.Add(new StateBlock("G") { Description = "Title", Statements = [new StateDeclaration(new StateRef("X"))] });
		document.Statements.Add(new StateDeclaration(new StateRef("G")) { Description = "more" });

		Verify(StateData.MayFail(document));
		Verify(StateData.From(document, null).Error == "Group nodes can only have label. Remove the additional description for node [G]");
	}

	[Test]
	public void ADocumentWithoutACompositeStateCannotFail() =>
		Verify(!StateData.MayFail(Parse("stateDiagram-v2\nA : one\nA : two\n")));

	private static StateData Data(string source)
	{
		var data = StateData.From(Parse(source), null);
		Verify(data.Error is null);
		return data;
	}

	private static StateDocument Parse(string source) => (StateDocument)Mermaid.Default.Parse(source);

	private static List<string> Nodes(StateData data) =>
	[
		.. data.Nodes.Select(n =>
			$"{n.Id} {n.Shape} parent={n.ParentId} label={string.Join('|', n.Label)}{(n.Type == "group" ? " group" : "")}{(n.Direction is { } d ? " " + d : "")}"),
	];

	private static List<string> Edges(StateData data) =>
	[
		.. data.Edges.Select(e => $"{e.Id} {e.Start} -> {e.End} {e.ArrowTypeEnd} label={e.Label}{(e.IsNoteEdge ? " note" : "")}"),
	];
}
