using AwesomeAssertions;
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

		written.Should().Be("""
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
		Mermaid.Default.Parse(written).Should().BeEquivalentTo(document, options => options.WithStrictOrdering());
	}

	[Test]
	public void EdgeIdsCountRepeatedEdgesAsMermaidDoes() =>
		new FlowchartBuilder().Edge("A", "B").Edge("A", "B").Edge("A", "B").Build().Edges.Select(edge => edge.Id)
			.Should().Equal("L_A_B_0", "L_A_B_2", "L_A_B_3");

	[Test]
	public void ASubgraphLeavesOutANodeAnEarlierOneHolds()
	{
		var document = new FlowchartBuilder().Subgraph("one", "One", "A", "B").Subgraph("two", "Two", "B", "C").Build();

		document.Subgraphs[1].Nodes.Should().Equal("C");
	}

	[Test]
	[Arguments("")]
	[Arguments("a ] b")]
	public void ASubgraphTitleReadsBackFromTheWrittenText(string title)
	{
		var document = new FlowchartBuilder().Subgraph("S", title, "C").Build();

		var written = Mermaid.Default.Write(document);

		Mermaid.Default.Parse(written).Should().BeEquivalentTo(document, options => options.WithStrictOrdering(), written);
	}

	[Test]
	[Arguments(" padded")]
	[Arguments("say \"hi\" [twice]")]
	public void ASubgraphTitleNoHeaderHoldsIsRefused(string title)
	{
		var adding = () => new FlowchartBuilder().Subgraph("S", title, "C");

		adding.Should().Throw<ArgumentException>().WithParameterName("title");
	}

	[Test]
	public void ClassDefDerivesTheTextStyles() =>
		new FlowchartBuilder().ClassDef("c", "fill:#f00,color:red", "stroke:#333").Build().Classes.Single().TextStyles
			.Should().Equal("bgFill:#f00,color:red");

	[Test]
	public void ASubgraphHoldsOnlyTheLastSubgraphsAddedInTheirOrder()
	{
		var builder = new FlowchartBuilder().Subgraph("a", "A", "x").Subgraph("b", "B", "y");

		var skipping = () => builder.Subgraph("outer", "Outer", "a");
		var reordering = () => builder.Subgraph("outer", "Outer", "b", "a");

		skipping.Should().Throw<ArgumentException>();
		reordering.Should().Throw<ArgumentException>();
	}

	[Test]
	[Arguments("XY")]
	[Arguments("")]
	public void RefusesADirectionMermaidDoesNotHave(string direction)
	{
		var build = () => new FlowchartBuilder(direction);

		build.Should().Throw<ArgumentException>();
	}

	[Test]
	public void RefusesAShapeMermaidDoesNotHave()
	{
		var build = () => new FlowchartBuilder().Node("A", "a", "Rect");

		build.Should().Throw<ArgumentException>();
	}
}
