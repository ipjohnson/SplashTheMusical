using System.Globalization;
using SplashTheMusical.Diagrams.Flowchart;

namespace SplashTheMusical.Conformance;

/// <summary>Section 7's round trip for flowcharts: for every accepted case, <c>Parse(Write(Parse(text)))</c> equals <c>Parse(text)</c>.</summary>
public class FlowchartRoundTripTests
{
	[Test]
	public void EveryAcceptedFlowchartCaseRoundTrips()
	{
		var checkedCases = 0;
		var problems = new List<string>();
		foreach (var name in CorpusCheck.Cases(CorpusCheck.CorpusRoot))
		{
			if (!OracleFiles.Read(CorpusCheck.CorpusRoot, name).Accepted)
			{
				continue;
			}

			var source = CorpusComparison.Source(CorpusCheck.CorpusRoot, name);
			if (!Mermaid.Default.TryParse(source, out var parsed, out _) || parsed is not FlowchartDocument document)
			{
				continue;
			}

			checkedCases++;
			var written = Mermaid.Default.Write(document);
			if (!Mermaid.Default.TryParse(written, out var again, out var diagnostics))
			{
				problems.Add($"{name}: the written flowchart did not parse: {string.Join("; ", diagnostics)}\n{written}");
				continue;
			}

			var expected = FlowchartModel.Lines(document);
			var actual = again is FlowchartDocument flowchart ? FlowchartModel.Lines(flowchart) : [$"a {again?.DiagramType} document"];
			var difference = FlowchartModel.FirstDifference(expected, actual);
			if (difference is not null)
			{
				problems.Add($"{name}: {difference}\n{written}");
			}
		}

		Verify(!problems.Any(), $"{string.Join("\n\n", problems)}");
		Verify(checkedCases == 874, $"was {checkedCases}");
	}
}

/// <summary>Every value of a flowchart document as lines, so two documents compare line by line.</summary>
internal static class FlowchartModel
{
	public static List<string> Lines(FlowchartDocument document)
	{
		var lines = new List<string>
		{
			$"type {document.DiagramType}",
			$"keyword {document.Keyword}",
			$"direction {document.Direction}",
			$"title {document.Title}",
			$"displayMode {document.DisplayMode}",
			$"config {Value(document.FrontMatterConfig)}",
			$"accTitle {document.AccessibilityTitle}",
			$"accDescr {document.AccessibilityDescription}",
			$"defaultEdgeStyles {List(document.DefaultEdgeStyles)}",
			$"defaultInterpolate {document.DefaultInterpolate}",
		};
		foreach (var directive in document.Directives)
		{
			lines.Add($"directive {directive.Type} {Value(directive.Arguments)} {directive.InvalidArguments}");
		}

		foreach (var node in document.Nodes)
		{
			lines.Add(
				$"node {node.Id} label={node.Label} labelType={node.LabelType} shape={node.Shape} styles={List(node.Styles)} classes={List(node.Classes)} "
				+ $"property={node.Property} link={node.Link} target={node.LinkTarget} callback={node.Callback} args={node.CallbackArguments} "
				+ $"tooltip={node.Tooltip} icon={node.Icon} form={node.Form} pos={node.LabelPosition} img={node.Image} constraint={node.Constraint} "
				+ $"w={Number(node.AssetWidth)} h={Number(node.AssetHeight)}");
		}

		foreach (var edge in document.Edges)
		{
			lines.Add(
				$"edge {edge.Id} {edge.Start}->{edge.End} user={edge.IsUserDefinedId} type={edge.Type} stroke={edge.Stroke} length={edge.Length} "
				+ $"label={edge.Label} labelType={edge.LabelType} interpolate={edge.Interpolate} styles={List(edge.Styles)} classes={List(edge.Classes)} "
				+ $"animate={edge.Animate} animation={edge.Animation} step={edge.Step}");
		}

		foreach (var subgraph in document.Subgraphs)
		{
			lines.Add(
				$"subgraph {subgraph.Id} title={subgraph.Title} labelType={subgraph.LabelType} nodes={List(subgraph.Nodes)} direction={subgraph.Direction} "
				+ $"classes={List(subgraph.Classes)} metadata={Value(subgraph.Metadata)}");
		}

		foreach (var definition in document.Classes)
		{
			lines.Add($"class {definition.Id} styles={List(definition.Styles)} text={List(definition.TextStyles)}");
		}

		return lines;
	}

	public static string? FirstDifference(List<string> expected, List<string> actual)
	{
		for (var i = 0; i < Math.Max(expected.Count, actual.Count); i++)
		{
			var e = i < expected.Count ? expected[i] : "(none)";
			var a = i < actual.Count ? actual[i] : "(none)";
			if (e != a)
			{
				return $"expected {e}\n  actual {a}";
			}
		}

		return null;
	}

	private static string List(List<string>? items) => items is null ? "null" : "[" + string.Join("|", items) + "]";

	private static string Number(double? value) => value?.ToString("R", CultureInfo.InvariantCulture) ?? "";

	private static string Value(MermaidValue? value) => value is null ? "null" : value.ToString() ?? "";
}
