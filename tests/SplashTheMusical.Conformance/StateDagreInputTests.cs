using SplashTheMusical.Config;
using SplashTheMusical.Diagrams;
using SplashTheMusical.Diagrams.State;
using SplashTheMusical.Icons;
using SplashTheMusical.Layout.Dagre;
using SplashTheMusical.Text;

namespace SplashTheMusical.Conformance;

/// <summary>
/// The state layout against mermaid.js: the graphs STM hands dagre for each state case against the graphs mermaid.js
/// handed dagre when the oracle drew the case a second time with <c>layout: dagre</c>, recorded in
/// <c>corpus/state/*.dagre.txt</c>. The cases that still differ are listed in <c>Fixtures/state-dagre-inputs.txt</c>
/// under the issue that tracks them. When <c>STM_DIFF_FILE</c> is set, every difference of every case is written to that
/// file.
/// </summary>
public class StateDagreInputTests
{
	/// <summary>The fewest cases whose graphs must equal mermaid.js's, so the comparison cannot pass by comparing nothing.</summary>
	private const int MinimumSame = 150;

	private static readonly Mermaid Stm = new(new MermaidOptions { SiteConfig = StmFactsProvider.OracleSite, Limits = ResourceLimits.Unlimited });

	private static string ListPath => Path.Combine(CorpusCheck.RepositoryRoot, "tests", "SplashTheMusical.Conformance", "Fixtures", "state-dagre-inputs.txt");

	/// <summary>
	/// Every state case's graphs equal the recorded ones, except the listed cases, which must still differ. The
	/// hand-drawn look is left out, as for flowchart, because roughjs's random strokes size its nodes.
	/// </summary>
	[Test]
	public void StmHandsDagreTheGraphsMermaidDid()
	{
		var listed = Listed();
		var unlisted = new List<string>();
		var same = new List<string>();
		var compared = 0;
		var all = new List<string>();
		foreach (var file in DagreRecords.Files(CorpusCheck.CorpusRoot).Where(file => file.StartsWith("state/", StringComparison.Ordinal)))
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
			var caseName = name["state/".Length..];
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

		var stale = same.Concat(listed.Keys.Where(name => !File.Exists(Path.Combine(CorpusCheck.CorpusRoot, "state", name + ".dagre.txt"))).Select(name => name + " is not a state case with a dagre record"));
		Verify(!unlisted.Any(), $"{string.Join("\n", unlisted)}");
		Verify(!stale.Any(), $"{string.Join("\n", stale)}");
		Verify(() => compared - Listed().Count >= MinimumSame);
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

	private static List<string> Differences(string source, List<DagreInput> expected)
	{
		if (!Stm.TryParse(source, out var document, out _) || document is not StateDocument state)
		{
			return ["refused"];
		}

		var config = MermaidConfig.Resolve(StmFactsProvider.OracleSite, state, state.DiagramType);
		var context = new DiagramRenderContext(config, "mermaid", TextMeasurer.Default, NoIcons.Instance, [], ResourceLimits.Unlimited);
		var layout = new StateModule().Layout(state, context);
		return FlowchartDagreInputTests.Compare(Generated(expected), Generated(layout.DagreInputs));
	}

	[Test]
	public void AGeneratedRegionIdIsNamedByItsFirstAppearance()
	{
		var graph = new DagreInput(
			new DagreOptions("TB", 50, null, 50, 8, 8),
			[new DagreNodeInput("id-abc123-7", null, 10, 10), new DagreNodeInput("id-abc123-7_start", "id-abc123-7", 14, 14), new DagreNodeInput("id-x", null, 1, 1)],
			[new DagreEdgeInput("id-abc123-7_start", "id-x", "edge1")]);

		var named = Generated([graph])[0];

		Verify(() => string.Join(",", named.Nodes.Select(n => n.Id + "<" + n.Parent)) == "id-generated-1<,id-generated-1_start<id-generated-1,id-x<");
		Verify(() => named.Edges[0].V == "id-generated-1_start");
	}

	/// <summary>
	/// The graphs with each last region's <c>generateId</c> id, <c>id-&lt;random&gt;-N</c>, named
	/// <c>id-generated-K</c> by the order the regions first appear, here and in the states named after them, because
	/// mermaid.js's N counts across the page and STM's does not.
	/// </summary>
	private static List<DagreInput> Generated(IReadOnlyList<DagreInput> inputs)
	{
		var names = new Dictionary<string, string>(StringComparer.Ordinal);
		string Name(string id)
		{
			var end = GeneratedEnd(id);
			if (end < 0)
			{
				return id;
			}

			var stem = id[..end];
			if (!names.TryGetValue(stem, out var name))
			{
				names[stem] = name = "id-generated-" + (names.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
			}

			return name + id[end..];
		}

		return
		[
			.. inputs.Select(input => input with
			{
				Nodes = [.. input.Nodes.Select(node => node with { Id = Name(node.Id), Parent = node.Parent is null ? null : Name(node.Parent) })],
				Edges = [.. input.Edges.Select(edge => edge with { V = Name(edge.V), W = Name(edge.W) })],
			}),
		];
	}

	/// <summary>The length of the <c>id-&lt;letters and digits&gt;-&lt;digits&gt;</c> the id starts with, when the id ends there or a <c>_</c> follows, or -1.</summary>
	private static int GeneratedEnd(string id)
	{
		if (!id.StartsWith("id-", StringComparison.Ordinal))
		{
			return -1;
		}

		var i = 3;
		while (i < id.Length && (char.IsAsciiLetterLower(id[i]) || char.IsAsciiDigit(id[i])))
		{
			i++;
		}

		if (i == 3 || i >= id.Length || id[i] != '-')
		{
			return -1;
		}

		var digits = ++i;
		while (i < id.Length && char.IsAsciiDigit(id[i]))
		{
			i++;
		}

		return i > digits && (i == id.Length || id[i] == '_') ? i : -1;
	}
}
