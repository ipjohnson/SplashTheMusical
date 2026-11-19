using AwesomeAssertions;
using SplashTheMusical.Config;
using SplashTheMusical.Diagrams;
using SplashTheMusical.Diagrams.Class;
using SplashTheMusical.Icons;
using SplashTheMusical.Tests.Rendering;
using SplashTheMusical.Text;

namespace SplashTheMusical.Tests.Diagrams;

/// <summary>
/// The class layout and renderer: the graphs <see cref="ClassLayoutStep"/> hands the dagre port, the sizes of the
/// <c>classBox</c> shape, the boxes of the drawing and its SVG.
/// </summary>
public class ClassRenderTests
{
	private const string Orders = """
		classDiagram
		  class Customer {
		    +String name
		    +String email
		  }
		  class Order {
		    +String id
		    +Date placedAt
		    +total() Money
		  }
		  class LineItem {
		    +int quantity
		  }
		  class Payment {
		    <<interface>>
		    +authorise() bool
		  }
		  Customer "1" --> "*" Order : places
		  Order "1" *-- "*" LineItem : contains
		  Order --> Payment : settled by
		""";

	private const string Namespaced = """
		classDiagram
		  namespace Shop {
		    class Cart
		    class Item
		  }
		  Cart --> Item
		  User --> Cart
		""";

	[Test]
	public void TheClassBoxIsAsLargeAsMermaidJsMakesIt()
	{
		// The node sizes of corpus/class/docs-classDiagram-002.dagre.txt: the name, an annotation, attributes and methods.
		var nodes = Layout(Orders).DagreInputs.Should().ContainSingle().Subject.Nodes.ToDictionary(node => node.Id);

		foreach (var (id, width, height) in new[] { ("Customer", 140.3515625, 135.0), ("Order", 140.71875, 156.0), ("LineItem", 128.6796875, 114.0), ("Payment", 171.875, 141.0) })
		{
			nodes[id].Width.Should().BeApproximately(width, 0.01, id);
			nodes[id].Height.Should().BeApproximately(height, 0.01, id);
		}
	}

	[Test]
	public void ANamespaceIsACompoundNodeThatHoldsItsClasses()
	{
		// The relation from User crosses Shop's border, so dagre lays out the whole diagram in one graph.
		var graph = Layout(Namespaced).DagreInputs.Should().ContainSingle().Subject;
		var parents = graph.Nodes.ToDictionary(node => node.Id, node => node.Parent);

		parents["Cart"].Should().Be("Shop");
		parents["Item"].Should().Be("Shop");
		parents["Shop"].Should().BeNull();
		parents["User"].Should().BeNull();
		graph.Edges.Select(edge => (edge.V, edge.W)).Should().Contain(("Cart", "Item")).And.Contain(("User", "Cart"));
	}

	[Test]
	public void ANamespaceWithNoCrossingRelationIsLaidOutOnItsOwnFirst()
	{
		var inputs = Layout("classDiagram\n  namespace Shop {\n    class Cart\n  }\n").DagreInputs;

		inputs.Should().HaveCount(2);
		inputs[0].Nodes.Select(node => (node.Id, node.Parent)).Should().Equal(("Cart", "Shop"), ("Shop", null));
		inputs[1].Nodes.Should().ContainSingle().Which.Id.Should().Be("Shop");
	}

	[Test]
	public void TheNamespaceFrameHoldsItsClassesAndTheOthersAreOutside()
	{
		var boxes = Layout(Namespaced).Boxes().ToDictionary(box => box.Kind + " " + box.Name);
		var group = boxes["group Shop"];

		foreach (var name in new[] { "node Cart", "node Item" })
		{
			var node = boxes[name];
			node.X.Should().BeGreaterThanOrEqualTo(group.X);
			node.Y.Should().BeGreaterThanOrEqualTo(group.Y);
			(node.X + node.Width).Should().BeLessThanOrEqualTo(group.X + group.Width);
			(node.Y + node.Height).Should().BeLessThanOrEqualTo(group.Y + group.Height);
		}

		var user = boxes["node User"];
		(user.Y + user.Height <= group.Y || user.Y >= group.Y + group.Height).Should().BeTrue();
	}

