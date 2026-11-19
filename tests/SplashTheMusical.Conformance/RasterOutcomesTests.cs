namespace SplashTheMusical.Conformance;

public class RasterOutcomesTests
{
	[Test]
	public void TheCommittedFileHasOneOutcomePerCorpusCase()
	{
		var problems = new List<string>();

		var outcomes = RasterOutcomes.Read(RasterOutcomes.FilePath, problems);

		Verify(problems.Count == 0, $"{string.Join('\n', problems)}");
		var outcomeProblems = RasterOutcomes.Problems(outcomes, CorpusCheck.Cases(CorpusCheck.CorpusRoot));
		Verify(outcomeProblems.Count == 0, $"{string.Join('\n', outcomeProblems)}");
	}

	[Test]
	public void EachOutcomeIsReadAndABadLineIsReported()
	{
		var problems = new List<string>();

		var outcomes = RasterOutcomes.Parse(
			["# L4 outcomes", "pie/a\tpass", "pie/b\tfail", "pie/c\tn/a", "mindmap/a\treport", "pie/d\tgood", "pie/e", "pie/a\tfail"],
			problems
		);

		Verify(() => outcomes["pie/a"] == Outcome.Pass);
		Verify(() => outcomes["pie/b"] == Outcome.Fail);
		Verify(() => outcomes["pie/c"] == Outcome.NotApplicable);
		Verify(() => outcomes["mindmap/a"] == Outcome.ReportOnly);
		LineAssert.Equal(
			problems,
			"line 6: pie/d\tgood is not a case, a tab and pass, fail, n/a or report",
			"line 7: pie/e is not a case, a tab and pass, fail, n/a or report",
			"line 8: pie/a is listed twice"
		);
	}

	[Test]
	public void ACaseWithoutAnOutcomeAndAnOutcomeWithoutACaseAreReported()
	{
		var outcomes = new Dictionary<string, Outcome> { ["pie/a"] = Outcome.Pass, ["pie/gone"] = Outcome.Fail };

		LineAssert.Equal(
			RasterOutcomes.Problems(outcomes, ["pie/a", Path.Combine("pie", "b")]),
			"pie/b: no L4 outcome",
			"pie/gone: has an L4 outcome and no case"
		);
	}
}
