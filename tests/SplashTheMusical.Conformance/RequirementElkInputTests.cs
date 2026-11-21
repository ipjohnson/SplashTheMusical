using System.Globalization;
using SplashTheMusical.Config;
using SplashTheMusical.Diagrams;
using SplashTheMusical.Diagrams.Requirement;
using SplashTheMusical.Icons;
using SplashTheMusical.Text;

namespace SplashTheMusical.Conformance;

/// <summary>
/// The requirement layout against mermaid.js: the size STM gives each requirement, element and relationship label
/// against the size mermaid.js gave ELK for it, recorded in <c>corpus/requirement/*.elk.txt</c>. mermaid.js draws every
/// requirement case with ELK unless it sets <c>layout: dagre</c>, and the oracle records no dagre graph for the type, so
/// this test shows that the sizes are the same and the layout engine is what differs at L3. The cases that still differ
/// are listed in <c>Fixtures/requirement-elk-inputs.txt</c> under the issue that tracks them. When <c>STM_DIFF_FILE</c>
/// is set, every difference of every case is written to that file.
/// </summary>
public class RequirementElkInputTests
{
	/// <summary>The fewest cases whose sizes must equal mermaid.js's, so the comparison cannot pass by comparing nothing.</summary>
	private const int MinimumSame = 100;

	private const double Tolerance = 0.01;

	private static readonly Mermaid Stm = new(new MermaidOptions { SiteConfig = StmFactsProvider.OracleSite, Limits = ResourceLimits.Unlimited });

	private static string ListPath => Path.Combine(CorpusCheck.RepositoryRoot, "tests", "SplashTheMusical.Conformance", "Fixtures", "requirement-elk-inputs.txt");

	/// <summary>
	/// Every requirement case's sizes equal the recorded ones, except the listed cases, which must still differ. The
	/// hand-drawn look is left out, as for flowchart, because roughjs's random strokes size its nodes.
	/// </summary>
	[Test]
	public void StmSizesTheNodesAndLabelsMermaidGaveElk()
	{
		var listed = Listed();
		var unlisted = new List<string>();
		var same = new List<string>();
		var compared = 0;
		var all = new List<string>();
		foreach (var file in ElkRecords.Files(CorpusCheck.CorpusRoot).Where(file => file.StartsWith("requirement/", StringComparison.Ordinal)))
		{
			var name = file[..^".elk.txt".Length];
			var facts = OracleFiles.Read(CorpusCheck.CorpusRoot, name);
			if (facts.Facts.Contains("config\tlook\t\"handDrawn\""))
			{
				continue;
			}

			var records = ElkRecords.Read(Path.Combine(CorpusCheck.CorpusRoot, file));
			if (records.Count == 0)
			{
				continue;
			}

			compared++;
			var differences = Differences(CorpusComparison.Source(CorpusCheck.CorpusRoot, name), records[0].Input);
			var caseName = name["requirement/".Length..];
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

		var stale = same.Concat(listed.Keys.Select(name => name + " is not a requirement case with an ELK record"));
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

	private static List<string> Differences(string source, ElkInput expected)
	{
		if (!Stm.TryParse(source, out var document, out _) || document is not RequirementDocument diagram)
		{
			return ["refused"];
		}

		var config = MermaidConfig.Resolve(StmFactsProvider.OracleSite, diagram, diagram.DiagramType);
		var context = new DiagramRenderContext(config, "mermaid", TextMeasurer.Default, NoIcons.Instance, [], ResourceLimits.Unlimited);
		var input = new RequirementModule().Layout(diagram, context).DagreInputs[0];
		var differences = new List<string>();
		var nodes = input.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
		foreach (var node in expected.Nodes.Where(node => node.Parent is not null))
		{
			if (!nodes.Remove(node.Id, out var actual))
			{
				differences.Add($"node {node.Id} missing");
			}
			else if (Differs(node.Width, actual.Width) || Differs(node.Height, actual.Height))
			{
				differences.Add($"node {node.Id} {Size(node.Width, node.Height)} != {Size(actual.Width, actual.Height)}");
			}
		}

		differences.AddRange(nodes.Keys.Select(id => $"node {id} extra"));
		var edges = input.Edges.Where(edge => edge.Name is not null).ToDictionary(edge => edge.Name!, StringComparer.Ordinal);
		foreach (var label in expected.Labels.Where(label => label.OwnerKind == "edge"))
		{
			if (!edges.TryGetValue(label.Owner, out var actual))
			{
				differences.Add($"edge {label.Owner} missing");
			}
			else if (Differs(label.Width, actual.Width) || Differs(label.Height, actual.Height))
			{
				differences.Add($"label {label.Owner} {Size(label.Width, label.Height)} != {Size(actual.Width, actual.Height)}");
			}
		}

		return differences;
	}

	private static bool Differs(double? expected, double actual) => Math.Abs((expected ?? 0) - actual) > Tolerance;

	private static string Size(double? width, double? height) =>
		string.Create(CultureInfo.InvariantCulture, $"{width ?? 0:0.###}x{height ?? 0:0.###}");
}
