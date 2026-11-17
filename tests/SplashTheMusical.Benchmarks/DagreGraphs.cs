using System.Globalization;
using SplashTheMusical.Layout.Dagre;

namespace SplashTheMusical.Benchmarks;

/// <summary>
/// Seeded graphs of a given size, shaped like a flowchart: each node after the first has an edge from one of the 8 nodes
/// before it, about a third of the nodes have a second edge from one of the 16 nodes before them, and a tenth of the
/// edges have a label. The same size always gives the same graph. Edges stay between nearby nodes because dagre adds a
/// dummy node for each rank an edge crosses, so an edge from any earlier node makes the layout quadratic in dagre too.
/// </summary>
internal static class DagreGraphs
{
	public static DagreInput Flowchart(int nodes)
	{
		var random = new Xorshift((uint)nodes);
		var nodeInputs = new List<DagreNodeInput>(nodes);
		var edges = new List<DagreEdgeInput>(nodes * 4 / 3);
		for (var i = 0; i < nodes; i++)
		{
			nodeInputs.Add(new DagreNodeInput(Id(i), null, 60 + random.Next(80), 40));
			if (i == 0)
			{
				continue;
			}

			var first = i - 1 - random.Next(Math.Min(i, 8));
			edges.Add(Edge(Id(first), Id(i), random));
			var second = i - 1 - random.Next(Math.Min(i, 16));
			if (second != first && random.Next(3) == 0)
			{
				edges.Add(Edge(Id(second), Id(i), random));
			}
		}

		return new DagreInput(new DagreOptions(RankDir: "TB", NodeSep: 50, RankSep: 50, MarginX: 8, MarginY: 8), nodeInputs, edges);
	}

	/// <summary>Runs every phase of dagre's layout on the input.</summary>
	public static LayoutGraph Layout(DagreInput input)
	{
		var g = DagreLayout.BuildLayoutGraph(input);
		DagreLayout.RunRankPhases(g);
		DagreLayout.RunOrderPhases(g);
		DagreLayout.RunPositionPhases(g);
		return g;
	}

	private static string Id(int i) => "n" + i.ToString(CultureInfo.InvariantCulture);

	private static DagreEdgeInput Edge(string v, string w, Xorshift random) =>
		random.Next(10) == 0
			? new DagreEdgeInput(v, w, null, Width: 40, Height: 20, LabelPos: "c")
			: new DagreEdgeInput(v, w, null);

	/// <summary>Marsaglia's xorshift32, so the graphs do not depend on <see cref="Random"/>'s algorithm.</summary>
	private sealed class Xorshift(uint seed)
	{
		private uint _state = seed * 2654435761u | 1;

		public int Next(int bound)
		{
			_state ^= _state << 13;
			_state ^= _state >> 17;
			_state ^= _state << 5;
			return (int)(_state % (uint)bound);
		}
	}
}
