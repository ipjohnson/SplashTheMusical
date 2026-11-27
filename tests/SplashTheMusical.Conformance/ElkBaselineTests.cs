namespace SplashTheMusical.Conformance;

public class ElkBaselineTests
{
	[Test]
	public void TheCommittedBaselineIsTheComparersScore()
	{
		var corpus = CorpusCheck.CorpusRoot;
		var dagre = ElkBaseline.Run(corpus, (graph, name, _) => ElkBaseline.Dagre(corpus, graph, name));
		var sugiyama = ElkBaseline.Run(corpus, (graph, _, type) => SugiyamaOnElkInput.Layout(graph, type));
		var baseline = ElkBaseline.Render(dagre, sugiyama, CorpusCheck.PinnedMermaid());
		if (Environment.GetEnvironmentVariable(ElkBaseline.WriteVariable) is { Length: > 0 })
		{
			Directory.CreateDirectory(Path.GetDirectoryName(ElkBaseline.FilePath)!);
			File.WriteAllText(ElkBaseline.FilePath, baseline);
		}

		var committed = File.Exists(ElkBaseline.FilePath) ? File.ReadAllText(ElkBaseline.FilePath) : "";
		LineAssert.Equal(committed.Split('\n'), baseline.Split('\n'), "mise run baseline rewrites docs/layered-layout/baseline.md");
	}

	[Test]
	public void TheSugiyamaVersionIsThePinnedOne()
	{
		var props = File.ReadAllText(Path.Combine(CorpusCheck.RepositoryRoot, "Directory.Packages.props"));
		var pin = $"<PackageVersion Include=\"Sugiyama\" Version=\"{SugiyamaOnElkInput.PackageVersion}\" />";
		Verify(props.Contains(pin, StringComparison.Ordinal), $"Directory.Packages.props does not pin {pin}. Update SugiyamaOnElkInput's version and commit.");
	}

	[Test]
	public void NoSourceProjectReferencesMermaider()
	{
		var src = Path.Combine(CorpusCheck.RepositoryRoot, "src");
		var references = Directory.EnumerateFiles(src, "*.csproj", SearchOption.AllDirectories)
			.Where(path =>
			{
				var text = File.ReadAllText(path);
				return text.Contains("\"Mermaider\"", StringComparison.Ordinal) || text.Contains("\"Sugiyama\"", StringComparison.Ordinal);
			})
			.Select(path => Path.GetRelativePath(CorpusCheck.RepositoryRoot, path))
			.ToList();
		Verify(references.Count == 0, $"{string.Join('\n', references)}");
	}

	[Test]
	public void TheComparisonNamesTheMeasuresSugiyamaPassesMoreAndLessOften()
	{
		var pass = new ElkLayoutScore(null, null, null, null);
		var dagre = new[]
		{
			new CaseScore("flowchart/a", "flowchart", pass, 1),
			new CaseScore("flowchart/b", "flowchart", new ElkLayoutScore(null, "order", null, "bends"), 2),
			new CaseScore("er/a", "er", ElkLayoutScore.Failed("threw"), 3, "threw"),
		};
		var sugiyama = new[]
		{
			new CaseScore("flowchart/a", "flowchart", new ElkLayoutScore("layer", null, null, null), 1),
			new CaseScore("flowchart/b", "flowchart", pass, 2),
			new CaseScore("er/a", "er", new ElkLayoutScore("layer", null, null, null), 3),
		};

		var lines = ElkBaseline.Render(dagre, sugiyama, "12.3.0").Split('\n');

		Verify(lines.Contains("| flowchart | 2 | 100.0% (2 of 2) | 50.0% (1 of 2) | 100.0% (2 of 2) | 50.0% (1 of 2) | 50.0% (1 of 2) |"), $"{string.Join('\n', lines)}");
		Verify(lines.Contains("| **Total** | 3 | 66.7% (2 of 3) | 33.3% (1 of 3) | 66.7% (2 of 3) | 33.3% (1 of 3) | 33.3% (1 of 3) |"), $"{string.Join('\n', lines)}");
		Verify(lines.Contains("2 of the 3 cases have more than one layer in ELK's layout. The coordinates are"), $"{string.Join('\n', lines)}");
		Verify(lines.Contains("Of all recorded cases, `Sugiyama` passes same order (3 cases against 1), boxes and canvas (3 cases against 2) and same bends (3 cases against 1) more often than the dagre port, and same layers (1 case against 2) less often. It passes all four (1 case against 1) as often."), $"{string.Join('\n', lines)}");
		Verify(lines.Contains("| er | 0 | +1 | +1 | +1 | 0 |"), $"{string.Join('\n', lines)}");
		Verify(lines.Contains("| dagre port | 1 |"), $"{string.Join('\n', lines)}");
		Verify(lines.Contains("| Sugiyama | 0 |"), $"{string.Join('\n', lines)}");
	}
}
