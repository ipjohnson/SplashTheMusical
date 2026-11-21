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
	public void PassesARowThatIsWithinMermaidersTimeWhenMeasuredAgain()
	{
		var output = new StringWriter();
		IReadOnlyList<ResultRow>? asked = null;
		var exit = Run([new("AOT", "flowchart-small", "parse", 33, 1500, 2000)], output, slow =>
		{
			asked = slow;
			return ([new("AOT", "flowchart-small", "parse", 33, 900, 2000)], [new("AOT", "flowchart-small", "parse", 33, 950, 2000)]);
		});

		exit.Should().Be(0, output.ToString());
		asked!.Select(r => r.Key).Should().Equal("AOT flowchart-small parse");
		var lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
		lines.Take(2).Should().Equal(
			"1 rows are slower than Mermaider's. Measuring them again.",
			"AOT flowchart-small parse: time 1500.0 ns is over Mermaider's 1000.0 ns, but within it when measured again, 900.0 ns to 950.0 ns");
		lines[2].Should().StartWith("1 rows within the budget of ");
	}

	[Test]
	public void FailsARowThatIsSlowerWhenMeasuredAgain()
	{
		var output = new StringWriter();
		var exit = Run([new("AOT", "flowchart-small", "parse", 33, 1500, 2000)], output,
			_ => ([new("AOT", "flowchart-small", "parse", 33, 1200, 2000)], [new("AOT", "flowchart-small", "parse", 33, 1100, 2000)]));

		exit.Should().Be(1);
		output.ToString().Should().Contain(
			"AOT flowchart-small parse: time 1500.0 ns is over Mermaider's 1000.0 ns, and again 1200.0 ns over 1100.0 ns");
	}

	[Test]
	public void FailsAnAllocationAtOnceWithoutMeasuringAgain()
	{
		var output = new StringWriter();
		var exit = Run([new("AOT", "flowchart-small", "parse", 33, 900, 2001)], output,
			_ => throw new InvalidOperationException("A row within Mermaider's time is not measured again."));

		exit.Should().Be(1);
		output.ToString().Should().StartWith("AOT flowchart-small parse: allocation 2001 B is over Mermaider's 2000 B");
	}

	[Test]
	public void FailsARowThatMeasuringAgainLeavesOut()
	{
		var output = new StringWriter();
		Run([new("AOT", "flowchart-small", "parse", 33, 1500, 2000)], output, _ => ([], [])).Should().Be(1);
		output.ToString().Should().Contain("AOT flowchart-small parse: time 1500.0 ns is over Mermaider's 1000.0 ns, and measuring it again gave no row");
	}

	[Test]
	public void TheParameterListLimitsARunToTheParametersItNames()
	{
		BenchmarkSet.Measures(null, "flowchart-small/parse").Should().BeTrue();
		BenchmarkSet.Measures("", "flowchart-small/parse").Should().BeTrue();
		BenchmarkSet.Measures("state-medium/parse,flowchart-small/parse", "flowchart-small/parse").Should().BeTrue();
		BenchmarkSet.Measures("state-medium/parse,flowchart-small/parse", "flowchart-small/render").Should().BeFalse();
		BenchmarkSet.Measures("flowchart-small/parse", "flowchart-small").Should().BeFalse();
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

	private static int Run(ResultRow[] stm, StringWriter output, Remeasure? remeasure = null)
	{
		var baseline = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".md");
		var run = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".md");
		try
		{
			ResultFile.Write(baseline, "Baseline", "", [], Baseline);
			ResultFile.Write(run, "Run", "", [], stm);
			return Budget.Run(baseline, run, Flowchart, output, remeasure);
		}
		finally
		{
			File.Delete(baseline);
			File.Delete(run);
		}
	}
}
