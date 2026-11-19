using AwesomeAssertions;
using SplashTheMusical.Diagrams;
using SplashTheMusical.Diagrams.Er;
using SplashTheMusical.Tests.Generation;

namespace SplashTheMusical.Tests.Diagrams;

/// <summary>
/// The ER parser rents its text, tokens and tables from the array pool, so a parse allocates only the document it
/// returns. <see cref="AssemblyReferenceTests.CoreDoesNotReferenceRegularExpressions"/> holds the parser to no Regex.
/// </summary>
public class ErAllocationTests
{
	private const string Source = "erDiagram\n  CUSTOMER ||--o{ ORDER : places\n  ORDER {\n    int id PK\n  }\n";

	private static object? Sink;

	// Tests running beside it share the array pool's per-core arrays, so a rent could allocate.
	[Test]
	[NotInParallel]
	public void ParsingAllocatesOnlyTheModel()
	{
		var context = new DiagramParseContext("er", 0, ResourceLimits.Default, []);
		ErDocument? parsed = null;
		var parse = Allocations.Measure(() => parsed = ErParser.Parse(Source, context));
		var model = Allocations.Measure(() => Sink = Model());

		DocumentDump.Lines(parsed!).Should().Equal(DocumentDump.Lines((ErDocument)Sink!));
		context.Diagnostics.Should().BeEmpty();
		parse.Should().Be(model);
	}

	/// <summary>The document <see cref="Source"/> parses to, built with the strings and lists the parser makes.</summary>
	private static ErDocument Model()
	{
		var customer = Copy("CUSTOMER");
		var order = Copy("ORDER");
		var attribute = new ErAttribute(Copy("int"), Copy("id"));
		attribute.Keys.Add(Copy("PK"));
		var attributes = new List<ErAttribute>();
		attributes.Add(attribute);
		var document = new ErDocument();
		ReadOnlySpan<ErStatement> statements =
		[
			new ErRelationship(customer, order, Copy("places")) { CardinalityB = ErCardinality.ZeroOrMore },
			new ErEntity(order) { Attributes = attributes },
		];
		document.Statements.AddRange(statements);
		return document;
	}

	private static string Copy(string text) => new(text.AsSpan());
}
