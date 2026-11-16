using System.Globalization;
using AwesomeAssertions;
using SplashTheMusical.Layout.Dagre;

namespace SplashTheMusical.Tests.Layout;

/// <summary>
/// The rank phases on small graphs. Each expected line is what dagre-d3-es 7.0.14 gives for the same graph: each input
/// node's rank in the graph's order, a cluster's first and last rank, each label's rank and the reversed edges.
/// </summary>
public class DagreRankTests
{
	[Test]
	public void RanksAChainEveryOtherRank() =>
		Ranks(new(), ["a", "b", "c"], [Edge("a", "b"), Edge("b", "c")])
			.Should().Be("a=0 b=2 c=4 | | reversed:");

	[Test]
	public void NetworkSimplexShortensTheShortBranch() =>
		Ranks(new(), ["a", "b", "c", "d"], [Edge("a", "b"), Edge("b", "c"), Edge("a", "d")])
			.Should().Be("a=0 b=2 c=4 d=2 | | reversed:");

	[Test]
	public void LongestPathPutsSinksOnTheLastRank() =>
		Ranks(new(Ranker: "longest-path"), ["a", "b", "c", "d"], [Edge("a", "b"), Edge("b", "c"), Edge("a", "d")])
			.Should().Be("a=0 b=2 c=4 d=4 | | reversed:");

	[Test]
	public void PutsALabelHalfwayAlongItsEdge() =>
		Ranks(new(), ["a", "b"], [Edge("a", "b") with { Width = 10, Height = 10, LabelPos = "c" }])
			.Should().Be("a=0 b=2 | a->b@1 | reversed:");

	[Test]
	public void KeepsAClusterBetweenItsBorders() =>
		Ranks(new(), ["C", "a C", "b C", "x"], [Edge("a", "b"), Edge("x", "a")])
			.Should().Be("C=2..6 a=3 b=5 x=0 | | reversed:");

	[Test]
	public void NestsClusters() =>
		Ranks(new(), ["O", "I O", "p I", "q O", "z"], [Edge("p", "q"), Edge("z", "p"), Edge("q", "z") with { MinLen = 2 }])
			.Should().Be("O=0..6 I=1..3 p=2 q=5 z=10 | | reversed: p->z");

	[Test]
	public void DepthFirstSearchReversesTheEdgeThatClosesTheCycle() =>
		Ranks(new(), ["a", "b"], [Edge("a", "b"), Edge("b", "a") with { Weight = 5 }])
			.Should().Be("a=0 b=2 | | reversed: a->b");

	[Test]
	public void GreedyFeedbackArcSetReversesTheLighterEdge() =>
		Ranks(new(Acyclicer: "greedy"), ["a", "b"], [Edge("a", "b"), Edge("b", "a") with { Weight = 5 }])
			.Should().Be("a=2 b=0 | | reversed: b->a");

	[Test]
	public void VisitsArrayIndexNamesFirst() =>
		Ranks(new(), ["b", "10", "2", "a"], [Edge("b", "10"), Edge("2", "a"), Edge("a", "b"), Edge("10", "2")])
			.Should().Be("2=0 10=6 b=4 a=2 | | reversed: 2->10");

	[Test]
	public void RemovesSelfEdgesBeforeRanking()
	{
		var g = DagreLayout.BuildLayoutGraph(new DagreInput(new(), [new("a", null, 0, 0)], [Edge("a", "a")]));
		DagreLayout.RunRankPhases(g);

		var a = g.Node(g.NodeHandle("a"));
		a.SelfEdges.Should().ContainSingle();
		a.Rank.Should().Be(0);
		g.EdgeCount.Should().Be(0);
	}

	private static DagreEdgeInput Edge(string v, string w) => new(v, w, null);

	/// <param name="nodes">Each node as its id, then a space and its parent's id when it has one.</param>
	private static string Ranks(DagreOptions options, string[] nodes, DagreEdgeInput[] edges)
	{
		var input = nodes
			.Select(node => node.Split(' '))
			.Select(parts => new DagreNodeInput(parts[0], parts.Length > 1 ? parts[1] : null, 0, 0))
			.ToList();
		var g = DagreLayout.BuildLayoutGraph(new DagreInput(options, input, edges));
		DagreLayout.RunRankPhases(g);

		var ranks = new List<string>();
		foreach (var v in g.Nodes())
		{
			var node = g.Node(v);
			if (node.Dummy is null)
			{
				ranks.Add(node.BorderTop >= 0
					? string.Create(CultureInfo.InvariantCulture, $"{g.Name(v)}={node.MinRank}..{node.MaxRank}")
					: string.Create(CultureInfo.InvariantCulture, $"{g.Name(v)}={node.Rank}"));
			}
		}

		var labels = new List<string>();
		var reversed = new List<string>();
		foreach (var e in g.Edges())
		{
			var edge = g.Edge(e);
			if (!double.IsNaN(edge.LabelRank))
			{
				labels.Add(string.Create(CultureInfo.InvariantCulture, $"{g.Name(g.EdgeV(e))}->{g.Name(g.EdgeW(e))}@{edge.LabelRank}"));
			}

			if (edge.Reversed)
			{
				reversed.Add($"{g.Name(g.EdgeV(e))}->{g.Name(g.EdgeW(e))}");
			}
		}

		return $"{string.Join(' ', ranks)} |{Spaced(labels)} | reversed:{Spaced(reversed)}";
	}

	private static string Spaced(List<string> items) => items.Count > 0 ? " " + string.Join(' ', items) : "";
}
