using SplashTheMusical.Config;
using SplashTheMusical.Diagrams;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Diagrams.Requirement;
using SplashTheMusical.Icons;
using SplashTheMusical.Rendering.Shapes;
using SplashTheMusical.Tests.Rendering;
using SplashTheMusical.Text;

namespace SplashTheMusical.Tests.Diagrams;

/// <summary>
/// The requirement layout and renderer: the graph <see cref="RequirementLayoutStep"/> hands the dagre port, the sizes,
/// texts and divider of the <c>requirementBox</c> shape, the boxes of the drawing and its SVG with the requirement
/// markers.
/// </summary>
public class RequirementRenderTests
{
	// corpus/requirement/docs-requirementDiagram-003.mmd, which mermaid.js lays out with dagre.
	private const string Checkout = """
		---
		config:
		  theme: default
		  look: classic
		  layout: dagre
		---
		requirementDiagram
		  requirement checkout_req {
		    id: 1
		    text: Orders must be payable online.
		    risk: high
		    verifymethod: test
		  }
		  functionalRequirement payment_req {
		    id: 1.1
		    text: Card payments must be authorised.
		    risk: high
		    verifymethod: test
		  }
		  element checkout_service {
		    type: service
		  }
		  checkout_req - contains -> payment_req
		  checkout_service - satisfies -> payment_req
		""";

	[Test]
	public void TheDrawingHasTheBoxesMermaidJsDraws()
	{
		// corpus/requirement/docs-requirementDiagram-003.boxes.txt.
		var layout = Layout(Checkout);
		var boxes = FlowchartLayout.BoxesOf(layout.Root).ToDictionary(box => box.Kind + " " + box.Name);
		void Near(string name, double x, double y, double width, double height)
		{
			Verify(boxes.TryGetValue(name, out var box), $"no box {name} in {string.Join(", ", boxes.Keys)}");
			Verify(() => Math.Abs(box.X - x) <= 0.01 && Math.Abs(box.Y - y) <= 0.01, $"{name} at {box.X}, {box.Y}");
			Verify(() => Math.Abs(box.Width - width) <= 0.01 && Math.Abs(box.Height - height) <= 0.01, $"{name} is {box.Width} by {box.Height}");
		}

		Near("node checkout_req", 8, 8, 280.578, 184);
		Near("node payment_req", 126.16, 266, 311.703, 184);
		Near("node checkout_service", 338.578, 44, 154.313, 112);
		Near("edge-label checkout_req --> payment_req", 99.805, 217, 96.969, 24);
		Near("edge-label checkout_service --> payment_req", 368.148, 217, 95.172, 24);
		var viewBox = layout.ViewBox!.Value;
		Verify(() => Math.Abs(viewBox.Width - 500.891) <= 0.01 && Math.Abs(viewBox.Height - 458) <= 0.01);
	}

	[Test]
	public void TheBoxStacksTheTypeTheNameAndTheBodyLines()
	{
		var node = Node(Layout(Checkout), "checkout_req");
		var labels = node.Labels!;
		var texts = labels.Select(label => string.Concat(label.Box.Lines.SelectMany(line => line).Select(run => run.Text))).ToList();
		SequenceAssert.Equal(texts, "<<Requirement>>", "checkout_req", "ID: 1", "Text: Orders must be payable online.", "Risk: High", "Verification: Test");

		// The name is bold, and the body lines start 10 inside the frame, under the divider.
		Verify(() => labels[1].Box.Lines[0].All(run => run.Bold));
		Verify(() => labels.Skip(2).All(label => label.Box.AlignStart && Math.Abs(label.X - (-140.2890625 + 10)) <= 0.01));
		Verify(() => !labels[0].Box.AlignStart && !labels[1].Box.AlignStart);
		var divider = node.Geometry!.Elements.Single(element => element.Class == "divider");
		var line = divider.Children!.Single();
		Verify(() => line.Kind == ShapeElementKind.Line && line.Y == -24);
	}

	[Test]
	public void ADividerNeedsABodyLineThatCountsTowardsTheHeight()
	{
		// requirementBox does not add the last body line, the verify method or the document reference, to the height it
		// compares, so a box with only that line has no divider.
		var layout = Layout("""
			requirementDiagram
			  requirement only_verify {
			    verifymethod: test
			  }
			  element only_docref {
			    docref: spec.md
			  }
			  element with_type {
			    type: service
			  }
			""");
		Verify(() => Dividers(Node(layout, "only_verify")) == 0);
		Verify(() => Dividers(Node(layout, "only_docref")) == 0);
		Verify(() => Dividers(Node(layout, "with_type")) == 1);
	}

