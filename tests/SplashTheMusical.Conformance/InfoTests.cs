namespace SplashTheMusical.Conformance;

public class InfoTests
{
	[Test]
	public void EveryInfoCasePassesL0ToL2()
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

		Verify(!failures.Any(), $"{string.Join('\n', failures)}");
		Verify(() => cases.Count == 9);
	}

	[Test]
	public void TheVersionIsTheOnePinnedByTheOracle() =>
		Verify(() => MermaidCompatibility.MermaidJsVersion == CorpusCheck.PinnedMermaid());
}
