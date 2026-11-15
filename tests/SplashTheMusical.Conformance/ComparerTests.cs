using AwesomeAssertions;

namespace SplashTheMusical.Conformance;

public class ComparerTests
{
	private static readonly CaseFacts Oracle = new(
		true,
		null,
		["drawn\tyes", "edge\tA\tB\tnone\tarrow\tsolid", "label\tA", "label\tA", "node\tA\tA\trect", "node\tB\tB\trect", "type\tflowchart-v2"],
		["box\tnode\tA\t12\t12\t40\t20", "box\tnode\tB\t12\t80\t40\t20"]
	);

	[Test]
	public void TheStubFailsEveryLevelOfEveryCaseTheOracleAcceptsAndMatchesEveryCaseItRefuses()
	{
		var results = CorpusComparison.Run(CorpusCheck.CorpusRoot, new StubFactsProvider());

		results.Should().HaveCount(CorpusCheck.Cases(CorpusCheck.CorpusRoot).Count);
		foreach (var result in results)
		{
			if (OracleFiles.Read(CorpusCheck.CorpusRoot, result.Name).Accepted)
			{
				result.Levels.Select(l => l.Outcome).Should().AllBeEquivalentTo(Outcome.Fail, result.Name);
				result[Level.L1].Differences.Should().OnlyContain(d => d.Missing, result.Name);
			}
			else
			{
				result.Levels.Select(l => l.Outcome).Should().Equal([Outcome.Pass, Outcome.NotApplicable, Outcome.NotApplicable], result.Name);
			}
		}

		results.Should().Contain(r => r[Level.L0].Outcome == Outcome.Pass);
		results.Should().Contain(r => r[Level.L0].Outcome == Outcome.Fail);
	}

	[Test]
	public void AProviderThatWritesTheOracleFactsPassesEveryLevelOfEveryCase()
	{
		var corpus = CorpusCheck.CorpusRoot;
		foreach (var name in CorpusCheck.Cases(corpus))
		{
			var result = CorpusComparison.Run(corpus, name, new FixedProvider(OracleFiles.Read(corpus, name)));
			result.Levels.Should().OnlyContain(l => l.Outcome != Outcome.Fail, name);
			result[Level.L0].Outcome.Should().Be(Outcome.Pass, name);
		}
	}

	[Test]
	public void EachLevelReportsTheLinesThatAreMissingAndExtra()
	{
		var actual = Oracle with
		{
			Facts = ["drawn\tyes", "edge\tA\tB\tnone\tarrow\tdotted", "label\tA", "node\tA\tA\trect", "node\tB\tB\trect", "type\tflowchart-v2"],
			Boxes = ["box\tnode\tA\t0\t0\t10\t10", "box\tedge\tA --> B\t0\t0\t0\t50"],
		};

		var result = Comparer.Compare("flowchart/docs-flowchart-001", Oracle, actual);

		result.Type.Should().Be("flowchart");
		result[Level.L0].Outcome.Should().Be(Outcome.Pass);
		result[Level.L1].Differences.Select(d => d.ToString()).Should().Equal(
			"L1 + edge\tA\tB\tnone\tarrow\tdotted",
			"L1 - edge\tA\tB\tnone\tarrow\tsolid"
		);
		result[Level.L2].Differences.Select(d => d.ToString()).Should().Equal(
			"L2 + box\tedge\tA --> B",
			"L2 - box\tnode\tB",
			"L2 - label\tA"
		);
	}

	[Test]
	public void ASourceOnlyTheOracleAcceptsFailsEveryLevel()
	{
		var result = Comparer.Compare("flowchart/x", Oracle, CaseFacts.Refused("no"));

		result[Level.L0].Differences.Select(d => d.ToString()).Should().Equal("L0 + accept\tno", "L0 - accept\tyes");
		result[Level.L1].Differences.Should().HaveCount(4).And.OnlyContain(d => d.Missing);
		result[Level.L2].Differences.Should().HaveCount(5).And.OnlyContain(d => d.Missing);
	}

	private sealed class FixedProvider(CaseFacts facts) : IFactsProvider
	{
		public string Name => "fixed";

		public CaseFacts Facts(string source) => facts;
	}
}
