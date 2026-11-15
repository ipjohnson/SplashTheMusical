using AwesomeAssertions;
using SplashTheMusical.Benchmarks;

namespace SplashTheMusical.Tests;

public class ComparisonTests
{
	private static readonly string Committed = Fixture("committed.md");
	private static readonly string Regressed = Fixture("regressed.md");

	[Test]
	public void ExitsZeroWhenTheRunMatchesTheCommittedNumbers()
	{
		var output = new StringWriter();
		Comparison.Run(Committed, Committed, output).Should().Be(0);
		output.ToString().Should().Contain("4 rows within 10%");
	}

	[Test]
	public void ExitsNonZeroWhenARunIsMoreThanTenPercentWorse()
	{
		var output = new StringWriter();
		var exit = Comparison.Run(Committed, Regressed, output);

		exit.Should().Be(1);
		output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Should().Equal(
			"JIT pie-small parse: time 1000.0 ns -> 1110.0 ns (+11.0%)",
			"JIT pie-small render: allocation 8000 B -> 8880 B (+11.0%)",
			"AOT pie-small render: allocation 0 B -> 24 B (was zero)",
			$"3 regressions against {Committed}");
	}

	[Test]
	public void ExitsNonZeroWhenTheRunLacksACommittedRow()
	{
		var output = new StringWriter();
		var run = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".md");
		try
		{
			ResultFile.Write(run, "Partial run", "", [], ResultFile.Read(Committed).Skip(1));
			Comparison.Run(Committed, run, output).Should().Be(1);
			output.ToString().Should().Contain("JIT pie-small parse: missing from the run");
		}
		finally
		{
			File.Delete(run);
		}
	}

	[Test]
	public void ReadsBackTheRowsItWrites()
	{
		var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".md");
		try
		{
			var rows = ResultFile.Read(Committed);
			ResultFile.Write(path, "Copy", "", [("Machine", "test")], rows);
			ResultFile.Read(path).Should().Equal(rows);
		}
		finally
		{
			File.Delete(path);
		}
	}

	private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Benchmarks", name);
}
