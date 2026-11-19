using AwesomeAssertions;
using SplashTheMusical.Diagrams.Er;
using SplashTheMusical.Tests.Generation;

namespace SplashTheMusical.Tests.Diagrams;

public class ErWriterTests
{
	[Test]
	public void WritesOneStatementPerLine()
	{
		var document = new ErBuilder("LR")
			.Entity("CUSTOMER", e => e.Attribute("string", "name", "PK", "the name").Attribute("int?", "age"), alias: "The customer")
			.Relationship("CUSTOMER", ErCardinality.OnlyOne, "ORDER", ErCardinality.ZeroOrMore, "places")
			.Subgraph("G", "Orders", g => g.Relationship("ORDER", ErCardinality.OneOrMore, "LINE-ITEM", ErCardinality.ZeroOrOne, "contains", identifying: false))
			.ClassDef("c", "fill:#f9f", "color:#fff")
			.Class("c", "ORDER")
			.Build();

		new Mermaid().Write(document).Should().Be("""
			erDiagram
			  direction LR
			  CUSTOMER["The customer"] {
			    string name PK "the name"
			    int? age
			  }
			  CUSTOMER ||--o{ ORDER : "places"
			  subgraph G ["Orders"]
			    ORDER }|..o| LINE-ITEM : "contains"
			  end
			  classDef c fill:#f9f,color:#fff
			  class ORDER c

			""");
	}

	[Test]
	public void QuotesNamesThatLexAsSomethingElse()
	{
		var document = new ErBuilder()
			.Relationship("end", ErCardinality.OnlyOne, "1", ErCardinality.OnlyOne, "")
			.Entity("one")
			.Entity("accTitle")
			.Entity("u")
			.Build();

		var text = new Mermaid().Write(document);

		text.Should().Be("erDiagram\n  \"end\" ||--|| \"1\" : \"\"\n  \"one\"\n  \"accTitle\"\n  u\n");
		Dump(new Mermaid().Parse(text)).Should().Equal(Dump(document));
	}

	[Test]
	public void WritesTheTildeRuleAndBackticksSoTheyReadBack()
	{
		var document = new ErBuilder()
			.Entity("A", e => e.Attribute("Map~string, int~", "items", "FK", "a~b").Attribute("big int", "PK").Attribute("x", "geo.accuracy"))
			.Build();

		var text = new Mermaid().Write(document);

		text.Should().Contain("    Map~string, int~\n      items\n      FK\n      \"a~b\"\n");
		text.Should().Contain("    `big int` `PK`\n");
		Dump(new Mermaid().Parse(text)).Should().Equal(Dump(document));
	}

	[Test]
	public void EveryAcceptedFormReadsBackTheSame()
	{
		var mermaid = new Mermaid();
		var source = "erDiagram\naccTitle: T\nA one or zero optionally to 1+ B:::c : \"x y\"\nP u--o{ Q : t\nsubgraph S[\"A b\" c]\nC\nend\n";
		var document = mermaid.Parse(source);

		Dump(mermaid.Parse(mermaid.Write(document))).Should().Equal(Dump(document));
	}

	private static List<string> Dump(MermaidDocument document) => DocumentDump.Lines(document);
}
