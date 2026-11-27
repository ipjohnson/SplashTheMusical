namespace SplashTheMusical.Conformance;

public class ElkSurveyTests
{
	private static readonly string[] Graph =
	[
		"layout\t1",
		"node\troot",
		"option\tnode\troot\t\telk.algorithm\telk.layered",
		"option\tnode\troot\t\telk.direction\tDOWN",
		"node\tg\troot",
		"option\tnode\tg\t\telk.algorithm\telk.box",
		"node\ta\tg\t10\t10",
		"node\tb\tg\t10\t10",
		"port\tbp\tb\t2\t2",
		"node\tc\troot\t10\t10",
		"node\td\troot\t10\t10",
		"node\te\troot\t10\t10",
		"label\tnode\tc\t0\tC\t5\t5",
		"edge\te1\troot\ta\tbp",
		"edge\te2\troot\tb\tc",
		"label\tedge\te2\t0\tlabel\t5\t5",
		"option\tedge-label\te2\t0\tedgeLabels.placement\tCENTER",
		"edge\te3\troot\tc\ta",
		"edge\te4\troot\tc\ta",
		"edge\te5\troot\td\td",
		"edge\te6\tg\ta\tb",
		"result",
		"place\troot\t\t0\t0\t0\t0",
	];

	[Test]
	public void TheCommittedSurveyIsTheCorpusSurvey()
	{
		var survey = ElkSurvey.Render(ElkSurvey.Read(CorpusCheck.CorpusRoot), CorpusCheck.PinnedMermaid());
		if (Environment.GetEnvironmentVariable(ElkSurvey.WriteVariable) is { Length: > 0 })
		{
			File.WriteAllText(ElkSurvey.FilePath, survey);
		}

		var committed = File.Exists(ElkSurvey.FilePath) ? File.ReadAllText(ElkSurvey.FilePath) : "";
		LineAssert.Equal(committed.Split('\n'), survey.Split('\n'), "mise run elk-survey rewrites docs/layered-layout/corpus-survey.md");
	}

	[Test]
	public void ShapeCountsEachFeatureOfAGraph()
	{
		var shape = ElkSurvey.Shape(ElkRecords.Parse(Graph, "inline"));

		Verify(() => shape.Nodes == 6 && shape.Edges == 6 && shape.Containers == 1 && shape.Depth == 2);
		Verify(() => shape.Ports == 1 && shape.NodeLabels == 1 && shape.LabelledEdges == 1);
		Verify(() => shape.SelfLoops == 1 && shape.ParallelEdges == 2 && shape.ReverseEdgePairs == 0);
		Verify(() => shape.HasCycle);
		Verify(() => shape.Components == 3);
		Verify(() => shape.CrossHierarchyEdges == 3 && shape.ContainerEdges == 0 && shape.NestedEdges == 1 && shape.Hyperedges == 0);
	}

	[Test]
	public void AnAcyclicGraphHasNoCycle()
	{
		var shape = ElkSurvey.Shape(ElkRecords.Parse(
			["layout\t1", "node\troot", "node\ta\troot", "node\tb\troot", "edge\te1\troot\ta\tb", "node\tc\troot", "edge\te2\troot\tb\tc", "edge\te3\troot\ta\tc", "result"],
			"inline"));

		Verify(() => !shape.HasCycle && shape.Components == 1);
	}

	[Test]
	public void RenderCountsCasesPerAlgorithmOptionAndType()
	{
		var records = ElkRecords.Parse(Graph, "inline");
		var lines = ElkSurvey.Render(
			[new ElkSurveyCase("flowchart/a", "flowchart", records), new ElkSurveyCase("flowchart/b", "flowchart", records), new ElkSurveyCase("state/c", "state", records)],
			"12.3.0").Split('\n');

		Verify(lines.Contains("| `elk.layered` | 3 | 0 |"), $"{string.Join('\n', lines)}");
		Verify(lines.Contains("| `elk.box` | 0 | 3 |"), $"{string.Join('\n', lines)}");
		Verify(lines.Contains("| root | `elk.direction` | `DOWN` | 3 | 3 | 2 | 1 |"), $"{string.Join('\n', lines)}");
		Verify(lines.Contains("| Self loops | 3 | 100.0% | 3 edges | 2 | 1 |"), $"{string.Join('\n', lines)}");
		Verify(lines.Contains("| Disconnected components | 3 | 100.0% |  | 2 | 1 |"), $"{string.Join('\n', lines)}");
		Verify(lines.Contains("| 1 | `flowchart/a` | 6 | 6 | 1 | 2 | 3 | 1 |"), $"{string.Join('\n', lines)}");
	}
}
