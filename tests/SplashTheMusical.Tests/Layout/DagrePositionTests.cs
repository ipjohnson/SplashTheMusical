using System.Globalization;
using AwesomeAssertions;
using SplashTheMusical.Layout.Dagre;

namespace SplashTheMusical.Tests.Layout;

/// <summary>
/// The whole layout on small graphs with what the recorded layouts leave out: the align option, labels to the left
/// and right of their edge, and a reversed edge. Each expected line is what dagre-d3-es 7.0.14 gives for the same graph:
/// the size, each node's centre, and each edge's points with its label's centre when it has one.
/// </summary>
public class DagrePositionTests
{
	[Test]
	public void UsesTheAlignmentTheGraphNames() =>
		Verify(Layout(new(Align: "UL"), ["a", "b", "c", "d"], [Edge("a", "b"), Edge("a", "c"), Edge("b", "d"), Edge("c", "d"), Edge("a", "d")]) == "size 130x160 | a 20,10 | b 20,80 | c 110,80 | d 110,150 | a-b 20,20 20,45 20,70 | a-c 40,17.77777777777778 110,45 110,70 | b-d 20,90 20,115 90,142.22222222222223 | c-d 110,90 110,115 110,140 | a-d 40,14.827586206896552 165,45 165,80 165,115 125.71428571428571,140");

	[Test]
	public void PutsLabelsBesideTheirEdges() =>
		Verify(Layout(new(RankDir: "LR"), ["a", "b", "c"], [Labelled("a", "b", "l"), Labelled("a", "c", "r")]) == "size 160x110 | a 20,55 | b 140,20 | c 140,90 | a-b 37.14285714285714,45 80,20 120,20 label 80,5 | a-c 37.14285714285714,65 80,90 120,90 label 80,105");

	[Test]
	public void RestoresAReversedEdgeWithItsPointsReversed() =>
		Verify(Layout(new(RankDir: "BT"), ["a", "b"], [Edge("a", "b"), Edge("b", "a")]) == "size 40x90 | a 20,80 | b 20,10 | a-b 17.142857142857142,70 10,45 17.142857142857142,20 | b-a 22.857142857142858,20 30,45 22.857142857142858,70");

	private static DagreEdgeInput Edge(string v, string w) => new(v, w, null);

	private static DagreEdgeInput Labelled(string v, string w, string labelPos) =>
		new(v, w, null, Width: 30, Height: 10, LabelPos: labelPos);

	/// <summary>Lays out nodes 40 wide and 20 high, and writes the result as the dagre-d3-es script does.</summary>
	private static string Layout(DagreOptions options, string[] nodes, DagreEdgeInput[] edges)
	{
		var input = nodes.Select(id => new DagreNodeInput(id, null, 40, 20)).ToList();
		var g = DagreLayout.BuildLayoutGraph(new DagreInput(options, input, edges));
		DagreLayout.RunRankPhases(g);
		DagreLayout.RunOrderPhases(g);
		DagreLayout.RunPositionPhases(g);

		var parts = new List<string>();
		foreach (var v in g.Nodes())
		{
			var node = g.Node(v);
			parts.Add($"{g.Name(v)} {Number(node.X)},{Number(node.Y)}");
		}

		foreach (var e in g.Edges())
		{
			var edge = g.Edge(e);
			var text = $"{g.Name(g.EdgeV(e))}-{g.Name(g.EdgeW(e))} {string.Join(' ', edge.Points!.Select(p => $"{Number(p.X)},{Number(p.Y)}"))}";
			if (edge.HasPosition)
			{
				text += $" label {Number(edge.X)},{Number(edge.Y)}";
			}

			parts.Add(text);
		}

		return $"size {Number(g.Label.Width)}x{Number(g.Label.Height)} | {string.Join(" | ", parts)}";
	}

	private static string Number(double value) => value.ToString(CultureInfo.InvariantCulture);
}
