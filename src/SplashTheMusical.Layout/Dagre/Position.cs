namespace SplashTheMusical.Layout.Dagre;

/// <summary>
/// A port of dagre's <c>position/index.js</c>: gives each node of the graph without its clusters a y from its rank and
/// an x from <see cref="BrandesKopf"/>.
/// </summary>
internal static class Position
{
	public static void Run(LayoutGraph g)
	{
		g = Util.AsNonCompoundGraph(g);
		PositionY(g);
		var layering = Util.BuildLayerMatrix(g);
		var xs = BrandesKopf.PositionX(g, layering);
		foreach (var layer in layering)
		{
			foreach (var v in layer)
			{
				g.Node(v).X = xs[v];
			}
		}
	}

	/// <summary>
	/// Puts each rank below the one before it, ranksep apart, and centres its nodes on the rank's tallest. lodash's
	/// <c>_.max</c> of an empty rank is undefined, so an empty rank gives NaN from there on, as it does in dagre.
	/// </summary>
	private static void PositionY(LayoutGraph g)
	{
		var rankSep = g.Label.RankSep;
		var prevY = 0.0;
		foreach (var layer in Util.BuildLayerMatrix(g))
		{
			var maxHeight = double.NaN;
			foreach (var v in layer)
			{
				var height = g.Node(v).Height;
				if (double.IsNaN(maxHeight) || height > maxHeight)
				{
					maxHeight = height;
				}
			}

			foreach (var v in layer)
			{
				g.Node(v).Y = prevY + (maxHeight / 2);
			}

			prevY += maxHeight + rankSep;
		}
	}
}
