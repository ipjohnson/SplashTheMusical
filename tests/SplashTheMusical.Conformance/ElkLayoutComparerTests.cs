namespace SplashTheMusical.Conformance;

public class ElkLayoutComparerTests
{
	// A over B and C, with B left of C. C is in a cluster K. The edge A to C turns twice.
	private static readonly string[] Lines =
	[
		"layout\t1",
		"node\troot",
		"option\tnode\troot\t\telk.direction\tDOWN",
		"node\tA\troot\t40\t20",
		"node\tB\troot\t40\t20",
		"node\tK\troot",
		"node\tC\tK\t40\t20",
		"edge\tAB\troot\tA\tB",
		"edge\tAC\troot\tA\tK",
		"result",
		"place\troot\t\t0\t0\t200\t100",
		"place\tA\troot\t10\t10\t40\t20",
		"place\tB\troot\t10\t70\t40\t20",
		"place\tK\troot\t80\t60\t60\t40",
		"place\tC\tK\t10\t10\t40\t20",
		"route\tAB\troot\troot\t",
		"section\tAB\tAB_s0\t30,30\t30,70\t30,50\tA\tB",
		"route\tAC\troot\troot\t",
		"section\tAC\tAC_s0\t40,30\t110,70\t40,50 75,50 110,50\tA\tC",
	];

	private static ElkRecord Record(string direction = "DOWN") =>
		ElkRecords.Parse(Lines.Select(l => l.Replace("DOWN", direction, StringComparison.Ordinal)), "test")[0];

	[Test]
	public void ElkPassesEveryMeasureOnEveryRecording()
	{
		var failures = new List<string>();
		var calls = 0;
		foreach (var file in ElkRecords.Files(CorpusCheck.CorpusRoot))
		{
			foreach (var record in ElkRecords.Read(Path.Combine(CorpusCheck.CorpusRoot, file)))
			{
				calls++;
				var graph = new ElkInputGraph(record.Input);
				var score = ElkLayoutComparer.Score(graph, ElkLayoutComparer.ElkLayout(record, graph), ElkLayoutComparer.ElkLayout(record, graph));
				if (!score.All)
				{
					failures.Add($"{file}: {score.LayerFailure ?? score.OrderFailure ?? score.BoxFailure ?? score.BendFailure}");
				}
			}
		}

		Verify(() => calls > 2000);
		Verify(failures.Count == 0, $"{string.Join('\n', failures.Take(20))}");
	}

	[Test]
	public void LayersAreReadFromTheCoordinatesAlongTheDirection()
	{
		var record = Record();
		var graph = new ElkInputGraph(record.Input);
		var layers = ElkLayoutComparer.Layers(graph, ElkLayoutComparer.ElkLayout(record, graph));

		Verify(() => string.Join(" | ", layers.Select(l => string.Join(' ', l))) == "A | B C");
	}

	[Test]
	public void AnUpLayoutCountsLayersFromTheBottom()
	{
		var record = Record("UP");
		var graph = new ElkInputGraph(record.Input);
		var layers = ElkLayoutComparer.Layers(graph, ElkLayoutComparer.ElkLayout(record, graph));

		Verify(() => string.Join(" | ", layers.Select(l => string.Join(' ', l))) == "B C | A");
	}

	[Test]
	public void ElksOwnLayoutPassesEveryMeasure()
	{
		var record = Record();
		var graph = new ElkInputGraph(record.Input);
		var score = ElkLayoutComparer.Score(record, ElkLayoutComparer.ElkLayout(record, graph));

		Verify(() => score.All);
	}

