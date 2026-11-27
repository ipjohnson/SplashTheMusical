using SplashTheMusical.Config;
using SplashTheMusical.Diagrams;
using SplashTheMusical.Diagrams.C4;
using SplashTheMusical.Icons;
using SplashTheMusical.Rendering.Shapes;
using SplashTheMusical.Tests.Rendering;
using SplashTheMusical.Text;

namespace SplashTheMusical.Tests.Diagrams;

/// <summary>
/// The C4 layout and renderer: the places <see cref="C4LayoutStep"/> gives shapes and boundaries, the shape each element
/// kind draws, the relations' lines, labels and markers, the style statements, and the SVG.
/// </summary>
public class C4RenderTests
{
	/// <summary>
	/// How far a place may be from the oracle's box. Chromium draws the oracle's SVG scaled to the page and rounds a text's
	/// box there, so a section of a label is up to 0.2 taller than STM's, and a boundary adds those of what it holds.
	/// </summary>
	private const double Tolerance = 1;

	[Test]
	public void ShapesFillRowsUpToTheScreenWidth()
	{
		// corpus/c4/e2e-c4-visual-check-update-layout-config-4-shapes-per-row: four in a row would pass the 800 of the screen.
		var layout = Layout("""
			C4Context
			title UpdateLayoutConfig ($c4ShapeInRow=4)
			System(a, "A")
			System(b, "B")
			System(c, "C")
			System(d, "D")
			UpdateLayoutConfig($c4ShapeInRow="4", $c4BoundaryInRow="1")
			""");
		Near(Shape(layout, "a"), 150, 166, 216, 71.179);
		Near(Shape(layout, "b"), 466, 166, 216, 71.179);
		Near(Shape(layout, "c"), 150, 337.179, 216, 71.179);
		Near(Shape(layout, "d"), 466, 337.179, 216, 71.179);
		NearViewBox(layout, 0, -70, 832, 578.358);
	}

	[Test]
	public void UpdateLayoutConfigLimitsTheShapesInARow()
	{
		var layout = Layout("""
			C4Context
			System(a, "A")
			System(b, "B")
			UpdateLayoutConfig($c4ShapeInRow="1")
			""");
		Verify(() => Shape(layout, "a").X == Shape(layout, "b").X);
		Verify(() => Shape(layout, "b").Y > Shape(layout, "a").Y);
	}

	[Test]
	public void NestedBoundariesHoldWhatTheyContain()
	{
		// corpus/c4/e2e-c4-nightly-check-boundaries-nested-boundaries.
		var layout = Layout("""
			C4Context
			title Nested boundaries
			Enterprise_Boundary(e, "Enterprise") {
			  System_Boundary(s, "System") {
			    System(sys, "System", "deeply nested")
			  }
			}
			""");
		Near(Shape(layout, "sys"), 250, 278, 216, 86.561);
		Near(Boundary(layout, "s"), 200, 178, 316, 236.561);
		Near(Boundary(layout, "e"), 150, 122, 416, 342.561);
		NearViewBox(layout, 0, -70, 716, 634.561);

		// The inner boundary is drawn before the outer one, after the shapes it holds.
		var order = layout.Drawings.Select(d => d is C4ShapeDrawing shape ? shape.Alias : ((C4BoundaryDrawing)d).Alias).ToList();
		SequenceAssert.Equal(order, "sys", "s", "e");

		var labels = Boundary(layout, "e").Texts.Select(t => t.Text).ToList();
		SequenceAssert.Equal(labels, "Enterprise", "[ENTERPRISE]");
		Verify(() => Boundary(layout, "e").Dashed);
		Verify(() => Boundary(layout, "e").Texts[0].Font.Weight == 700 && Boundary(layout, "e").Texts[0].Font.Size == 16);
	}

	[Test]
	public void DeploymentNodesAreSolidFramesInRows()
	{
		// corpus/c4/e2e-c4-nightly-check-diagram-types-deployment-diagram.
		var layout = Layout("""
			C4Deployment
			title Deployment diagram
			Deployment_Node(dc, "Data Center", "us-east-1") {
			  Deployment_Node(srv, "App Server", "Ubuntu 22.04") {
			    Container(api, "API", "Java, Spring")
			  }
			  Deployment_Node(dbn, "DB Server", "Ubuntu 22.04") {
			    ContainerDb(db, "Database", "PostgreSQL")
			  }
			}
			Rel(api, db, "reads/writes", "JDBC")
			""");
		Near(Boundary(layout, "dc"), 150, 122, 782, 354.686);
		Near(Boundary(layout, "srv"), 200, 178, 316, 221.179);
		Near(Boundary(layout, "dbn"), 566, 178, 316, 248.686);
		Near(Shape(layout, "api"), 250, 278, 216, 71.179);
		Near(Shape(layout, "db"), 616, 278, 216, 98.686);
		Verify(() => !Boundary(layout, "dc").Dashed);
		NearViewBox(layout, 0, -70, 1082, 646.686);
	}

