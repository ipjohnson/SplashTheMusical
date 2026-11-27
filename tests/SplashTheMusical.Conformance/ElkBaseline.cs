using System.Globalization;
using System.Text;

namespace SplashTheMusical.Conformance;

/// <summary>An engine's score on one corpus case, over every ELK call the case records.</summary>
/// <param name="Case">The case's path in <c>corpus/</c>, without its extension.</param>
/// <param name="ElkLayers">The most layers of any of the case's calls in ELK's layout, read as the comparer reads them.</param>
/// <param name="Error">The message of the first exception the engine threw, or null.</param>
internal sealed record CaseScore(string Case, string Type, ElkLayoutScore Score, int ElkLayers, string? Error = null);

/// <summary>
/// The baseline of ipjohnson/SplashTheMusical#203: how often the dagre port and Mermaider's <c>Sugiyama</c> engine,
/// run on the graphs mermaid.js gives ELK, pass each measure of <see cref="ElkLayoutComparer"/>.
/// </summary>
internal static class ElkBaseline
{
	/// <summary>The environment variable that rewrites the committed baseline when it is set to any value.</summary>
	public const string WriteVariable = "STM_WRITE_BASELINE";

	public static string FilePath => Path.Combine(CorpusCheck.RepositoryRoot, "docs", "layered-layout", "baseline.md");

	/// <summary>The scores of an engine on every case of the corpus with an ELK file, in ordinal order of the files.</summary>
	/// <param name="layout">Lays out a recorded input graph. It is given the graph, the case and the diagram type.</param>
	public static List<CaseScore> Run(string corpus, Func<ElkInputGraph, string, string, CandidateLayout> layout)
	{
		var scores = new List<CaseScore>();
		foreach (var file in ElkRecords.Files(corpus))
		{
			var name = file[..^".elk.txt".Length];
			var type = file[..file.IndexOf('/', StringComparison.Ordinal)];
			scores.Add(Score(name, type, ElkRecords.Read(Path.Combine(corpus, file)), layout));
		}

		return scores;
	}

	/// <summary>The dagre port with the graph options of the case's <c>.dagre.txt</c>, or mermaid's flowchart options when it has none.</summary>
	public static CandidateLayout Dagre(string corpus, ElkInputGraph graph, string name)
	{
		var path = Path.Combine(corpus, name + ".dagre.txt");
		var options = File.Exists(path) && DagreRecords.Read(path) is [var first, ..] ? first.Input.Options : DagreOnElkInput.FlowchartDefaults;
		return DagreOnElkInput.Layout(graph, options);
	}

