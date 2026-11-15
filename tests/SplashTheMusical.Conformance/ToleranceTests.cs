namespace SplashTheMusical.Conformance;

public class ToleranceTests
{
	[Test]
	public async Task EveryCorpusTypeHasAValidToleranceFile()
	{
		var types = CorpusCheck.Cases(CorpusCheck.CorpusRoot).Select(n => n.Split('/', '\\')[0]).Distinct(StringComparer.Ordinal);

		await Assert.That(Tolerance.Problems(Tolerance.Root, types)).IsEmpty();
	}

	[Test]
	public async Task ATighterToleranceNeedsNoReason()
	{
		var problems = new List<string>();

		var tolerance = Tolerance.Parse("pie.json", """{ "canvas": 0.02, "overlap": 0.9, "raster": 0.95 }""", problems);

		await Assert.That(tolerance).IsEqualTo(new Tolerance(0.02, 0.9, 0.95));
		await Assert.That(problems).IsEmpty();
	}

	[Test]
	public async Task ALooserToleranceFailsWithoutAReason()
	{
		var problems = new List<string>();

		_ = Tolerance.Parse("pie.json", """{ "canvas": 0.08, "overlap": 0.8, "raster": 0.9, "reason": " " }""", problems);
		_ = Tolerance.Parse("xychart.json", """{ "canvas": 0.05, "overlap": 0.7, "raster": 0.9 }""", problems);
		_ = Tolerance.Parse("radar.json", """{ "canvas": 0.05, "overlap": 0.8, "raster": 0.85 }""", problems);
		_ = Tolerance.Parse("venn.json", """{ "canvas": 0.05, "overlap": 0.8, "raster": 0.9, "rasterReportOnly": true }""", problems);

		await LineAssert.Equal(
			problems,
			"pie.json: canvas 0.08, overlap 0.8, raster 0.9 and rasterReportOnly false are looser than 0.05, 0.8, 0.9 and false, and it has no reason",
			"xychart.json: canvas 0.05, overlap 0.7, raster 0.9 and rasterReportOnly false are looser than 0.05, 0.8, 0.9 and false, and it has no reason",
			"radar.json: canvas 0.05, overlap 0.8, raster 0.85 and rasterReportOnly false are looser than 0.05, 0.8, 0.9 and false, and it has no reason",
			"venn.json: canvas 0.05, overlap 0.8, raster 0.9 and rasterReportOnly true are looser than 0.05, 0.8, 0.9 and false, and it has no reason"
		);
	}

	[Test]
	public async Task ALooserToleranceWithAReasonIsKept()
	{
		var problems = new List<string>();

		var tolerance = Tolerance.Parse(
			"mindmap.json",
			"""{ "canvas": 0.2, "overlap": 0.5, "raster": 0.9, "rasterReportOnly": true, "reason": "force-directed" }""",
			problems
		);

		await Assert.That(tolerance).IsEqualTo(new Tolerance(0.2, 0.5, 0.9, true, "force-directed"));
		await Assert.That(problems).IsEmpty();
	}

	[Test]
	public async Task AMalformedFileIsReported()
	{
		var problems = new List<string>();

		_ = Tolerance.Parse("a.json", "[]", problems);
		_ = Tolerance.Parse("b.json", """{ "canvas": 2, "overlap": "high", "rasterReportOnly": 1, "reason": 1, "ssim": 0.9 }""", problems);
		_ = Tolerance.Parse("c.json", "{", problems);

		await Assert.That(problems).Count().IsEqualTo(8);
		await LineAssert.Equal(
			problems.Take(7),
			"a.json: is not a JSON object",
			"b.json: canvas is not a number from 0 to 1",
			"b.json: overlap is not a number from 0 to 1",
			"b.json: rasterReportOnly is not true or false",
			"b.json: reason is not a string",
			"b.json: ssim is not canvas, overlap, raster, rasterReportOnly or reason",
			"b.json: needs canvas, overlap and raster"
		);
		await Assert.That(problems[7]).StartsWith("c.json: ");
	}

	[Test]
	public async Task AMissingFileAndAFileWithoutCasesAreReported()
	{
		var folder = Directory.CreateTempSubdirectory("stm-tolerances-").FullName;
		try
		{
			File.WriteAllText(Path.Combine(folder, "pie.json"), """{ "canvas": 0.05, "overlap": 0.8, "raster": 0.9 }""");
			File.WriteAllText(Path.Combine(folder, "gone.json"), """{ "canvas": 0.05, "overlap": 0.8, "raster": 0.9 }""");

			await LineAssert.Equal(
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
