using AwesomeAssertions;
using SplashTheMusical.Diagrams.State;

namespace SplashTheMusical.Tests.Diagrams;

public class StateWriterTests
{
	[Test]
	public void WritesAPlainStateBeforeALineWithoutABrace()
	{
		const string Source = "stateDiagram-v2\n  state s0\n  s1 --> s2\n  state s3 {\n    a\n  }\n";

		Mermaid.Default.Write(Mermaid.Default.Parse(Source)).Should().Be(Source);
	}

	// stateDiagram.jison's <STATE>\w+\s+\w+.*?\{ reads `s0` and `state s1 {` on the next line as one state name.
	[Test]
	public void RefusesAPlainStateBeforeALineThatStartsWithAWordAndHoldsABrace()
	{
		var document = new StateDocument
		{
			Statements = [new StateBlock("s0"), new StateBlock("s1") { Statements = [new StateDeclaration(new StateRef("a"))] }],
		};

		var writing = () => Mermaid.Default.Write(document);

		writing.Should().Throw<ArgumentException>().WithMessage("*`state s0`*");
		Mermaid.Default.TryParse("stateDiagram-v2\n  state s0\n  state s1 {\n    a\n  }\n", out _, out _).Should().BeFalse();
	}
}
