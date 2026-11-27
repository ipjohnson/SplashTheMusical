using AwesomeAssertions;
using SplashTheMusical.Diagrams.Block;

namespace SplashTheMusical.Tests.Diagrams;

public class BlockParserTests
{
	[Test]
	public void ReadsBlocksColumnsAndWidths()
	{
		var document = Parse("block-beta\n  columns 3\n  a[\"A label\"] b:2 c(\"round\")\n  d:0\n  columns auto\n");

		document.DiagramType.Should().Be("block");
		((BlockColumns)document.Statements[0]).Columns.Should().Be(3);
		var nodes = document.Statements.Skip(1).Take(4).Cast<BlockNode>().Select(n => (n.Id, n.Label, n.Shape, n.Width));
		nodes.Should().Equal(("a", "A label", BlockShape.Square, 1), ("b", null, BlockShape.Na, 2), ("c", "round", BlockShape.Round, 1), ("d", null, BlockShape.Na, 0));
		((BlockColumns)document.Statements[5]).Columns.Should().BeNull();
	}

	[Test]
	public void ReadsEveryShapeFromItsBrackets()
	{
		var document = Parse("""
			block
			  a["x"] b("x") c(("x")) d>"x"] e{"x"} f{{"x"}} g(["x"]) h[["x"]] i[("x")] j((("x")))
			  k[/"x"/] l[\"x"\] m[/"x"\] n[\"x"/] o[/"x"] p(-"x"-)
			""");

		document.Statements.Cast<BlockNode>().Select(n => n.Shape).Should().Equal(
			BlockShape.Square, BlockShape.Round, BlockShape.Circle, BlockShape.RectLeftInvArrow, BlockShape.Diamond,
			BlockShape.Hexagon, BlockShape.Stadium, BlockShape.Subroutine, BlockShape.Cylinder, BlockShape.DoubleCircle,
			BlockShape.LeanRight, BlockShape.LeanLeft, BlockShape.Trapezoid, BlockShape.InvTrapezoid, BlockShape.Na, BlockShape.Na);
	}

	[Test]
	public void KeepsTheSpaceAroundABlockArrowsDirectionsAsTheDbDoes()
	{
		var arrow = (BlockNode)Parse("block\n  a<[\"go\"]>( right, x y):2\n").Statements[0];

		(arrow.Shape, arrow.Label, arrow.Width).Should().Be((BlockShape.BlockArrow, "go", 2));
		arrow.Directions.Should().Equal(" right", "x ", "y");
	}

	[Test]
	public void ReadsTheArrowheadsAndTheLineOfTheLastLinkToken()
	{
		var document = Parse("block\n  A -- \"x\" x--o B\n  C <-- \"y\" --> D\n  E ==> F\n  G -.- H\n  I --- J\n  K <--> L\n");

		var links = document.Statements.Cast<BlockEdge>().Select(e => e.Links.Single()).Select(l => (l.Label, l.Start, l.End, l.Stroke));
		links.Should().Equal(
			("x", BlockArrowHead.Cross, BlockArrowHead.Circle, BlockStroke.Normal),
			("y", BlockArrowHead.None, BlockArrowHead.Point, BlockStroke.Normal),
			(null, BlockArrowHead.None, BlockArrowHead.Point, BlockStroke.Thick),
			(null, BlockArrowHead.None, BlockArrowHead.None, BlockStroke.Dotted),
			(null, BlockArrowHead.None, BlockArrowHead.None, BlockStroke.Normal),
			(null, BlockArrowHead.Point, BlockArrowHead.Point, BlockStroke.Normal));
	}

	[Test]
	public void KeepsAChainAndItsShapes()
	{
		var edge = (BlockEdge)Parse("block\n  A:2 --> B(\"b\") --> C\n").Statements.Single();

		edge.Nodes.Select(n => (n.Id, n.Width, n.Shape)).Should().Equal(("A", 2, BlockShape.Na), ("B", 1, BlockShape.Round), ("C", 1, BlockShape.Na));
		edge.Links.Should().HaveCount(2);
	}

