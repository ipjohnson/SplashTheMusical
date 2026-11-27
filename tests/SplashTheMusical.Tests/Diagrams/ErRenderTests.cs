using SplashTheMusical.Config;
using SplashTheMusical.Diagrams;
using SplashTheMusical.Diagrams.Er;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Icons;
using SplashTheMusical.Rendering.Shapes;
using SplashTheMusical.Tests.Rendering;
using SplashTheMusical.Text;

namespace SplashTheMusical.Tests.Diagrams;

/// <summary>
/// The ER layout and renderer: the graphs <see cref="ErLayoutStep"/> hands the dagre port, the sizes and rows of the
/// <c>erBox</c> shape, the boxes of the drawing and its SVG with the crow's foot markers.
/// </summary>
public class ErRenderTests
{
	// corpus/er/docs-entityRelationshipDiagram-012.mmd: keys, comments and an entity without attributes.
	private const string Drivers = """
		erDiagram
		    CAR ||--o{ NAMED-DRIVER : allows
		    CAR {
		        string registrationNumber PK
		        string make
		        string model
		        string[] parts
		    }
		    PERSON ||--o{ NAMED-DRIVER : is
		    PERSON {
		        string driversLicense PK "The license #"
		        string(99) firstName "Only 99 characters are allowed"
		        string lastName
		        string phone UK
		        int age
		    }
		    NAMED-DRIVER {
		        string carRegistrationNumber PK, FK
		        string driverLicence PK, FK
		    }
		    MANUFACTURER only one to zero or more CAR : makes
		""";

	[Test]
	public void TheEntityBoxIsAsLargeAsMermaidJsMakesIt()
	{
		// The node sizes of corpus/er/e2e-er-diagram-visual-check-entities-with-keys-and-comments-without-htmllabels-default.dagre.txt.
		var inputs = Layout("""
			---
			config:
			  htmlLabels: false
			---
			erDiagram
			  AUTHOR_WITH_LONG_ENTITY_NAME {
			    string name PK "comment"
			  }
			  AUTHOR_WITH_LONG_ENTITY_NAME }|..|{ BOOK : writes
			  BOOK {
			      string description
			      float price "price comment"
			      string title PK "title comment"
			      string author FK
			    }
			""").DagreInputs;
		Verify(() => inputs.Count == 1);
		var nodes = inputs[0].Nodes.ToDictionary(node => node.Id);
		var book = nodes["entity-BOOK-1"];
		var author = nodes["entity-AUTHOR_WITH_LONG_ENTITY_NAME-0"];

		Verify(() => Math.Abs(book.Width - 312.453125) <= 0.01);
		Verify(() => Math.Abs(book.Height - 173.75) <= 0.01);

		// The underscores between letters are text, so they count in the name's width.
		Verify(() => Math.Abs(author.Width - 305.9375) <= 0.01);
		Verify(() => Math.Abs(author.Height - 69.5) <= 0.01);

		SequenceAssert.Equal(inputs[0].Edges.Select(edge => (edge.V, edge.W)), ("entity-AUTHOR_WITH_LONG_ENTITY_NAME-0", "entity-BOOK-1"));
	}

	[Test]
	public void AnUnsetHtmlLabelsScalesThePaddingOfHtmlLabels()
	{
		// corpus/er/docs-entityRelationshipDiagram-003: the root htmlLabels is unset, so erBox multiplies er.diagramPadding
		// and er.entityPadding by 1.25 while its labels are still HTML, as the case's .dagre.txt records.
		var inputs = Layout("""
			erDiagram
			  CUSTOMER {
			    string name
			    string email
			  }
			""").DagreInputs;
		var customer = inputs[0].Nodes.Single();

		Verify(() => Math.Abs(customer.Width - 129.859375) <= 0.01);
		Verify(() => Math.Abs(customer.Height - 119.25) <= 0.01);
	}

