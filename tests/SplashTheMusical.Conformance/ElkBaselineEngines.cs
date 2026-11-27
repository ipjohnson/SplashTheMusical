using SplashTheMusical.Layout.Dagre;
using Sg = Sugiyama;

namespace SplashTheMusical.Conformance;

/// <summary>Runs the dagre port on a recorded ELK input graph.</summary>
internal static class DagreOnElkInput
{
	/// <summary>The graph options mermaid gives dagre for a flowchart at default config: nodesep, edgesep, ranksep and the margins.</summary>
	public static readonly DagreOptions FlowchartDefaults = new(null, 50, 20, 50, 8, 8);

	/// <param name="options">
	/// The graph options mermaid gives dagre for the case, whose rank direction is replaced by ELK's direction.
	/// </param>
	public static CandidateLayout Layout(ElkInputGraph graph, DagreOptions options)
	{
		var rankDir = graph.Direction switch
		{
			"UP" => "BT",
			"RIGHT" => "LR",
			"LEFT" => "RL",
			_ => "TB",
		};
		var nodes = graph.Nodes
			.Select(n =>
			{
				var leaf = graph.Children[n.Id].Count == 0;
				return new DagreNodeInput(
					n.Id,
					n.Parent == graph.Root ? null : n.Parent,
					leaf ? n.Width ?? 0 : 0,
					leaf ? n.Height ?? 0 : 0);
			})
			.ToList();
		var edges = graph.Edges
			.Select(e => new DagreEdgeInput(e.Source, e.Target, e.Id, Width: e.LabelWidth, Height: e.LabelHeight, LabelPos: "c"))
			.ToList();
		var g = DagreLayout.Run(new DagreInput(options with { RankDir = rankDir }, nodes, edges));

		var boxes = new Dictionary<string, LayoutBox>(StringComparer.Ordinal);
		foreach (var node in graph.Nodes)
		{
			var label = g.Node(g.NodeHandle(node.Id));
			boxes[node.Id] = new LayoutBox(label.X - label.Width / 2, label.Y - label.Height / 2, label.Width, label.Height);
		}

		var routes = new Dictionary<string, IReadOnlyList<(double X, double Y)>>(StringComparer.Ordinal);
		foreach (var edge in graph.Edges)
		{
			var e = g.FindEdge(g.NodeHandle(edge.Source), g.NodeHandle(edge.Target), edge.Id);
			if (e >= 0)
			{
				routes[edge.Id] = g.Edge(e).Points ?? [];
			}
		}

		return new CandidateLayout(g.Label.Width, g.Label.Height, boxes, routes);
	}
}

/// <summary>
/// Runs Mermaider's <c>Sugiyama</c> engine on a recorded ELK input graph, with the entry point and options Mermaider's
/// layout engine for the diagram type passes it.
/// </summary>
internal static class SugiyamaOnElkInput
{
	/// <summary>
	/// The mermaider commit that the <c>Sugiyama</c> package of <see cref="PackageVersion"/> was built from, as its nuspec
	/// names it. It is in nullean/mermaider and in the ipjohnson/mermaider fork.
	/// </summary>
	public const string Commit = "d35b8836d585efb13f8caa17b082510e17626502";

	/// <summary>The version of the <c>Sugiyama</c> package in <c>Directory.Packages.props</c>.</summary>
	public const string PackageVersion = "0.17.0";

