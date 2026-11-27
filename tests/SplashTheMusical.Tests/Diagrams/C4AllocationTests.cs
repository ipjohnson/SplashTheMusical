using AwesomeAssertions;
using SplashTheMusical.Diagrams;
using SplashTheMusical.Diagrams.C4;
using SplashTheMusical.Tests.Generation;

namespace SplashTheMusical.Tests.Diagrams;

/// <summary>
/// The C4 parser rents its text, tokens and tables from the array pool, so a parse allocates only the document it
/// returns. <see cref="AssemblyReferenceTests.CoreDoesNotReferenceRegularExpressions"/> holds the parser to no Regex.
/// </summary>
public class C4AllocationTests
{
	private const string Source = "C4Context\n  title Banking\n  Person(a, \"A\", \"A customer\")\n  Boundary(b, \"B\") {\n    System(s, \"S\", $tags=\"v1\")\n  }\n  Rel(a, s, \"Uses\")\n";

	private static object? Sink;

	// Tests running beside it share the array pool's per-core arrays, so a rent could allocate.
	[Test]
	[NotInParallel]
	public void ParsingAllocatesOnlyTheModel()
	{
		var context = new DiagramParseContext("c4", 0, ResourceLimits.Default, []);
		C4Document? parsed = null;
		var parse = Allocations.Measure(() => parsed = C4Parser.Parse(Source, context));
		var model = Allocations.Measure(() => Sink = Model());

		DocumentDump.Lines(parsed!).Should().Equal(DocumentDump.Lines((C4Document)Sink!));
		context.Diagnostics.Should().BeEmpty();
		parse.Should().Be(model);
	}

	/// <summary>The document <see cref="Source"/> parses to, built with the strings and lists the parser makes.</summary>
	private static C4Document Model()
	{
		var a = Copy("a");
		var s = Copy("s");
		var document = new C4Document { DiagramTitle = Copy("Banking") };
		ReadOnlySpan<C4Argument> person = [new(a), new(Copy("A")), new(Copy("A customer"))];
		ReadOnlySpan<C4Argument> boundary = [new(Copy("b")), new(Copy("B"))];
		ReadOnlySpan<C4Argument> system = [new(s), new(Copy("S")), new(Copy("tags"), Copy("v1"))];
		ReadOnlySpan<C4Argument> rel = [new(a), new(s), new(Copy("Uses"))];
		ReadOnlySpan<C4Statement> children = [new C4Element(C4ElementKind.System, List(system))];
		ReadOnlySpan<C4Statement> statements =
		[
			new C4Element(C4ElementKind.Person, List(person)),
			new C4Boundary(C4BoundaryKind.Boundary, List(boundary)) { Statements = List(children) },
			new C4Relation(C4RelationKind.Rel, List(rel)),
		];
		document.Statements.AddRange(statements);
		return document;
	}

	private static List<T> List<T>(ReadOnlySpan<T> items)
	{
		var list = new List<T>(items.Length);
		list.AddRange(items);
		return list;
	}

	private static string Copy(string text) => new(text.AsSpan());
}
