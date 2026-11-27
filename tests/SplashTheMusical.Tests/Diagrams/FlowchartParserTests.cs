using SplashTheMusical.Diagrams.Flowchart;

namespace SplashTheMusical.Tests.Diagrams;

public class FlowchartParserTests
{
	[Test]
	public void ReadsNodesEdgesAndLabels()
	{
		var document = Parse("flowchart LR\n  A[Start] --> B{Is it?}\n  B -- Yes --> C(Done)\n  B -.->|No| D((End))\n");

		Verify(document.DiagramType == "flowchart-v2");
		Verify(document.Keyword == "flowchart");
		Verify(document.Direction == "LR");
		SequenceAssert.Equal(document.Nodes.Select(node => (node.Id, node.Label, node.Shape)), ("A", "Start", "square"), ("B", "Is it?", "diamond"), ("C", "Done", "round"), ("D", "End", "circle"));
		SequenceAssert.Equal(document.Edges.Select(edge => (edge.Id, edge.Start, edge.End, edge.Label, edge.Type, edge.Stroke)), ("L_A_B_0", "A", "B", "", FlowchartEdgeType.Point, FlowchartStroke.Normal), ("L_B_C_0", "B", "C", "Yes", FlowchartEdgeType.Point, FlowchartStroke.Normal), ("L_B_D_0", "B", "D", "No", FlowchartEdgeType.Point, FlowchartStroke.Dotted));
	}

	[Test]
	public void GraphTdReadsAsTopToBottom()
	{
		var document = Parse("graph TD\nA --> B");

		Verify(document.Keyword == "graph");
		Verify(document.Direction == "TB");
	}

	[Test]
	public void FlowchartElkKeepsItsDetectorId() =>
		Verify(Parse("flowchart-elk TD\nA --> B").DiagramType == "flowchart-elk");

	[Test]
	public void AnAmpersandLinksEveryNodeOfOneGroupToEveryNodeOfTheNext()
	{
		var document = Parse("flowchart TD\nA & B --> C & D");

		SequenceAssert.Equal(document.Edges.Select(edge => edge.Id), "L_A_C_0", "L_A_D_0", "L_B_C_0", "L_B_D_0");
	}

	[Test]
	public void ALexicalErrorAfterACompleteStatementRefusesTheSource()
	{
		Verify(!Mermaid.Default.TryParse("flowchart TD\nA --> B\n°", out _, out var diagnostics));

		Verify(diagnostics.Count == 1);
		Verify(diagnostics[0].Message.StartsWith("Lexical error on line 3.", StringComparison.Ordinal));
	}

	[Test]
	public void AShapeDataLabelKeepsAnEntityThatYamlWouldReadAsAComment()
	{
		var document = Parse("flowchart TD\nA@{ label: a #quot; b }");

		Verify(document.Nodes.Count == 1);
		Verify(document.Nodes[0].Label == "a #quot; b");
	}

	[Test]
	public void AShapeDataShapeAppliesToTheNode()
	{
		var document = Parse("flowchart TD\nA@{ shape: hex, label: \"Hex\" }");

		Verify(document.Nodes.Count == 1);
		var node = document.Nodes[0];
		Verify(node.Shape == "hex");
		Verify(node.Label == "Hex");
	}

	[Test]
	public void ClassDefSplitsAtCommasThatAreNotEscaped()
	{
		var document = Parse("flowchart TD\nclassDef c,d fill:#f9f,color:red,font-family:a\\,b\n");

		SequenceAssert.Equal(document.Classes.Select(c => c.Id), "c", "d");
		var styles = document.Classes[0].Styles;
		SequenceAssert.Equal(styles, "fill:#f9f", "color:red", "font-family:a,b");
		SequenceAssert.Equal(document.Classes[0].TextStyles, "color:red");
	}

	[Test]
	public void ASubgraphTitleWithSpaceGivesNoId()
	{
		var document = Parse("flowchart TD\nsubgraph one two\nA\nend\nsubgraph three\nB\nend\nsubgraph s1 [The title]\nC\nend");

		SequenceAssert.Equal(document.Subgraphs.Select(subgraph => (subgraph.Id, subgraph.Title)), ("subGraph0", "one two"), ("three", "three"), ("s1", "The title"));
		SequenceAssert.Equal(document.Subgraphs.Select(subgraph => subgraph.Nodes.Single()), "A", "B", "C");
	}

	[Test]
	public void LinkStyleAddsFillNoneAndInterpolate()
	{
		var document = Parse("flowchart TD\nA --> B\nlinkStyle 0 interpolate basis stroke:red\n");

		Verify(document.Edges.Count == 1);
		var edge = document.Edges[0];
		Verify(edge.Interpolate == "basis");
		SequenceAssert.Equal(edge.Styles!, "stroke:red", "fill:none");
	}

	[Test]
	public void StyleAndClassApplyToTheNode()
	{
		var document = Parse("flowchart TD\nA:::first\nstyle A fill:#f9f,stroke:#333\nclass A second\n");

		Verify(document.Nodes.Count == 1);
		var node = document.Nodes[0];
		SequenceAssert.Equal(node.Styles, "fill:#f9f", "stroke:#333");
		SequenceAssert.Equal(node.Classes, "first", "second");
	}

	[Test]
	[Arguments("flowchart TD\nsubgraph\nA\nend")]
	[Arguments("flowchart TD\nA --> B\nlinkStyle 1 stroke:red")]
	[Arguments("flowchart TD\nA --> B\nlinkStyle 00 stroke:red")]
	[Arguments("flowchart TD\nA@{ shape: nothing }")]
	[Arguments("flowchart TD\nA@{ shape: Rect }")]
	[Arguments("flowchart TD\nA(-text-)")]
	[Arguments("flowchart TD\nA --> B\nend")]
	[Arguments("flowchart TD\nsubgraph a\nA")]
	[Arguments("flowchart TD\nA[text")]
	[Arguments("flowchart XY\nA")]
	public void RefusesWhatMermaidRefuses(string source) =>
		Verify(!Mermaid.Default.TryParse(source, out _, out _));

	[Test]
	[Arguments("flowchart")]
	[Arguments("graph")]
	[Arguments("flowchart TD;A-->B;")]
	[Arguments("%% comment\nflowchart TD\n%% another\nA --> B")]
	[Arguments("flowchart TD\r\nA --> B\r\n")]
	[Arguments("flowchart TD\nA --> B\nclick A href \"https://example.com\" \"tip\" _blank")]
	[Arguments("flowchart TD\naccTitle: T\naccDescr { D }\nA")]
	public void AcceptsWhatMermaidAccepts(string source) =>
		Verify(Mermaid.Default.TryParse(source, out _, out _));

	private static FlowchartDocument Parse(string source) => (FlowchartDocument)Mermaid.Default.Parse(source);
}