	[Test]
	public void AnEntityWithoutAttributesTakesTheMinimumWidthByTheConfigFont()
	{
		// The node sizes of corpus/er/docs-entityRelationshipDiagram-001.dagre.txt and docs-entityRelationshipDiagram-007.dagre.txt.
		// calculateTextWidth measures PERSON at the root fontSize of 16, which reaches er.minEntityWidth, so the box fits
		// the 14px label. DELIVERY-ADDRESS wraps at flowchart.wrappingWidth, 120.
		var inputs = Layout("""
			erDiagram
			  ORDER ||--o{ PERSON : has
			  ORDER ||--o{ DELIVERY-ADDRESS : uses
			""").DagreInputs;
		var nodes = inputs[0].Nodes.ToDictionary(node => node.Id);

		Verify(() => Math.Abs(nodes["entity-ORDER-0"].Width - 100) <= 0.01);
		Verify(() => Math.Abs(nodes["entity-PERSON-1"].Width - 99.125) <= 0.01);
		Verify(() => Math.Abs(nodes["entity-DELIVERY-ADDRESS-2"].Width - 160) <= 0.01);
		Verify(() => Math.Abs(nodes["entity-DELIVERY-ADDRESS-2"].Height - 102) <= 0.01);
	}

	[Test]
	public void EdgeLabelsAreMeasuredAtTheStylesheetsFontSize()
	{
		// corpus/er/docs-entityRelationshipDiagram-004: the default theme's font is 16px, and .edgeLabel .label sets 14px.
		var inputs = Layout("""
			---
			config:
			  theme: default
			  look: classic
			  layout: dagre
			---
			erDiagram
			  CUSTOMER ||--o{ ORDER : places
			""").DagreInputs;
		var edge = inputs[0].Edges.Single();

		Verify(() => Math.Abs(edge.Width - 40.46875) <= 0.01);
		Verify(() => Math.Abs(edge.Height - 21) <= 0.01);
	}

	[Test]
	public void EachRelationshipIsAnEdgeBetweenTheEntities()
	{
		SequenceAssert.Equal(
			Layout(Drivers).DagreInputs.Single().Edges.Select(edge => (edge.V, edge.W)),
			("entity-CAR-0", "entity-NAMED-DRIVER-1"),
			("entity-PERSON-2", "entity-NAMED-DRIVER-1"),
			("entity-MANUFACTURER-3", "entity-CAR-0"));
	}

	[Test]
	public void AnEntityWithoutAttributesIsAtLeastMinEntityWidthWide()
	{
		var nodes = Layout("erDiagram\n  A\n").DagreInputs.Single().Nodes;

		Verify(() => nodes.Count == 1);
		Verify(() => nodes[0].Width == 100);
		Verify(() => nodes[0].Height == 81);
	}

	[Test]
	public void EachAttributeIsARowAndTheRowsAlternate()
	{
		var car = Node(Layout(Drivers), "entity-CAR-0");
		var elements = car.Geometry!.Elements;

		Verify(() => elements[0].Class == "outer-path");
		SequenceAssert.Equal(
			elements.Where(element => element.Class?.StartsWith("row-rect", StringComparison.Ordinal) == true).Select(element => element.Class),
			"row-rect-odd",
			"row-rect-even",
			"row-rect-odd",
			"row-rect-even");

		// The name, then a type, a name, keys and a comment for each attribute.
		Verify(() => car.Labels!.Count == 1 + (4 * 4));
	}

	[Test]
	public void TheKeysAndCommentColumnsAreDividedOnlyWhenAnAttributeHasThem()
	{
		var layout = Layout(Drivers);

		// Each box has the name's line twice and the type's column. CAR adds keys, PERSON keys and comments.
		Verify(() => Dividers(Node(layout, "entity-CAR-0")) == 4);
		Verify(() => Dividers(Node(layout, "entity-PERSON-2")) == 5);
		Verify(() => Dividers(Node(Layout("erDiagram\n  A {\n    int id\n  }\n"), "entity-A-0")) == 3);
	}

