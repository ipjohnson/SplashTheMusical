using AwesomeAssertions;
using SplashTheMusical.Diagrams;
using SplashTheMusical.Diagrams.State;

namespace SplashTheMusical.Tests.Diagrams;

/// <summary>
/// The state parser rents its text, tokens and tables from the array pool, so a parse allocates only the document it
/// returns. <see cref="AssemblyReferenceTests.CoreDoesNotReferenceRegularExpressions"/> holds the parser to no Regex.
/// </summary>
public class StateAllocationTests
{
	private const string Source = "stateDiagram-v2\n  [*] --> Idle\n  Idle --> Busy : start\n  state Busy {\n    Work\n  }\n";

	private static object? Sink;

	// Tests running beside it share the array pool's per-core arrays, so a rent could allocate.
	[Test]
	[NotInParallel]
	public void ParsingAllocatesOnlyTheModel()
	{
		var context = new DiagramParseContext("stateDiagram", 0, ResourceLimits.Default, []);
		StateDocument? parsed = null;
		var parse = Allocations.Measure(() => parsed = StateParser.Parse(Source, context));
		var model = Allocations.Measure(() => Sink = Model());

		StateDump.Lines(parsed!).Should().Equal(StateDump.Lines((StateDocument)Sink!));
		context.Diagnostics.Should().BeEmpty();
		parse.Should().Be(model);
	}

	/// <summary>The document <see cref="Source"/> parses to, built with the strings and lists the parser makes.</summary>
	private static StateDocument Model()
	{
		var start = Copy("[*]");
		var idle = Copy("Idle");
		var busy = Copy("Busy");
		var work = Copy("Work");
		var body = new List<StateStatement>();
		ReadOnlySpan<StateStatement> inner = [new StateDeclaration(new StateRef(work))];
		body.AddRange(inner);
		var document = new StateDocument { Keyword = Copy("stateDiagram-v2") };
		ReadOnlySpan<StateStatement> statements =
		[
			new StateTransition(new StateRef(start), new StateRef(idle)),
			new StateTransition(new StateRef(idle), new StateRef(busy)) { Label = Copy("start") },
			new StateBlock(busy) { Statements = body },
		];
		document.Statements.AddRange(statements);
		return document;
	}

	private static string Copy(string text) => new(text.AsSpan());
}