	[Test]
	public void ReadsCompositesAndSpaces()
	{
		var document = Parse("block\n  block:G[\"g\"]:2\n    columns 2\n    space:3\n    x\n  end\n  block-beta\n    y space\n  end\n");

		var named = (BlockComposite)document.Statements[0];
		var header = (BlockNode)named.Header!;
		(header.Id, header.Label, header.Width).Should().Be(("G", "g", 2));
		((BlockSpace)named.Statements[1]).Width.Should().Be(3);
		((BlockNode)named.Statements[2]).Id.Should().Be("x");
		var anonymous = (BlockComposite)document.Statements[1];
		anonymous.Header.Should().BeNull();
		((BlockSpace)anonymous.Statements[1]).Width.Should().Be(1);
	}

	[Test]
	public void ReadsClassesAndStylesAsTheDbSplitsThem()
	{
		var document = Parse("block\n  A\n  a\n  classDef blue fill:#6e6ce6,stroke:#333;\n  class A, B blue\n  style A fill:#969;\n  style a-b fill:red\n");

		var definition = (BlockClassDefinition)document.Statements[2];
		(definition.Name, string.Join("|", definition.Styles)).Should().Be(("blue", "fill:#6e6ce6|stroke:#333"));
		var assignment = (BlockClassAssignment)document.Statements[3];
		assignment.Ids.Should().Equal("A", " B");
		assignment.ClassName.Should().Be("blue");
		((BlockStyle)document.Statements[4]).Styles.Should().Equal("fill:#969");
		var dashed = (BlockStyle)document.Statements[5];
		(dashed.Id, dashed.Styles.Single()).Should().Be(("a", "-b fill:red"));
	}

	[Test]
	public void KeepsEntityCodesAsWritten()
	{
		var node = (BlockNode)Parse("block\n  n#35;1[\"#quot;q#quot; &amp;\"]\n").Statements[0];

		(node.Id, node.Label).Should().Be(("n#35;1", "#quot;q#quot; &amp;"));
	}

	[Test]
	[Arguments("block\n  accDescr {}\n  A\n")]
	[Arguments("block\n  %%\n  A\n")]
	[Arguments("block\n  A ~~~ B\n")]
	[Arguments("block\n  columns\n  auto\n")]
	[Arguments("block\n  a,b\n  style a,b fill:red\n")]
	[Arguments("block\n  style Z\n")]
	[Arguments("block\n  class Z foo\n  style Z fill:red\n")]
	[Arguments("block\n  block:G --> H\n    X\n  end\n")]
	public void AcceptsWhatMermaidJsAccepts(string source) =>
		Mermaid.Default.TryParse(source, out _, out var diagnostics).Should().BeTrue(string.Join("; ", diagnostics));

	[Test]
	[Arguments("block")]
	[Arguments("block\n  block\n  end\n")]
	[Arguments("block\n  A\n  end\n")]
	[Arguments("block\n  A\n  style B fill:red\n")]
	[Arguments("block\n  A --> B --> C\n  style A fill:red\n")]
	[Arguments("block\n  default\n")]
	[Arguments("block\n  A[\"`md`\"]\n")]
	[Arguments("block\n  A\n  classDef foo\n")]
	[Arguments("block\n  classDef DEFAULT fill:red\n  A\n")]
	[Arguments("block\n  A --> B:2\n")]
	[Arguments("block\n  A[ \"b\"]\n")]
	[Arguments("block\n  space :2\n")]
	[Arguments("block\n  accTitle: x\n  A\n")]
	[Arguments("block\n  A -- \"x\" -->\n")]
	[Arguments("block-beta\ntitle Release train\n  Plan(Plan)\n")]
	public void RefusesWhatMermaidJsRefuses(string source) =>
		Mermaid.Default.TryParse(source, out _, out _).Should().BeFalse();

	[Test]
	public void ReadsANumberPastTheLargestIntAsIt()
	{
		var node = (BlockNode)Parse("block\n  A:99999999999\n").Statements[0];

		node.Width.Should().Be(int.MaxValue);
	}

	[Test]
	public void FailsToLayOutColumnsZeroAsMermaidJsDoes()
	{
		var document = Parse("block\n  columns 0\n  A B\n");

		var render = () => Mermaid.Default.RenderSvg(document);

		render.Should().Throw<MermaidParseException>().Which.Message.Should().Contain("Columns must be an integer !== 0.");
	}

	private static BlockDocument Parse(string source)
	{
		Mermaid.Default.TryParse(source, out var document, out var diagnostics).Should().BeTrue(string.Join("; ", diagnostics));
		return (BlockDocument)document!;
	}
}
