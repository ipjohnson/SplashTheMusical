namespace SplashTheMusical.Conformance;

public class ComparerTests
{
	private static readonly CaseFacts Oracle = new(
		true,
		null,
		["drawn\tyes", "edge\tA\tB\tnone\tarrow\tsolid", "label\tA", "label\tA", "node\tA\tA\trect", "node\tB\tB\trect", "type\tflowchart-v2"],
		["box\tnode\tA\t12\t12\t40\t20", "box\tnode\tB\t12\t80\t40\t20"],
		"canvas\t0\t0\t64\t112"
	);

	[Test]
	public async Task TheStubFailsEveryLevelOfEveryCaseTheOracleAcceptsAndMatchesEveryCaseItRefuses()
	{
		var results = CorpusComparison.Run(CorpusCheck.CorpusRoot, new StubFactsProvider());

		await Assert.That(results.Count).IsEqualTo(CorpusCheck.Cases(CorpusCheck.CorpusRoot).Count);
		var wrong = new List<string>();
		foreach (var result in results)
		{
			var outcomes = result.Levels.Select(l => l.Outcome).ToList();
			var expected = OracleFiles.Read(CorpusCheck.CorpusRoot, result.Name).Accepted
				? outcomes.All(o => o == Outcome.Fail) && result[Level.L1].Differences.All(d => d.Missing)
				: outcomes.SequenceEqual([Outcome.Pass, Outcome.NotApplicable, Outcome.NotApplicable, Outcome.NotApplicable]);
			if (!expected)
			{
				wrong.Add(result.Name);
			}
		}

		await Assert.That(wrong).IsEmpty();
		await Assert.That(results).Contains(r => r[Level.L0].Outcome == Outcome.Pass);
		await Assert.That(results).Contains(r => r[Level.L0].Outcome == Outcome.Fail);
	}

	[Test]
	public async Task AProviderThatWritesTheOracleFactsPassesEveryLevelOfEveryCase()
	{
		var corpus = CorpusCheck.CorpusRoot;
		var wrong = new List<string>();
		foreach (var name in CorpusCheck.Cases(corpus))
		{
			var result = CorpusComparison.Run(corpus, name, new FixedProvider(OracleFiles.Read(corpus, name)));
			if (result.Levels.Any(l => l.Outcome == Outcome.Fail) || result[Level.L0].Outcome != Outcome.Pass)
			{
				wrong.Add(name);
			}
		}

		await Assert.That(wrong).IsEmpty();
	}

	[Test]
	public async Task EachLevelReportsTheLinesThatAreMissingAndExtra()
	{
		var actual = Oracle with
		{
			Facts = ["drawn\tyes", "edge\tA\tB\tnone\tarrow\tdotted", "label\tA", "node\tA\tA\trect", "node\tB\tB\trect", "type\tflowchart-v2"],
			Boxes = ["box\tnode\tA\t0\t0\t10\t10", "box\tedge\tA --> B\t0\t0\t0\t50"],
		};

		var result = Comparer.Compare("flowchart/docs-flowchart-001", Oracle, actual);

		await Assert.That(result.Type).IsEqualTo("flowchart");
		await Assert.That(result[Level.L0].Outcome).IsEqualTo(Outcome.Pass);
		await LineAssert.Equal(
			result[Level.L1].Differences.Select(d => d.ToString()),
			"L1 + edge\tA\tB\tnone\tarrow\tdotted",
			"L1 - edge\tA\tB\tnone\tarrow\tsolid"
		);
		await LineAssert.Equal(result[Level.L2].Differences.Select(d => d.ToString()), "L2 + box\tedge\tA --> B", "L2 - box\tnode\tB", "L2 - label\tA");
		await LineAssert.Equal(
			result[Level.L3].Differences.Select(d => d.ToString()),
			"L3 + box\tnode\tA\t0\t0\t10\t10",
			"L3 - box\tnode\tA\t12\t12\t40\t20"
		);
	}

	[Test]
	public async Task ASourceOnlyTheOracleAcceptsFailsEveryLevel()
	{
		var result = Comparer.Compare("flowchart/x", Oracle, CaseFacts.Refused("no"));

		await LineAssert.Equal(result[Level.L0].Differences.Select(d => d.ToString()), "L0 + accept\tno", "L0 - accept\tyes");
		await Assert.That(result[Level.L1].Differences).Count().IsEqualTo(4);
		await Assert.That(result[Level.L1].Differences).All(d => d.Missing);
		await Assert.That(result[Level.L2].Differences).Count().IsEqualTo(5);
		await Assert.That(result[Level.L2].Differences).All(d => d.Missing);
		await LineAssert.Equal(result[Level.L3].Differences.Select(d => d.ToString()), "L3 - canvas\t0\t0\t64\t112");
	}

	private sealed class FixedProvider(CaseFacts facts) : IFactsProvider
	{
		public string Name => "fixed";

		public CaseFacts Facts(string source) => facts;
	}
}
