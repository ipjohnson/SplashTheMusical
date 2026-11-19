using AwesomeAssertions;
using SplashTheMusical.Benchmarks;

namespace SplashTheMusical.Tests;

public class StmBenchmarksTests
{
	[Test]
	public void EveryStageRunsOnTheBenchmarkCasesOfItsTypes()
	{
		var cases = StmBenchmarks.Cases().ToList();
		cases.Should().Contain(["flowchart-small/parse", "flowchart-medium/layout", "flowchart-large/render", "flowchart-large/write", "state-small/parse", "state-medium/layout", "state-large/render", "state-large/write"]);
		foreach (var id in cases)
		{
			var benchmark = new StmBenchmarks { Case = id };
			benchmark.Setup();
			benchmark.Run().Should().NotBeNull(id);
		}
	}
}