	[Test]
	public void ANoteIsANodeWithADottedEdgeToItsClass()
	{
		var layout = Layout("classDiagram\n  class Duck\n  note for Duck \"can fly\"\n");
		var graph = layout.DagreInputs.Should().ContainSingle().Subject;

		graph.Nodes.Select(node => node.Id).Should().Equal("Duck", "note0");
		graph.Edges.Should().ContainSingle().Which.Name.Should().Be("edgeNote0");
	}

	[Test]
	public void AnSvgLabelClassBoxStartsItsMembersAtTheCentre()
	{
		// corpus/class/e2e-class-diagram-nightly-check-dagre-dagre-11-1-...-without-htmllabels.dagre.txt: addText's text
		// starts at x 0, so the box is half the name's width and the widest member's wide, with the padding.
		var node = Single(Layout("""
			---
			config:
			  htmlLabels: false
			---
			classDiagram
			  class Class10~T~ {
			  int[] id
			  test(int[] ids) bool
			  testArray() bool[]
			  }
			""").DagreInputs).Nodes[0];

		Verify(() => Math.Abs(node.Width - 177.3125) < 0.01);
		Verify(() => node.Height == 139);
	}

	[Test]
	public void ANoteDoesNotWrapAndItsEdgeKeepsDagresLabelPosition()
	{
		// corpus/class/e2e-class-diagram-nightly-check-dagre-dagre-18b-should-render-a-simple-class-diagram-with-notes:
		// the note's white-space: nowrap keeps its second line whole, past the wrapping width of 120, and the note's
		// edge has no labelpos, so dagre's r widens it by the label offset.
		var graph = Single(Layout("classDiagram\n  class Class10\n  note for Class10 \"Cool class\n  I said it's very cool class!\"\n").DagreInputs);
		var note = graph.Nodes.First(node => node.Id == "note0");
		var edge = Single(graph.Edges);

		Verify(() => Math.Abs(note.Width - 165.625) < 0.01);
		Verify(() => note.Height == 54);
		Verify(() => edge.LabelPos == "r");
	}

	[Test]
	public void TheSvgDrawsTheClassBoxesAndRelationsAndPassesTheSanitizer()
	{
		var svg = SvgDocuments.Checked(Mermaid.Default.RenderSvg("""
			---
			title: Shop
			---
			classDiagram
			  namespace Sales {
			    class Order {
			      <<entity>>
			      +String id
			      +total()* Money
			    }
			  }
			  class Customer
			  Customer "1" o-- "*" Order : places
			  Order ..|> Payable
			  Order --() Printable
			  note for Customer "a note"
			"""));

		svg.Should().Contain("aria-roledescription=\"classDiagram\"");
		svg.Should().Contain("class=\"basic label-container outer-path\"");
		svg.Should().Contain("class=\"divider");
		svg.Should().Contain("id=\"mermaid-classId-Order-0\"");
		svg.Should().Contain("marker-start=\"url(#mermaid_classDiagram-aggregationStart");
		svg.Should().Contain("marker-end=\"url(#mermaid_classDiagram-extensionEnd");
		svg.Should().Contain("marker-end=\"url(#mermaid_classDiagram-lollipopEnd");
		svg.Should().Contain("edge-pattern-dashed", "a realization is a dashed relation");
		svg.Should().Contain("edge-pattern-dotted", "a note's edge is dotted");
		svg.Should().Contain("class=\"classDiagramTitleText\"");
		foreach (var text in new[] { "«entity»", "Order", "+String id", "+total() : Money", "Customer", "places", "a note", "Shop", "Sales" })
		{
			svg.Should().Contain(">" + text + "<");
		}
	}

	private static ClassLayout Layout(string source)
	{
		var document = (ClassDocument)Mermaid.Default.Parse(source);
		var config = MermaidConfig.Resolve(null, document, document.DiagramType);
		var context = new DiagramRenderContext(config, "mermaid", TextMeasurer.Default, NoIcons.Instance, [], ResourceLimits.Unlimited);
		return new ClassModule().Layout(document, context);
	}
}
