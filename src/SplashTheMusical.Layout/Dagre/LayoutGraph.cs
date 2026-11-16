namespace SplashTheMusical.Layout.Dagre;

/// <summary>The graph dagre's phases work on: a compound multigraph of <see cref="LayoutNode"/> and <see cref="LayoutEdge"/>.</summary>
internal sealed class LayoutGraph : Graph<LayoutNode, LayoutEdge>
{
	public LayoutGraph(GraphLabel label, UniqueIds ids, bool multigraph = true, bool compound = true)
		: base(directed: true, multigraph: multigraph, compound: compound)
	{
		Label = label;
		Ids = ids;
	}

	/// <summary>graphlib's <c>graph()</c>. dagre's derived graphs share it with the graph they come from.</summary>
	public GraphLabel Label { get; }

	public UniqueIds Ids { get; }
}

/// <summary>The graph label of dagre's layout graph.</summary>
internal sealed class GraphLabel
{
	public string RankDir { get; set; } = "tb";

	public double NodeSep { get; set; } = 50;

	public double EdgeSep { get; set; } = 20;

	public double RankSep { get; set; } = 50;

	/// <summary>dagre reads a missing margin as 0.</summary>
	public double? MarginX { get; set; }

	public double? MarginY { get; set; }

	public string? Align { get; set; }

	public string? Acyclicer { get; set; }

	public string? Ranker { get; set; }

	/// <summary>The nesting graph's root node while it is in the graph, or -1.</summary>
	public int NestingRoot { get; set; } = -1;

	public double NodeRankFactor { get; set; } = double.NaN;

	/// <summary>
	/// <c>assignRankMinMax</c>'s result. It calls <c>_.max(maxRank, node.maxRank)</c>, which returns undefined for a
	/// number, so a graph with a cluster gets NaN here, as dagre gets undefined.
	/// </summary>
	public double MaxRank { get; set; } = double.NaN;

	/// <summary><c>normalize.run</c>'s first dummy node of each edge it split, in the order it split them.</summary>
	public List<int> DummyChains { get; } = [];
}

/// <summary>The label of a node of dagre's layout graph. NaN stands for an attribute dagre has not set.</summary>
internal sealed class LayoutNode
{
	public double Width { get; set; }

	public double Height { get; set; }

	/// <summary>The dummy type, such as <c>border</c> or <c>edge-proxy</c>, or null for a node of the input.</summary>
	public string? Dummy { get; set; }

	public double Rank { get; set; } = double.NaN;

	public bool HasRank => !double.IsNaN(Rank);

	public int BorderTop { get; set; } = -1;

	public int BorderBottom { get; set; } = -1;

	public double MinRank { get; set; } = double.NaN;

	public double MaxRank { get; set; } = double.NaN;

	/// <summary>The edge an <c>edge-proxy</c> dummy stands for.</summary>
	public int ProxyEdge { get; set; } = -1;

	/// <summary>The self edges <c>removeSelfEdges</c> took out, in the graph's order.</summary>
	public List<SelfEdge>? SelfEdges { get; set; }

	/// <summary>The edge a dummy of <c>normalize.run</c> stands for. The handle keeps the edge's ends after it is removed.</summary>
	public int EdgeObj { get; set; } = -1;

	/// <summary>The label of the edge a dummy of <c>normalize.run</c> stands for.</summary>
	public LayoutEdge? EdgeLabel { get; set; }

	/// <summary>The label position an <c>edge-label</c> dummy takes from its edge.</summary>
	public string? LabelPos { get; set; }

	/// <summary>A cluster's left border node on each rank from <see cref="MinRank"/>, set by <c>addBorderSegments</c>.</summary>
	public int[]? BorderLeft { get; set; }

	public int[]? BorderRight { get; set; }

	/// <summary><c>borderLeft</c> or <c>borderRight</c> for a node of a cluster's side border.</summary>
	public string? BorderType { get; set; }

	/// <summary>The node's place in its rank, which the order phase sets. -1 until then.</summary>
	public int Order { get; set; } = -1;
}

/// <summary>The label of an edge of dagre's layout graph.</summary>
internal sealed class LayoutEdge
{
	public double MinLen { get; set; } = 1;

	public double Weight { get; set; } = 1;

	public double Width { get; set; }

	public double Height { get; set; }

	public double LabelOffset { get; set; } = 10;

	public string LabelPos { get; set; } = "r";

	public bool Reversed { get; set; }

	/// <summary>The name <c>acyclic</c> keeps for a reversed edge, so that its undo restores it.</summary>
	public string? ForwardName { get; set; }

	public bool NestingEdge { get; set; }

	public double LabelRank { get; set; } = double.NaN;
}

/// <summary>A self edge with the name it had, which <c>removeSelfEdges</c> keeps on its node.</summary>
internal sealed record SelfEdge(int V, int W, string? Name, LayoutEdge Label);

/// <summary>
/// lodash's <c>uniqueId</c>, which names dagre's dummy nodes and reversed edges. lodash counts in one global; here each
/// layout has its own counter. A name decides an order only where two dummies of one prefix are compared as strings.
/// </summary>
internal sealed class UniqueIds(int start = 0)
{
	private int _counter = start;

	public string Next(string prefix) => prefix + (++_counter).ToString(System.Globalization.CultureInfo.InvariantCulture);
}