	[Test]
	public void EachElementKindDrawsItsShape()
	{
		var layout = Layout("""
			C4Container
			Person(p, "P")
			Person_Ext(pe, "PE")
			SystemDb(sdb, "Db")
			ContainerQueue_Ext(cq, "Queue", "AMQP")
			Component(c, "C")
			Container(t, "Tagged", $tags="Component")
			System(s, "Shaped", $shape="database")
			""");
		ShapeElementKind Kind(string alias) => Shape(layout, alias).Geometry.Elements[0].Kind;

		// The person is a group of the body and the head.
		Verify(() => Kind("p") == ShapeElementKind.Group && Shape(layout, "p").Geometry.Elements[0].Children!.Any(e => e.Kind == ShapeElementKind.Circle));
		Verify(() => Shape(layout, "pe").Classes == "node c4-shape c4-external_person c4-external");
		Verify(() => Kind("sdb") == ShapeElementKind.Path && Shape(layout, "sdb").Geometry.Intersect.GetType().Name.Contains("Cylinder", StringComparison.Ordinal));
		Verify(() => Kind("cq") == ShapeElementKind.Path && Shape(layout, "cq").Geometry.Intersect.GetType().Name.Contains("Tilted", StringComparison.Ordinal));

		// The rounded rect takes the inline corner radius of 12, and a tag or $shape keyword picks another shape.
		Verify(() => Kind("c") == ShapeElementKind.Rect && Shape(layout, "c").Geometry.Elements[0].Rx == 12);
		Verify(() => Kind("t") == ShapeElementKind.Polygon);
		Verify(() => Kind("s") == ShapeElementKind.Path);

		// The stereotype names the kind and the technology, smaller than the bold name.
		var sections = Shape(layout, "cq").Sections;
		SequenceAssert.Equal(sections.Select(s => s.Class), "c4-name", "c4-type");
		Verify(() => Text(sections[1]) == "[Container: AMQP]");
		Verify(() => sections[0].Font.Weight == 700 && sections[0].Font.Size == 14 && sections[1].Font.Size == 10.5);
		Verify(() => Text(Shape(layout, "sdb").Sections[1]) == "[Software System]");
		Verify(() => Text(Shape(layout, "p").Sections[1]) == "[Person]");
	}

	[Test]
	public void ANameWrapsAsNormalTextAndIsMeasuredBold()
	{
		// corpus/c4/e2e-c4-nightly-check-elements-system-variants: the queue's name fits 166 as normal text, not bold.
		var layout = Layout("""
			C4Context
			SystemQueue_Ext(sqe, "SystemQueue_Ext", "An external message queue")
			""");
		var shape = Shape(layout, "sqe");
		Verify(() => shape.Sections[0].Box.Lines.Count == 1);
		Verify(() => Math.Abs(shape.Width - 239.365) <= Tolerance && Math.Abs(shape.Height - 56.561) <= Tolerance, $"{shape.Width} by {shape.Height}");
	}

	[Test]
	public void ANamedArgumentInTheLabelsPlaceDrawsNoName()
	{
		// corpus/c4/parser-c4person-004: the label is { sprite: "users" }, an object.
		var layout = Layout("C4Context\nPerson(customerA, $sprite=\"users\")\n");
		var shape = Shape(layout, "customerA");
		SequenceAssert.Equal(shape.Sections.Select(Text), "[Person]");
		Verify(() => Math.Abs(shape.Height - 138.125) <= Tolerance, $"{shape.Height}");
	}

