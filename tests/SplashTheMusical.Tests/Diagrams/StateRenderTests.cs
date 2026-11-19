using SplashTheMusical.Config;
using SplashTheMusical.Diagrams;
using SplashTheMusical.Diagrams.State;
using SplashTheMusical.Icons;
using SplashTheMusical.Tests.Rendering;
using SplashTheMusical.Text;

namespace SplashTheMusical.Tests.Diagrams;

/// <summary>
/// The state layout and renderer: the graphs <see cref="StateLayoutStep"/> hands the dagre port, the boxes of the
/// drawing and the SVG of the cluster and node shapes state adds to the shared dagre renderer.
/// </summary>
public class StateRenderTests
{
	private const string Composite = """
		stateDiagram-v2
		  [*] --> Idle
		  state Parent {
		    A --> B
		  }
		  Idle --> A
		""";

	[Test]
	public void ACompositeStateIsACompoundNodeThatHoldsItsStates()
	{
		// The transition into A crosses Parent's border, so dagre lays out the whole diagram in one graph.
		var graphs = Layout(Composite).DagreInputs;
		Verify(graphs.Count == 1);
		var graph = graphs[0];
		var parents = graph.Nodes.ToDictionary(node => node.Id, node => node.Parent);

		Verify(parents["A"] == "Parent");
		Verify(parents["B"] == "Parent");
		Verify(parents["Parent"] is null);
		Verify(parents["Idle"] is null);
		var edges = graph.Edges.Select(edge => (edge.V, edge.W)).ToList();
		Verify(edges.Contains(("A", "B")));
		Verify(edges.Contains(("Idle", "A")));
	}

	[Test]
	public void ACompositeStateWithNoCrossingTransitionIsLaidOutOnItsOwnFirst()
	{
		// As in corpus/state/e2e-state-diagram-nightly-check-simplest-composite-state.dagre.txt.
		var inputs = Layout("stateDiagram\n  state Parent {\n  C\n  }\n").DagreInputs;

		Verify(inputs.Count == 2);
		SequenceAssert.Equal(inputs[0].Nodes.Select(node => (node.Id, node.Parent)), ("C", "Parent"), ("Parent", null));
		Verify(Math.Abs((inputs[0].Options.RankSep ?? 0) - 75) <= 1e-9);
		Verify(inputs[1].Nodes.Count == 1);
		Verify(inputs[1].Nodes[0].Id == "Parent");
		Verify(inputs[1].Nodes[0].Width > inputs[0].Nodes[0].Width);
	}

	[Test]
	public void ACompositeStateInsideItselfIsAParseError()
	{
		// graphlib's setParent refuses the cycle in mermaid.js too. Crash fuzz source 41109 of seed 1.
		var layout = () => Layout("stateDiagram-v2\n    state P {\n        state P {\n            [*] --> f\n        }\n    }\n");

		Verify(Throws<MermaidParseException>(() => layout()).Message.Contains("Setting P as parent of P would create a cycle", StringComparison.Ordinal));
	}

	[Test]
	public void ConcurrentRegionsAreCompoundNodesInsideTheirState()
	{
		// A region no transition leaves is laid out in a graph of its own, and is a leaf of its parent's graph.
		var inputs = Layout("stateDiagram-v2\n  [*] --> Active\n  state Active {\n    X --> Y\n    --\n    P --> Q\n  }\n").DagreInputs;
		var parents = new Dictionary<string, string?>(StringComparer.Ordinal);
		foreach (var node in inputs.SelectMany(input => input.Nodes))
		{
			if (node.Parent is not null || !parents.ContainsKey(node.Id))
			{
				parents[node.Id] = node.Parent;
			}
		}

		var regionOfX = parents["X"];
		var regionOfP = parents["P"];
		Verify(regionOfX is not null);
		Verify(regionOfX != "Active");
		Verify(regionOfP is not null);
		Verify(regionOfP != regionOfX);
		Verify(parents[regionOfX!] == "Active");
		Verify(parents[regionOfP!] == "Active");
	}

	[Test]
	public void TheCompositeFrameHoldsItsStatesAndTheOthersAreOutside()
	{
		var boxes = Layout(Composite).Boxes().ToDictionary(box => box.Kind + " " + box.Name);
		var group = boxes["group Parent"];

		foreach (var name in new[] { "node A", "node B" })
		{
			var node = boxes[name];
			Verify(node.X >= group.X);
			Verify(node.Y >= group.Y);
			Verify((node.X + node.Width) <= group.X + group.Width);
			Verify((node.Y + node.Height) <= group.Y + group.Height);
		}

		var idle = boxes["node Idle"];
		Verify((idle.Y + idle.Height <= group.Y || idle.Y >= group.Y + group.Height));
	}

	[Test]
	public void TheSvgDrawsTheStateShapesAndPassesTheSanitizer()
	{
		var svg = SvgDocuments.Checked(Mermaid.Default.RenderSvg("""
			stateDiagram-v2
			  [*] --> Idle
			  Idle : waiting
			  Idle : for work
			  state Busy {
			    Run --> Done
			    --
			    Log --> Flush
			  }
			  Idle --> Busy : start
			  note right of Idle : a note
			  Busy --> [*]
			"""));

		Verify(svg.Contains("aria-roledescription=\"stateDiagram\"", StringComparison.Ordinal));
		Verify(svg.Contains("class=\"outer\"", StringComparison.Ordinal), $"a composite state is drawn as roundedWithTitle");
		Verify(svg.Contains("class=\"inner\"", StringComparison.Ordinal));
		Verify(svg.Contains("class=\"divider\"", StringComparison.Ordinal), $"a concurrent region is drawn as divider");
		Verify(svg.Contains("fill=\"none\"", StringComparison.Ordinal), $"a note's group is drawn as noteGroup");
		Verify(svg.Contains("marker-end=\"url(#mermaid_stateDiagram-barbEnd-margin)\"", StringComparison.Ordinal));

		// A state with two descriptions is a rectWithTitle whose title is the first description, so its id is not drawn.
		Verify(!svg.Contains(">Idle<", StringComparison.Ordinal));
		foreach (var text in new[] { "waiting", "for work", "Busy", "start", "a note" })
		{
			Verify(svg.Contains(">" + text + "<", StringComparison.Ordinal));
		}
	}

	private static StateLayout Layout(string source)
	{
		var document = (StateDocument)Mermaid.Default.Parse(source);
		var config = MermaidConfig.Resolve(null, document, document.DiagramType);
		var context = new DiagramRenderContext(config, "mermaid", TextMeasurer.Default, NoIcons.Instance, [], ResourceLimits.Unlimited);
		return new StateModule().Layout(document, context);
	}
}
