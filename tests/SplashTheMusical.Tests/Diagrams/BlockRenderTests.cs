using SplashTheMusical.Config;
using SplashTheMusical.Diagrams;
using SplashTheMusical.Diagrams.Block;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Icons;
using SplashTheMusical.Rendering.Shapes;
using SplashTheMusical.Tests.Rendering;
using SplashTheMusical.Text;

namespace SplashTheMusical.Tests.Diagrams;

/// <summary>
/// The block layout and renderer: the grid <see cref="BlockLayoutStep"/> ports from <c>layout.ts</c>, the
/// <c>composite</c> and <c>block_arrow</c> shapes of <see cref="BlockShapes"/>, the edges and their labels, and the SVG.
/// The expected boxes are the oracle's, from the <c>.boxes.txt</c> file of the corpus case each test names.
/// </summary>
public class BlockRenderTests
{
	[Test]
	public void NestedBlocksArrowsAndSpacesAreWhereMermaidJsPutsThem()
	{
		// corpus/block/docs-block-001: the composite ID spans the root's one column and shares its width among A, B
		// and C, the space takes a row, and B's wide label sets every block's width.
		var boxes = Boxes("""
			block
			columns 1
			  db(("DB"))
			  blockArrowId6<["&nbsp;&nbsp;&nbsp;"]>(down)
			  block:ID
			    A
			    B["A wide one in the middle"]
			    C
			  end
			  space
			  D
			  ID --> D
			  C --> D
			  style B fill:#969,stroke:#333,stroke-width:4px
			""", out var viewBox);
		Near(boxes, "node A", 8, -20, 205.453, 40);
		Near(boxes, "node B", 221.453, -20, 205.453, 40);
		Near(boxes, "node C", 434.906, -20, 205.453, 40);
		Near(boxes, "node D", 0, 100, 648.359, 56);
		Near(boxes, "node ID", 0, -28, 648.359, 56);
		Near(boxes, "node blockArrowId6", 293.508, -80, 61.344, 36);
		Near(boxes, "node db", 303.821, -148.358, 40.716, 40.716);
		Near(boxes, "edge C --> D", 374.303, 20, 129.978, 77.943);
		Near(boxes, "edge ID --> D", 324.18, 28, 0, 68);
		Verify(() => Math.Abs(viewBox.X + 5) <= 0.01 && Math.Abs(viewBox.Y + 153.358) <= 0.01);
		Verify(() => Math.Abs(viewBox.Width - 658.359) <= 0.01 && Math.Abs(viewBox.Height - 314.358) <= 0.01);
	}

	[Test]
	public void ASpanIsItsColumnsWideWithThePaddingBetweenThem()
	{
		// corpus/block/docs-block-004.
		var boxes = Boxes("""
			block
			  columns 3
			  a["A label"] b:2 c:2 d
			""", out _);
		Near(boxes, "node a", 0, -44, 80.047, 40);
		Near(boxes, "node b", 88.047, -44, 168.094, 40);
		Near(boxes, "node c", 0, 4, 168.094, 40);
		Near(boxes, "node d", 176.094, 4, 80.047, 40);
	}

	[Test]
	public void SpacesTakeTheirColumnsAndAnEdgeLabelSitsHalfWay()
	{
		// corpus/block/docs-block-022.
		var boxes = Boxes("""
			block
			  A space:2 B
			  A-- "X" -->B
			""", out var viewBox);
		Near(boxes, "node A", 0, -20, 42.672, 40);
		Near(boxes, "node B", 152.016, -20, 42.672, 40);
		Near(boxes, "edge A --> B", 42.672, 0, 105.344, 0);
		Near(boxes, "edge-label A --> B", 92.008, -12, 10.672, 24);
		Verify(() => Math.Abs(viewBox.Width - 204.688) <= 0.01 && Math.Abs(viewBox.Height - 50) <= 0.01);
	}

	[Test]
	public void ASpanPastTheEndOfARowFillsOnlyTheRestOfIt()
	{
		// c starts in the last column of the first row, so it takes that column only and d starts the second row.
		var nodes = Layout("""
			block
			  columns 3
			  a b c:2 d
			""").Root.Nodes.ToDictionary(node => node.Id);
		Verify(() => nodes["c"].Y == nodes["a"].Y);
		Verify(() => nodes["d"].Y > nodes["a"].Y && nodes["d"].X == nodes["a"].X);
	}

