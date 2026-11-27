using AwesomeAssertions;
using SplashTheMusical.Diagrams.C4;

namespace SplashTheMusical.Tests.Diagrams;

/// <summary><see cref="C4Db"/> runs the calls as <c>c4Db.ts</c> does, with its quirks.</summary>
public class C4DbTests
{
	[Test]
	public void ReadsArgumentsByPositionAndMergesACallOfTheSameAlias()
	{
		var db = Db("""
			C4Container
			Person(a, "A", "first")
			Container(c, "C", "Java", "runs")
			Person(a, "A again")
			System(s, "S", $tags="v1", $techn="Go")
			""");

		db.Shapes.Select(s => (s.StringOf("alias"), s.TextOf("typeC4Shape"), s.TextOf("label"), s.TextOf("techn"), s.TextOf("descr"))).Should().Equal(
			("a", "person", "A again", null, "first"),
			("c", "container", "C", "Java", "runs"),
			("s", "system", "S", "Go", null));
		db.Shapes[2].StringOf("tags").Should().Be("v1");
	}

	[Test]
	public void InsertsTheTypeOfABoundaryAndTracksItsParent()
	{
		var db = Db("""
			C4Context
			Enterprise_Boundary("e") {
			  System_Boundary(s, "S", "ignored") {
			    Person(p, "P")
			  }
			  Container_Boundary(c, "C") {
			    Person(q, "Q")
			  }
			}
			Deployment_Node(n, "N", "Ubuntu", "desc") {
			  Person(r, "R")
			}
			Person(t, "T")
			""");

		db.Boundaries.Select(b => (b.StringOf("alias"), b.TextOf("label"), b.TextOf("type"), b.StringOf("parentBoundary"))).Should().Equal(
			("global", "global", "global", ""),
			("e", "ENTERPRISE", "system", "global"),
			("s", "S", "SYSTEM", "e"),
			("c", "C", "CONTAINER", "e"),
			("n", "N", "Ubuntu", "global"));
		db.Boundaries[4].StringOf("nodeType").Should().Be("node");
		db.Shapes.Select(s => s.StringOf("parentBoundary")).Should().Equal("s", "c", "n", "global");
	}

	[Test]
	public void KeepsOneRelationPerPairOfEndsAndDropsTheIndexOfRelIndex()
	{
		var db = Db("""
			C4Dynamic
			Person(a, "A")
			Person(b, "B")
			Rel(a, b, "first", "HTTP")
			BiRel(a, b, "second")
			RelIndex(1, b, a, "back")
			Rel("a", "b")
			UpdateRelStyle(a, b, "blue", $lineColor="red", "0x10", "7px")
			""");

		db.Rels.Select(r => (r.StringOf("type"), r.StringOf("from"), r.StringOf("to"), r.TextOf("label"), r.TextOf("techn"))).Should().Equal(
			("birel", "a", "b", "second", "HTTP"),
			("rel", "b", "a", "back", ""));
		var rel = db.Rels[0];
		(rel.StringOf("textColor"), rel.StringOf("lineColor"), rel.StringOf("offsetX"), rel.StringOf("offsetY")).Should().Be(("blue", "red", "16", "7"));
	}

	[Test]
	public void UpdatesTheStyleOfAnElementOrABoundaryAsPlainStrings()
	{
		var db = Db("""
			C4Context
			Boundary(b, "B") {
			  Person(a, "A")
			}
			UpdateElementStyle(a, $bgColor="grey", "white", $techn="raw")
			UpdateElementStyle(b, $borderColor="red")
			UpdateElementStyle(missing, $bgColor="grey")
			""");

		var a = db.Shapes.Single();
		(a.StringOf("bgColor"), a.StringOf("fontColor"), a.StringOf("techn"), a.TextOf("techn")).Should().Be(("grey", "white", "raw", null));
		db.Boundaries[1].StringOf("borderColor").Should().Be("red");
	}

	[Test]
	public void SetsTheShapesAndBoundariesPerRowOnlyToANumberOfOneOrMore()
	{
		var named = Db("C4Context\nUpdateLayoutConfig($c4BoundaryInRow=\"3\", $c4ShapeInRow=\"0\")\n");
		(named.ShapeInRow, named.BoundaryInRow).Should().Be((3.0, 2.0));
		var positional = Db("C4Context\nUpdateLayoutConfig(\"x\", \"5\")\n");
		(positional.ShapeInRow, positional.BoundaryInRow).Should().Be((4.0, 5.0));
	}

	[Test]
	public void FindsARelationEndThatIsNoShapeOrBoundary()
	{
		Db("C4Context\nPerson(a, \"A\")\nRel(a, global, \"x\")\nRel(a, $k=\"a\", \"y\")\n").RelWithUnknownEnd()!.TextOf("label").Should().Be("y");
		Db("C4Context\nRel(a, b, \"x\")\nPerson(a, \"A\")\nPerson(b, \"B\")\n").RelWithUnknownEnd().Should().BeNull();
	}

	[Test]
	[Arguments("12px", 12.0)]
	[Arguments("  -3", -3.0)]
	[Arguments("+0X1f", 31.0)]
	[Arguments("x1", double.NaN)]
	[Arguments("", double.NaN)]
	public void ParsesIntegersAsJavaScriptDoes(string text, double expected) =>
		C4Db.JsParseInt(text).Should().Be(expected);

	private static C4Db Db(string source) => C4Db.From((C4Document)new Mermaid().Parse(source));
}
