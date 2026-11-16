namespace SplashTheMusical.Layout.Dagre;

/// <summary>
/// A port of dagre's <c>nesting-graph.js</c>, from Sander, "Layout of Compound Directed Graphs". It adds a border node
/// at the top and the bottom of each cluster, edges that keep a cluster's nodes between its borders, and a root that
/// connects the graph. Edge minlens are scaled so that nodes and borders never share a rank.
/// </summary>
internal static class NestingGraph
{
	public static void Run(LayoutGraph g)
	{
		var root = Util.AddDummyNode(g, "root", new LayoutNode(), "_root");
		var depths = TreeDepths(g);
		var height = 0;
		foreach (var depth in depths.Values)
		{
			height = Math.Max(height, depth);
		}

		height--;
		var nodeSep = (2 * height) + 1;

		g.Label.NestingRoot = root;

		// Multiplies minlen by nodeSep to align nodes on non-border ranks.
		foreach (var e in g.Edges())
		{
			g.Edge(e).MinLen *= nodeSep;
		}

		// A weight that is enough to keep clusters vertically compact.
		double weight = 0;
		foreach (var e in g.Edges())
		{
			weight += g.Edge(e).Weight;
		}

		weight++;

		foreach (var child in g.Children())
		{
			Dfs(g, root, nodeSep, weight, height, depths, child);
		}

		g.Label.NodeRankFactor = nodeSep;
	}

	public static void Cleanup(LayoutGraph g)
	{
		g.RemoveNode(g.Label.NestingRoot);
		g.Label.NestingRoot = -1;
		foreach (var e in g.Edges())
		{
			if (g.Edge(e).NestingEdge)
			{
				g.RemoveEdge(e);
			}
		}
	}

	private static void Dfs(LayoutGraph g, int root, int nodeSep, double weight, int height, Dictionary<int, int> depths, int v)
	{
		var children = g.Children(v);
		if (children.Length == 0)
		{
			if (v != root)
			{
				g.SetEdge(root, v, new LayoutEdge { Weight = 0, MinLen = nodeSep });
			}

			return;
		}

		var top = Util.AddBorderNode(g, "_bt");
		var bottom = Util.AddBorderNode(g, "_bb");
		var label = g.Node(v);

		g.SetParent(top, v);
		label.BorderTop = top;
		g.SetParent(bottom, v);
		label.BorderBottom = bottom;

		foreach (var child in children)
		{
			Dfs(g, root, nodeSep, weight, height, depths, child);

			var childNode = g.Node(child);
			var childTop = childNode.BorderTop >= 0 ? childNode.BorderTop : child;
			var childBottom = childNode.BorderBottom >= 0 ? childNode.BorderBottom : child;
			var thisWeight = childNode.BorderTop >= 0 ? weight : 2 * weight;
			var minlen = childTop != childBottom ? 1 : height - depths[v] + 1;

			g.SetEdge(top, childTop, new LayoutEdge { Weight = thisWeight, MinLen = minlen, NestingEdge = true });
			g.SetEdge(childBottom, bottom, new LayoutEdge { Weight = thisWeight, MinLen = minlen, NestingEdge = true });
		}

		if (g.Parent(v) == Graph<LayoutNode, LayoutEdge>.Root)
		{
			g.SetEdge(root, top, new LayoutEdge { Weight = 0, MinLen = height + depths[v] });
		}
	}

	private static Dictionary<int, int> TreeDepths(LayoutGraph g)
	{
		var depths = new Dictionary<int, int>();

		void Dfs(int v, int depth)
		{
			foreach (var child in g.Children(v))
			{
				Dfs(child, depth + 1);
			}

			depths[v] = depth;
		}

		foreach (var v in g.Children())
		{
			Dfs(v, 1);
		}

		return depths;
	}
}
