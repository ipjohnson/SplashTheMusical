namespace SplashTheMusical.Layout.Dagre;

/// <summary>
/// What dagre's <c>buildLayoutGraph</c> reads from the graph it is given: the graph's options and its nodes and edges
/// in the graph's order. A null option takes dagre's default.
/// </summary>
internal sealed record DagreInput(DagreOptions Options, IReadOnlyList<DagreNodeInput> Nodes, IReadOnlyList<DagreEdgeInput> Edges);

internal sealed record DagreOptions(
	string? RankDir = null,
	double? NodeSep = null,
	double? EdgeSep = null,
	double? RankSep = null,
	double? MarginX = null,
	double? MarginY = null,
	string? Align = null,
	string? Acyclicer = null,
	string? Ranker = null);

/// <param name="Parent">The id of the cluster that holds the node, or null at the top.</param>
internal sealed record DagreNodeInput(string Id, string? Parent, double Width, double Height);

/// <param name="Name">The edge's name in the multigraph, or null for the unnamed edge.</param>
internal sealed record DagreEdgeInput(
	string V,
	string W,
	string? Name,
	double MinLen = 1,
	double Weight = 1,
	double Width = 0,
	double Height = 0,
	double LabelOffset = 10,
	string LabelPos = "r");
