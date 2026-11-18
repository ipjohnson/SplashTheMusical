using System.Text.Json;
using SplashTheMusical.Config;
using SplashTheMusical.Diagrams;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Icons;
using SplashTheMusical.Text;

namespace SplashTheMusical.Conformance;

/// <summary>
/// The style attributes mermaid.js writes on each node, cluster and edge of the flowchart cases of corpus/styles.txt,
/// against the style text STM's flowchart layout gives the same elements.
/// </summary>
public class FlowchartStyleTests
{
	private const string SvgId = "styles";

	private static readonly MermaidValue Site = MermaidValue.FromEntries(new OrderedDictionary<string, MermaidValue>
	{
		["startOnLoad"] = MermaidValue.False,
		["securityLevel"] = MermaidValue.FromString("strict"),
	});

	private static readonly Mermaid Stm = new(new MermaidOptions { SiteConfig = Site, Limits = ResourceLimits.Unlimited });

	/// <summary>
	/// Each node's shape and label style, each cluster's rect style and colour slot, and each edge's path style and
	/// markers. The neo look prefixes a path's style with a dash array from the path's length, which is left out.
	/// </summary>
	[Test]
	public async Task EveryElementGetsTheOracleStyle()
	{
		var lines = File.ReadAllLines(Path.Combine(CorpusCheck.CorpusRoot, "styles.txt"));
		var cases = lines
			.Where(line => line.StartsWith("source\tflowchart-", StringComparison.Ordinal))
			.Select(line => line.Split('\t', 3))
			.ToList();
		var problems = new List<string>();
		var compared = 0;
		foreach (var fields in cases)
		{
			var name = fields[1];
			var oracle = lines
				.Where(line => line.StartsWith("element\t" + name + "\t", StringComparison.Ordinal))
				.Select(line => Normalize(line.Split('\t')[2..]))
				.Order(StringComparer.Ordinal)
				.ToList();
			using var json = JsonDocument.Parse(fields[2]);
			var stm = Elements(json.RootElement.GetString()!).Order(StringComparer.Ordinal).ToList();
			compared += oracle.Count;
			foreach (var missing in oracle.Except(stm))
			{
				problems.Add($"{name}: mermaid {missing}");
			}

			foreach (var extra in stm.Except(oracle))
			{
				problems.Add($"{name}: stm     {extra}");
			}
		}

		await Assert.That(string.Join('\n', problems)).IsEmpty();
		await Assert.That(cases.Count).IsEqualTo(13);
		await Assert.That(compared).IsGreaterThan(50);
	}

	// A node's DOM id ends in mermaid.js's vertexCounter, which counts every addVertex call, where STM counts nodes, so
	// nodes are compared without it.
	private static string Normalize(string[] fields)
	{
		if (fields[0] == "node")
		{
			fields[1] = WithoutCounter(fields[1]);
		}

		if (fields[0] == "edge" && fields[2].StartsWith("stroke-dasharray: ", StringComparison.Ordinal))
		{
			const string End = "stroke-dashoffset: 0;";
			fields[2] = fields[2][(fields[2].IndexOf(End, StringComparison.Ordinal) + End.Length)..];
		}

		return string.Join('\t', fields);
	}

	private static List<string> Elements(string source)
	{
		var document = (FlowchartDocument)Stm.Parse(source);
		var config = MermaidConfig.Resolve(Site, document, document.DiagramType);
		var context = new DiagramRenderContext(config, SvgId, TextMeasurer.Default, NoIcons.Instance, [], ResourceLimits.Unlimited);
		var layout = new FlowchartModule(document.DiagramType).Layout(document, context);
		var elements = new List<string>();
		void Walk(FlowchartDrawing drawing)
		{
			foreach (var cluster in drawing.Clusters)
			{
				elements.Add($"cluster\t{cluster.Id}\t{cluster.NodeStyles}\t{cluster.ColorId}");
			}

			foreach (var edge in drawing.Edges)
			{
				elements.Add($"edge\t{edge.Id}\t{edge.PathStyle}\t{Url(edge.MarkerStart)}\t{Url(edge.MarkerEnd)}");
			}

			foreach (var node in drawing.Nodes)
			{
				if (node.Subgraph is { } subgraph)
				{
					Walk(subgraph);
				}
				else if (!node.IsDummy)
				{
					elements.Add($"node\t{WithoutCounter(node.DomId)}\t{node.NodeStyles}\t{node.LabelStyles}");
				}
			}
		}

		Walk(layout.Root);
		return elements;
	}

	private static string WithoutCounter(string domId) => domId[..domId.LastIndexOf('-')];

	private static string Url(string? id) => id is null ? "" : "url(#" + id + ")";
}
