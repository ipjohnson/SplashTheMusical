using SplashTheMusical.Config;
using SplashTheMusical.Diagrams;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Icons;
using SplashTheMusical.Layout.Dagre;
using SplashTheMusical.Text;

namespace SplashTheMusical.Tests.Diagrams;

/// <summary>
/// The graphs the flowchart hands dagre for collapsed, empty and extracted subgraphs, as mermaid.js 12.3.0 hands them,
/// which <c>corpus/flowchart/*.dagre.txt</c> records.
/// </summary>
public class FlowchartSubgraphLayoutTests
{
	private static IReadOnlyList<DagreInput> DagreInputs(string source)
	{
		var mermaid = new Mermaid(new MermaidOptions { Limits = ResourceLimits.Unlimited });
		Verify(mermaid.TryParse(source, out var document, out _));
		var flowchart = (FlowchartDocument)document!;
		var config = MermaidConfig.Resolve(null, flowchart, flowchart.DiagramType);
		var context = new DiagramRenderContext(config, "mermaid", TextMeasurer.Default, NoIcons.Instance, [], ResourceLimits.Unlimited);
		return new FlowchartModule(flowchart.DiagramType).Layout(flowchart, context).DagreInputs;
	}

	[Test]
	public void ACollapsedSubgraphIsOneNodeAtLeast80WideAndItsEdgesKeepTheirIds()
	{
		var inputs = DagreInputs("flowchart LR\n  A --> B\n  subgraph S\n    B --> C\n  end\n  C --> D\n  S@{ view: collapsed }\n");

		Verify(inputs.Count == 1);
		var graph = inputs[0];
		Verify(graph.Nodes.Select(node => node.Id).Order(StringComparer.Ordinal).SequenceEqual(["A", "D", "S"]));
		var collapsed = graph.Nodes.Single(node => node.Id == "S");
		Verify(Math.Abs(collapsed.Width - 80) <= 1e-9);
		Verify(collapsed.Height > collapsed.Width / 2, $"the shape adds the separator and the row of dots below the title");
		SequenceAssert.Equal(graph.Edges.Select(edge => (edge.V, edge.W, edge.Name)), ("A", "S", "L_A_B_0"), ("S", "D", "L_C_D_0"));
	}

	[Test]
	public void AnEmptySubgraphIsAsWideAsItsTitleAndNotTheMinimumNodeWidth()
	{
		var inputs = DagreInputs("flowchart TB\n  subgraph S\n  end\n  A --> B\n");

		Verify(inputs.Count == 1);
		var empty = inputs[0].Nodes.Single(node => node.Id == "S");
		var vertex = inputs[0].Nodes.Single(node => node.Id == "A");
		Verify(empty.Width < vertex.Width);
		Verify(Math.Abs(empty.Height - vertex.Height) <= 1e-9);
	}

	[Test]
	public void ExtractedSubgraphsAreLaidOutByHowDeeplyTheyNestThenInTheOrderTheyStart()
	{
		// The root graph holds C before B, so a depth-first layout would lay out C1, C and then B.
		var inputs = DagreInputs("flowchart TB\n  subgraph B\n    Y\n  end\n  subgraph C\n    subgraph C1\n      X\n    end\n  end\n");

		SequenceAssert.Equal(inputs.Select(input => (input.Nodes[^1].Id, input.Options.RankDir, input.Options.RankSep)), ("C1", "TB", 100), ("B", "LR", 75), ("C", "LR", 75), (inputs[^1].Nodes[^1].Id, "TB", 50));
	}
}
