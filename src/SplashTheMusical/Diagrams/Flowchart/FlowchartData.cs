using SplashTheMusical.Layout.Paths;
using SplashTheMusical.Rendering.Shapes;

namespace SplashTheMusical.Diagrams.Flowchart;

/// <summary>
/// A node of mermaid.js's layout data, as <c>FlowDB.getData</c> makes it, and what the dagre renderer adds to it as it
/// measures, lays out and draws it.
/// </summary>
internal sealed class FlowNode
{
	public required string Id { get; init; }

	public bool IsGroup { get; init; }

	public string Label { get; init; } = "";

	public FlowchartLabelType LabelType { get; init; }

	public string? ParentId { get; set; }

	/// <summary>The shape name, which <see cref="NodeShapes.Find"/> resolves.</summary>
	public string Shape { get; init; } = "squareRect";

	/// <summary>A subgraph's own direction.</summary>
	public string? Dir { get; init; }

	public double Padding { get; init; }

	public string DomId { get; init; } = "";

	public string Classes { get; init; } = "";

	public bool IsDummy { get; init; }

	/// <summary>The source's node, with the icon and image of a <c>@{ }</c> block. Null for a subgraph.</summary>
	public FlowchartNode? Vertex { get; init; }

	// Measured and laid out.

	public double Width { get; set; }

	public double Height { get; set; }

	public double X { get; set; }

	public double Y { get; set; }

	public ShapeGeometry? Geometry { get; set; }

	public FlowchartLabelBox? LabelBox { get; set; }

	/// <summary>A subgraph the extractor took out of its graph, whose <see cref="Graph"/> is laid out on its own.</summary>
	public bool ClusterNode { get; init; }

	public FlowGraph? Graph { get; init; }

	/// <summary>The subgraph's node before it was extracted.</summary>
	public FlowNode? ClusterData { get; init; }

	/// <summary><c>node.diff</c>: how far <c>insertCluster</c> widened the frame for its title, less the padding.</summary>
	public double Diff { get; set; }

	public FlowchartDrawing? Drawing { get; set; }

	/// <summary>Whether <c>insertCluster</c> drew the node as a frame, which edges meet as a rectangle.</summary>
	public bool IsFrame { get; set; }

	public FlowNode Clone() => (FlowNode)MemberwiseClone();
}

/// <summary>An edge of mermaid.js's layout data, and what the dagre renderer adds to it.</summary>
internal sealed class FlowEdge
{
	public required string Id { get; set; }

	public int Index { get; init; }

	public required string Start { get; init; }

	public required string End { get; init; }

	public string Label { get; set; } = "";

	public FlowchartLabelType LabelType { get; init; }

	public double MinLen { get; init; } = 1;

	public string ArrowTypeStart { get; set; } = "none";

	public string ArrowTypeEnd { get; set; } = "arrow_point";

	public FlowchartStroke Stroke { get; init; }

	public string? Curve { get; init; }

	public string Classes { get; init; } = "";

	public bool Animated { get; init; }

	// Measured and laid out.

	public double Width { get; set; }

	public double Height { get; set; }

	public FlowchartLabelBox? LabelBox { get; set; }

	public List<PathPoint>? Points { get; set; }

	public double X { get; set; }

	public double Y { get; set; }

	public bool HasPosition { get; set; }

	public string? FromCluster { get; set; }

	public string? ToCluster { get; set; }

	/// <summary>The id of the self loop a segment belongs to, and the segment's place in it.</summary>
	public string? SelfLoopId { get; set; }

	public int SelfLoopOrder { get; set; } = -1;

	public FlowEdge? OriginalEdge { get; set; }

	public FlowEdge Clone() => (FlowEdge)MemberwiseClone();
}

/// <summary>A graphlib graph of the dagre renderer, with the settings dagre reads from it.</summary>
internal sealed class FlowGraph
{
	public Layout.Dagre.Graph<FlowNode, FlowEdge> G { get; } = new(directed: true, multigraph: true, compound: true);

	public string RankDir { get; set; } = "TB";

	public double NodeSep { get; set; } = 50;

	public double RankSep { get; set; } = 50;

	public double MarginX { get; set; } = 8;

	public double MarginY { get; set; } = 8;
}

