using SplashTheMusical.Config;
using SplashTheMusical.Diagrams;
using SplashTheMusical.Diagrams.Er;
using SplashTheMusical.Icons;
using SplashTheMusical.Layout.Dagre;
using SplashTheMusical.Text;

namespace SplashTheMusical.Conformance;

/// <summary>
/// The ER layout against mermaid.js: the graphs STM hands dagre for each ER case against the graphs mermaid.js handed
/// dagre, recorded in <c>corpus/er/*.dagre.txt</c>. A case that mermaid.js draws with ELK records the graph it drew a
/// second time with <c>layout: dagre</c>, so this test shows that the layout engine is all that differs for it. The
/// cases that still differ are listed in <c>Fixtures/er-dagre-inputs.txt</c> under the issue that tracks them. When
/// <c>STM_DIFF_FILE</c> is set, every difference of every case is written to that file.
/// </summary>
public class ErDagreInputTests
{
	/// <summary>The fewest cases whose graphs must equal mermaid.js's, so the comparison cannot pass by comparing nothing.</summary>
	private const int MinimumSame = 250;

	private static readonly Mermaid Stm = new(new MermaidOptions { SiteConfig = StmFactsProvider.OracleSite, Limits = ResourceLimits.Unlimited });

	private static string ListPath => Path.Combine(CorpusCheck.RepositoryRoot, "tests", "SplashTheMusical.Conformance", "Fixtures", "er-dagre-inputs.txt");

	/// <summary>
	/// Every ER case's graphs equal the recorded ones, except the listed cases, which must still differ. The hand-drawn
	/// look is left out, as for flowchart, because roughjs's random strokes size its nodes. A case that sets
	/// <c>layout: elk</c> records no dagre graph, so it has nothing to compare.
	/// </summary>
	[Test]
	public void StmHandsDagreTheGraphsMermaidDid()
	{
		var listed = Listed();
		var unlisted = new List<string>();
		var same = new List<string>();
		var compared = 0;
		var all = new List<string>();
		foreach (var file in DagreRecords.Files(CorpusCheck.CorpusRoot).Where(file => file.StartsWith("er/", StringComparison.Ordinal)))
		{
			var name = file[..^".dagre.txt".Length];
			var facts = OracleFiles.Read(CorpusCheck.CorpusRoot, name);
			if (facts.Facts.Contains("config\tlook\t\"handDrawn\""))
			{
				continue;
			}

			var expected = DagreRecords.Read(Path.Combine(CorpusCheck.CorpusRoot, file)).Select(record => record.Input).ToList();
			if (expected.Count == 0)
			{
				continue;
			}

			compared++;
			var source = CorpusComparison.Source(CorpusCheck.CorpusRoot, name);
			var differences = Differences(source, expected);
			var caseName = name["er/".Length..];
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

		var stale = same.Concat(listed.Keys.Select(name => name + " is not an ER case with a dagre record"));
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
		if (!Stm.TryParse(source, out var document, out _) || document is not ErDocument diagram)
		{
			return ["refused"];
		}

		var config = MermaidConfig.Resolve(StmFactsProvider.OracleSite, diagram, diagram.DiagramType);
		var context = new DiagramRenderContext(config, "mermaid", TextMeasurer.Default, NoIcons.Instance, [], ResourceLimits.Unlimited);
		var layout = new ErModule().Layout(diagram, context);
		return FlowchartDagreInputTests.Compare(expected, layout.DagreInputs);
	}
}
