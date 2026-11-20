using AwesomeAssertions;
using SplashTheMusical.Diagrams.Requirement;

namespace SplashTheMusical.Tests.Diagrams;

public class RequirementParserTests
{
	[Test]
	public void ReadsRequirementsElementsAndRelationships()
	{
		var document = Parse("""
			requirementDiagram
			  functionalRequirement login:::c1,c2 {
			    id: R1
			    text: "Users: sign in"
			    risk: High
			    verifymethod: test
			  }
			  element "auth service" {
			    type: service
			    docRef: design/auth.md
			  }
			  element tests {
			  }
			  "auth service" - satisfies -> login
			  login <- verifies - tests
			""");

		document.DiagramType.Should().Be("requirement");
		var login = (RequirementDefinition)document.Statements[0];
		(login.Name, login.Kind, login.Id, login.Text).Should().Be(("login", RequirementKind.FunctionalRequirement, "R1", "Users: sign in"));
		(login.Risk, login.VerifyMethod).Should().Be((RequirementRisk.High, RequirementVerifyMethod.Test));
		login.Classes.Should().Equal("c1", "c2");
		var element = (RequirementElement)document.Statements[1];
		(element.Name, element.Type, element.DocRef).Should().Be(("auth service", "service", "design/auth.md"));
		document.Statements.Skip(3).Cast<RequirementRelationship>().Select(r => (r.Source, r.Type, r.Target)).Should().Equal(
			("auth service", RequirementRelationshipType.Satisfies, "login"),
			("tests", RequirementRelationshipType.Verifies, "login"));
	}

	[Test]
	public void KeepsTheFirstValueOfARepeatedField()
	{
		var document = Parse("requirementDiagram\nrequirement r {\nid: first\nid: second\nrisk: low\nrisk: high\nverifymethod: test\nverifymethod: analysis\n}\nelement e {\ntype: a\ntype: b\n}\n");

		var definition = (RequirementDefinition)document.Statements[0];
		(definition.Id, definition.Risk, definition.VerifyMethod, definition.Text).Should().Be(("first", RequirementRisk.Low, RequirementVerifyMethod.Test, null));
		((RequirementElement)document.Statements[1]).Type.Should().Be("a");
	}

	[Test]
	public void ReadsStylesClassesAndDirections()
	{
		var document = Parse("requirementDiagram\nrequirement a {\n}\nclassDef c1 fill:#f96, color:red\nstyle a,\"b c\" stroke:#333\nclass a c1,c2\na:::c3\ndirection LR\n");

		var definition = (RequirementClassDefinition)document.Statements[1];
		(definition.Names, definition.Styles).Should().BeEquivalentTo((new[] { "c1" }, new[] { "fill:#f96", "color:red" }));
		((RequirementStyle)document.Statements[2]).Ids.Should().Equal("a", "b c");
		((RequirementClassAssignment)document.Statements[3]).Classes.Should().Equal("c1", "c2");
		var shorthand = (RequirementClassAssignment)document.Statements[4];
		(shorthand.Ids.Single(), shorthand.Classes.Single()).Should().Be(("a", "c3"));
		((RequirementDirection)document.Statements[5]).Direction.Should().Be("LR");
	}

	[Test]
	[Arguments("requirementDiagram\nrequirement login {\nid: REQ-1\n}\n")]
	[Arguments("requirementDiagram\nrequirement r {\ntext: test entity\n}\n")]
	[Arguments("requirementDiagram\nrequirement r {\nrisk: severe\n}\n")]
	[Arguments("requirementDiagram\na - likes -> b\n")]
	[Arguments("requirementDiagram\nelement e {\n")]
	[Arguments("requirementDiagram\na - traces -> b\n")]
	[Arguments("requirementDiagram\nelement a {\n}\na - traces -> b\n")]
	[Arguments("requirementDiagram\nelement b {\n}\na - traces -> b\n")]
	[Arguments("requirementDiagram\nelement a {\n}\nb:::c\na - traces -> b\n")]
	public void RefusesWhatMermaidRefuses(string source)
	{
		var mermaid = new Mermaid();

		mermaid.TryParse(source, out var document, out var diagnostics).Should().BeFalse();
		document.Should().BeNull();
		diagnostics.Should().ContainSingle(d => d.Severity == DiagnosticSeverity.Error);
	}

	[Test]
	[Arguments("requirementDiagram\na - traces -> b\nelement a {\n}\nrequirement b {\n}\n")]
	[Arguments("requirementDiagram\nelement a {\n}\na - traces -> a\n")]
	public void AcceptsARelationshipWhoseEndsAreDefinedAnywhere(string source)
	{
		var mermaid = new Mermaid();

		mermaid.TryParse(source, out var document, out _).Should().BeTrue();
		document.Should().NotBeNull();
	}

	[Test]
	public void TheDbKeepsTheFirstDefinitionAndStylesOnlyWhatComesBefore()
	{
		var db = RequirementDb.From(Parse("""
			requirementDiagram
			  requirement a {
			    id: 1
			  }
			  style a,missing,b fill:red
			  element b {
			  }
			  requirement a {
			    id: 2
			  }
			  class a,b c
			  classDef c color:blue,fill:green
			  direction RL
			"""));

		var a = db.Requirements.Single();
		(a.Id, string.Join(' ', a.Classes)).Should().Be(("1", "default c"));
		a.CssStyles.Should().Equal("fill:red", "color:blue", "fill:green");
		db.Elements.Single().CssStyles.Should().Equal("color:blue", "fill:green");
		db.Classes.Single().TextStyles.Should().Equal("color:blue");
		db.Direction.Should().Be("RL");
	}

	private static RequirementDocument Parse(string source) => (RequirementDocument)new Mermaid().Parse(source);
}
