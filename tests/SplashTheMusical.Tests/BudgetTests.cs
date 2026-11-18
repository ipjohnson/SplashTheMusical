using AwesomeAssertions;
using SplashTheMusical.Benchmarks;

namespace SplashTheMusical.Tests;

public class BudgetTests
{
	private static readonly HashSet<string> Flowchart = new(StringComparer.Ordinal) { "flowchart" };

	private static readonly ResultRow[] Baseline =
	[
		new("AOT", "flowchart-small", "parse", 33, 1000, 2000),
		new("AOT", "flowchart-small", "render", 33, 5000, 8000),
		new("AOT", "pie-small", "parse", 40, 10, 10),
	];

	[Test]
	public void ExitsZeroWhenEveryParseRowIsWithinMermaidersNumbers()
	{
		var output = new StringWriter();
		var exit = Run([new("AOT", "flowchart-small", "parse", 33, 1000, 2000), new("AOT", "flowchart-small", "render", 33, 9000, 9000)], output);

		exit.Should().Be(0);
		output.ToString().Should().StartWith("1 rows within the budget");
	}

	[Test]
	public void ExitsNonZeroWhenAParseRowIsSlowerOrAllocatesMore()
	{
		var output = new StringWriter();
		var exit = Run([new("AOT", "flowchart-small", "parse", 33, 1000.5, 2001)], output);

		exit.Should().Be(1);
		output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Take(2).Should().Equal(
			"AOT flowchart-small parse: time 1000.5 ns is over Mermaider's 1000.0 ns",
			"AOT flowchart-small parse: allocation 2001 B is over Mermaider's 2000 B");
	}

	[Test]
	public void ExitsNonZeroWhenTheRunLacksAParseRowOfAMeasuredType()
	{
		var output = new StringWriter();
		Run([], output).Should().Be(1);
		output.ToString().Should().Contain("AOT flowchart-small parse: missing from");
	}

	[Test]
	public void TheCommittedNumbersAreWithinTheMermaiderBaseline()
	{
		var output = new StringWriter();
		var exit = Budget.Run(
			Repository.Resolve("docs/benchmarks/mermaider-baseline.md"),
			Repository.Resolve("docs/benchmarks/stm.md"),
			StmBenchmarks.Measured,
			output);

		exit.Should().Be(0, output.ToString());
	}

	private static int Run(ResultRow[] stm, StringWriter output)
	{
		var baseline = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".md");
		var run = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".md");
		try
		{
			ResultFile.Write(baseline, "Baseline", "", [], Baseline);
			ResultFile.Write(run, "Run", "", [], stm);
			return Budget.Run(baseline, run, Flowchart, output);
		}
		finally
		{
			File.Delete(baseline);
			File.Delete(run);
		}
	}
}
