using AwesomeAssertions;
using SplashTheMusical.Diagrams.Flowchart;

namespace SplashTheMusical.Tests.Diagrams;

public class FlowchartParserTests
{
	[Test]
	public void ReadsNodesEdgesAndLabels()
	{
		var document = Parse("flowchart LR\n  A[Start] --> B{Is it?}\n  B -- Yes --> C(Done)\n  B -.->|No| D((End))\n");

		document.DiagramType.Should().Be("flowchart-v2");
		document.Keyword.Should().Be("flowchart");
		document.Direction.Should().Be("LR");
		document.Nodes.Select(node => (node.Id, node.Label, node.Shape)).Should().Equal(
			("A", "Start", "square"),
			("B", "Is it?", "diamond"),
			("C", "Done", "round"),
			("D", "End", "circle"));
		document.Edges.Select(edge => (edge.Id, edge.Start, edge.End, edge.Label, edge.Type, edge.Stroke)).Should().Equal(
			("L_A_B_0", "A", "B", "", FlowchartEdgeType.Point, FlowchartStroke.Normal),
			("L_B_C_0", "B", "C", "Yes", FlowchartEdgeType.Point, FlowchartStroke.Normal),
			("L_B_D_0", "B", "D", "No", FlowchartEdgeType.Point, FlowchartStroke.Dotted));
	}

	[Test]
	public void GraphTdReadsAsTopToBottom()
	{
		var document = Parse("graph TD\nA --> B");

		document.Keyword.Should().Be("graph");
		document.Direction.Should().Be("TB");
	}

	[Test]
	public void FlowchartElkKeepsItsDetectorId() =>
		Parse("flowchart-elk TD\nA --> B").DiagramType.Should().Be("flowchart-elk");

	[Test]
	public void AnAmpersandLinksEveryNodeOfOneGroupToEveryNodeOfTheNext()
	{
		var document = Parse("flowchart TD\nA & B --> C & D");

		document.Edges.Select(edge => edge.Id).Should().Equal("L_A_C_0", "L_A_D_0", "L_B_C_0", "L_B_D_0");
	}

	[Test]
	public void ALexicalErrorAfterACompleteStatementRefusesTheSource()
	{
		Mermaid.Default.TryParse("flowchart TD\nA --> B\n°", out _, out var diagnostics).Should().BeFalse();

		diagnostics.Should().ContainSingle().Which.Message.Should().StartWith("Lexical error on line 3.");
	}

	[Test]
	public void AShapeDataLabelKeepsAnEntityThatYamlWouldReadAsAComment()
	{
		var document = Parse("flowchart TD\nA@{ label: a #quot; b }");

		document.Nodes.Should().ContainSingle().Which.Label.Should().Be("a #quot; b");
	}

	[Test]
	public void AShapeDataShapeAppliesToTheNode()
	{
		var document = Parse("flowchart TD\nA@{ shape: hex, label: \"Hex\" }");

		var node = document.Nodes.Should().ContainSingle().Subject;
		node.Shape.Should().Be("hex");
		node.Label.Should().Be("Hex");
	}

	[Test]
	public void ClassDefSplitsAtCommasThatAreNotEscaped()
	{
		var document = Parse("flowchart TD\nclassDef c,d fill:#f9f,color:red,font-family:a\\,b\n");

		document.Classes.Select(c => c.Id).Should().Equal("c", "d");
		var styles = document.Classes[0].Styles;
		styles.Should().Equal("fill:#f9f", "color:red", "font-family:a,b");
		document.Classes[0].TextStyles.Should().Equal("color:red");
	}

	[Test]
	public void ASubgraphTitleWithSpaceGivesNoId()
	{
		var document = Parse("flowchart TD\nsubgraph one two\nA\nend\nsubgraph three\nB\nend\nsubgraph s1 [The title]\nC\nend");

		document.Subgraphs.Select(subgraph => (subgraph.Id, subgraph.Title)).Should().Equal(
			("subGraph0", "one two"),
			("three", "three"),
			("s1", "The title"));
		document.Subgraphs.Select(subgraph => subgraph.Nodes.Single()).Should().Equal("A", "B", "C");
	}

	[Test]
	public void LinkStyleAddsFillNoneAndInterpolate()
	{
		var document = Parse("flowchart TD\nA --> B\nlinkStyle 0 interpolate basis stroke:red\n");

		var edge = document.Edges.Should().ContainSingle().Subject;
		edge.Interpolate.Should().Be("basis");
		edge.Styles.Should().Equal("stroke:red", "fill:none");
	}

	[Test]
	public void StyleAndClassApplyToTheNode()
	{
		var document = Parse("flowchart TD\nA:::first\nstyle A fill:#f9f,stroke:#333\nclass A second\n");

		var node = document.Nodes.Should().ContainSingle().Subject;
		node.Styles.Should().Equal("fill:#f9f", "stroke:#333");
		node.Classes.Should().Equal("first", "second");
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
		Mermaid.Default.TryParse(source, out _, out _).Should().BeFalse();

	[Test]
	[Arguments("flowchart")]
	[Arguments("graph")]
	[Arguments("flowchart TD;A-->B;")]
	[Arguments("%% comment\nflowchart TD\n%% another\nA --> B")]
	[Arguments("flowchart TD\r\nA --> B\r\n")]
	[Arguments("flowchart TD\nA --> B\nclick A href \"https://example.com\" \"tip\" _blank")]
	[Arguments("flowchart TD\naccTitle: T\naccDescr { D }\nA")]
	public void AcceptsWhatMermaidAccepts(string source) =>
		Mermaid.Default.TryParse(source, out _, out _).Should().BeTrue();

	private static FlowchartDocument Parse(string source) => (FlowchartDocument)Mermaid.Default.Parse(source);
}