	public static string Render(IReadOnlyList<CaseScore> dagre, IReadOnlyList<CaseScore> sugiyama, string mermaid)
	{
		var text = new StringBuilder();
		text.Append("# Layered layout baseline\n\n");
		text.Append(CultureInfo.InvariantCulture, $"How close the dagre port and Mermaider's `Sugiyama` engine come to the layouts ELK gives mermaid.js {mermaid}, on the\n");
		text.Append(CultureInfo.InvariantCulture, $"{Count(dagre.Count)} corpus cases with an `.elk.txt` recording. Each engine lays out the graph mermaid.js gave ELK, and\n");
		text.Append("`ElkLayoutComparer` in `tests/SplashTheMusical.Conformance` scores its layout against the one ELK returned.\n");
		text.Append("`mise run baseline` writes this file, and `ElkBaselineTests` fails when it is out of date.\n\n");
		text.Append("This is the starting point of the layered layout engine of ipjohnson/SplashTheMusical#25, which is written under\n");
		text.Append("[the clean-room rules](clean-room.md).\n\n");

		text.Append("## Measures\n\n");
		text.Append("ELK gives no layers, so they are read from its coordinates, and from an engine's in the same way. Along the layout\n");
		text.Append("direction, a layer is a maximal run of leaf nodes whose extents overlap. A layer's order is its leaves' order across\n");
		text.Append("the layout direction, by their centres. A case passes a measure when every ELK call it records does.\n\n");
		text.Append(CultureInfo.InvariantCulture, $"{Count(dagre.Count(c => c.ElkLayers > 1))} of the {Count(dagre.Count)} cases have more than one layer in ELK's layout. The coordinates are\n");
		text.Append("the canvas's, so the boxes measure also holds an engine to ELK's padding around the drawing.\n\n");
		text.Append("- **Same layers**: every leaf node is in the layer ELK put it in.\n");
		text.Append("- **Same order**: the leaves of each of ELK's layers are in ELK's order.\n");
		text.Append(CultureInfo.InvariantCulture, $"- **Boxes and canvas**: the box of every node, clusters included, overlaps ELK's by at least {ElkLayoutComparer.MinOverlap * 100:0}% of the larger\n");
		text.Append(CultureInfo.InvariantCulture, $"  box, and the canvas's width and height are each within {ElkLayoutComparer.CanvasTolerance * 100:0}% of ELK's.\n");
		text.Append("- **Same bends**: every edge turns as many times as ELK's. Repeated points and points on a straight run are not bends.\n\n");

		text.Append("## The dagre port\n\n");
		text.Append("`src/SplashTheMusical.Layout/Dagre`, given the graph options of the case's `.dagre.txt` with ELK's direction, or\n");
		text.Append("mermaid's flowchart options when the case has none. Clusters are compound nodes, and an edge that ends on a cluster\n");
		text.Append("ends on its first leaf. The edge label's size is the edge's.\n\n");
		Table(text, dagre);

		text.Append("## Mermaider's Sugiyama\n\n");
		text.Append(CultureInfo.InvariantCulture, $"The `Sugiyama` package {SugiyamaOnElkInput.PackageVersion}, built from `src/Sugiyama` of mermaider at commit\n");
		text.Append(CultureInfo.InvariantCulture, $"[`{SugiyamaOnElkInput.Commit[..8]}`](https://github.com/ipjohnson/mermaider/commit/{SugiyamaOnElkInput.Commit}). It is a package\n");
		text.Append("reference of `tests/SplashTheMusical.Conformance` only. Each type gets the entry point and options Mermaider's layout\n");
		text.Append("engine for it passes: `SugiyamaLayout.Compute` with the ER options for ER, and `HierarchicalLayout.Compute` with the\n");
		text.Append("class, requirement, state or flowchart options for the rest. Clusters are subgraphs, and an edge that ends on a cluster\n");
		text.Append("ends on its first leaf with the cluster as its group.\n\n");
		Table(text, sugiyama);

		text.Append("## Sugiyama against the dagre port\n\n");
		var more = new List<string>();
		var fewer = new List<string>();
		var same = new List<string>();
		foreach (var (name, passes) in Measures)
		{
			var d = dagre.Count(c => passes(c.Score));
			var s = sugiyama.Count(c => passes(c.Score));
			var line = string.Create(CultureInfo.InvariantCulture, $"{name.ToLowerInvariant()} ({Count(s)} {(s == 1 ? "case" : "cases")} against {Count(d)})");
			(s > d ? more : s < d ? fewer : same).Add(line);
		}

		text.Append("Of all recorded cases, `Sugiyama` passes ");
		text.Append(more.Count == 0 ? "no measure more often than the dagre port" : $"{Join(more)} more often than the dagre port");
		text.Append(fewer.Count == 0 ? ", and none less often" : $", and {Join(fewer)} less often");
		text.Append(same.Count == 0 ? ".\n\n" : $". It passes {Join(same)} as often.\n\n");
		text.Append("The cases `Sugiyama` passes minus the cases the dagre port passes:\n\n");
		text.Append("| Type |");
		foreach (var (name, _) in Measures)
		{
			text.Append(' ').Append(name).Append(" |");
		}

		text.Append("\n|---|");
		text.Append(string.Concat(Enumerable.Repeat("---:|", Measures.Length)));
		text.Append('\n');
		foreach (var type in Types(dagre).Append(null))
		{
			text.Append(type is null ? "| **Total** |" : $"| {type} |");
			foreach (var (_, passes) in Measures)
			{
				var d = dagre.Count(c => (type is null || c.Type == type) && passes(c.Score));
				var s = sugiyama.Count(c => (type is null || c.Type == type) && passes(c.Score));
				text.Append(' ').Append((s - d).ToString("+#,0;-#,0;0", CultureInfo.InvariantCulture)).Append(" |");
			}

			text.Append('\n');
		}

		text.Append("\n## Cases an engine could not lay out\n\n");
		text.Append("A case whose layout throws fails every measure.\n\n");
		text.Append("| Engine | Cases |\n|---|---:|\n");
		text.Append(CultureInfo.InvariantCulture, $"| dagre port | {Count(dagre.Count(c => c.Error is not null))} |\n");
		text.Append(CultureInfo.InvariantCulture, $"| Sugiyama | {Count(sugiyama.Count(c => c.Error is not null))} |\n");
		return text.ToString();
	}