	public static CandidateLayout Layout(ElkInputGraph graph, string type)
	{
		var direction = graph.Direction switch
		{
			"UP" => Sg.LayoutDirection.BT,
			"RIGHT" => Sg.LayoutDirection.LR,
			"LEFT" => Sg.LayoutDirection.RL,
			_ => Sg.LayoutDirection.TD,
		};
		var nodes = graph.Nodes
			.Where(n => graph.Children[n.Id].Count == 0)
			.Select(n => new Sg.LayoutNode(n.Id, n.Width ?? 0, n.Height ?? 0))
			.ToList();
		var edges = graph.Edges
			.Select(e => new Sg.LayoutEdge(e.Source, e.Target, e.LabelWidth, e.LabelHeight, 1)
			{
				SourceGroup = e.SourceCluster,
				TargetGroup = e.TargetCluster,
			})
			.ToList();
		var subgraphs = Subgraphs(graph, graph.Root);
		var layoutGraph = new Sg.LayoutGraph(direction, nodes, edges, subgraphs);
		var (hierarchical, options) = Options(type, subgraphs.Count > 0);
		var result = hierarchical ? Sg.HierarchicalLayout.Compute(layoutGraph, options) : Sg.SugiyamaLayout.Compute(layoutGraph, options);

		var boxes = new Dictionary<string, LayoutBox>(StringComparer.Ordinal);
		foreach (var node in result.Nodes)
		{
			boxes[node.Id] = new LayoutBox(node.X, node.Y, node.Width, node.Height);
		}

		var groups = new Stack<Sg.LayoutGroupResult>(result.Groups);
		while (groups.TryPop(out var group))
		{
			boxes[group.Id] = new LayoutBox(group.X, group.Y, group.Width, group.Height);
			foreach (var child in group.Children)
			{
				groups.Push(child);
			}
		}

		var routes = new Dictionary<string, IReadOnlyList<(double X, double Y)>>(StringComparer.Ordinal);
		foreach (var edge in result.Edges)
		{
			if (edge.OriginalIndex >= 0 && edge.OriginalIndex < graph.Edges.Count)
			{
				routes[graph.Edges[edge.OriginalIndex].Id] = [.. edge.Points.Select(p => (p.X, p.Y))];
			}
		}

		return new CandidateLayout(result.Width, result.Height, boxes, routes);
	}

	/// <summary>
	/// The entry point and options of Mermaider's engine for the type at <see cref="Commit"/>: <c>LightweightErLayoutEngine</c>
	/// for ER, <c>LightweightClassLayoutEngine</c> for class, <c>RequirementSvgRenderer</c> for requirement and
	/// <c>LightweightLayoutEngine</c> for the rest, with its state settings for state. Mermaider widens the layer spacing
	/// for edge labels and the rows of ER components after the call, which is not repeated here.
	/// </summary>
	private static (bool Hierarchical, Sg.LayoutOptions Options) Options(string type, bool hasClusters) => type switch
	{
		"er" => (false, new Sg.LayoutOptions
		{
			Padding = 20,
			NodeSpacing = 20,
			LayerSpacing = 72,
			CrossingIterations = 8,
			StrictTopDownFanout = true,
			ForceBottomExitFanOut = true,
			TightSourceLayering = true,
			SeparateComponents = false,
			UseRealFirstTiebreaker = true,
			PortAwareLayout = true,
			CrossingRestarts = 160,
		}),
		"class" => (true, new Sg.LayoutOptions
		{
			Padding = 40,
			NodeSpacing = 20,
			LayerSpacing = hasClusters ? 76 : 60,
			NaturalBackEdgeRouting = true,
			ForceBottomExitFanOut = true,
		}),
		"requirement" => (true, new Sg.LayoutOptions { Padding = 24, NodeSpacing = 36, LayerSpacing = 80, NaturalBackEdgeRouting = true }),
		"state" => (true, new Sg.LayoutOptions
		{
			Padding = 16,
			NodeSpacing = 36,
			LayerSpacing = 30,
			ForceBottomExitFanOut = true,
			NaturalBackEdgeRouting = true,
			UseModelOrderForVirtualNodes = true,
		}),
		_ => (true, new Sg.LayoutOptions { Padding = 16, NodeSpacing = 36, LayerSpacing = 40, UseModelOrderForVirtualNodes = true }),
	};

	private static List<Sg.LayoutSubgraph> Subgraphs(ElkInputGraph graph, string parent) =>
		[
			.. graph.Children[parent]
				.Where(id => graph.Children[id].Count > 0)
				.Select(id => new Sg.LayoutSubgraph(
					id,
					graph.ClusterLabels.TryGetValue(id, out var label) ? label : "",
					[.. graph.Children[id].Where(c => graph.Children[c].Count == 0)],
					Subgraphs(graph, id))),
		];
}
