namespace SplashTheMusical.Conformance;

public class InfoTests
{
	[Test]
	public async Task EveryInfoCasePassesL0ToL2()
	{
		var corpus = CorpusCheck.CorpusRoot;
		var failures = new List<string>();
		var cases = CorpusCheck.Cases(corpus).Where(name => name.StartsWith("info", StringComparison.Ordinal) && name[4] is '/' or '\\').ToList();
		foreach (var name in cases)
		{
			var result = CorpusComparison.Run(corpus, name, new StmFactsProvider());
			foreach (var level in new[] { Level.L0, Level.L1, Level.L2 })
			{
				if (result[level].Outcome == Outcome.Fail)
				{
					failures.Add($"{name} {level}: {string.Join("; ", result[level].Differences)}");
				}
			}
		}

		await Assert.That(string.Join('\n', failures)).IsEmpty();
		await Assert.That(cases.Count).IsEqualTo(9);
	}

	[Test]
	public async Task TheVersionIsTheOnePinnedByTheOracle() =>
		await Assert.That(MermaidCompatibility.MermaidJsVersion).IsEqualTo(CorpusCheck.PinnedMermaid());
}