	[Test]
	public void RelationsJoinTheOutlinesAndCarryTheirLabels()
	{
		var layout = Layout("""
			C4Container
			Container(a, "A", "Tech")
			Container(b, "B", "Tech")
			Container(c, "C", "Tech")
			Rel_Back(a, b, "reads from", "JDBC")
			BiRel(b, c, "syncs")
			Rel(a, c, "calls")
			UpdateRelStyle(b, c, $textColor="red", $lineColor="blue", $offsetX="5", $offsetY="-10")
			""");
		var back = layout.Rels[0];
		var both = layout.Rels[1];
		var plain = layout.Rels[2];

		// a and b are side by side: the first relation is a line between their facing sides.
		Verify(() => back.IsLine && !both.IsLine && !plain.IsLine);
		Verify(() => Math.Abs(back.Start.X - 366) <= 0.01 && Math.Abs(back.End.X - 466) <= 0.01);
		Verify(() => back.ArrowStart && !back.ArrowEnd);
		Verify(() => both.ArrowStart && both.ArrowEnd);
		Verify(() => !plain.ArrowStart && plain.ArrowEnd);

		// The label is half its width right of the middle of the line, and the technology is italic below it.
		SequenceAssert.Equal(back.Texts.Select(t => t.Text), "reads from", "[JDBC]");
		var labelWidth = MermaidText.CalculateTextWidth("reads from", back.Texts[0].Font);
		Verify(() => Math.Abs(back.Texts[0].X - (416 + (labelWidth / 2))) <= 0.01, $"{back.Texts[0].X}");
		Verify(() => back.Texts[1].Italic && back.Texts[1].Y == back.Texts[0].Y + 17);

		// UpdateRelStyle colours the line and the label and moves the label.
		Verify(() => both.Stroke == "blue" && both.Texts[0].Fill == "red");
		Verify(() => Math.Abs(both.Texts[0].Y - (((both.Start.Y + both.End.Y) / 2) - 10)) <= 0.01);
		Verify(() => back.Stroke == "#444444" && back.Texts[0].Fill == "#444444");
		Verify(() => both.PathData.StartsWith('M') && both.PathData.Contains(" Q", StringComparison.Ordinal) && both.PathData.EndsWith(' '));
	}

	[Test]
	public void DynamicDiagramsNumberTheirRelations()
	{
		var layout = Layout("""
			C4Dynamic
			Container(a, "A")
			Container(b, "B")
			Rel(a, b, "first")
			Rel(b, a, "second")
			""");
		SequenceAssert.Equal(layout.Rels.Select(r => r.Texts[0].Text), "1: first", "2: second");
	}

	[Test]
	public void UpdateElementStylePaintsTheElement()
	{
		var layout = Layout("""
			C4Context
			Person(a, "A")
			System(b, "B")
			UpdateElementStyle(a, $fontColor="red", $bgColor="grey", $borderColor="red")
			""");
		var a = Shape(layout, "a");
		Verify(() => a.Fill == "grey" && a.Stroke == "red" && a.FontColor == "red");
		var b = Shape(layout, "b");
		Verify(() => b.Fill == "#1168BD" && b.Stroke == "#3C7FC0" && b.FontColor == "#FFFFFF");
		Verify(() => layout.ElementRules.Count == 2);
	}

	[Test]
	public void ARelationToAnUnknownOrUndrawnEndIsRefused()
	{
		var unknown = Throws<MermaidParseException>(() => Layout("C4Context\nPerson(a, \"A\")\nRel(a, b, \"x\")\n"));
		Verify(() => unknown.Diagnostics[0].Message == "C4 rel \"a\" -> \"b\" references an unknown shape or boundary");

		// The global boundary is never drawn, so it has no outline to meet.
		var global = Throws<MermaidParseException>(() => Layout("C4Context\nPerson(a, \"A\")\nRel(a, global, \"x\")\n"));
		Verify(() => global.Diagnostics[0].Message.StartsWith("C4 element \"global\" has no intersect function", StringComparison.Ordinal));
	}

