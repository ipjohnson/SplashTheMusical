using AwesomeAssertions;
using SplashTheMusical.Diagrams.C4;

namespace SplashTheMusical.Tests.Diagrams;

public class C4ParserTests
{
	[Test]
	public void ReadsElementsBoundariesRelationsAndUpdates()
	{
		var document = Parse("""
			C4Container
			  title Container diagram
			  Person(customer, "Customer", "A customer")
			  System_Boundary(c1, "Internet Banking") {
			    Container(spa, "Single-Page App", "JavaScript", $sprite="browser")
			    ContainerDb(db, "Database", "SQL", "Stores everything")
			  }
			  Rel_Back(spa, customer, "Uses", "HTTPS")
			  UpdateRelStyle(spa, customer, $textColor="blue", $offsetY="-10")
			  UpdateLayoutConfig($c4ShapeInRow="3", $c4BoundaryInRow="1")
			""");

		(document.DiagramType, document.Kind, document.DiagramTitle).Should().Be(("c4", C4DiagramKind.Container, "Container diagram"));
		var person = (C4Element)document.Statements[0];
		person.Kind.Should().Be(C4ElementKind.Person);
		Values(person).Should().Equal("customer", "Customer", "A customer");
		var boundary = (C4Boundary)document.Statements[1];
		(boundary.Kind, Values(boundary)).Should().BeEquivalentTo((C4BoundaryKind.SystemBoundary, new[] { "c1", "Internet Banking" }));
		var spa = (C4Element)boundary.Statements[0];
		(spa.Arguments[3].Name, spa.Arguments[3].Value).Should().Be(("sprite", "browser"));
		((C4Element)boundary.Statements[1]).Kind.Should().Be(C4ElementKind.ContainerDb);
		var relation = (C4Relation)document.Statements[2];
		(relation.Kind, Values(relation)).Should().BeEquivalentTo((C4RelationKind.RelBack, new[] { "spa", "customer", "Uses", "HTTPS" }));
		document.Statements.Skip(3).Cast<C4Update>().Select(u => u.Kind).Should().Equal(C4UpdateKind.RelStyle, C4UpdateKind.LayoutConfig);
	}

	[Test]
	[Arguments("C4Context", C4DiagramKind.Context)]
	[Arguments("C4Container", C4DiagramKind.Container)]
	[Arguments("C4Component", C4DiagramKind.Component)]
	[Arguments("C4Dynamic", C4DiagramKind.Dynamic)]
	[Arguments("C4Deployment", C4DiagramKind.Deployment)]
	public void ReadsEachHeader(string header, C4DiagramKind kind) =>
		Parse(header + "\nPerson(a, \"A\")\n").Kind.Should().Be(kind);

	[Test]
	public void ReadsArgumentsAsTheLexerSplitsThem()
	{
		var document = Parse("C4Context\nPerson(,a, b, \"\",, \"c d\" , $k = \"  v \")\nSystem( , \"x\")\n");

		Values((C4Statement)document.Statements[0]).Should().Equal("", "a", "b", "", "", "c d", "", "v");
		((C4Statement)document.Statements[0]).Arguments[^1].Name.Should().Be("k");
		Values(document.Statements[1]).Should().Equal("", "x");
	}

	[Test]
	public void AnArgumentWithoutQuotesRunsToTheNextComma()
	{
		var document = Parse("C4Context\nPerson(a, b)\nSystem(c, \"d\")\n");

		Values(document.Statements.Single()).Should().Equal("a", "b)\nSystem(c", "d");
	}

	[Test]
	public void AccTitleSetsTheTitleAndTheLastTitleWins()
	{
		Parse("C4Context\ntitle  spaced\nPerson(a, \"A\")\n").DiagramTitle.Should().Be(" spaced");
		Parse("C4Context\ntitle first\naccTitle: second #9829;\nPerson(a, \"A\")\n").DiagramTitle.Should().Be("second #9829;");
		var document = Parse("C4Context\naccDescr {\n  many\n  lines\n}\nPerson(a, \"A\")\n");
		(document.DiagramTitle, document.AccessibilityTitle, document.AccessibilityDescription).Should().Be((null, null, "many\n  lines"));
	}

	[Test]
	public void ReadsABoundaryWithItsBraceOnTheNextLine()
	{
		var document = Parse("C4Deployment\nNode(n, \"N\")\n{\nNode_L(m, \"M\")\n{ Container(c, \"C\")\n}\n}\n");

		var node = (C4Boundary)document.Statements.Single();
		var inner = (C4Boundary)node.Statements.Single();
		(node.Kind, inner.Kind, inner.Statements.Count).Should().Be((C4BoundaryKind.Node, C4BoundaryKind.NodeL, 1));
	}

