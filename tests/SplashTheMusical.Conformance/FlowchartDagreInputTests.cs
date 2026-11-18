using System.Globalization;
using SplashTheMusical.Config;
using SplashTheMusical.Diagrams;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Icons;
using SplashTheMusical.Layout.Dagre;
using SplashTheMusical.Text;

namespace SplashTheMusical.Conformance;

/// <summary>
/// The flowchart layout against mermaid.js: the graphs STM hands dagre for each flowchart case against the graphs
/// mermaid.js handed dagre, recorded in <c>corpus/flowchart/*.dagre.txt</c>. With the replay tests, which hold the port to
/// dagre's output on the recorded graphs, a case that passes here is laid out as mermaid.js lays it out with dagre.
/// The cases that still differ are listed in <c>Fixtures/flowchart-dagre-inputs.txt</c> under the issue that tracks them.
/// When <c>STM_DIFF_FILE</c> is set, every difference of every case is written to that file.
/// </summary>
public class FlowchartDagreInputTests
{
	/// <summary>How far a size may be off, in user units.</summary>
	private const double Tolerance = 0.01;

	/// <summary>The fewest cases whose graphs must equal mermaid.js's, so the comparison cannot pass by comparing nothing.</summary>
	private const int MinimumSame = 700;

	private static readonly Mermaid Stm = new(new MermaidOptions { SiteConfig = StmFactsProvider.OracleSite, Limits = ResourceLimits.Unlimited });

	private static string ListPath => Path.Combine(CorpusCheck.RepositoryRoot, "tests", "SplashTheMusical.Conformance", "Fixtures", "flowchart-dagre-inputs.txt");

	/// <summary>
	/// Every flowchart case's graphs equal the recorded ones, except the listed cases, which must still differ. The
	/// hand-drawn look is left out, because mermaid sizes its nodes from roughjs's random strokes, and the plan holds that
	/// look to L2.
	/// </summary>
	[Test]
	public async Task StmHandsDagreTheGraphsMermaidDid()
	{
		var listed = Listed();
		var unlisted = new List<string>();
		var same = new List<string>();
		var compared = 0;
		var all = new List<string>();
		foreach (var file in DagreRecords.Files(CorpusCheck.CorpusRoot).Where(file => file.StartsWith("flowchart/", StringComparison.Ordinal)))
		{
			var name = file[..^".dagre.txt".Length];
			var facts = OracleFiles.Read(CorpusCheck.CorpusRoot, name);
			if (facts.Facts.Contains("config\tlook\t\"handDrawn\""))
			{
				continue;
			}

			compared++;
			var source = CorpusComparison.Source(CorpusCheck.CorpusRoot, name);
			var expected = DagreRecords.Read(Path.Combine(CorpusCheck.CorpusRoot, file)).Select(record => record.Input).ToList();
			var differences = Differences(source, expected);
			var caseName = name["flowchart/".Length..];
			all.AddRange(differences.Select(difference => caseName + ": " + difference));
			if (differences.Count == 0)
			{
				if (listed.ContainsKey(caseName))
				{
					same.Add(caseName + ", listed under " + listed[caseName] + ", no longer differs");
				}
			}
			else if (!listed.Remove(caseName))
			{
				unlisted.Add(caseName + ": " + string.Join(" | ", differences.Take(4)));
			}
		}

		var diffFile = Environment.GetEnvironmentVariable("STM_DIFF_FILE");
		if (!string.IsNullOrEmpty(diffFile))
		{
			File.WriteAllLines(diffFile, all);
		}

		var stale = same.Concat(listed.Keys.Where(name => !File.Exists(Path.Combine(CorpusCheck.CorpusRoot, "flowchart", name + ".dagre.txt"))).Select(name => name + " is not a flowchart case with a dagre record"));
		await Assert.That(string.Join("\n", unlisted)).IsEqualTo("");
		await Assert.That(string.Join("\n", stale)).IsEqualTo("");
		await Assert.That(compared - Listed().Count).IsGreaterThanOrEqualTo(MinimumSame);
	}

	[Test]
	public async Task AGraphEqualsItselfAndNotAGraphWithAnotherNodeSize()
	{
		var graph = new DagreInput(
			new DagreOptions("LR", 50, null, 50, 8, 8),
			[new DagreNodeInput("A", null, 152, 45), new DagreNodeInput("B", null, 152, 45)],
			[new DagreEdgeInput("A", "B", "L_A_B_0", LabelPos: "c")]);
		var wider = graph with { Nodes = [graph.Nodes[0], graph.Nodes[1] with { Width = 152.5 }] };

		await Assert.That(string.Join("\n", Compare([graph], [graph with { Options = graph.Options with { EdgeSep = 20 } }]))).IsEqualTo("");
		await Assert.That(string.Join("\n", Compare([graph], [wider]))).IsEqualTo("node-size #0 B 152.5x45, mermaid 152x45");
	}

