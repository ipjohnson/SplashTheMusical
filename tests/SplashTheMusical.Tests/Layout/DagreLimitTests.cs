using System.Diagnostics;
using AwesomeAssertions;
using SplashTheMusical.Benchmarks;
using SplashTheMusical.Layout.Dagre;

namespace SplashTheMusical.Tests.Layout;

public class DagreLimitTests
{
	const int Runs = 3;

	/// <summary>
	/// The time is measured on the wall clock, as the deadline is, so the test runs alone. The fastest of
	/// <see cref="Runs"/> layouts is held to the deadline, so load from outside the test run does not fail it.
	/// </summary>
	[Test]
	[NotInParallel]
	public void LaysOutFiveThousandNodesInsideTheDefaultDeadline()
	{
		var input = DagreGraphs.Flowchart(5_000);

		var fastest = TimeSpan.MaxValue;
		LayoutGraph? g = null;
		for (var run = 0; run < Runs; run++)
		{
			var stopwatch = Stopwatch.StartNew();
			g = DagreGraphs.Layout(input);
			stopwatch.Stop();
			if (stopwatch.Elapsed < fastest)
			{
				fastest = stopwatch.Elapsed;
			}
		}

		fastest.Should().BeLessThan(ResourceLimits.Default.RenderDeadline!.Value);
		var placed = 0;
		foreach (var v in g!.Nodes())
		{
			var node = g.Node(v);
			if (double.IsFinite(node.X) && double.IsFinite(node.Y))
			{
				placed++;
			}
		}

		placed.Should().Be(5_000);
	}
}
