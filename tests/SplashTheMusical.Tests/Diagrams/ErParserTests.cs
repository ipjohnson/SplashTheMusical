using AwesomeAssertions;
using SplashTheMusical.Diagrams.Er;

namespace SplashTheMusical.Tests.Diagrams;

public class ErParserTests
{
	[Test]
	public void ReadsEntitiesAttributesAndRelationships()
	{
		var document = Parse("""
			erDiagram
			  CUSTOMER ||--o{ ORDER : places
			  ORDER }|..|{ "DELIVERY ADDRESS" : "uses"
			  CUSTOMER["The customer"] {
			    string name PK, FK "the name"
			    int? age
			    List~int~ ids
			    string `check-in date`
			  }
			""");

		document.DiagramType.Should().Be("er");
		var places = (ErRelationship)document.Statements[0];
		(places.EntityA, places.EntityB, places.Role).Should().Be(("CUSTOMER", "ORDER", "places"));
		(places.CardinalityA, places.CardinalityB, places.Identifying).Should().Be((ErCardinality.OnlyOne, ErCardinality.ZeroOrMore, true));
		var uses = (ErRelationship)document.Statements[1];
		(uses.EntityB, uses.Role, uses.Identifying).Should().Be(("DELIVERY ADDRESS", "uses", false));
		(uses.CardinalityA, uses.CardinalityB).Should().Be((ErCardinality.OneOrMore, ErCardinality.OneOrMore));
		var customer = (ErEntity)document.Statements[2];
		customer.Alias.Should().Be("The customer");
		customer.Attributes!.Select(a => $"{a.Type} {a.Name} {string.Join(',', a.Keys)} {a.Comment}").Should().Equal(
			"string name PK,FK the name",
			"int? age  ",
			"List~int~ ids  ",
			"string check-in date  ");
	}

	[Test]
	public void ReadsTheWordCardinalitiesAndLines()
	{
		var document = Parse("erDiagram\nA one or more to zero or many B : r\nC 1 optionally to many(1) D : s\nP u--o{ Q : t\n");

		document.Statements.Cast<ErRelationship>().Select(r => (r.CardinalityA, r.CardinalityB, r.Identifying)).Should().Equal(
			(ErCardinality.OneOrMore, ErCardinality.ZeroOrMore, true),
			(ErCardinality.OnlyOne, ErCardinality.OneOrMore, false),
			(ErCardinality.MdParent, ErCardinality.ZeroOrMore, true));
	}

	[Test]
	[Arguments("erDiagram\n  A ||--|| B : one-to-one\n")]
	[Arguments("erDiagram\n  A ||--o{ B\n")]
	[Arguments("erDiagram\n \"Blo%rf\"\n")]
	[Arguments("erDiagram\nA xxx B : has\n")]
	[Arguments("erDiagram\nclassDef a font-size:1.5px\n")]
	[Arguments("erDiagram\nsubgraph G\nA\n")]
	public void RefusesWhatMermaidRefuses(string source)
	{
		var mermaid = new Mermaid();

		mermaid.TryParse(source, out var document, out var diagnostics).Should().BeFalse();
		document.Should().BeNull();
		diagnostics.Should().ContainSingle(d => d.Severity == DiagnosticSeverity.Error);
	}

	[Test]
	public void ReadsStylesClassesAndDirections()
	{
		var document = Parse("erDiagram\nA:::c1,c2\nclassDef c1 stroke:lightblue, color: #0000FF\nstyle A,B fill:pink;\nclass A,B c3\nx direction LR y\n");

		((ErEntity)document.Statements[0]).Classes.Should().Equal("c1", "c2");
		var definition = (ErClassDefinition)document.Statements[1];
		definition.Names.Should().Equal("c1");
		definition.Styles.Should().Equal("stroke:lightblue", "color:#0000FF");
		((ErStyle)document.Statements[2]).Styles.Should().Equal("fill:pink");
		((ErClassAssignment)document.Statements[3]).Classes.Should().Equal("c3");
		((ErDirection)document.Statements[4]).Direction.Should().Be("LR");
	}

	[Test]
	public void ReadsSubgraphsAndTheDbGivesTheirMembers()
	{
		var document = Parse("""
			erDiagram
			  subgraph outer [Outer title]
			    A
			    subgraph inner
			      direction LR
			      B ||--|| C : r
			    end
			  end
			  inner ||--|| A : s
			""");

		var outer = (ErSubgraph)document.Statements[0];
		(outer.Id, outer.Title).Should().Be(("outer", "Outer title"));
		var db = ErDb.From(document);
		db.Subgraphs.Select(s => $"{s.Id} {s.Title} {s.Direction} {string.Join(',', s.Nodes)}").Should().Equal(
			"inner inner LR B,C",
			"outer Outer title  A,inner");
		db.Entities.Select(e => e.Id).Should().Equal("entity-A-0", "entity-B-1", "entity-C-2", "entity-inner-3");
		db.Relationships[1].Should().Match<ErDbRelationship>(r => r.EntityA == "inner" && r.AIsSubgraph && r.EntityB == "entity-A-0");
	}

	[Test]
	public void TheDbKeepsTheFirstAliasAndAddsClassesAndStyles()
	{
		var db = ErDb.From(Parse("erDiagram\nA[First]\nA[Second]:::c\nstyle A fill:red\nclassDef c color:red,fill:blue\n"));

		var entity = db.Entities.Single();
		(entity.Alias, entity.CssClasses).Should().Be(("First", "default c"));
		entity.CssStyles.Should().Equal("fill:red");
		db.Class("c")!.Styles.Should().Equal("color:red", "fill:blue");
		db.Class("c")!.TextStyles.Should().Equal("color:red");
	}

	private static ErDocument Parse(string source) => (ErDocument)new Mermaid().Parse(source);
}
