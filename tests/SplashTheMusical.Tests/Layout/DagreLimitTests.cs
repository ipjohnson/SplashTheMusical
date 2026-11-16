using System.Diagnostics;
using AwesomeAssertions;
using SplashTheMusical.Benchmarks;

namespace SplashTheMusical.Tests.Layout;

public class DagreLimitTests
{
	/// <summary>The time is measured on the wall clock, as the deadline is, so the test runs alone.</summary>
	[Test]
	[NotInParallel]
	public void LaysOutFiveThousandNodesInsideTheDefaultDeadline()
	{
		var input = DagreGraphs.Flowchart(5_000);

		var stopwatch = Stopwatch.StartNew();
		var g = DagreGraphs.Layout(input);
		stopwatch.Stop();

		stopwatch.Elapsed.Should().BeLessThan(ResourceLimits.Default.RenderDeadline!.Value);
		var placed = 0;
		foreach (var v in g.Nodes())
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
