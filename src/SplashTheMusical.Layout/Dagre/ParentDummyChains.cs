namespace SplashTheMusical.Layout.Dagre;

/// <summary>
/// A port of dagre's <c>parent-dummy-chains.js</c>. Each dummy of a split edge gets as parent the cluster it passes
/// through on its rank, on the path from the edge's tail up to the lowest common cluster and down to its head.
/// </summary>
internal static class ParentDummyChains
{
	public static void Run(LayoutGraph g)
	{
		var postorderNums = Postorder(g);

		foreach (var chainStart in g.Label.DummyChains)
		{
			var v = chainStart;
			var node = g.Node(v);
			var edgeObj = node.EdgeObj;
			var (path, lca) = FindPath(g, postorderNums, g.EdgeV(edgeObj), g.EdgeW(edgeObj));
			var pathIdx = 0;
			int pathV;
			var ascending = true;
			var end = g.EdgeW(edgeObj);

			while (v != end)
			{
				node = g.Node(v);

				if (ascending)
				{
					while ((pathV = path[pathIdx]) != lca && g.Node(pathV).MaxRank < node.Rank)
					{
						pathIdx++;
					}

					if (pathV == lca)
					{
						ascending = false;
					}
				}

				if (!ascending)
				{
					while (pathIdx < path.Count - 1 && g.Node(path[pathIdx + 1]).MinRank <= node.Rank)
					{
						pathIdx++;
					}
				}

				g.SetParent(v, path[pathIdx]);
				v = g.Successors(v)[0];
			}
		}
	}

	/// <summary>The path from v up to the lowest common ancestor of v and w and down to w, and that ancestor.</summary>
	private static (List<int> Path, int Lca) FindPath(LayoutGraph g, Dictionary<int, (int Low, int Lim)> postorderNums, int v, int w)
	{
		var vPath = new List<int>();
		var wPath = new List<int>();
		var low = Math.Min(postorderNums[v].Low, postorderNums[w].Low);
		var lim = Math.Max(postorderNums[v].Lim, postorderNums[w].Lim);

		var parent = v;
		do
		{
			parent = g.Parent(parent);
			vPath.Add(parent);
		}
		while (parent != Graph<LayoutNode, LayoutEdge>.Root && (postorderNums[parent].Low > low || lim > postorderNums[parent].Lim));

		var lca = parent;

		parent = w;
		while ((parent = g.Parent(parent)) != lca)
		{
			wPath.Add(parent);
		}

		wPath.Reverse();
		vPath.AddRange(wPath);
		return (vPath, lca);
	}

	private static Dictionary<int, (int Low, int Lim)> Postorder(LayoutGraph g)
	{
		var result = new Dictionary<int, (int Low, int Lim)>();
		var lim = 0;

		void Dfs(int v)
		{
			var low = lim;
			foreach (var child in g.Children(v))
			{
				Dfs(child);
			}

			result[v] = (low, lim++);
		}

		foreach (var v in g.Children())
		{
			Dfs(v);
		}

		return result;
	}
}
