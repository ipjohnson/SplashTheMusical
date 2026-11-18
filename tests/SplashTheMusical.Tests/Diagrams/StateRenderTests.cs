using AwesomeAssertions;
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
		var graph = Layout(Composite).DagreInputs.Should().ContainSingle().Subject;
		var parents = graph.Nodes.ToDictionary(node => node.Id, node => node.Parent);

		parents["A"].Should().Be("Parent");
		parents["B"].Should().Be("Parent");
		parents["Parent"].Should().BeNull();
		parents["Idle"].Should().BeNull();
		graph.Edges.Select(edge => (edge.V, edge.W)).Should().Contain(("A", "B")).And.Contain(("Idle", "A"));
	}

	[Test]
	public void ACompositeStateWithNoCrossingTransitionIsLaidOutOnItsOwnFirst()
	{
		// As in corpus/state/e2e-state-diagram-nightly-check-simplest-composite-state.dagre.txt.
		var inputs = Layout("stateDiagram\n  state Parent {\n  C\n  }\n").DagreInputs;

		inputs.Should().HaveCount(2);
		inputs[0].Nodes.Select(node => (node.Id, node.Parent)).Should().Equal(("C", "Parent"), ("Parent", null));
		inputs[0].Options.RankSep.Should().Be(75);
		inputs[1].Nodes.Should().ContainSingle().Which.Id.Should().Be("Parent");
		inputs[1].Nodes[0].Width.Should().BeGreaterThan(inputs[0].Nodes[0].Width);
	}

	[Test]
	public void ACompositeStateInsideItselfIsAParseError()
	{
		// graphlib's setParent refuses the cycle in mermaid.js too. Crash fuzz source 41109 of seed 1.
		var layout = () => Layout("stateDiagram-v2\n    state P {\n        state P {\n            [*] --> f\n        }\n    }\n");

		layout.Should().Throw<MermaidParseException>().WithMessage("*Setting P as parent of P would create a cycle*");
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
		regionOfX.Should().NotBeNull().And.NotBe("Active");
		regionOfP.Should().NotBeNull().And.NotBe(regionOfX);
		parents[regionOfX!].Should().Be("Active");
		parents[regionOfP!].Should().Be("Active");
	}

	[Test]
	public void TheCompositeFrameHoldsItsStatesAndTheOthersAreOutside()
	{
		var boxes = Layout(Composite).Boxes().ToDictionary(box => box.Kind + " " + box.Name);
		var group = boxes["group Parent"];

		foreach (var name in new[] { "node A", "node B" })
		{
			var node = boxes[name];
			node.X.Should().BeGreaterThanOrEqualTo(group.X);
			node.Y.Should().BeGreaterThanOrEqualTo(group.Y);
			(node.X + node.Width).Should().BeLessThanOrEqualTo(group.X + group.Width);
			(node.Y + node.Height).Should().BeLessThanOrEqualTo(group.Y + group.Height);
		}

		var idle = boxes["node Idle"];
		(idle.Y + idle.Height <= group.Y || idle.Y >= group.Y + group.Height).Should().BeTrue();
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

		svg.Should().Contain("aria-roledescription=\"stateDiagram\"");
		svg.Should().Contain("class=\"outer\"", "a composite state is drawn as roundedWithTitle");
		svg.Should().Contain("class=\"inner\"");
		svg.Should().Contain("class=\"divider\"", "a concurrent region is drawn as divider");
		svg.Should().Contain("fill=\"none\"", "a note's group is drawn as noteGroup");
		svg.Should().Contain("marker-end=\"url(#mermaid_stateDiagram-barbEnd-margin)\"");

		// A state with two descriptions is a rectWithTitle whose title is the first description, so its id is not drawn.
		svg.Should().NotContain(">Idle<");
		foreach (var text in new[] { "waiting", "for work", "Busy", "start", "a note" })
		{
			svg.Should().Contain(">" + text + "<");
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
