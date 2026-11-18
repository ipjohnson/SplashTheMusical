namespace SplashTheMusical.Conformance;

public class ScratchStateDiffTests
{
	[Test]
	public async Task WriteStateDifferences()
	{
		if (Environment.GetEnvironmentVariable("STM_STATE_DIFF") is not { Length: > 0 } path)
		{
			return;
		}

		var provider = new StmFactsProvider();
		var lines = new List<string>();
		var counts = new int[3];
		foreach (var name in CorpusCheck.Cases(CorpusCheck.CorpusRoot).Where(n => n.StartsWith("state/", StringComparison.Ordinal)))
		{
			CaseComparison result;
			try
			{
				result = CorpusComparison.Run(CorpusCheck.CorpusRoot, name, provider);
			}
			catch (Exception e)
			{
				lines.Add($"{name} CRASH {e.GetType().Name}: {e.Message}");
				continue;
			}

			for (var level = 0; level < 2; level++)
			{
				if (result.Levels[level].Outcome == Outcome.Fail)
				{
					counts[level]++;
					foreach (var difference in result.Levels[level].Differences)
					{
						lines.Add($"{name} {difference}");
					}
				}
			}

			if (result.Levels[0].Outcome == Outcome.Fail)
			{
				lines.Add($"{name} stm error: {provider.Facts(CorpusComparison.Source(CorpusCheck.CorpusRoot, name)).Error}");
			}
		}

		lines.Insert(0, $"L0 fail {counts[0]}, L1 fail {counts[1]}");
		await File.WriteAllLinesAsync(path, lines);
	}
}