	[Test]
	public void ASelfRelationshipIsLaidOutAsALoopAndDrawnAsOnePath()
	{
		var layout = Layout("erDiagram\n  NODE ||--o{ NODE : parent\n");
		var graph = layout.DagreInputs.Single();

		// The entity and the two points of its loop, joined by three edges that the renderer merges.
		Verify(() => graph.Nodes.Count == 3);
		Verify(() => graph.Edges.Count == 3);
		Verify(() => layout.Root.Edges.Count == 1);
	}

	[Test]
	public void TheEntitiesAndRelationshipsHaveBoxes()
	{
		var boxes = FlowchartLayout.BoxesOf(Layout(Drivers).Root).Select(box => box.Kind + " " + box.Name).ToList();

		foreach (var name in new[] { "node entity-CAR-0", "node entity-PERSON-2", "node entity-NAMED-DRIVER-1", "node entity-MANUFACTURER-3" })
		{
			Verify(() => boxes.Contains(name), $"{name} has no box in {string.Join(", ", boxes)}");
		}

		Verify(() => boxes.Count(box => box.StartsWith("edge ", StringComparison.Ordinal)) == 3);
	}

	[Test]
	public void TheSvgDrawsTheRowsAndTheCrowsFootMarkersAndPassesTheSanitizer()
	{
		var svg = SvgDocuments.Checked(Mermaid.Default.RenderSvg("""
			---
			title: Fleet
			---
			erDiagram
			  direction LR
			  CAR ||--o{ DRIVER : allows
			  CAR {
			    string reg PK "plate"
			    string make
			  }
			  DRIVER }|..|{ CAR : rents
			  CAR |o--|| CAR : tows
			"""));

		foreach (var part in new[]
		{
			"aria-roledescription=\"er\"",
			"class=\"outer-path\"",
			"class=\"row-rect-odd\"",
			"class=\"row-rect-even\"",
			"class=\"divider\"",
			// DRIVER has no attributes.
			"class=\"basic label-container\"",
			"marker-start=\"url(#mermaid_er-onlyOneStart)\" marker-end=\"url(#mermaid_er-zeroOrMoreEnd)\"",
			"marker-start=\"url(#mermaid_er-oneOrMoreStart)\" marker-end=\"url(#mermaid_er-oneOrMoreEnd)\"",
			"marker-start=\"url(#mermaid_er-zeroOrOneStart)\" marker-end=\"url(#mermaid_er-onlyOneEnd)\"",
			// A non-identifying relationship is dashed.
			"edge-pattern-dashed relationshipLine",
			"edge-pattern-solid relationshipLine",
			"class=\"erDiagramTitleText\"",
		})
		{
			Verify(() => svg.Contains(part, StringComparison.Ordinal), $"the SVG has no {part}");
		}

		foreach (var text in new[] { "CAR", "DRIVER", "string", "reg", "PK", "plate", "make", "allows", "rents", "tows", "Fleet" })
		{
			Verify(() => svg.Contains(">" + text + "<", StringComparison.Ordinal), $"the SVG has no text {text}");
		}
	}

	private static FlowchartNodeDrawing Node(ErLayout layout, string id) => layout.Root.Nodes.Single(node => node.Id == id);

	private static int Dividers(FlowchartNodeDrawing node) => node.Geometry!.Elements.Count(element => element.Kind == ShapeElementKind.Line && element.Class == "divider");

	private static ErLayout Layout(string source)
	{
		var document = (ErDocument)Mermaid.Default.Parse(source);
		var config = MermaidConfig.Resolve(null, document, document.DiagramType);
		var context = new DiagramRenderContext(config, "mermaid", TextMeasurer.Default, NoIcons.Instance, [], ResourceLimits.Unlimited);
		return new ErModule().Layout(document, context);
	}
}