	[Test]
	public void ABlockWithoutColumnsPutsItsChildrenInOneRow()
	{
		var nodes = Layout("""
			block
			  block
			    a
			    b
			    c
			  end
			""").Root.Nodes;
		var children = nodes.Where(node => node.Id is "a" or "b" or "c").ToList();
		Verify(() => children.Count == 3 && children.All(node => node.Y == children[0].Y));
		Verify(() => children[0].X < children[1].X && children[1].X < children[2].X);
	}

	[Test]
	public void ColumnsZeroFailsAsMermaidJsDoes()
	{
		var thrown = Throws<MermaidParseException>(() => Layout("""
			block
			  columns 0
			  a b
			"""));
		Verify(() => thrown.Diagnostics[0].Message == "Columns must be an integer !== 0.");
	}

	[Test]
	public void ArrowDirectionsPickTheOutline()
	{
		static string Key(params string[] directions) =>
			string.Join(' ', BlockShapes.ArrowPoints(directions, 10, 20, 8, null).Select(point => $"{point.X},{point.Y}"));

		// x is right and left, y is up and down, and the order the directions come in does not matter.
		Verify(() => Key("x") == Key("left", "right"));
		Verify(() => Key("y") == Key("down", "up"));
		Verify(() => Key("x", "y") == Key("right", "left", "up", "down"));
		Verify(() => BlockShapes.ArrowPoints(["right", "left", "up", "down"], 10, 20, 8, null).Length == 16);

		// The lexer leaves the space of "( right)" in the direction, which getArrowPoints does not know.
		Verify(() => Key(" right") == "0,0");
		Verify(() => Key() == "0,0");

		// The natural width is the label and two arrowheads of half the height, which is the label and the padding.
		var down = BlockShapes.ArrowPoints(["down"], 10, 20, 8, null);
		Verify(() => down.Max(point => point.X) == 10 + 36 + 8 && down.Min(point => point.Y) == -36 + 4);
	}

	[Test]
	public void ASpanningBlockArrowTakesTheWidthOfItsColumns()
	{
		var nodes = Layout("""
			block
			  columns 3
			  a["A wide label here"] b c
			  arrow<["x"]>(right):3
			""").Root.Nodes.ToDictionary(node => node.Id);
		// A right arrow's outline starts half its height in from the left of the width it is given.
		var polygon = nodes["arrow"].Geometry!.Elements.Single();
		var row = nodes["c"].X + (nodes["c"].Geometry!.Width / 2) - (nodes["a"].X - (nodes["a"].Geometry!.Width / 2));
		var right = polygon.Points!.Max(point => point.X);
		Verify(() => polygon.Kind == ShapeElementKind.Polygon);
		Verify(() => Math.Abs(right - row) <= 0.01, $"the arrow ends at {right} and the row is {row} wide");
		Verify(() => Math.Abs(polygon.Translate!.Value.X + (row / 2)) <= 0.01);
	}

	[Test]
	public void ACompositeIsDrawnAtTheSizeTheGridGivesIt()
	{
		var layout = Layout("""
			block
			  block:group
			    a b
			  end
			""");
		var group = layout.Root.Nodes.Single(node => node.Id == "group");
		var rect = group.Geometry!.Elements.Single();
		Verify(() => rect.Class == "basic cluster composite label-container");
		Verify(() => group.Classes == "node default flowchart-label default flowchart-label");
		var a = layout.Root.Nodes.Single(node => node.Id == "a");
		Verify(() => Math.Abs(rect.Width - ((2 * (a.Geometry!.Width + 8)) + 8)) <= 0.01);
	}

	[Test]
	public void TheHeightGetsTheMagicFactorWithoutUseMaxWidth()
	{
		// docs-block-022 is 194.688 by 40, so the factor is round(0.125 * 4.87), 1.
		var layout = Layout("""
			---
			config:
			  block:
			    useMaxWidth: false
			---
			block
			  A space:2 B
			  A-- "X" -->B
			""");
		Verify(() => Math.Abs(layout.Height.GetValueOrDefault() - 51) <= 0.01);
		Verify(() => Layout("block\n  A\n").Height is null);
	}

