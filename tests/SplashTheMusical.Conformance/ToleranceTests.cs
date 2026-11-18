namespace SplashTheMusical.Conformance;

public class ToleranceTests
{
	[Test]
	public void EveryCorpusTypeHasAValidToleranceFile()
	{
		var types = CorpusCheck.Cases(CorpusCheck.CorpusRoot).Select(n => n.Split('/', '\\')[0]).Distinct(StringComparer.Ordinal);

		var toleranceProblems = Tolerance.Problems(Tolerance.Root, types);
		Verify(toleranceProblems.Count == 0, $"{string.Join('\n', toleranceProblems)}");
	}

	[Test]
	public void ATighterToleranceNeedsNoReason()
	{
		var problems = new List<string>();

		var tolerance = Tolerance.Parse("pie.json", """{ "canvas": 0.02, "overlap": 0.9, "raster": 0.95 }""", problems);

		Verify(tolerance == new Tolerance(0.02, 0.9, 0.95), $"was {tolerance}");
		Verify(problems.Count == 0, $"{string.Join('\n', problems)}");
	}

	[Test]
	public void ALooserToleranceFailsWithoutAReason()
	{
		var problems = new List<string>();

		_ = Tolerance.Parse("pie.json", """{ "canvas": 0.08, "overlap": 0.8, "raster": 0.9, "reason": " " }""", problems);
		_ = Tolerance.Parse("xychart.json", """{ "canvas": 0.05, "overlap": 0.7, "raster": 0.9 }""", problems);
		_ = Tolerance.Parse("radar.json", """{ "canvas": 0.05, "overlap": 0.8, "raster": 0.85 }""", problems);
		_ = Tolerance.Parse("venn.json", """{ "canvas": 0.05, "overlap": 0.8, "raster": 0.9, "rasterReportOnly": true }""", problems);

		LineAssert.Equal(
			problems,
			"pie.json: canvas 0.08, overlap 0.8, raster 0.9 and rasterReportOnly false are looser than 0.05, 0.8, 0.9 and false, and it has no reason",
			"xychart.json: canvas 0.05, overlap 0.7, raster 0.9 and rasterReportOnly false are looser than 0.05, 0.8, 0.9 and false, and it has no reason",
			"radar.json: canvas 0.05, overlap 0.8, raster 0.85 and rasterReportOnly false are looser than 0.05, 0.8, 0.9 and false, and it has no reason",
			"venn.json: canvas 0.05, overlap 0.8, raster 0.9 and rasterReportOnly true are looser than 0.05, 0.8, 0.9 and false, and it has no reason"
		);
	}

	[Test]
	public void ALooserToleranceWithAReasonIsKept()
	{
		var problems = new List<string>();

		var tolerance = Tolerance.Parse(
			"mindmap.json",
			"""{ "canvas": 0.2, "overlap": 0.5, "raster": 0.9, "rasterReportOnly": true, "reason": "force-directed" }""",
			problems
		);

		Verify(tolerance == new Tolerance(0.2, 0.5, 0.9, true, "force-directed"), $"was {tolerance}");
		Verify(problems.Count == 0, $"{string.Join('\n', problems)}");
	}

	[Test]
	public void AMalformedFileIsReported()
	{
		var problems = new List<string>();

		_ = Tolerance.Parse("a.json", "[]", problems);
		_ = Tolerance.Parse("b.json", """{ "canvas": 2, "overlap": "high", "rasterReportOnly": 1, "reason": 1, "ssim": 0.9 }""", problems);
		_ = Tolerance.Parse("c.json", "{", problems);

		Verify(problems.Count() == 8, $"was {problems.Count()}");
		LineAssert.Equal(
			problems.Take(7),
			"a.json: is not a JSON object",
			"b.json: canvas is not a number from 0 to 1",
			"b.json: overlap is not a number from 0 to 1",
			"b.json: rasterReportOnly is not true or false",
			"b.json: reason is not a string",
			"b.json: ssim is not canvas, overlap, raster, rasterReportOnly or reason",
			"b.json: needs canvas, overlap and raster"
		);
		Verify(problems[7].StartsWith("c.json: ", StringComparison.Ordinal), $"was {problems[7]}");
	}

	[Test]
	public void AMissingFileAndAFileWithoutCasesAreReported()
	{
		var folder = Directory.CreateTempSubdirectory("stm-tolerances-").FullName;
		try
		{
			File.WriteAllText(Path.Combine(folder, "pie.json"), """{ "canvas": 0.05, "overlap": 0.8, "raster": 0.9 }""");
			File.WriteAllText(Path.Combine(folder, "gone.json"), """{ "canvas": 0.05, "overlap": 0.8, "raster": 0.9 }""");

			LineAssert.Equal(
				Tolerance.Problems(folder, ["pie", "xychart"]),
				"xychart: no xychart.json",
				"gone.json: the corpus has no gone cases"
			);
		}
		finally
		{
			Directory.Delete(folder, recursive: true);
		}
	}
}
