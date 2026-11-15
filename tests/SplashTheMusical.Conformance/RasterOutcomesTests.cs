namespace SplashTheMusical.Conformance;

public class RasterOutcomesTests
{
	[Test]
	public async Task TheCommittedFileHasOneOutcomePerCorpusCase()
	{
		var problems = new List<string>();

		var outcomes = RasterOutcomes.Read(RasterOutcomes.FilePath, problems);

		await Assert.That(problems).IsEmpty();
		await Assert.That(RasterOutcomes.Problems(outcomes, CorpusCheck.Cases(CorpusCheck.CorpusRoot))).IsEmpty();
	}

	[Test]
	public async Task EachOutcomeIsReadAndABadLineIsReported()
	{
		var problems = new List<string>();

		var outcomes = RasterOutcomes.Parse(
			["# L4 outcomes", "pie/a\tpass", "pie/b\tfail", "pie/c\tn/a", "mindmap/a\treport", "pie/d\tgood", "pie/e", "pie/a\tfail"],
			problems
		);

		await Assert.That(outcomes["pie/a"]).IsEqualTo(Outcome.Pass);
		await Assert.That(outcomes["pie/b"]).IsEqualTo(Outcome.Fail);
		await Assert.That(outcomes["pie/c"]).IsEqualTo(Outcome.NotApplicable);
		await Assert.That(outcomes["mindmap/a"]).IsEqualTo(Outcome.ReportOnly);
		await LineAssert.Equal(
			problems,
			"line 6: pie/d\tgood is not a case, a tab and pass, fail, n/a or report",
			"line 7: pie/e is not a case, a tab and pass, fail, n/a or report",
			"line 8: pie/a is listed twice"
		);
	}

	[Test]
	public async Task ACaseWithoutAnOutcomeAndAnOutcomeWithoutACaseAreReported()
	{
		var outcomes = new Dictionary<string, Outcome> { ["pie/a"] = Outcome.Pass, ["pie/gone"] = Outcome.Fail };

		await LineAssert.Equal(
			RasterOutcomes.Problems(outcomes, ["pie/a", Path.Combine("pie", "b")]),
			"pie/b: no L4 outcome",
			"pie/gone: has an L4 outcome and no case"
		);
	}
}
