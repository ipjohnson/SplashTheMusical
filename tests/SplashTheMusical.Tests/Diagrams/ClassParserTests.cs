using SplashTheMusical.Diagrams.Class;

namespace SplashTheMusical.Tests.Diagrams;

public class ClassParserTests
{
	[Test]
	public void ReadsTheStatementsAfterAMultilineAccessibilityDescription()
	{
		var document = (ClassDocument)Mermaid.Default.Parse("classDiagram\n  accDescr {\n  My description\n  }\n  note \"x\"\n  class A\n");

		Verify(document.AccessibilityDescription == "My description");
		Verify(document.Statements.Count == 2);
		Verify(document.Statements[0] is ClassNote { Text: "x" });
		Verify(document.Statements[1] is ClassDefinition { Name: "A" });
	}

	[Test]
	public void RefusesAStatementOnTheLineThatEndsAMultilineAccessibilityDescription() =>
		Verify(!Mermaid.Default.TryParse("classDiagram\n  accDescr {\n  a\n  } class A\n", out _, out _), $"the grammar needs a NEWLINE after the description");
}