/// <summary><c>FlowDB.getData</c> in <c>diagrams/flowchart/flowDb.ts</c>: the nodes and edges the layout gets.</summary>
internal static class FlowchartData
{
	/// <param name="padding"><c>flowchart.padding</c>, or 8 when it is 0.</param>
	/// <param name="defaultCurve"><c>flowchart.curve</c>.</param>
	public static (List<FlowNode> Nodes, List<FlowEdge> Edges) Get(FlowchartDocument document, double padding, string? defaultCurve)
	{
		var subgraphs = document.Subgraphs;
		var lookup = new Dictionary<string, FlowchartSubgraph>(StringComparer.Ordinal);
		foreach (var subgraph in subgraphs)
		{
			lookup[subgraph.Id] = subgraph;
		}

		var subgraphParent = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (var subgraph in subgraphs)
		{
			foreach (var child in subgraph.Nodes)
			{
				if (lookup.ContainsKey(child))
				{
					subgraphParent[child] = subgraph.Id;
				}
			}
		}

		bool IsCollapsed(string id) =>
			lookup.TryGetValue(id, out var s) && s.Metadata is { Kind: MermaidValueKind.Mapping } metadata
			&& metadata.Entries.TryGetValue("view", out var view) && view is { Kind: MermaidValueKind.String, String: "collapsed" };

		string? OutermostCollapsed(string id)
		{
			string? result = null;
			var seen = new HashSet<string>(StringComparer.Ordinal);
			for (string? current = id; current is not null && seen.Add(current); current = subgraphParent.GetValueOrDefault(current))
			{
				if (IsCollapsed(current))
				{
					result = current;
				}
			}

			return result;
		}

		var hidden = new HashSet<string>(StringComparer.Ordinal);
		var collapsedAncestor = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (var subgraph in subgraphs)
		{
			if (OutermostCollapsed(subgraph.Id) is not { } ancestor)
			{
				continue;
			}

			if (subgraph.Id != ancestor)
			{
				_ = hidden.Add(subgraph.Id);
				collapsedAncestor[subgraph.Id] = ancestor;
			}

			foreach (var child in subgraph.Nodes)
			{
				if (child != ancestor)
				{
					_ = hidden.Add(child);
					collapsedAncestor[child] = ancestor;
				}
			}
		}

		var parents = new Dictionary<string, string>(StringComparer.Ordinal);
		var groups = new HashSet<string>(StringComparer.Ordinal);
		for (var i = subgraphs.Count - 1; i >= 0; i--)
		{
			var subgraph = subgraphs[i];
			if (hidden.Contains(subgraph.Id))
			{
				continue;
			}

			if (subgraph.Nodes.Count > 0)
			{
				_ = groups.Add(subgraph.Id);
			}

			foreach (var id in subgraph.Nodes)
			{
				parents[id] = subgraph.Id;
			}
		}

		var nodes = new List<FlowNode>();
		var ids = new HashSet<string>(StringComparer.Ordinal);
		for (var i = subgraphs.Count - 1; i >= 0; i--)
		{
			var subgraph = subgraphs[i];
			if (hidden.Contains(subgraph.Id))
			{
				continue;
			}

			var collapsed = IsCollapsed(subgraph.Id);
			_ = ids.Add(subgraph.Id);
			nodes.Add(new FlowNode
			{
				Id = subgraph.Id,
				Label = subgraph.Title,
				LabelType = subgraph.LabelType,
				ParentId = parents.GetValueOrDefault(subgraph.Id),
				Padding = 8,
				Classes = string.Join(' ', subgraph.Classes),
				Shape = collapsed ? "collapsedGroup" : "rect",
				Dir = subgraph.Direction,
				IsGroup = !collapsed,
				DomId = subgraph.Id,
			});
		}

		for (var i = 0; i < document.Nodes.Count; i++)
		{
			var vertex = document.Nodes[i];
			if (hidden.Contains(vertex.Id) || !ids.Add(vertex.Id))
			{
				continue;
			}

			nodes.Add(new FlowNode
			{
				Id = vertex.Id,
				Label = vertex.Label,
				LabelType = vertex.LabelType,
				ParentId = parents.GetValueOrDefault(vertex.Id),
				Padding = padding,
				Classes = "default " + string.Join(' ', vertex.Classes),
				Shape = FlowchartShapes.Drawn(vertex),
				Vertex = vertex,
				IsGroup = groups.Contains(vertex.Id),
				DomId = "flowchart-" + vertex.Id + "-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture),
			});
		}

		var edges = new List<FlowEdge>();
		for (var index = 0; index < document.Edges.Count; index++)
		{
			var raw = document.Edges[index];
			var start = collapsedAncestor.GetValueOrDefault(raw.Start) ?? raw.Start;
			var end = collapsedAncestor.GetValueOrDefault(raw.End) ?? raw.End;
			if (start == end && (collapsedAncestor.ContainsKey(raw.Start) || collapsedAncestor.ContainsKey(raw.End)))
			{
				continue;
			}

			var (arrowStart, arrowEnd) = raw.Type switch
			{
				FlowchartEdgeType.Circle => ("none", "arrow_circle"),
				FlowchartEdgeType.Cross => ("none", "arrow_cross"),
				FlowchartEdgeType.DoublePoint => ("arrow_point", "arrow_point"),
				FlowchartEdgeType.DoubleCircle => ("arrow_circle", "arrow_circle"),
				FlowchartEdgeType.DoubleCross => ("arrow_cross", "arrow_cross"),
				_ => ("none", "arrow_point"),
			};
			if (raw.Stroke == FlowchartStroke.Invisible || raw.Type == FlowchartEdgeType.Open)
			{
				(arrowStart, arrowEnd) = ("none", "none");
			}

			edges.Add(new FlowEdge
			{
				Id = start == raw.Start && end == raw.End ? raw.Id : $"L_{start}_{end}_{index}",
				Index = index,
				Start = start,
				End = end,
				Label = raw.Label,
				LabelType = raw.LabelType,
				MinLen = raw.Length ?? 1,
				ArrowTypeStart = arrowStart,
				ArrowTypeEnd = arrowEnd,
				Stroke = raw.Stroke,
				Curve = raw.Interpolate ?? document.DefaultInterpolate ?? defaultCurve,
				Classes = raw.Stroke == FlowchartStroke.Invisible ? "" : "edge-thickness-normal edge-pattern-solid flowchart-link",
				Animated = raw.Animate == true || raw.Animation is not null,
			});
		}

		return (nodes, edges);
	}
}