	private static readonly (string Name, Func<ElkLayoutScore, bool> Passes)[] Measures =
	[
		("Same layers", s => s.SameLayers),
		("Same order", s => s.SameOrder),
		("Boxes and canvas", s => s.BoxesOverlap),
		("Same bends", s => s.SameBends),
		("All four", s => s.All),
	];

	private static CaseScore Score(string name, string type, IReadOnlyList<ElkRecord> records, Func<ElkInputGraph, string, string, CandidateLayout> layout)
	{
		var scores = new List<ElkLayoutScore>();
		var layers = 0;
		string? error = null;
		foreach (var record in records)
		{
			var graph = new ElkInputGraph(record.Input);
			var elk = ElkLayoutComparer.ElkLayout(record, graph);
			layers = Math.Max(layers, ElkLayoutComparer.Layers(graph, elk).Count);
			try
			{
				scores.Add(ElkLayoutComparer.Score(graph, elk, layout(graph, name, type)));
			}
			catch (Exception exception)
			{
				error ??= $"{exception.GetType().Name}: {exception.Message}";
				scores.Add(ElkLayoutScore.Failed(error));
			}
		}

		return new CaseScore(
			name,
			type,
			new ElkLayoutScore(
				scores.Select(s => s.LayerFailure).FirstOrDefault(f => f is not null),
				scores.Select(s => s.OrderFailure).FirstOrDefault(f => f is not null),
				scores.Select(s => s.BoxFailure).FirstOrDefault(f => f is not null),
				scores.Select(s => s.BendFailure).FirstOrDefault(f => f is not null)),
			layers,
			error);
	}

	private static void Table(StringBuilder text, IReadOnlyList<CaseScore> scores)
	{
		text.Append("| Type | Cases |");
		foreach (var (name, _) in Measures)
		{
			text.Append(' ').Append(name).Append(" |");
		}

		text.Append("\n|---|---:|");
		text.Append(string.Concat(Enumerable.Repeat("---:|", Measures.Length)));
		text.Append('\n');
		foreach (var type in Types(scores).Append(null))
		{
			var cases = scores.Where(c => type is null || c.Type == type).ToList();
			text.Append(type is null ? "| **Total** |" : $"| {type} |").Append(' ').Append(Count(cases.Count)).Append(" |");
			foreach (var (_, passes) in Measures)
			{
				text.Append(' ').Append(Rate(cases.Count(c => passes(c.Score)), cases.Count)).Append(" |");
			}

			text.Append('\n');
		}

		text.Append('\n');
	}

	private static IEnumerable<string?> Types(IReadOnlyList<CaseScore> scores) =>
		scores.Select(c => c.Type).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);

	private static string Join(List<string> items) =>
		items.Count == 1 ? items[0] : string.Join(", ", items.Take(items.Count - 1)) + " and " + items[^1];

	private static string Rate(int passed, int applies) =>
		applies == 0
			? "n/a"
			: string.Create(CultureInfo.InvariantCulture, $"{100.0 * passed / applies:0.0}% ({Count(passed)} of {Count(applies)})");

	private static string Count(int count) => count.ToString("#,0", CultureInfo.InvariantCulture);
}
