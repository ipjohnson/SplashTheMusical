using SplashTheMusical.Tests.Generation;
using SplashTheMusical.Diagrams.Flowchart;

namespace SplashTheMusical.Tests.Diagrams;

public class FlowchartBuilderTests
{
	[Test]
	public void BuildsWhatTheParserReadsFromTheWrittenText()
	{
		var document = new FlowchartBuilder("LR")
			.Node("A", "Start", "stadium")
			.Node("B", "Is it (really)?", "diamond")
			.Edge("A", "B", "next")
			.Edge("B", "C", "yes", stroke: FlowchartStroke.Dotted)
			.Edge("B", "D", type: FlowchartEdgeType.Open, stroke: FlowchartStroke.Thick, length: 2)
			.Edge("A", "B", id: "again")
			.Subgraph("inner", "Inner", "C")
			.Subgraph("outer", "The outer group", "inner", "D", "C")
			.ClassDef("done", "fill:#0f0", "color:#fff")
			.Class("D", "done")
			.Style("A", "stroke:#333")
			.Build();

		var written = Mermaid.Default.Write(document);

		Verify(written == """
			flowchart LR
			    A([Start])
			    B{"Is it (really)?"}
			    C
			    D
			    style A stroke:#333
			    class D done
			    A -->|next| B
			    B -.->|yes| C
			    B ==== D
			    A again@--> B
			    subgraph outer[The outer group]
			        subgraph inner[Inner]
			            C
			        end
			        D
			    end
			    classDef done fill:#0f0,color:#fff

			""".ReplaceLineEndings("\n"));
		SequenceAssert.Equal(DocumentDump.Lines(Mermaid.Default.Parse(written)), DocumentDump.Lines(document).ToArray());
	}

	[Test]
	public void EdgeIdsCountRepeatedEdgesAsMermaidDoes()
	{
		var edges = new FlowchartBuilder().Edge("A", "B").Edge("A", "B").Edge("A", "B").Build().Edges;

		SequenceAssert.Equal(edges.Select(edge => edge.Id), "L_A_B_0", "L_A_B_2", "L_A_B_3");
	}

	[Test]
	public void ASubgraphLeavesOutANodeAnEarlierOneHolds()
	{
		var document = new FlowchartBuilder().Subgraph("one", "One", "A", "B").Subgraph("two", "Two", "B", "C").Build();

		SequenceAssert.Equal(document.Subgraphs[1].Nodes, "C");
	}

	[Test]
	[Arguments("")]
	[Arguments("a ] b")]
	public void ASubgraphTitleReadsBackFromTheWrittenText(string title)
	{
		var document = new FlowchartBuilder().Subgraph("S", title, "C").Build();

		var written = Mermaid.Default.Write(document);

		SequenceAssert.Equal(DocumentDump.Lines(Mermaid.Default.Parse(written)), DocumentDump.Lines(document).ToArray());
	}

	[Test]
	[Arguments(" padded")]
	[Arguments("say \"hi\" [twice]")]
	public void ASubgraphTitleNoHeaderHoldsIsRefused(string title)
	{
		var adding = () => new FlowchartBuilder().Subgraph("S", title, "C");

		Verify(Throws<ArgumentException>(() => adding()).ParamName == "title");
	}

	[Test]
	public void ClassDefDerivesTheTextStyles()
	{
		var styles = new FlowchartBuilder().ClassDef("c", "fill:#f00,color:red", "stroke:#333").Build().Classes.Single().TextStyles;

		SequenceAssert.Equal(styles, "bgFill:#f00,color:red");
	}

	[Test]
	public void ASubgraphHoldsOnlyTheLastSubgraphsAddedInTheirOrder()
	{
		var builder = new FlowchartBuilder().Subgraph("a", "A", "x").Subgraph("b", "B", "y");

		var skipping = () => builder.Subgraph("outer", "Outer", "a");
		var reordering = () => builder.Subgraph("outer", "Outer", "b", "a");

		Throws<ArgumentException>(() => skipping());
		Throws<ArgumentException>(() => reordering());
	}

	[Test]
	[Arguments("XY")]
	[Arguments("")]
	public void RefusesADirectionMermaidDoesNotHave(string direction)
	{
		var build = () => new FlowchartBuilder(direction);

		Throws<ArgumentException>(() => build());
	}

	[Test]
	public void RefusesAShapeMermaidDoesNotHave()
	{
		var build = () => new FlowchartBuilder().Node("A", "a", "Rect");

		Throws<ArgumentException>(() => build());
	}
}
