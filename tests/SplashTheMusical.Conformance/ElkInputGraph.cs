namespace SplashTheMusical.Conformance;

/// <summary>
/// The input graph of a recorded ELK call in the terms a layered layout takes: its direction, its leaf nodes, its
/// clusters and its edges, each between two leaves.
/// </summary>
internal sealed class ElkInputGraph
{
	public ElkInputGraph(ElkInput input)
	{
		var root = input.Nodes.FirstOrDefault(n => n.Parent is null)
			?? throw new FormatException("The ELK input has no root node.");
		Root = root.Id;
		Direction = input.Options.FirstOrDefault(o => !o.IsProperty && o.OwnerKind == "node" && o.Owner == Root && o.Key == "elk.direction")?.Value ?? "DOWN";

		var children = new Dictionary<string, List<string>>(StringComparer.Ordinal);
		foreach (var node in input.Nodes)
		{
			children[node.Id] = [];
		}

		foreach (var node in input.Nodes)
		{
			if (node.Parent is { } parent && children.TryGetValue(parent, out var siblings))
			{
				siblings.Add(node.Id);
			}
		}

		Children = children;
		Nodes = [.. input.Nodes.Where(n => n.Parent is not null)];
		Leaves = [.. Nodes.Where(n => children[n.Id].Count == 0).Select(n => n.Id)];
		Clusters = [.. Nodes.Where(n => children[n.Id].Count > 0).Select(n => n.Id)];
		Parents = input.Nodes.Where(n => n.Parent is not null).ToDictionary(n => n.Id, n => n.Parent!, StringComparer.Ordinal);

		var labels = input.Labels
			.Where(l => l.OwnerKind == "node" && l.Index == 0)
			.GroupBy(l => l.Owner, StringComparer.Ordinal)
			.ToDictionary(g => g.Key, g => g.First().Text, StringComparer.Ordinal);
		ClusterLabels = labels;

		var edges = new List<ElkInputEdge>();
		foreach (var edge in input.Edges)
		{
			if (edge.Sources.Count != 1 || edge.Targets.Count != 1)
			{
				throw new FormatException($"The edge '{edge.Id}' has {edge.Sources.Count} sources and {edge.Targets.Count} targets.");
			}

			var label = input.Labels.FirstOrDefault(l => l.OwnerKind == "edge" && l.Owner == edge.Id && l.Index == 0);
			var source = edge.Sources[0];
			var target = edge.Targets[0];
			edges.Add(new ElkInputEdge(
				edge.Id,
				Leaf(source),
				Leaf(target),
				children.TryGetValue(source, out var s) && s.Count > 0 ? source : null,
				children.TryGetValue(target, out var t) && t.Count > 0 ? target : null,
				label?.Width ?? 0,
				label?.Height ?? 0));
		}

		Edges = edges;
	}

	public string Root { get; }

	/// <summary>The root's <c>elk.direction</c>: <c>DOWN</c>, <c>UP</c>, <c>RIGHT</c> or <c>LEFT</c>.</summary>
	public string Direction { get; }

	/// <summary>Every node but the root, in input order.</summary>
	public IReadOnlyList<ElkNodeInput> Nodes { get; }

	/// <summary>The nodes with no children, in input order.</summary>
	public IReadOnlyList<string> Leaves { get; }

	/// <summary>The nodes with children, in input order.</summary>
	public IReadOnlyList<string> Clusters { get; }

	public IReadOnlyDictionary<string, List<string>> Children { get; }

	public IReadOnlyDictionary<string, string> Parents { get; }

	/// <summary>The text of each node's first label.</summary>
	public IReadOnlyDictionary<string, string> ClusterLabels { get; }

	public IReadOnlyList<ElkInputEdge> Edges { get; }

	/// <summary>
	/// The node itself when it is a leaf. For a cluster, its first child that is a leaf, or the same of its first child
	/// when it has none. A layout that takes edges between leaves ends an edge to a cluster there.
	/// </summary>
	private string Leaf(string id)
	{
		if (!Children.TryGetValue(id, out var children))
		{
			throw new FormatException($"An edge ends on '{id}', which is not a node.");
		}

		while (children.Count > 0)
		{
			id = children.FirstOrDefault(c => Children[c].Count == 0) ?? children[0];
			children = Children[id];
		}

		return id;
	}
}

/// <param name="SourceCluster">The cluster the edge was given as its source, whose first leaf is <paramref name="Source"/>, or null.</param>
/// <param name="TargetCluster">The cluster the edge was given as its target, or null.</param>
internal sealed record ElkInputEdge(
	string Id,
	string Source,
	string Target,
	string? SourceCluster,
	string? TargetCluster,
	double LabelWidth,
	double LabelHeight);
