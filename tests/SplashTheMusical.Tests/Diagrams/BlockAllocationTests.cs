using AwesomeAssertions;
using SplashTheMusical.Diagrams;
using SplashTheMusical.Diagrams.Block;
using SplashTheMusical.Tests.Generation;

namespace SplashTheMusical.Tests.Diagrams;

/// <summary>
/// The block parser rents its text, tokens and tables from the array pool, so a parse allocates only the document it
/// returns. <see cref="AssemblyReferenceTests.CoreDoesNotReferenceRegularExpressions"/> holds the parser to no Regex.
/// </summary>
public class BlockAllocationTests
{
	private const string Source = "block\n  columns 2\n  a[\"A\"] b:2\n  a --> b\n  space\n  style a fill:#f9f\n";

	private static object? Sink;

	// Tests running beside it share the array pool's per-core arrays, so a rent could allocate.
	[Test]
	[NotInParallel]
	public void ParsingAllocatesOnlyTheModel()
	{
		var context = new DiagramParseContext("block", 0, ResourceLimits.Default, []);
		BlockDocument? parsed = null;
		var parse = Allocations.Measure(() => parsed = BlockParser.Parse(Source, context));
		var model = Allocations.Measure(() => Sink = Model());

		DocumentDump.Lines(parsed!).Should().Equal(DocumentDump.Lines((BlockDocument)Sink!));
		context.Diagnostics.Should().BeEmpty();
		parse.Should().Be(model);
	}

	/// <summary>The document <see cref="Source"/> parses to, built with the strings and lists the parser makes.</summary>
	private static BlockDocument Model()
	{
		var a = Copy("a");
		var b = Copy("b");
		var nodes = new List<BlockNode>(2);
		ReadOnlySpan<BlockNode> ends = [new BlockNode(a), new BlockNode(b)];
		nodes.AddRange(ends);
		var links = new List<BlockLink>(1);
		ReadOnlySpan<BlockLink> link = [new BlockLink()];
		links.AddRange(link);
		var styles = new List<string>(1);
		ReadOnlySpan<string> style = [Copy("fill:#f9f")];
		styles.AddRange(style);
		var document = new BlockDocument();
		ReadOnlySpan<BlockStatement> statements =
		[
			new BlockColumns(2),
			new BlockNode(a) { Label = Copy("A"), Shape = BlockShape.Square },
			new BlockNode(b) { Width = 2 },
			new BlockEdge(nodes, links),
			new BlockSpace(),
			new BlockStyle(a, styles),
		];
		document.Statements.AddRange(statements);
		return document;
	}

	private static string Copy(string text) => new(text.AsSpan());
}