	[Test]
	[Arguments("C4Context")]
	[Arguments("C4Context\n")]
	[Arguments("C4Context Person(a, \"A\")\n")]
	[Arguments("C4Context\nPerson(a, \"A\") Person(b, \"B\")\n")]
	[Arguments("C4Context\nPerson(a, \"A\") %% a comment\nPerson(b, \"B\")\n")]
	[Arguments("C4Context\nPerson (a, \"A\")\n")]
	[Arguments("C4Context\nPerson()\n")]
	[Arguments("C4Context\nPerson\n")]
	[Arguments("C4Context\nPerson(a, b)\n")]
	[Arguments("C4Context\nPerson(a, \"A\n")]
	[Arguments("C4Context\nPerson(a, $k)\n")]
	[Arguments("C4Context\nPerson(a, \"go direction TB\")\n")]
	[Arguments("C4Context\ntitle a direction\n  LR\nPerson(a, \"A\")\n")]
	[Arguments("C4Context\nBoundary(b, \"B\")\nPerson(a, \"A\")\n")]
	[Arguments("C4Context\nBoundary(b, \"B\") {\n}\n")]
	[Arguments("C4Context\nBoundary(b, \"B\") { Person(a, \"A\")\n}\n")]
	[Arguments("C4Context\nBoundary(b, \"B\") {\ntitle t\nPerson(a, \"A\")\n}\n")]
	[Arguments("C4Context\nBoundary(b, \"B\") {\nPerson(a, \"A\")\n")]
	[Arguments("C4Context\naccDescr {x}accDescr {y}\n")]
	[Arguments("C4Context\nPerson(a, \"A\"}\n")]
	[Arguments("C4Context\nperson(a, \"A\")\n")]
	public void RefusesWhatMermaidRefuses(string source)
	{
		var mermaid = new Mermaid();

		mermaid.TryParse(source, out var document, out var diagnostics).Should().BeFalse();
		document.Should().BeNull();
		diagnostics.Should().ContainSingle(d => d.Severity == DiagnosticSeverity.Error);
	}

	[Test]
	[Arguments("C4Context\ntitle only\n")]
	[Arguments("C4Context\naccDescr {x}Person(a, \"A\")\n")]
	[Arguments("C4Context\nPerson(a, \"A\")\n%% a comment\nPerson(b, \"B\")\n")]
	[Arguments("C4Context\nPerson(a, \"A\")\ntitle t\nPerson(b, \"B\")\n")]
	[Arguments("C4Context\nPerson(a, \"A\",)\n")]
	[Arguments("C4Context\ntitle a\ntitle b\n\n")]
	public void AcceptsWhatMermaidAccepts(string source)
	{
		var mermaid = new Mermaid();

		mermaid.TryParse(source, out var document, out var diagnostics).Should().BeTrue(string.Join("; ", diagnostics));
		document.Should().NotBeNull();
	}

	[Test]
	public void RefusesToDrawARelationToAnUnknownAlias()
	{
		var mermaid = new Mermaid();
		var document = mermaid.Parse("C4Context\nPerson(a, \"A\")\nRel(a, b, \"uses\")\n");

		var render = () => mermaid.RenderSvg(document);

		render.Should().Throw<MermaidParseException>().Which.Message.Should().Contain("C4 rel \"a\" -> \"b\" references an unknown shape or boundary");
		mermaid.RenderSvg(mermaid.Parse("C4Context\nBoundary(b, \"B\") {\nPerson(a, \"A\")\n}\nRel(a, b, \"uses\")\n")).Should().NotBeNull();

		// mermaid.js never draws the global boundary, so a relation to it has no outline to meet.
		var global = () => mermaid.RenderSvg(mermaid.Parse("C4Context\nBoundary(b, \"B\") {\nPerson(a, \"A\")\n}\nRel(a, global, \"x\")\n"));
		global.Should().Throw<MermaidParseException>().Which.Message.Should().Contain("C4 element \"global\" has no intersect function");
	}

	[Test]
	public void ReportsTheLineOfAnError()
	{
		new Mermaid().TryParse("C4Context\nPerson(a, \"A\")\nPerson(b, \"B\") Person(c, \"C\")\n", out _, out var diagnostics);

		diagnostics.Single().Line.Should().Be(3);
	}

	private static C4Document Parse(string source) => (C4Document)new Mermaid().Parse(source);

	private static IEnumerable<string> Values(C4Statement statement) => statement.Arguments.Select(a => a.Value);
}
