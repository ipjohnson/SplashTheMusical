using AwesomeAssertions;
using SplashTheMusical.Diagrams;
using SplashTheMusical.Diagrams.Requirement;
using SplashTheMusical.Tests.Generation;

namespace SplashTheMusical.Tests.Diagrams;

/// <summary>
/// The requirement parser rents its text, tokens and tables from the array pool, so a parse allocates only the document
/// it returns. <see cref="AssemblyReferenceTests.CoreDoesNotReferenceRegularExpressions"/> holds the parser to no Regex.
/// </summary>
public class RequirementAllocationTests
{
	private const string Source = "requirementDiagram\n  requirement r {\n    id: 1\n    risk: high\n  }\n  e - satisfies -> r\n";

	private static object? Sink;

	// Tests running beside it share the array pool's per-core arrays, so a rent could allocate.
	[Test]
	[NotInParallel]
	public void ParsingAllocatesOnlyTheModel()
	{
		var context = new DiagramParseContext("requirement", 0, ResourceLimits.Default, []);
		RequirementDocument? parsed = null;
		var parse = Allocations.Measure(() => parsed = RequirementParser.Parse(Source, context));
		var model = Allocations.Measure(() => Sink = Model());

		DocumentDump.Lines(parsed!).Should().Equal(DocumentDump.Lines((RequirementDocument)Sink!));
		context.Diagnostics.Should().BeEmpty();
		parse.Should().Be(model);
	}

	/// <summary>The document <see cref="Source"/> parses to, built with the strings and lists the parser makes.</summary>
	private static RequirementDocument Model()
	{
		var r = Copy("r");
		var document = new RequirementDocument();
		ReadOnlySpan<RequirementStatement> statements =
		[
			new RequirementDefinition(r) { Id = Copy("1"), Risk = RequirementRisk.High },
			new RequirementRelationship(Copy("e"), r, RequirementRelationshipType.Satisfies),
		];
		document.Statements.AddRange(statements);
		return document;
	}

	private static string Copy(string text) => new(text.AsSpan());
}