	[Test]
	public void AColourThemeStampsTheCompositesSlot()
	{
		var svg = SvgDocuments.Checked(Mermaid.Default.RenderSvg("""
			---
			config:
			  theme: redux-color
			---
			block
			  block:one
			    a
			  end
			  block:two
			    b
			  end
			"""));
		Verify(() => svg.Contains("data-id=\"one\" data-look=\"classic\" data-color-id=\"color-0\"", StringComparison.Ordinal));
		Verify(() => svg.Contains("data-id=\"two\" data-look=\"classic\" data-color-id=\"color-1\"", StringComparison.Ordinal));
		Verify(() => !svg.Contains("data-id=\"a\" data-look=\"classic\" data-color-id", StringComparison.Ordinal));
		Verify(() => svg.Contains("[data-look=\"classic\"][data-color-id=\"color-0\"].node rect.composite{", StringComparison.Ordinal));
	}

	[Test]
	public void TheSvgDrawsTheEdgesOverTheBlocksAndPassesTheSanitizer()
	{
		var svg = SvgDocuments.Checked(Mermaid.Default.RenderSvg("""
			block
			  columns 2
			  a(("A")) b{"B"}
			  c[("C")] d>"D"]
			  a -- "to b" --> b
			  c -.- d
			  classDef hot fill:#f96
			  class c hot
			  style d stroke:#f00
			"""));

		foreach (var part in new[]
		{
			"aria-roledescription=\"block\"",
			"<g class=\"block\">",
			"id=\"mermaid_block-pointEnd\"",
			"id=\"mermaid-mermaid-1-a-b\"",
			"marker-end=\"url(#mermaid_block-pointEnd)\"",
			" edge-thickness-normal edge-pattern-solid flowchart-link LS-a1 LE-b1",
			" edge-thickness-normal edge-pattern-dotted flowchart-link LS-a1 LE-b1",
			"class=\"node hot flowchart-label stm-style-",
			".node .cluster{",
			".hot&gt;*{",
		})
		{
			Verify(() => svg.Contains(part, StringComparison.Ordinal), $"the SVG has no {part}");
		}

		Verify(() => svg.Contains(">to b<", StringComparison.Ordinal));
		var lastNode = svg.IndexOf("id=\"mermaid-d\"", StringComparison.Ordinal);
		var firstEdge = svg.IndexOf("data-edge=\"true\"", StringComparison.Ordinal);
		Verify(() => lastNode > 0 && firstEdge > lastNode, $"the first edge is at {firstEdge} and the last block at {lastNode}");
	}

	private static Dictionary<string, FlowchartBox> Boxes(string source, out SvgViewBox viewBox)
	{
		var layout = Layout(source);
		viewBox = layout.ViewBox!.Value;
		return FlowchartLayout.BoxesOf(layout.Root).ToDictionary(box => box.Kind + " " + box.Name);
	}

	private static void Near(Dictionary<string, FlowchartBox> boxes, string name, double x, double y, double width, double height)
	{
		Verify(boxes.TryGetValue(name, out var box), $"no box {name} in {string.Join(", ", boxes.Keys)}");
		Verify(() => Math.Abs(box.X - x) <= 0.01 && Math.Abs(box.Y - y) <= 0.01, $"{name} at {box.X}, {box.Y}");
		Verify(() => Math.Abs(box.Width - width) <= 0.01 && Math.Abs(box.Height - height) <= 0.01, $"{name} is {box.Width} by {box.Height}");
	}

	private static BlockLayout Layout(string source)
	{
		var document = (BlockDocument)Mermaid.Default.Parse(source);
		var config = MermaidConfig.Resolve(null, document, document.DiagramType);
		var context = new DiagramRenderContext(config, "mermaid", TextMeasurer.Default, NoIcons.Instance, [], ResourceLimits.Unlimited);
		return new BlockModule().Layout(document, context);
	}
}
