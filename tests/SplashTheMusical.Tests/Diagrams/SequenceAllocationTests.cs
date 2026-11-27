using AwesomeAssertions;
using SplashTheMusical.Diagrams;
using SplashTheMusical.Diagrams.Sequence;
using SplashTheMusical.Tests.Generation;

namespace SplashTheMusical.Tests.Diagrams;

/// <summary>
/// The sequence parser rents its text, tokens, tables and the db's checks from the array pool, so a parse allocates
/// only the document it returns. <see cref="AssemblyReferenceTests.CoreDoesNotReferenceRegularExpressions"/> holds the
/// parser to no Regex.
/// </summary>
public class SequenceAllocationTests
{
	private const string Source = "sequenceDiagram\n  participant A as Alice\n  A->>+B: hi\n  Note over A,B: n\n  alt x\n    B-->>-A: ok\n  else y\n    A-xB: no\n  end\n";

	private static object? Sink;

	// Tests running beside it share the array pool's per-core arrays, so a rent could allocate.
	[Test]
	[NotInParallel]
	public void ParsingAllocatesOnlyTheModel()
	{
		var context = new DiagramParseContext("sequence", 0, ResourceLimits.Default, []);
		SequenceDocument? parsed = null;
		var parse = Allocations.Measure(() => parsed = SequenceParser.Parse(Source, context));
		var model = Allocations.Measure(() => Sink = Model());

		DocumentDump.Lines(parsed!).Should().Equal(DocumentDump.Lines((SequenceDocument)Sink!));
		context.Diagnostics.Should().BeEmpty();
		parse.Should().Be(model);
	}

	/// <summary>The document <see cref="Source"/> parses to, built with the strings and lists the parser makes.</summary>
	private static SequenceDocument Model()
	{
		var a = Copy("A");
		var b = Copy("B");
		var document = new SequenceDocument();
		ReadOnlySpan<SequenceStatement> statements =
		[
			new SequenceParticipant(a) { Description = Copy("Alice") },
			new SequenceMessage(a, b) { Modifier = SequenceMessageModifier.Activate, Text = Copy("hi") },
			new SequenceNote(SequenceNotePlacement.Over, a, b) { Text = Copy("n") },
			new SequenceBlock(SequenceBlockKind.Alt)
			{
				Text = Copy("x"),
				Statements = List<SequenceStatement>(new SequenceMessage(b, a, SequenceArrow.Dotted) { Modifier = SequenceMessageModifier.Deactivate, Text = Copy("ok") }),
				Sections = List(new SequenceBlockSection
				{
					Text = Copy("y"),
					Statements = List<SequenceStatement>(new SequenceMessage(a, b, SequenceArrow.SolidCross) { Text = Copy("no") }),
				}),
			},
		];
		document.Statements.AddRange(statements);
		return document;
	}

	private static List<T> List<T>(T item)
	{
		ReadOnlySpan<T> items = [item];
		var list = new List<T>(items.Length);
		list.AddRange(items);
		return list;
	}

	private static string Copy(string text) => new(text.AsSpan());
}
