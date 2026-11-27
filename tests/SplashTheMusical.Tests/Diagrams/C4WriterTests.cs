using AwesomeAssertions;
using SplashTheMusical.Diagrams.C4;
using SplashTheMusical.Tests.Generation;

namespace SplashTheMusical.Tests.Diagrams;

public class C4WriterTests
{
	[Test]
	public void WritesOneCallPerLineAndABoundarysCallsInsideItsBraces()
	{
		var document = new C4Builder(C4DiagramKind.Context, title: "Banking")
			.Element(C4ElementKind.Person, "customer", "Customer", "A customer of the bank")
			.Boundary(C4BoundaryKind.EnterpriseBoundary, "bank", "Bank", b => b
				.Element(C4ElementKind.SystemDbExt, "mainframe", "Mainframe")
				.Boundary(C4BoundaryKind.Boundary, "inner", "Inner", i => i.Element(C4ElementKind.System, "s", "S"), "group"))
			.Relation(C4RelationKind.RelBack, "customer", "mainframe", "Uses", "HTTPS")
			.Build();
		document.Statements.Add(new C4Update(C4UpdateKind.LayoutConfig, [new C4Argument("c4ShapeInRow", "3"), new C4Argument("c4BoundaryInRow", "")]));

		var text = new Mermaid().Write(document);

		text.Should().Be("""
			C4Context
			  title Banking
			  Person("customer", "Customer", "A customer of the bank")
			  Enterprise_Boundary("bank", "Bank") {
			    SystemDb_Ext("mainframe", "Mainframe")
			    Boundary("inner", "Inner", "group") {
			      System("s", "S")
			    }
			  }
			  Rel_Back("customer", "mainframe", "Uses", "HTTPS")
			  UpdateLayoutConfig($c4ShapeInRow="3", $c4BoundaryInRow=" ")

			""");
		RoundTripPropertyTests.AssertRoundTrips(document);
	}

	[Test]
	public void WritesTheTitleAndTheDescriptionInAFormThatHoldsThem()
	{
		var documents = new[]
		{
			new C4Document { DiagramTitle = " spaced #quot;", AccessibilityDescription = "one line" },
			new C4Document { DiagramTitle = "a # b; c", AccessibilityDescription = "x;" },
			new C4Document { AccessibilityDescription = "two\n  lines" },
		};

		documents.Select(Write).Should().Equal(
			"C4Context\n  title  spaced #quot;\n  accDescription one line\n",
			"C4Context\n  accTitle: a # b; c\n  accDescr: x;\n",
			"C4Context\n  accDescr {\ntwo\n  lines\n  }\n");
		foreach (var document in documents)
		{
			RoundTripPropertyTests.AssertRoundTrips(document);
		}
	}

	[Test]
	public void WritesAnArgumentWithAQuoteBareWhenACommaFollowsIt()
	{
		var document = new C4Document
		{
			Statements = { new C4Element(C4ElementKind.Person, [new C4Argument("a"), new C4Argument("say \"hi\""), new C4Argument("d")]) },
		};

		Write(document).Should().Be("C4Context\n  Person(\"a\", say \"hi\", \"d\")\n");
		RoundTripPropertyTests.AssertRoundTrips(document);
	}

	[Test]
	public void RefusesWhatNoTextReadsBackAsItself()
	{
		Refuses(new C4Document());
		Refuses(new C4Document { DiagramTitle = "t", AccessibilityTitle = "a" });
		Refuses(new C4Document { DiagramTitle = " # " });
		Refuses(new C4Document { AccessibilityDescription = "a }\nb" });
		Refuses(Element(new C4Argument(" padded")));
		Refuses(Element(new C4Argument("a"), new C4Argument("last \"quoted\"")));
		Refuses(Element(new C4Argument("a"), new C4Argument("a, \"b\""), new C4Argument("c")));
		Refuses(Element(new C4Argument("a"), new C4Argument("k=", "v")));
		Refuses(Element(new C4Argument("a"), new C4Argument("k", "v\"")));
		Refuses(Element(new C4Argument("a"), new C4Argument("go direction TB")));
		Refuses(Element(new C4Argument("a"), new C4Argument("x\n%% comment")));
		Refuses(Element(new C4Argument("a"), new C4Argument("%%{init: {}}%%")));
		Refuses(new C4Document { Statements = { new C4Element(C4ElementKind.Person, []) } });
		Refuses(new C4Document { Statements = { new C4Boundary(C4BoundaryKind.Boundary, [new C4Argument("b")]) } });
	}

	private static C4Document Element(params C4Argument[] arguments) =>
		new() { Statements = { new C4Element(C4ElementKind.Person, [.. arguments]) } };

	private static string Write(C4Document document) => new Mermaid().Write(document);

	private static void Refuses(C4Document document)
	{
		var write = () => Write(document);

		write.Should().Throw<ArgumentException>();
	}
}