	[Test]
	public void TheSvgHasMermaidJsIdsMarkersAndTexts()
	{
		var svg = SvgDocuments.Checked(Mermaid.Default.RenderSvg("""
			C4Context
			title Context
			Enterprise_Boundary(b0, "Bank") {
			  Person(customer, "Customer", "A customer", $sprite="person")
			  System(sys, "Banking", "Holds accounts", $link="https://example.com")
			}
			Rel(customer, sys, "Uses", "HTTPS")
			BiRel(sys, customer, "Notifies")
			"""));
		Verify(() => svg.Contains("id=\"mermaid-customer\"", StringComparison.Ordinal));
		Verify(() => svg.Contains("class=\"node c4-shape c4-person stm-style-0\"", StringComparison.Ordinal));
		Verify(() => svg.Contains("id=\"mermaid-arrowhead\"", StringComparison.Ordinal) && svg.Contains("id=\"mermaid-arrowend\"", StringComparison.Ordinal));
		Verify(() => svg.Contains("id=\"mermaid-crosshead\"", StringComparison.Ordinal) && svg.Contains("id=\"mermaid-filled-head\"", StringComparison.Ordinal));
		Verify(() => svg.Contains("marker-end=\"url(#mermaid-arrowhead)\"", StringComparison.Ordinal));
		Verify(() => svg.Contains("stroke-dasharray=\"7.0,7.0\"", StringComparison.Ordinal));
		Verify(() => svg.Contains(">[ENTERPRISE]</tspan>", StringComparison.Ordinal));
		Verify(() => svg.Contains(">[HTTPS]</tspan>", StringComparison.Ordinal));
		Verify(() => svg.Contains("class=\"text-inner-tspan\">Customer</tspan>", StringComparison.Ordinal));
		Verify(() => svg.Contains(">Context</text>", StringComparison.Ordinal));
		Verify(() => svg.Contains(".stm-style-0{color:rgb(255, 255, 255);}", StringComparison.Ordinal), $"{svg}");
		Verify(() => svg.Contains(".c4-shape.c4-person .label{font-family:\"Open Sans\",sans-serif;font-size:14px;font-weight:normal;}", StringComparison.Ordinal));

		// A sprite or a link draws nothing, and nothing is fetched.
		Verify(() => !svg.Contains("<image", StringComparison.Ordinal) && !svg.Contains("example.com", StringComparison.Ordinal));
		Verify(() => svg.Contains("<line ", StringComparison.Ordinal) && svg.Contains(" Q", StringComparison.Ordinal));
	}

	private static string Text(C4LabelSection section) => string.Concat(section.Box.Lines.SelectMany(l => l).Select(r => r.Text));

	private static C4ShapeDrawing Shape(C4Layout layout, string alias) => layout.Drawings.OfType<C4ShapeDrawing>().Single(s => s.Alias == alias);

	private static C4BoundaryDrawing Boundary(C4Layout layout, string alias) => layout.Drawings.OfType<C4BoundaryDrawing>().Single(b => b.Alias == alias);

	/// <summary>The oracle's element box is the shape's group, which its outline fills.</summary>
	private static void Near(C4ShapeDrawing shape, double x, double y, double width, double height)
	{
		var bounds = shape.Geometry.Bounds;
		var (left, top) = (shape.CenterX + bounds.MinX, shape.CenterY + bounds.MinY);
		Verify(() => Math.Abs(left - x) <= Tolerance && Math.Abs(top - y) <= Tolerance, $"{shape.Alias} at {left}, {top}");
		Verify(() => Math.Abs(bounds.Width - width) <= Tolerance && Math.Abs(bounds.Height - height) <= Tolerance, $"{shape.Alias} is {bounds.Width} by {bounds.Height}");
	}

	private static void Near(C4BoundaryDrawing boundary, double x, double y, double width, double height)
	{
		Verify(() => Math.Abs(boundary.X - x) <= Tolerance && Math.Abs(boundary.Y - y) <= Tolerance, $"{boundary.Alias} at {boundary.X}, {boundary.Y}");
		Verify(() => Math.Abs(boundary.Width - width) <= Tolerance && Math.Abs(boundary.Height - height) <= Tolerance, $"{boundary.Alias} is {boundary.Width} by {boundary.Height}");
	}

	private static void NearViewBox(C4Layout layout, double x, double y, double width, double height)
	{
		var viewBox = layout.ViewBox!.Value;
		Verify(() => viewBox.X == x && viewBox.Y == y, $"viewBox at {viewBox.X}, {viewBox.Y}");
		Verify(() => Math.Abs(viewBox.Width - width) <= Tolerance && Math.Abs(viewBox.Height - height) <= Tolerance, $"viewBox is {viewBox.Width} by {viewBox.Height}");
	}

	private static C4Layout Layout(string source)
	{
		var document = (C4Document)Mermaid.Default.Parse(source);
		var config = MermaidConfig.Resolve(null, document, document.DiagramType);
		var context = new DiagramRenderContext(config, "mermaid", TextMeasurer.Default, NoIcons.Instance, [], ResourceLimits.Unlimited);
		return new C4Module().Layout(document, context);
	}
}
