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

	[Test]
	public void WritesQuotesInLabelsAsEntities()
	{
		var document = Document(
			new ErEntity("A") { Attributes = [new ErAttribute("int", "x") { Comment = "say \"hi\"" }] },
			new ErRelationship("A", "B", "a\"b"));

		var text = new Mermaid().Write(document);

		text.Should().Contain("int x \"say #quot;hi#quot;\"").And.Contain("A ||--|| B : \"a#quot;b\"");
		var read = (ErDocument)new Mermaid().Parse(text);
		((ErRelationship)read.Statements[1]).Role.Should().Be("a#quot;b");
		((ErEntity)read.Statements[0]).Attributes![0].Comment.Should().Be("say #quot;hi#quot;");
	}

	[Test]
	[Arguments("a\nB ||--|| C : x")]
	[Arguments("%% not a comment")]
	[Arguments("100%")]
	[Arguments("direction")]
	public void RolesThatLexAsOneWordReadBackTheSame(string role)
	{
		var document = Document(new ErRelationship("A", "B", role));

		Dump(new Mermaid().Parse(new Mermaid().Write(document))).Should().Equal(Dump(document));
	}

	[Test]
	public void AttributeWordsThatLexAsKeysOrCommentsAreBackquoted()
	{
		var document = Document(new ErEntity("A") { Attributes = [new ErAttribute("PK-x", "fk"), new ErAttribute("a~b", "x")] });

		var text = new Mermaid().Write(document);

		text.Should().Contain("`PK-x` `fk`").And.Contain("`a~b`");
		Dump(new Mermaid().Parse(text)).Should().Equal(Dump(document));
	}

	public static IEnumerable<string> UnwritableCases() => Unwritable.Keys;

	[Test]
	[MethodDataSource(nameof(UnwritableCases))]
	public void RefusesValuesNoTextReadsBackAsItself(string name)
	{
		var write = () => new Mermaid().Write(Unwritable[name]());

		write.Should().Throw<ArgumentException>();
	}

	private static readonly Dictionary<string, Func<ErDocument>> Unwritable = new(StringComparer.Ordinal)
	{
		["name with a quote"] = () => Document(new ErEntity("a\"b")),
		["name with a percent"] = () => Document(new ErEntity("a%b")),
		["name with a line break"] = () => Document(new ErEntity("a\nb")),
		["alias with a quote"] = () => Document(new ErEntity("A") { Alias = "a\"]" }),
		["subgraph title with a quote"] = () => Document(new ErSubgraph("G") { Title = "t\"]" }),
		["empty subgraph title"] = () => Document(new ErSubgraph("G") { Title = "" }),
		["role with a carriage return"] = () => Document(new ErRelationship("A", "B", "a\rb")),
		["role with a comment line"] = () => Document(new ErRelationship("A", "B", "a\n  %% b")),
		["role with a directive"] = () => Document(new ErRelationship("A", "B", "%%{init: {}}%%")),
		["role with a placeholder"] = () => Document(new ErRelationship("A", "B", "a\uFB02\u00B0b")),
		["role that reads as a direction"] = () => Document(new ErRelationship("A", "B", "go direction LR")),
		["class across the direction rule"] = () => Document(new ErClassAssignment(["direction"], ["TB"])),
		["name ending a line before a direction"] = () => Document(new ErEntity("direction"), new ErEntity("TBL")),
		["comment with a comment line"] = () => Document(new ErEntity("A") { Attributes = [new ErAttribute("int", "x") { Comment = "a\n%%b" }] }),
		["class with a space"] = () => Document(new ErEntity("A") { Classes = ["a b"] }),
		["class with a bracket"] = () => Document(new ErEntity("A") { Classes = ["a]"] }),
		["class with a comma"] = () => Document(new ErRelationship("A", "B", "r") { ClassesB = ["a,b"] }),
		["class that is a keyword"] = () => Document(new ErEntity("A") { Classes = ["end"] }),
		["class that starts an accessibility rule"] = () => Document(new ErRelationship("A", "B", "r") { ClassesB = ["accTitle"] }),
		["empty class list"] = () => Document(new ErEntity("A") { Classes = [] }),
		["class assignment id with a quote"] = () => Document(new ErClassAssignment(["a\"b"], ["c"])),
		["style id with a dot"] = () => Document(new ErStyle(["a.b"], ["fill:red"])),
		["classDef name with a space"] = () => Document(new ErClassDefinition(["a b"], ["fill:red"])),
		["style with a space"] = () => Document(new ErStyle(["A"], ["fill: red"])),
		["style with a semicolon"] = () => Document(new ErStyle(["A"], ["fill:red;"])),
		["style with a line break"] = () => Document(new ErClassDefinition(["c"], ["fill:red\nA"])),
		["no styles"] = () => Document(new ErStyle(["A"], [])),
		["direction TD"] = () => Document(new ErDirection("TD")),
		["attribute key"] = () => Document(new ErEntity("A") { Attributes = [new ErAttribute("int", "x") { Keys = ["PX"] }] }),
		["attribute type with a backquote"] = () => Document(new ErEntity("A") { Attributes = [new ErAttribute("a`b", "x")] }),
		["attribute name with a quote"] = () => Document(new ErEntity("A") { Attributes = [new ErAttribute("int", "a\"b")] }),
		["attribute word that would start a comment line"] = () => Document(new ErEntity("A") { Attributes = [new ErAttribute("%%~a~", "x")] }),
		["attribute word the tilde rule reads with its backquotes"] = () => Document(new ErEntity("A") { Attributes = [new ErAttribute("a~b~ c", "x")] }),
	};

	private static ErDocument Document(params ErStatement[] statements) => new() { Statements = [.. statements] };

	private static List<string> Dump(MermaidDocument document) => DocumentDump.Lines(document);
}
