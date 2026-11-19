using AwesomeAssertions;
using SplashTheMusical.Diagrams.Class;

namespace SplashTheMusical.Tests.Diagrams;

public class ClassParserTests
{
	[Test]
	public void ReadsTheStatementsAfterAMultilineAccessibilityDescription()
	{
		var document = (ClassDocument)Mermaid.Default.Parse("classDiagram\n  accDescr {\n  My description\n  }\n  note \"x\"\n  class A\n");

		document.AccessibilityDescription.Should().Be("My description");
		document.Statements.Should().HaveCount(2);
		document.Statements[0].Should().BeOfType<ClassNote>().Which.Text.Should().Be("x");
		document.Statements[1].Should().BeOfType<ClassDefinition>().Which.Name.Should().Be("A");
	}

	[Test]
	public void RefusesAStatementOnTheLineThatEndsAMultilineAccessibilityDescription() =>
		Mermaid.Default.TryParse("classDiagram\n  accDescr {\n  a\n  } class A\n", out _, out _).Should().BeFalse("the grammar needs a NEWLINE after the description");
}
