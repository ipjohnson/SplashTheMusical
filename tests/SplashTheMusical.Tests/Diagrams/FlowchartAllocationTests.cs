using SplashTheMusical.Diagrams;
using SplashTheMusical.Diagrams.Flowchart;

namespace SplashTheMusical.Tests.Diagrams;

/// <summary>
/// The flowchart parser rents its text, tokens and tables from the array pool, so a parse allocates only the document
/// it returns. <see cref="AssemblyReferenceTests.CoreDoesNotReferenceRegularExpressions"/> holds the parser to no Regex.
/// </summary>
public class FlowchartAllocationTests
{
	private const string Source = "flowchart LR\n  subgraph S [Group]\n    A[Start] -->|go| B\n  end\n";

	private static object? Sink;

	// Tests running beside it share the array pool's per-core arrays, so a rent could allocate.
	[Test]
	[NotInParallel]
	public void ParsingAllocatesOnlyTheModel()
	{
		var context = new DiagramParseContext(FlowchartModule.FlowchartV2, 0, ResourceLimits.Default, []);
		FlowchartDocument? parsed = null;
		var parse = Allocations.Measure(() => parsed = FlowchartParser.Parse(Source, context));
		var model = Allocations.Measure(() => Sink = Model());

		Verify(Describe(parsed!) == Describe((FlowchartDocument)Sink!));
		Verify(!context.Diagnostics.Any());
		Verify(parse == model);
	}

	/// <summary>The document <see cref="Source"/> parses to, built with the strings and lists the parser makes.</summary>
	private static FlowchartDocument Model()
	{
		var a = new FlowchartNode(Copy("A")) { Label = Copy("Start"), Shape = "square" };
		var b = new FlowchartNode(Copy("B"));
		var edge = new FlowchartEdge(a.Id, b.Id, Copy("L_A_B_0")) { Label = Copy("go") };
		var subgraph = new FlowchartSubgraph(Copy("S"), Copy("Group"));
		subgraph.Nodes.Add(b.Id);
		subgraph.Nodes.Add(a.Id);
		var document = new FlowchartDocument(FlowchartModule.FlowchartV2) { Keyword = Copy("flowchart"), Direction = "LR" };
		ReadOnlySpan<FlowchartNode> nodes = [a, b];
		ReadOnlySpan<FlowchartEdge> edges = [edge];
		ReadOnlySpan<FlowchartSubgraph> subgraphs = [subgraph];
		document.Nodes.AddRange(nodes);
		document.Edges.AddRange(edges);
		document.Subgraphs.AddRange(subgraphs);
		return document;
	}

	private static string Copy(string text) => new(text.AsSpan());

	private static string Describe(FlowchartDocument document) =>
		string.Join('\n', [
			$"{document.Keyword} {document.Direction}",
			.. document.Nodes.Select(n => $"node {n.Id} {n.Label} {n.Shape}"),
			.. document.Edges.Select(e => $"edge {e.Id} {e.Start} {e.End} {e.Label}"),
			.. document.Subgraphs.Select(s => $"subgraph {s.Id} {s.Title} {string.Join(',', s.Nodes)}"),
		]);
}
