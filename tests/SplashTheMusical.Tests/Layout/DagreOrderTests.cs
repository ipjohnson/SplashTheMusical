using AwesomeAssertions;
using SplashTheMusical.Layout.Dagre;

namespace SplashTheMusical.Tests.Layout;

/// <summary>
/// The normalize and order phases on small graphs. Each expected line is what dagre-d3-es 7.0.14 gives for the same
/// graph: the nodes of each rank in order, with <c>(d)</c> for an edge dummy, <c>(l)</c> for an edge label dummy,
/// <c>(bl)</c> and <c>(br)</c> for a cluster's left and right border, and <c>(b)</c> for its top or bottom border.
/// </summary>
public class DagreOrderTests
{
	[Test]
	public void SplitsALongEdgeIntoADummyPerRank() =>
		Verify(Orders(["a", "b", "c"], [Edge("a", "b"), Edge("b", "c"), Edge("a", "c")]) == "0: a | 1: (d) (d) | 2: b (d) | 3: (d) (d) | 4: c");

	[Test]
	public void PutsTheLabelDummyOnTheLabelRank()
	{
		var labelled = Edge("a", "b") with { Width = 10, Height = 10, LabelPos = "c" };

		Verify(Orders(["a", "b", "c"], [labelled, Edge("a", "c")]) == "0: a | 1: (l) (d) | 2: b c");
	}

	[Test]
	public void SweepsAwayACrossing() =>
		Verify(Orders(["a", "b", "c", "d"], [Edge("a", "d"), Edge("b", "c"), Edge("b", "d")]) == "0: a b | 1: (d) (d) (d) | 2: d c");

	[Test]
	public void UncrossesAFan() =>
		Verify(Orders(["r", "a", "b", "c", "x", "y", "z"], [Edge("r", "a"), Edge("r", "b"), Edge("r", "c"), Edge("a", "z"), Edge("b", "y"), Edge("c", "x")]) == "0: r | 1: (d) (d) (d) | 2: a b c | 3: (d) (d) (d) | 4: z y x");

	[Test]
	public void BreaksBarycenterTiesWithTheBias() =>
		Verify(Orders(["a", "b", "c", "d", "e"], [Edge("a", "c"), Edge("b", "c"), Edge("a", "d"), Edge("b", "e"), Edge("a", "e"), Edge("b", "d")]) == "0: a b | 1: (d) (d) (d) (d) (d) (d) | 2: c d e");

	[Test]
	public void StartsWithArrayIndexNamesInNumericOrder() =>
		Verify(Orders(["b", "10", "2", "a"], []) == "0: 2 10 b a");

	[Test]
	public void KeepsAClusterBetweenItsBorders() =>
		Verify(Orders(["C", "a C", "b C", "x", "y"], [Edge("x", "a"), Edge("y", "b"), Edge("x", "y")]) == "0: x | 1: (d) (d) | 2: (d) y | 3: (d) (d) | 4: (bl) (b) (d) (d) (br) | 5: (bl) a b (br) | 6: (bl) (br) (b)");

	[Test]
	public void ParentsDummiesInTheClustersTheyCross() =>
		Verify(Orders(["O", "I O", "p I", "q O", "z"], [Edge("z", "p"), Edge("p", "q"), Edge("z", "q")]) == "0: z | 1: (d) (d) | 2: (bl) (b) (d) (d) (br) | 3: (bl) (bl) (b) (d) (br) (d) (br) | 4: (bl) (bl) p (br) (d) (br) | 5: (bl) (bl) (b) (d) (br) (d) (br) | 6: (bl) (d) (d) (br) | 7: (bl) q (br) | 8: (bl) (br) (b)");

	[Test]
	public void SortsLikeV8WhenTheComparatorIsInconsistent()
	{
		// Node 22 gives [5, NaN, 1, 2, 4]: the NaN compares equal, so 5, NaN, 1, 4 is the first run.
		List<double> items = [5, double.NaN, 1, 4, 2];
		JsSort.Sort(items, (a, b) => a - b);

		Verify(items.SequenceEqual([5, double.NaN, 1, 2, 4]));
	}

	private static DagreEdgeInput Edge(string v, string w) => new(v, w, null);

	/// <param name="nodes">Each node as its id, then a space and its parent's id when it has one.</param>
	private static string Orders(string[] nodes, DagreEdgeInput[] edges)
	{
		var input = nodes
			.Select(node => node.Split(' '))
			.Select(parts => new DagreNodeInput(parts[0], parts.Length > 1 ? parts[1] : null, 0, 0))
			.ToList();
		var g = DagreLayout.BuildLayoutGraph(new DagreInput(new(), input, edges));
		DagreLayout.RunRankPhases(g);
		DagreLayout.RunOrderPhases(g);

		var layers = Util.BuildLayerMatrix(g);
		return string.Join(" | ", layers.Select((layer, rank) => $"{rank}: {string.Join(' ', layer.Select(v => Show(g, v)))}"));
	}

	private static string Show(LayoutGraph g, int v)
	{
		var node = g.Node(v);
		return node.Dummy switch
		{
			"edge" => "(d)",
			"edge-label" => "(l)",
			"border" when node.BorderType == "borderLeft" => "(bl)",
			"border" when node.BorderType == "borderRight" => "(br)",
			"border" => "(b)",
			_ => g.Name(v),
		};
	}
}