	[Test]
	public void EachMeasureFailsOnItsOwn()
	{
		var record = Record();
		var graph = new ElkInputGraph(record.Input);
		var elk = ElkLayoutComparer.ElkLayout(record, graph);

		var swapped = elk with
		{
			Boxes = new Dictionary<string, LayoutBox>(elk.Boxes, StringComparer.Ordinal)
			{
				["B"] = elk.Boxes["C"],
				["C"] = elk.Boxes["B"],
			},
		};
		var order = ElkLayoutComparer.Score(record, swapped);
		Verify(() => order.SameLayers);
		Verify(() => order.OrderFailure == "layer 1 is in the order C B, ELK's is B C");
		Verify(() => order.BoxFailure == "the box of 'B' overlaps ELK's by 0%");

		var lowered = elk with
		{
			Boxes = new Dictionary<string, LayoutBox>(elk.Boxes, StringComparer.Ordinal) { ["A"] = elk.Boxes["A"] with { Y = 70 } },
		};
		Verify(() => ElkLayoutComparer.Score(record, lowered).LayerFailure == "node 'B' is in layer 0, ELK's is 1");

		var wide = elk with { Width = 211 };
		Verify(() => ElkLayoutComparer.Score(record, wide).BoxFailure == "the canvas is 211 x 100, ELK's is 200 x 100");
		Verify(() => ElkLayoutComparer.Score(record, elk with { Width = 209 }).BoxesOverlap);

		var straight = elk with
		{
			Edges = new Dictionary<string, IReadOnlyList<(double X, double Y)>>(elk.Edges, StringComparer.Ordinal) { ["AC"] = [(40, 30), (110, 70)] },
		};
		var bends = ElkLayoutComparer.Score(record, straight);
		Verify(() => bends.BendFailure == "edge 'AC' has 0 bends, ELK's has 2");
		Verify(() => bends.SameLayers && bends.SameOrder && bends.BoxesOverlap);

		var missing = elk with { Edges = new Dictionary<string, IReadOnlyList<(double X, double Y)>>(StringComparer.Ordinal) };
		Verify(() => ElkLayoutComparer.Score(record, missing).BendFailure == "the layout has no route for edge 'AB'");
	}

	[Test]
	public void ALayoutWithoutCoordinatesIsScoredOnItsLayers()
	{
		var record = Record();
		var empty = new CandidateLayout(0, 0, new Dictionary<string, LayoutBox>(), new Dictionary<string, IReadOnlyList<(double X, double Y)>>(), [["A"], ["B", "C"]]);
		var score = ElkLayoutComparer.Score(record, empty);
		Verify(() => score.SameLayers && score.SameOrder);
		Verify(() => !score.BoxesOverlap && !score.SameBends);

		var reordered = empty with { Layers = [["A"], ["C", "B"]] };
		Verify(() => ElkLayoutComparer.Score(record, reordered).OrderFailure == "layer 1 is in the order C B, ELK's is B C");

		var flat = empty with { Layers = [["A", "B", "C"]] };
		Verify(() => ElkLayoutComparer.Score(record, flat).LayerFailure == "node 'B' is in layer 0, ELK's is 1");

		var partial = empty with { Layers = [["A"], ["B"]] };
		Verify(() => ElkLayoutComparer.Score(record, partial).LayerFailure == "the layout has no node 'C'");
	}

	[Test]
	public void BendsAreTurns()
	{
		Verify(() => ElkLayoutComparer.Bends([]) == 0);
		Verify(() => ElkLayoutComparer.Bends([(0, 0), (0, 10)]) == 0);
		Verify(() => ElkLayoutComparer.Bends([(0, 0), (0, 5), (0, 5), (0, 10)]) == 0);
		Verify(() => ElkLayoutComparer.Bends([(0, 0), (0, 10), (10, 10)]) == 1);
		Verify(() => ElkLayoutComparer.Bends([(0, 0), (0, 10), (5, 10), (10, 10), (10, 20)]) == 2);
		Verify(() => ElkLayoutComparer.Bends([(0, 0), (0, 10), (0, 5)]) == 1);
	}

	[Test]
	public void OverlapIsTheShareOfTheLargerBox()
	{
		var box = new LayoutBox(0, 0, 10, 10);
		Verify(() => ElkLayoutComparer.Overlap(box, box) == 1);
		Verify(() => ElkLayoutComparer.Overlap(box, box with { X = 2 }) == 0.8);
		Verify(() => ElkLayoutComparer.Overlap(box, box with { Width = 20 }) == 0.5);
		Verify(() => ElkLayoutComparer.Overlap(box, box with { X = 10 }) == 0);
		Verify(() => ElkLayoutComparer.Overlap(new LayoutBox(5, 5, 0, 0), new LayoutBox(5, 5, 0, 0)) == 1);
	}
}