	[Test]
	public void ABoldStyleWidensEveryText()
	{
		// corpus/requirement/docs-requirementDiagram-009.elk.txt: the class's font-weight is in the label style every
		// text is drawn with.
		var nodes = Layout("""
			requirementDiagram
			requirement test_req:::important {
			    id: 1
			    text: "class styling example"
			    risk: low
			    verifymethod: test
			}
			element test_entity {
			    type: simulation
			}
			classDef important font-weight:bold
			class test_entity important
			""").DagreInputs[0].Nodes.ToDictionary(node => node.Id);

		Verify(() => Math.Abs(nodes["test_req"].Width - 200.265625) <= 0.01);
		Verify(() => Math.Abs(nodes["test_entity"].Width - 130.203125) <= 0.01);
		Verify(() => Math.Abs(nodes["test_entity"].Height - 103) <= 0.01);
	}

	[Test]
	public void TheRendererReadsTheStateConfigsSpacing()
	{
		var input = Layout("""
			---
			config:
			  state:
			    nodeSpacing: 70
			    rankSpacing: 90
			  requirement:
			    nodeSpacing: 10
			---
			requirementDiagram
			  element a {
			    type: service
			  }
			  element b {
			    type: service
			  }
			  a - traces -> b
			""").DagreInputs[0];
		Verify(() => input.Options.NodeSep == 70 && input.Options.RankSep == 90);
	}

	[Test]
	public void TheSvgDrawsTheBoxesTheMarkersAndTheLabelBackgroundsAndPassesTheSanitizer()
	{
		var svg = SvgDocuments.Checked(Mermaid.Default.RenderSvg("""
			---
			title: Checkout
			config:
			  look: classic
			  theme: default
			---
			requirementDiagram
			  requirement checkout_req {
			    id: 1
			    text: Orders must be payable online.
			  }
			  element checkout_service {
			    type: service
			  }
			  checkout_req - contains -> checkout_service
			  checkout_service - satisfies -> checkout_req
			"""));

		foreach (var part in new[]
		{
			"aria-roledescription=\"requirement\"",
			"id=\"mermaid_requirement-requirement_containsStart\"",
			"id=\"mermaid_requirement-requirement_arrowEnd\"",
			"marker-start=\"url(#mermaid_requirement-requirement_containsStart)\"",
			"marker-end=\"url(#mermaid_requirement-requirement_arrowEnd)\"",
			"id=\"mermaid-checkout_req\"",
			"class=\"basic label-container outer-path\"",
			"class=\"divider\"",
			"stroke-width=\"1.3\"",
			"edge-pattern-dashed relationshipLine",
			"class=\"background\"",
			"fill=\"rgba(232,232,232, 0.8)\"",
			"text-anchor=\"start\"",
			"class=\"requirementDiagramTitleText\"",
		})
		{
			Verify(() => svg.Contains(part, StringComparison.Ordinal), $"the SVG has no {part}");
		}

		foreach (var text in new[] { "&lt;&lt;Requirement&gt;&gt;", "&lt;&lt;Element&gt;&gt;", "checkout_req", "ID: 1", "Type: service", "&lt;&lt;contains&gt;&gt;", "&lt;&lt;satisfies&gt;&gt;", "Checkout" })
		{
			Verify(() => svg.Contains(">" + text + "<", StringComparison.Ordinal), $"the SVG has no text {text}");
		}
	}

	private static FlowchartNodeDrawing Node(RequirementLayout layout, string id) => layout.Root.Nodes.Single(node => node.Id == id);

	private static int Dividers(FlowchartNodeDrawing node) => node.Geometry!.Elements.Count(element => element.Class == "divider");

	private static RequirementLayout Layout(string source)
	{
		var document = (RequirementDocument)Mermaid.Default.Parse(source);
		var config = MermaidConfig.Resolve(null, document, document.DiagramType);
		var context = new DiagramRenderContext(config, "mermaid", TextMeasurer.Default, NoIcons.Instance, [], ResourceLimits.Unlimited);
		return new RequirementModule().Layout(document, context);
	}
}