	/// <summary>The cases of the list, each with its issue line.</summary>
	private static Dictionary<string, string> Listed()
	{
		var listed = new Dictionary<string, string>(StringComparer.Ordinal);
		var category = "";
		foreach (var line in File.ReadLines(ListPath))
		{
			if (line.Length == 0 || line[0] == '#')
			{
				continue;
			}

			if (line.Contains(' ', StringComparison.Ordinal))
			{
				category = line;
			}
			else
			{
				listed.Add(line, category);
			}
		}

		return listed;
	}

	/// <summary>How the graphs of each call to dagre STM makes for the source differ from the recorded ones.</summary>
	private static List<string> Differences(string source, List<DagreInput> expected)
	{
		if (!Stm.TryParse(source, out var document, out _) || document is not FlowchartDocument flowchart)
		{
			return ["refused"];
		}

		var config = MermaidConfig.Resolve(StmFactsProvider.OracleSite, flowchart, flowchart.DiagramType);
		var context = new DiagramRenderContext(config, "mermaid", TextMeasurer.Default, NoIcons.Instance, [], ResourceLimits.Unlimited);
		var layout = new FlowchartModule(flowchart.DiagramType).Layout(flowchart, context);
		return Compare(expected, layout.DagreInputs);
	}

	private static List<string> Compare(IReadOnlyList<DagreInput> expected, IReadOnlyList<DagreInput> actual)
	{
		var differences = new List<string>();
		if (actual.Count != expected.Count)
		{
			differences.Add($"layouts {actual.Count}, mermaid {expected.Count}");
			return differences;
		}

		for (var i = 0; i < actual.Count; i++)
		{
			Compare(i, expected[i], actual[i], differences);
		}

		return differences;
	}

	private static void Compare(int index, DagreInput expected, DagreInput actual, List<string> differences)
	{
		if (Defaulted(expected.Options) != Defaulted(actual.Options))
		{
			differences.Add($"options #{index} {Defaulted(actual.Options)}, mermaid {Defaulted(expected.Options)}");
		}

		var nodes = actual.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
		foreach (var node in expected.Nodes)
		{
			if (!nodes.Remove(node.Id, out var found))
			{
				differences.Add($"node-missing #{index} {node.Id}");
				continue;
			}

			if (found.Parent != node.Parent)
			{
				differences.Add($"node-parent #{index} {node.Id} {found.Parent}, mermaid {node.Parent}");
			}

			if (!Near(found.Width, node.Width) || !Near(found.Height, node.Height))
			{
				differences.Add(string.Create(CultureInfo.InvariantCulture, $"node-size #{index} {node.Id} {found.Width:0.###}x{found.Height:0.###}, mermaid {node.Width:0.###}x{node.Height:0.###}"));
			}
		}

		differences.AddRange(nodes.Keys.Select(id => $"node-extra #{index} {id}"));
		var edges = actual.Edges.ToDictionary(edge => (edge.V, edge.W, edge.Name));
		foreach (var edge in expected.Edges)
		{
			if (!edges.Remove((edge.V, edge.W, edge.Name), out var found))
			{
				differences.Add($"edge-missing #{index} {edge.V}->{edge.W} {edge.Name}");
				continue;
			}

			if (!Near(found.Width, edge.Width) || !Near(found.Height, edge.Height))
			{
				differences.Add(string.Create(CultureInfo.InvariantCulture, $"edge-size #{index} {edge.V}->{edge.W} {found.Width:0.###}x{found.Height:0.###}, mermaid {edge.Width:0.###}x{edge.Height:0.###}"));
			}

			if (found.MinLen != edge.MinLen || found.Weight != edge.Weight || found.LabelOffset != edge.LabelOffset || found.LabelPos != edge.LabelPos)
			{
				differences.Add($"edge-attributes #{index} {found}, mermaid {edge}");
			}
		}

		differences.AddRange(edges.Keys.Select(key => $"edge-extra #{index} {key.V}->{key.W} {key.Name}"));
	}

	/// <summary>The options with dagre's defaults where they are unset, as the oracle writes them.</summary>
	private static DagreOptions Defaulted(DagreOptions options) => options with
	{
		RankDir = options.RankDir ?? "tb",
		NodeSep = options.NodeSep ?? 50,
		EdgeSep = options.EdgeSep ?? 20,
		RankSep = options.RankSep ?? 50,
	};

	private static bool Near(double actual, double expected) => Math.Abs(actual - expected) <= Tolerance || (double.IsNaN(actual) && double.IsNaN(expected));
}
