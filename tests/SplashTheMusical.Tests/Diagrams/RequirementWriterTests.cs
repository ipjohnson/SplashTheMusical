using AwesomeAssertions;
using SplashTheMusical.Diagrams.Requirement;
using SplashTheMusical.Tests.Generation;

namespace SplashTheMusical.Tests.Diagrams;

public class RequirementWriterTests
{
	[Test]
	public void WritesOneStatementPerLineAndOneFieldPerLine()
	{
		var document = new RequirementBuilder("LR")
			.Requirement("login", RequirementKind.FunctionalRequirement, id: "R1", text: "Users sign in", risk: RequirementRisk.High, verifyMethod: RequirementVerifyMethod.Test)
			.Element("auth_service", type: "service", docRef: "design/auth.md")
			.Relationship("auth_service", RequirementRelationshipType.Satisfies, "login")
			.Build();

		new Mermaid().Write(document).Should().Be("""
			requirementDiagram
			  direction LR
			  functionalRequirement login {
			    id: R1
			    text: Users sign in
			    risk: high
			    verifymethod: test
			  }
			  element auth_service {
			    type: service
			    docref: design/auth.md
			  }
			  auth_service - satisfies -> login

			""");
	}

	[Test]
	public void QuotesTextThatLexesAsSomethingElse()
	{
		var document = new RequirementBuilder()
			.Requirement("test", text: "test entity", id: "REQ-1")
			.Element("my element", type: "a: b")
			.Element("Ünïcödé")
			.Element("1.2")
			.Relationship("Ünïcödé", RequirementRelationshipType.Traces, "1.2")
			.Build();

		var text = new Mermaid().Write(document);

		text.Should().Be("""
			requirementDiagram
			  requirement "test" {
			    id: "REQ-1"
			    text: "test entity"
			  }
			  element my element {
			    type: "a: b"
			  }
			  element "Ünïcödé" {
			  }
			  element 1.2 {
			  }
			  "Ünïcödé" - traces -> 1.2

			""");
		Dump(new Mermaid().Parse(text)).Should().Equal(Dump(document));
	}

	[Test]
	public void WritesStylesClassesAndTheShorthandClass()
	{
		var mermaid = new Mermaid();
		var source = "requirementDiagram\naccTitle: T\nrequirement a:::c1 {\n}\nclassDef c1 fill:#f96, color:red\nstyle a,\"b c\" stroke:#333\nb:::c2\n";
		var document = mermaid.Parse(source);

		var text = mermaid.Write(document);

		text.Should().Contain("  class b c2\n").And.Contain("  style a,\"b c\" stroke:#333\n").And.Contain("  requirement a:::c1 {\n");
		Dump(mermaid.Parse(text)).Should().Equal(Dump(document));
	}

	[Test]
	[Arguments("say \"hi\": now")]
	[Arguments("")]
	[Arguments("go direction LR")]
	[Arguments("a\r\nb")]
	public void RefusesTextThatCannotReadBack(string value)
	{
		var document = new RequirementBuilder().Requirement("r", text: value).Build();

		var write = () => new Mermaid().Write(document);

		write.Should().Throw<ArgumentException>();
	}

	[Test]
	public void RefusesAStyleThatLosesASemicolon()
	{
		var document = new RequirementDocument { Statements = [new RequirementStyle(["a"], ["fill:#f96;"])] };

		var write = () => new Mermaid().Write(document);

		write.Should().Throw<ArgumentException>();
	}

	private static List<string> Dump(MermaidDocument document) => DocumentDump.Lines(document);
}
