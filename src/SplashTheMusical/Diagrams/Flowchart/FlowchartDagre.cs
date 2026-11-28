// Upstream: packages/mermaid/src/rendering-util/layout-algorithms/dagre/index.js
// Upstream: packages/mermaid/src/rendering-util/layout-algorithms/dagre/mermaid-graphlib.js
using SplashTheMusical.Layout.Dagre;
using SplashTheMusical.Layout.Markers;
using SplashTheMusical.Layout.Paths;
using SplashTheMusical.Rendering.Shapes;
using DagreGraph = SplashTheMusical.Layout.Dagre.Graph<SplashTheMusical.Diagrams.Flowchart.FlowNode, SplashTheMusical.Diagrams.Flowchart.FlowEdge>;

namespace SplashTheMusical.Diagrams.Flowchart;

/// <summary>
/// mermaid.js 12.3.0's dagre renderer for one flowchart: <c>prepareLayoutForDagre</c>, the cluster handling of
/// <c>mermaid-graphlib.js</c>, and the recursive measure, layout and paint of <c>dagre/index.js</c>. A subgraph without
/// edges that cross its border is taken out of its graph and laid out on its own, then placed as one node. The other
/// subgraphs are compound nodes of the graph that holds them, and their edges are moved to a node inside them.
/// </summary>
/// <remarks>An instance holds what mermaid.js keeps in module state for one render: <c>clusterDb</c>, <c>descendants</c> and <c>parents</c>.</remarks>
internal sealed class FlowchartDagre
{
	private readonly OrderedDictionary<string, ClusterInfo> _clusterDb = new(StringComparer.Ordinal);
	private readonly Dictionary<string, List<string>> _descendants = new(StringComparer.Ordinal);
	private readonly FlowchartMeasure _measure;
	private readonly bool _mergeSelfLoops;
	private readonly double _titleTopMargin;
	private readonly double _titleTotalMargin;
	private readonly List<(int Height, DagreInput Input)> _inputs = [];

	/// <summary>The detector id of the state diagram, which the same renderer draws.</summary>
	public const string StateDiagramType = "stateDiagram";

	/// <summary>The detector id of the class diagram, which the same renderer draws.</summary>
	public const string ClassDiagramType = "classDiagram";

	/// <summary>The detector id of the entity relationship diagram, which the same renderer draws.</summary>
	public const string ErDiagramType = "er";

	public FlowchartDagre(FlowchartMeasure measure, string diagramType, double titleTopMargin, double titleBottomMargin)
	{
		_measure = measure;
		// shouldMergeSelfLoopSegments: flowchart-v2, stateDiagram, classDiagram and er among the types STM draws, not flowchart-elk.
		_mergeSelfLoops = diagramType is FlowchartModule.FlowchartV2 or StateDiagramType or ClassDiagramType or ErDiagramType;
		_titleTopMargin = titleTopMargin;
		_titleTotalMargin = titleTopMargin + titleBottomMargin;
	}

	/// <summary>
	/// The input of each dagre layout, in the order mermaid.js calls dagre. <c>measureDagreGraph</c> measures the nodes
	/// of a graph in one <c>Promise.all</c>, so the extracted subgraphs of a graph are measured side by side, and a graph
	/// is laid out once its most deeply nested extracted subgraph is drawn. The graphs are laid out by that depth, then
	/// in the order their measuring started, which is the depth-first order STM lays them out in. The layouts do not
	/// depend on each other's order.
	/// </summary>
	public IReadOnlyList<DagreInput> Inputs => [.. _inputs.OrderBy(input => input.Height).Select(input => input.Input)];

	/// <summary>Lays out and draws the nodes and edges, and returns the drawing of the top <c>g.root</c>.</summary>
	public FlowchartDrawing Render(List<FlowNode> nodes, List<FlowEdge> edges, string direction, double nodeSpacing, double rankSpacing)
	{
		var graph = Prepare(nodes, edges, direction, nodeSpacing, rankSpacing);
		Measure(graph, null);
		Run(graph);
		return Paint(graph, top: true).Drawing;
	}

	private sealed class ClusterInfo
	{
		/// <summary>The node edges of the cluster are moved to.</summary>
		public string? Id { get; set; }

		public FlowNode? ClusterData { get; init; }

		public bool ExternalConnections { get; set; }

		public FlowNode? Node { get; set; }
	}

	// prepareLayoutForDagre

	private FlowGraph Prepare(List<FlowNode> nodes, List<FlowEdge> edges, string direction, double nodeSpacing, double rankSpacing)
	{
		var graph = new FlowGraph { RankDir = direction, NodeSep = nodeSpacing, RankSep = rankSpacing };
		var g = graph.G;
		foreach (var node in nodes)
		{
			var v = g.SetNode(node.Id, node.Clone());
			if (!string.IsNullOrEmpty(node.ParentId))
			{
				g.SetParent(v, g.EnsureNode(node.ParentId));
			}
		}

		foreach (var edge in edges)
		{
			if (edge.Start == edge.End)
			{
				AddSelfLoop(g, edge);
			}
			else
			{
				_ = g.SetEdge(g.EnsureNode(edge.Start), g.EnsureNode(edge.End), edge.Clone(), edge.Id);
			}
		}

		AdjustClustersAndEdges(graph);
		return graph;
	}

	/// <summary>dagre cannot lay out a self loop, so it goes through two small nodes beside the node.</summary>
	private static void AddSelfLoop(DagreGraph g, FlowEdge edge)
	{
		var nodeId = edge.Start;
		var v = g.EnsureNode(nodeId);
		var node = g.NodeOrDefault(v);
		var special1 = nodeId + "---" + nodeId + "---1";
		var special2 = nodeId + "---" + nodeId + "---2";
		foreach (var special in (ReadOnlySpan<string>)[special1, special2])
		{
			var s = g.SetNode(special, new FlowNode
			{
				Id = special,
				DomId = special,
				ParentId = node?.ParentId,
				Shape = "labelRect",
				Padding = 0,
				Width = 10,
				Height = 10,
				IsDummy = true,
			});
			g.SetParent(s, string.IsNullOrEmpty(node?.ParentId) ? DagreGraph.Root : g.EnsureNode(node.ParentId));
		}

		var original = edge.Clone();
		var edge1 = edge.Clone();
		var mid = edge.Clone();
		var edge2 = edge.Clone();
		edge1.OriginalEdge = original;
		edge1.SelfLoopId = original.Id;
		edge1.SelfLoopOrder = 0;
		mid.OriginalEdge = original;
		mid.SelfLoopId = original.Id;
		mid.SelfLoopOrder = 1;
		edge2.OriginalEdge = original;
		edge2.SelfLoopId = original.Id;
		edge2.SelfLoopOrder = 2;
		edge1.Label = "";
		edge1.ArrowTypeEnd = "none";
		edge1.Id = nodeId + "-cyclic-special-1";
		mid.ArrowTypeStart = "none";
		mid.ArrowTypeEnd = "none";
		mid.Id = nodeId + "-cyclic-special-mid";
		edge2.Label = "";
		edge2.ArrowTypeStart = "none";
		if (node?.IsGroup == true)
		{
			edge1.FromCluster = nodeId;
			edge2.ToCluster = nodeId;
		}

		edge2.Id = nodeId + "-cyclic-special-2";
		var s1 = g.NodeHandle(special1);
		var s2 = g.NodeHandle(special2);
		_ = g.SetEdge(v, s1, edge1, nodeId + "-cyclic-special-0");
		_ = g.SetEdge(s1, s2, mid, nodeId + "-cyclic-special-1");
		_ = g.SetEdge(s2, v, edge2, nodeId + "-cyclic-special-2");
	}

	// mermaid-graphlib.js

	private static string[] ChildNames(DagreGraph g, string? id)
	{
		var v = id is null ? -1 : g.NodeHandle(id);
		if (v < 0)
		{
			return [];
		}

		var children = g.Children(v);
		var names = new string[children.Length];
		for (var i = 0; i < children.Length; i++)
		{
			names[i] = g.Name(children[i]);
		}

		return names;
	}

	private static bool HasChildren(DagreGraph g, string? id) => id is not null && g.NodeHandle(id) is var v and >= 0 && g.HasChildren(v);

	private static string? ParentName(DagreGraph g, string? id)
	{
		var v = id is null ? -1 : g.NodeHandle(id);
		if (v < 0)
		{
			return null;
		}

		var parent = g.Parent(v);
		return parent == DagreGraph.Root ? null : g.Name(parent);
	}

	private bool IsDescendant(string? id, string? ancestorId) =>
		id is not null && ancestorId is not null && _descendants.TryGetValue(ancestorId, out var list) && list.Contains(id);

	private bool EdgeInCluster(string v, string w, string clusterId)
	{
		if (v == clusterId || w == clusterId)
		{
			return false;
		}

		var descendants = _descendants.GetValueOrDefault(clusterId);
		return descendants is not null && (descendants.Contains(v) || descendants.Contains(w));
	}

	private List<string> ExtractDescendants(string id, DagreGraph g)
	{
		var children = ChildNames(g, id);
		var result = new List<string>(children);
		foreach (var child in children)
		{
			result.AddRange(ExtractDescendants(child, g));
		}

		return result;
	}

	/// <summary>The edges of id1, with id1 as tail replaced by id2, that id2 also has.</summary>
	private static int CommonEdgeCount(DagreGraph g, string? id1, string? id2)
	{
		var edges = g.Edges();
		var count = 0;
		foreach (var e in edges)
		{
			var v = g.Name(g.EdgeV(e));
			var w = g.Name(g.EdgeW(e));
			if (v != id1 && w != id1)
			{
				continue;
			}

			var pv = v == id1 ? id2 : v;
			var pw = w;
			foreach (var f in edges)
			{
				var fv = g.Name(g.EdgeV(f));
				var fw = g.Name(g.EdgeW(f));
				if ((fv == id2 || fw == id2) && fv == pv && fw == pw)
				{
					count++;
					break;
				}
			}
		}

		return count;
	}

	private static string? FindNonClusterChild(string id, DagreGraph g, string? clusterId)
	{
		var children = ChildNames(g, id);
		if (children.Length < 1)
		{
			return id;
		}

		string? reserve = null;
		foreach (var child in children)
		{
			var found = FindNonClusterChild(child, g, clusterId);
			var common = CommonEdgeCount(g, clusterId, found);
			if (!string.IsNullOrEmpty(found))
			{
				if (common > 0)
				{
					reserve = found;
				}
				else
				{
					return found;
				}
			}
		}

		return reserve;
	}

	private string GetAnchorId(string id) =>
		_clusterDb.TryGetValue(id, out var cluster) && cluster.ExternalConnections && !string.IsNullOrEmpty(cluster.Id) ? cluster.Id : id;

	private void AdjustClustersAndEdges(FlowGraph graph)
	{
		var g = graph.G;
		foreach (var v in g.Nodes())
		{
			if (g.HasChildren(v))
			{
				var id = g.Name(v);
				_descendants[id] = ExtractDescendants(id, g);
				_clusterDb[id] = new ClusterInfo { Id = FindNonClusterChild(id, g, id), ClusterData = g.NodeOrDefault(v) };
			}
		}

		foreach (var v in g.Nodes())
		{
			if (!g.HasChildren(v))
			{
				continue;
			}

			var id = g.Name(v);
			foreach (var e in g.Edges())
			{
				var d1 = IsDescendant(g.Name(g.EdgeV(e)), id);
				var d2 = IsDescendant(g.Name(g.EdgeW(e)), id);
				if (d1 ^ d2)
				{
					_clusterDb[id].ExternalConnections = true;
				}
			}
		}

		foreach (var id in _clusterDb.Keys.ToList())
		{
			var cluster = _clusterDb[id];
			var nonClusterChild = cluster.Id;
			var parent = ParentName(g, nonClusterChild);
			if (parent != id && parent is not null && _clusterDb.TryGetValue(parent, out var parentCluster) && !parentCluster.ExternalConnections)
			{
				cluster.Id = parent;
			}

			var hasDirectOutgoingEdge = false;
			foreach (var e in g.Edges())
			{
				hasDirectOutgoingEdge |= g.Name(g.EdgeV(e)) == id;
			}

			if (!string.IsNullOrEmpty(nonClusterChild) && cluster.ExternalConnections && hasDirectOutgoingEdge && IsNodeInExtractableCluster(g, nonClusterChild, id)
				&& FindSafeAnchorNode(g, id, ParentName(g, nonClusterChild)) is { } safeAnchor)
			{
				cluster.Id = safeAnchor;
			}
		}

		foreach (var e in g.Edges())
		{
			var edge = g.Edge(e);
			var ev = g.Name(g.EdgeV(e));
			var ew = g.Name(g.EdgeW(e));
			if (!_clusterDb.ContainsKey(ev) && !_clusterDb.ContainsKey(ew))
			{
				continue;
			}

			var v = GetAnchorId(ev);
			var w = GetAnchorId(ew);
			var name = g.EdgeName(e);
			g.RemoveEdge(e);
			if (v != ev)
			{
				if (ParentName(g, v) is { } parent && _clusterDb.TryGetValue(parent, out var cluster))
				{
					cluster.ExternalConnections = true;
				}

				edge.FromCluster = ev;
			}

			if (w != ew)
			{
				if (ParentName(g, w) is { } parent && _clusterDb.TryGetValue(parent, out var cluster))
				{
					cluster.ExternalConnections = true;
				}

				edge.ToCluster = ew;
			}

			_ = g.SetEdge(g.EnsureNode(v), g.EnsureNode(w), edge, name);
		}

		Extractor(graph, 0);
	}

	private void Extractor(FlowGraph graph, int depth)
	{
		if (depth > 10)
		{
			return;
		}

		var g = graph.G;
		var nodes = g.Nodes().Select(g.Name).ToArray();
		var hasChildren = false;
		foreach (var node in nodes)
		{
			hasChildren |= HasChildren(g, node);
		}

		if (!hasChildren)
		{
			return;
		}

		foreach (var node in nodes)
		{
			if (!_clusterDb.TryGetValue(node, out var cluster) || cluster.ExternalConnections || !HasChildren(g, node))
			{
				continue;
			}

			var dir = graph.RankDir == "TB" ? "LR" : "TB";
			if (!string.IsNullOrEmpty(cluster.ClusterData?.Dir))
			{
				dir = cluster.ClusterData.Dir;
			}

			var clusterGraph = new FlowGraph { RankDir = dir, NodeSep = 50, RankSep = 50 };
			Copy(node, graph, clusterGraph, node);
			_ = g.SetNode(node, new FlowNode { Id = node, ClusterNode = true, ClusterData = cluster.ClusterData, Graph = clusterGraph });
		}

		foreach (var v in g.Nodes())
		{
			if (g.NodeOrDefault(v) is { ClusterNode: true, Graph: { } inner })
			{
				Extractor(inner, depth + 1);
			}
		}
	}

	/// <summary>Moves the members of a cluster, with the edges between them, into the graph extracted for <paramref name="rootId"/>.</summary>
	private void Copy(string clusterId, FlowGraph graph, FlowGraph newGraph, string rootId)
	{
		var g = graph.G;
		var n = newGraph.G;
		var nodes = new List<string>(ChildNames(g, clusterId));
		if (clusterId != rootId)
		{
			nodes.Add(clusterId);
		}

		foreach (var node in nodes)
		{
			var v = g.NodeHandle(node);
			if (v < 0)
			{
				continue;
			}

			if (g.HasChildren(v))
			{
				Copy(node, graph, newGraph, rootId);
			}
			else
			{
				var nv = n.SetNode(node, g.NodeOrDefault(v)!);
				var parent = ParentName(g, node);
				if (rootId != parent)
				{
					n.SetParent(nv, parent is null ? DagreGraph.Root : n.EnsureNode(parent));
				}

				if (clusterId != rootId && node != clusterId)
				{
					n.SetParent(nv, n.EnsureNode(clusterId));
				}

				foreach (var e in g.NodeEdges(v))
				{
					var ev = g.Name(g.EdgeV(e));
					var ew = g.Name(g.EdgeW(e));
					if (EdgeInCluster(ev, ew, rootId))
					{
						_ = n.SetEdge(n.EnsureNode(ev), n.EnsureNode(ew), g.Edge(e), g.EdgeName(e));
					}
				}
			}

			g.RemoveNode(g.NodeHandle(node));
		}
	}

	private bool IsNodeInExtractableCluster(DagreGraph g, string node, string rootId)
	{
		for (var parent = ParentName(g, node); parent is not null && parent != rootId; parent = ParentName(g, parent))
		{
			if (_clusterDb.TryGetValue(parent, out var cluster) && !cluster.ExternalConnections)
			{
				return true;
			}
		}

		return false;
	}

	private string? FindSafeAnchorNode(DagreGraph g, string clusterId, string? excludedCluster)
	{
		foreach (var child in ChildNames(g, clusterId))
		{
			if (child == excludedCluster || IsDescendant(child, excludedCluster))
			{
				continue;
			}

			var candidate = FindNonClusterChild(child, g, clusterId);
			if (!string.IsNullOrEmpty(candidate) && !IsNodeInExtractableCluster(g, candidate, clusterId))
			{
				return candidate;
			}
		}

		return null;
	}

	private static List<string> SortNodesByHierarchy(DagreGraph g)
	{
		static List<string> Sorter(DagreGraph g, string[] nodes)
		{
			var result = new List<string>(nodes);
			foreach (var node in nodes)
			{
				result.AddRange(Sorter(g, ChildNames(g, node)));
			}

			return result;
		}

		var roots = g.Children(DagreGraph.Root);
		return Sorter(g, [.. roots.Select(g.Name)]);
	}

	// measureDagreGraph, runDagreGraphLayout and paintDagreLayoutCore

	private void Measure(FlowGraph graph, FlowNode? parentCluster)
	{
		var g = graph.G;
		foreach (var v in g.Nodes())
		{
			if (!g.HasNode(v))
			{
				continue;
			}

			var node = g.NodeOrDefault(v);
			if (parentCluster?.ClusterData is { } clusterData)
			{
				var cluster = g.SetNode(parentCluster.Id, clusterData.Clone());
				if (g.Parent(v) == DagreGraph.Root && v != cluster)
				{
					g.SetParent(v, cluster);
				}
			}

			if (node is null)
			{
				continue;
			}

			if (node is { ClusterNode: true, Graph: { } inner })
			{
				inner.RankSep = graph.RankSep + 25;
				inner.NodeSep = graph.NodeSep;
				var (drawing, diff) = RenderSubgraph(inner, node);
				var bounds = drawing.Bounds();
				node.Width = bounds.IsEmpty ? 0 : bounds.Width;
				node.Height = bounds.IsEmpty ? 0 : bounds.Height;
				node.Diff = diff;
				node.Drawing = drawing;
			}
			else if (g.HasChildren(v))
			{
				_clusterDb[node.Id] = new ClusterInfo { Id = FindNonClusterChild(node.Id, g, null), Node = node };
			}
			else
			{
				_measure.Node(node, graph.RankDir);
			}
		}

		foreach (var e in g.Edges())
		{
			var edge = g.Edge(e);
			if (_mergeSelfLoops && edge.SelfLoopOrder >= 0 && edge.SelfLoopOrder != 1)
			{
				continue;
			}

			_measure.Edge(edge);
		}
	}

	private (FlowchartDrawing Drawing, double Diff) RenderSubgraph(FlowGraph graph, FlowNode parentCluster)
	{
		Measure(graph, parentCluster);
		Run(graph);
		return Paint(graph, top: false);
	}

	private void Run(FlowGraph graph)
	{
		var g = graph.G;
		var nodes = new List<DagreNodeInput>(g.NodeCount);
		foreach (var v in g.Nodes())
		{
			var parent = g.Parent(v);
			var node = g.NodeOrDefault(v);
			nodes.Add(new DagreNodeInput(g.Name(v), parent == DagreGraph.Root ? null : g.Name(parent), node?.Width ?? 0, node?.Height ?? 0));
		}

		var edges = new List<DagreEdgeInput>(g.EdgeCount);
		foreach (var e in g.Edges())
		{
			var edge = g.Edge(e);
			edges.Add(new DagreEdgeInput(g.Name(g.EdgeV(e)), g.Name(g.EdgeW(e)), g.EdgeName(e), edge.MinLen, 1, edge.Width, edge.Height, 10, edge.LabelPos));
		}

		var input = new DagreInput(
			new DagreOptions(RankDir: graph.RankDir, NodeSep: graph.NodeSep, RankSep: graph.RankSep, MarginX: graph.MarginX, MarginY: graph.MarginY),
			nodes,
			edges);
		_inputs.Add((Height(graph), input));
		var laid = DagreLayout.Run(input);

		foreach (var v in g.Nodes())
		{
			if (g.NodeOrDefault(v) is not { } node)
			{
				continue;
			}

			var placed = laid.Node(laid.NodeHandle(g.Name(v)));
			node.X = placed.X;
			node.Y = placed.Y;
			if (g.HasChildren(v))
			{
				node.Width = placed.Width;
				node.Height = placed.Height;
			}
		}

		foreach (var e in g.Edges())
		{
			var edge = g.Edge(e);
			var le = laid.FindEdge(laid.NodeHandle(g.Name(g.EdgeV(e))), laid.NodeHandle(g.Name(g.EdgeW(e))), g.EdgeName(e));
			if (le < 0)
			{
				continue;
			}

			var routed = laid.Edge(le);
			edge.Points = routed.Points is { } points ? [.. points.Select(p => new PathPoint(p.X, p.Y))] : [];
			edge.HasPosition = routed.HasPosition;
			if (routed.HasPosition)
			{
				edge.X = routed.X;
				edge.Y = routed.Y;
			}
		}
	}

	/// <summary>How deeply extracted subgraphs nest in the graph: 0 when it holds none.</summary>
	private static int Height(FlowGraph graph)
	{
		var g = graph.G;
		var height = 0;
		foreach (var v in g.Nodes())
		{
			if (g.NodeOrDefault(v) is { ClusterNode: true, Graph: { } inner })
			{
				height = Math.Max(height, Height(inner) + 1);
			}
		}

		return height;
	}

	private (FlowchartDrawing Drawing, double Diff) Paint(FlowGraph graph, bool top)
	{
		var g = graph.G;
		var drawing = new FlowchartDrawing();
		foreach (var name in SortNodesByHierarchy(g))
		{
			var v = g.NodeHandle(name);
			if (g.NodeOrDefault(v) is not { } node)
			{
				continue;
			}

			if (node.ClusterNode)
			{
				node.Y += _titleTotalMargin;
				if (_clusterDb.TryGetValue(node.Id, out var info))
				{
					info.Node = node;
				}

				drawing.Nodes.Add(new FlowchartNodeDrawing
				{
					Id = node.Id,
					DomId = node.Id,
					X = node.X + node.Diff - (node.Width / 2),
					Y = node.Y - (node.Height / 2) - 8,
					Subgraph = node.Drawing,
				});
			}
			else if (g.HasChildren(v))
			{
				node.Height += _titleTotalMargin;
				drawing.Clusters.Add(InsertCluster(node));
				if (_clusterDb.TryGetValue(node.Id, out var info))
				{
					info.Node = node;
				}
				else
				{
					_clusterDb[node.Id] = new ClusterInfo { Node = node };
				}
			}
			else
			{
				node.Y += _titleTotalMargin / 2;
				var (nodeStyles, labelStyles) = FlowchartStyles.NodeStyles(node.CssCompiledStyles, node.CssStyles);
				drawing.Nodes.Add(new FlowchartNodeDrawing
				{
					Id = node.Id,
					DomId = node.DomId,
					Classes = node.IsGroup ? "node cluster" : ("node " + node.Classes).TrimEnd(),
					X = node.X,
					Y = node.Y,
					Geometry = node.Geometry,
					Label = node.LabelBox,
					Labels = node.Labels,
					NodeStyles = nodeStyles,
					LabelStyles = labelStyles,
					ColorId = node.Shape is "collapsedGroup" or "classBox" or "erBox" or "requirementBox" ? ColorId(node) : null,
					Icon = node.Vertex?.Icon,
					IconColor = node.Vertex?.Icon is null ? null : FlowchartStyles.StyleValue(node.CssCompiledStyles, node.CssStyles, "stroke"),
					Image = node.Vertex?.Image,
					IsGroup = node.IsGroup,
					IsDummy = node.IsDummy,
				});
			}
		}

		var offset = _titleTotalMargin / 2;
		foreach (var (edge, start, end) in EdgesToRender(graph, offset))
		{
			if (edge.Points is { } points)
			{
				for (var i = 0; i < points.Count; i++)
				{
					points[i] = points[i] with { Y = points[i].Y + offset };
				}
			}

			var startNode = g.NodeOrDefault(g.NodeHandle(start));
			var endNode = g.NodeOrDefault(g.NodeHandle(end));
			drawing.Edges.Add(InsertEdge(edge, startNode, endNode));
		}

		var diff = 0.0;
		if (!top)
		{
			foreach (var v in g.Nodes())
			{
				if (g.NodeOrDefault(v) is { IsGroup: true } n)
				{
					diff = n.Diff;
				}
			}
		}

		return (drawing, diff);
	}

	/// <summary><c>insertCluster</c>'s <c>rect</c>: a frame at least as wide as its title and the padding, with the title at its top.</summary>
	private FlowchartClusterDrawing InsertCluster(FlowNode node) => node.Shape switch
	{
		"roundedWithTitle" => RoundedWithTitle(node),
		"divider" => UntitledCluster(node, "divider"),
		"noteGroup" => UntitledCluster(node, "noteGroup"),
		_ => RectCluster(node),
	};

	/// <summary>
	/// <c>insertCluster</c>'s <c>roundedWithTitle</c>, a composite state: an outer frame at least as wide as its title
	/// and the padding, and an inner frame below the title.
	/// </summary>
	private FlowchartClusterDrawing RoundedWithTitle(FlowNode node)
	{
		var label = _measure.StateClusterLabel(node);
		var narrow = node.Width <= label.Width + node.Padding;
		var width = narrow ? label.Width + node.Padding : node.Width;
		node.Diff = narrow ? ((width - node.Width) / 2) - node.Padding : -node.Padding;
		var height = node.Height;
		var x = node.X - (width / 2);
		var y = node.Y - (height / 2);
		var innerY = node.Y - (node.Height / 2) + label.Height + 2;
		var innerHeight = node.Height - label.Height - 6;
		var (nodeStyles, labelStyles) = FlowchartStyles.NodeStyles(node.CssCompiledStyles, node.CssStyles);
		var cluster = new FlowchartClusterDrawing
		{
			Id = node.Id,
			DomId = node.DomId,
			Shape = node.Shape,
			Classes = node.Classes,
			X = x,
			Y = y,
			Width = width,
			Height = height,
			Inner = (innerY, innerHeight),
			Label = label,
			NodeStyles = nodeStyles,
			LabelStyles = labelStyles,
			ColorId = ColorId(node),
			LabelX = node.X - (label.Width / 2),
			LabelY = y + 1 - (label.Html ? 0 : 3),
		};
		node.Width = width;
		node.Height = height;
		node.IsFrame = true;
		return cluster;
	}

	/// <summary>
	/// <c>insertCluster</c>'s <c>divider</c>, a concurrent region drawn as a dashed frame, and <c>noteGroup</c>, the
	/// invisible frame around a note and its state. Neither draws its label.
	/// </summary>
	private FlowchartClusterDrawing UntitledCluster(FlowNode node, string shape)
	{
		if (shape == "divider")
		{
			node.Diff = -node.Padding;
		}

		var (nodeStyles, labelStyles) = FlowchartStyles.NodeStyles(node.CssCompiledStyles, node.CssStyles);
		var cluster = new FlowchartClusterDrawing
		{
			Id = node.Id,
			DomId = node.DomId,
			Shape = shape,
			Classes = shape == "noteGroup" ? "note-cluster" : node.Classes,
			X = node.X - (node.Width / 2),
			Y = node.Y - (node.Height / 2),
			Width = node.Width,
			Height = node.Height,
			Label = FlowchartLabelBox.Empty(_measure.EdgeHtmlLabels),
			NodeStyles = nodeStyles,
			LabelStyles = labelStyles,
			ColorId = shape == "divider" ? ColorId(node) : null,
		};
		node.IsFrame = true;
		return cluster;
	}

	private FlowchartClusterDrawing RectCluster(FlowNode node)
	{
		var label = _measure.ClusterLabel(node);
		var width = node.Width <= label.Width + node.Padding ? label.Width + node.Padding : node.Width;
		node.Diff = node.Width <= label.Width + node.Padding ? ((width - node.Width) / 2) - node.Padding : -node.Padding;
		var height = node.Height;
		var x = node.X - (width / 2);
		var y = node.Y - (height / 2);
		var (nodeStyles, labelStyles) = FlowchartStyles.NodeStyles(node.CssCompiledStyles, node.CssStyles);
		var cluster = new FlowchartClusterDrawing
		{
			Id = node.Id,
			DomId = node.Id,
			Classes = ("cluster " + node.Classes).TrimEnd(),
			X = x,
			Y = y,
			Width = width,
			Height = height,
			Label = label,
			NodeStyles = nodeStyles,
			LabelStyles = labelStyles,
			ColorId = ColorId(node),
			LabelX = node.X - (label.Width / 2),
			LabelY = node.Y - (node.Height / 2) + _titleTopMargin,
		};
		node.Width = width;
		node.Height = height;
		node.IsFrame = true;
		return cluster;
	}

	// getEdgesToRender: a self loop's three segments become one edge.

	private List<(FlowEdge Edge, string Start, string End)> EdgesToRender(FlowGraph graph, double yOffset)
	{
		var g = graph.G;
		var groups = new OrderedDictionary<string, List<(FlowEdge Edge, string Start, string End)>>(StringComparer.Ordinal);
		var result = new List<(FlowEdge, string, string)>();
		foreach (var e in g.Edges())
		{
			var edge = g.Edge(e);
			var start = g.Name(g.EdgeV(e));
			var end = g.Name(g.EdgeW(e));
			if (_mergeSelfLoops && edge.SelfLoopId is { } key)
			{
				if (!groups.TryGetValue(key, out var list))
				{
					groups[key] = list = [];
				}

				list.Add((edge, start, end));
			}
			else
			{
				result.Add((edge, start, end));
			}
		}

		foreach (var segments in groups.Values)
		{
			if (segments.Count != 3)
			{
				result.AddRange(segments);
				continue;
			}

			segments.Sort((a, b) => a.Edge.SelfLoopOrder.CompareTo(b.Edge.SelfLoopOrder));
			var (first, middle, last) = (segments[0].Edge, segments[1].Edge, segments[2].Edge);
			var original = first.OriginalEdge ?? middle.OriginalEdge ?? last.OriginalEdge ?? middle;
			var node = g.NodeOrDefault(g.NodeHandle(original.Start));
			if (node is null)
			{
				result.AddRange(segments);
				continue;
			}

			var side = SelfLoopSide(g, node, segments, original.Start, graph.RankDir);
			var points = SelfLoopPoints(node, side, yOffset, middle.Width);
			var (lx, ly) = SelfLoopLabelPosition(node, points, side, yOffset, middle.Width, middle.Height);
			var merged = original.Clone();
			merged.Points = points;
			merged.X = lx;
			merged.Y = ly;
			merged.HasPosition = true;
			merged.Width = middle.Width;
			merged.Height = middle.Height;
			merged.LabelBox = middle.LabelBox;
			merged.FromCluster = first.FromCluster ?? middle.FromCluster ?? last.FromCluster;
			merged.ToCluster = first.ToCluster ?? middle.ToCluster ?? last.ToCluster;
			merged.SelfLoopId = null;
			merged.SelfLoopOrder = -1;
			merged.OriginalEdge = null;
			result.Add((merged, original.Start, original.End));
		}

		return result;
	}

	private static string DefaultSelfLoopSide(string rankDir) => rankDir switch
	{
		"BT" => "bottom",
		"LR" => "right",
		"RL" => "left",
		_ => "top",
	};

	private static string SelfLoopSide(DagreGraph g, FlowNode node, List<(FlowEdge Edge, string Start, string End)> segments, string nodeId, string rankDir)
	{
		var hints = new List<PathPoint>();
		var dummies = new List<string>();
		foreach (var (_, start, end) in segments)
		{
			if (start != nodeId && !dummies.Contains(start))
			{
				dummies.Add(start);
			}

			if (end != nodeId && !dummies.Contains(end))
			{
				dummies.Add(end);
			}
		}

		foreach (var id in dummies)
		{
			if (g.NodeOrDefault(g.NodeHandle(id)) is { } dummy && !double.IsNaN(dummy.X) && !double.IsNaN(dummy.Y))
			{
				hints.Add(new PathPoint(dummy.X, dummy.Y));
			}
		}

		if (hints.Count == 0)
		{
			foreach (var (edge, _, _) in segments)
			{
				hints.AddRange(edge.Points ?? []);
			}
		}

		if (hints.Count == 0)
		{
			return DefaultSelfLoopSide(rankDir);
		}

		double cx = 0, cy = 0;
		foreach (var point in hints)
		{
			cx += point.X / hints.Count;
			cy += point.Y / hints.Count;
		}

		var dx = cx - node.X;
		var dy = cy - node.Y;
		if (Math.Abs(dx) > Math.Abs(dy))
		{
			return dx > 0 ? "right" : "left";
		}

		if (Math.Abs(dy) > 0)
		{
			return dy > 0 ? "bottom" : "top";
		}

		return DefaultSelfLoopSide(rankDir);
	}

	private static List<PathPoint> SelfLoopPoints(FlowNode node, string side, double yOffset, double labelWidth)
	{
		var x = node.X;
		var y = node.Y - yOffset;
		var halfWidth = node.Width / 2;
		var halfHeight = node.Height / 2;
		var maxSpan = Math.Max(36, Math.Min(100, node.Width * 0.8));
		var span = Math.Clamp(Math.Max(labelWidth, node.Width * 0.35), 36, Math.Max(36, maxSpan));
		var depth = Math.Clamp(Math.Min(node.Width, node.Height) * 0.45, 24, 48);
		switch (side)
		{
			case "bottom":
				var bottom = y + halfHeight;
				return [new(x - (span / 2), bottom), new(x - (span / 2), bottom + depth), new(x + (span / 2), bottom + depth), new(x + (span / 2), bottom)];
			case "right":
				var right = x + halfWidth;
				return [new(right, y - (span / 2)), new(right + depth, y - (span / 2)), new(right + depth, y + (span / 2)), new(right, y + (span / 2))];
			case "left":
				var left = x - halfWidth;
				return [new(left, y - (span / 2)), new(left - depth, y - (span / 2)), new(left - depth, y + (span / 2)), new(left, y + (span / 2))];
			default:
				var topY = y - halfHeight;
				return [new(x - (span / 2), topY), new(x - (span / 2), topY - depth), new(x + (span / 2), topY - depth), new(x + (span / 2), topY)];
		}
	}

	private static (double X, double Y) SelfLoopLabelPosition(FlowNode node, List<PathPoint> points, string side, double yOffset, double labelWidth, double labelHeight)
	{
		const double Gap = 4;
		var x = node.X;
		var y = node.Y - yOffset;
		return side switch
		{
			"bottom" => (x, points.Max(p => p.Y) + (labelHeight / 2) + Gap),
			"right" => (points.Max(p => p.X) + (labelWidth / 2) + Gap, y),
			"left" => (points.Min(p => p.X) - (labelWidth / 2) - Gap, y),
			_ => (x, points.Min(p => p.Y) - (labelHeight / 2) - Gap),
		};
	}

	// insertEdge and positionEdgeLabel

	/// <summary>
	/// <c>insertEdge</c> as the block renderer calls it, between nodes it placed itself: the edge has no <c>look</c>, and
	/// <c>positionEdgeLabel</c> gets only the original points, so the label stays at the edge's <c>x</c> and <c>y</c>.
	/// </summary>
	internal FlowchartEdgeDrawing PlacedEdge(FlowEdge edge, FlowNode? tail, FlowNode? head) => InsertEdge(edge, tail, head, placed: true);

	private FlowchartEdgeDrawing InsertEdge(FlowEdge edge, FlowNode? tail, FlowNode? head, bool placed = false)
	{
		var look = placed ? "" : _measure.Look;
		var original = edge.Points ?? [];
		var points = new List<PathPoint>(original);
		var changed = false;
		if (tail is not null && head is not null && HasIntersect(tail) && HasIntersect(head) && points.Count >= 3)
		{
			var inner = points.GetRange(1, points.Count - 2);
			points = [Intersect(tail, inner[0]), .. inner, Intersect(head, inner[^1])];
		}

		if (edge.ToCluster is { } toCluster && _clusterDb.TryGetValue(toCluster, out var to) && to.Node is { } toNode)
		{
			points = CutPathAtIntersect(original, toNode);
			changed = true;
		}

		if (edge.FromCluster is { } fromCluster && _clusterDb.TryGetValue(fromCluster, out var from) && from.Node is { } fromNode)
		{
			points.Reverse();
			points = CutPathAtIntersect(points, fromNode);
			points.Reverse();
			changed = true;
		}

		var curve = CurveNames.Parse(edge.Curve);
		var data = points.Count > 0 ? EdgePath.ToString([.. points], curve, edge.ArrowTypeStart, edge.ArrowTypeEnd) : "";
		var classes = " " + StrokeClasses(edge) + (edge.Classes.Length > 0 ? " " + edge.Classes : "");
		var useMargin = !edge.Animated && look == "neo";
		var markerStart = EdgeMarkers.MarkerId(_measure.SvgId, _measure.DiagramType, edge.ArrowTypeStart, MarkerEnd.Start, useMargin);
		var markerEnd = EdgeMarkers.MarkerId(_measure.SvgId, _measure.DiagramType, edge.ArrowTypeEnd, MarkerEnd.End, useMargin);
		var pathStyle = FlowchartStyles.EdgeStyle(edge.CssCompiledStyles, edge.Styles);

		// The handDrawn branch of insertEdge takes the whole first "stroke:" style as the colour.
		var strokeColor = look == "handDrawn"
			? edge.Styles.FirstOrDefault(s => s.StartsWith("stroke:", StringComparison.Ordinal))
			: FlowchartStyles.StrokeColor(pathStyle);
		var colored = new List<ColoredMarker>();
		if (!string.IsNullOrWhiteSpace(strokeColor))
		{
			markerStart = Colored(markerStart, edge.ArrowTypeStart, strokeColor, colored);
			markerEnd = Colored(markerEnd, edge.ArrowTypeEnd, strokeColor, colored);
		}
		if (points.Count > 0 && !IsLabelCoordinateInPath(points[points.Count / 2], data))
		{
			changed = true;
		}

		var (labelX, labelY) = (edge.X, edge.Y);
		if (changed && !placed && points.Count > 0 && original.Count > 0)
		{
			var updatedMid = CalcLabelPosition(points);
			var originalMid = CalcLabelPosition(original);
			labelX = edge.X + (updatedMid.X - originalMid.X);
			labelY = edge.Y + (updatedMid.Y - originalMid.Y);
		}

		return new FlowchartEdgeDrawing
		{
			Id = edge.Id,
			Index = edge.Index,
			Start = edge.Start,
			End = edge.End,
			PathData = data,
			PathBounds = data.Length > 0 ? PathBounds.Measure(data) : ShapeBounds.None,
			Classes = classes,
			MarkerStart = markerStart,
			MarkerEnd = markerEnd,
			ColoredMarkers = colored,
			PathStyle = pathStyle,
			LabelStyles = string.Concat(edge.Styles.Select(s => ";" + s)),
			Invisible = edge.Stroke == FlowchartStroke.Invisible,
			Thick = edge.Stroke == FlowchartStroke.Thick,
			Label = edge.LabelBox,
			LabelX = labelX,
			LabelY = labelY + (_titleTotalMargin / 2),
		};
	}

	/// <summary>
	/// <c>addEdgeMarker</c> with a stroke colour: the marker's id with the colour, its characters other than letters and
	/// digits made <c>_</c>, after a <c>_</c>. A colour the SVG grammar refuses leaves the marker as it is.
	/// </summary>
	private static string? Colored(string? markerId, string arrowType, string color, List<ColoredMarker> colored)
	{
		var paint = color.Trim();
		if (markerId is null || !Rendering.SvgValues.IsAllowedPaint(paint))
		{
			return markerId;
		}

		var id = string.Create(color.Length, color, (span, text) =>
		{
			for (var i = 0; i < text.Length; i++)
			{
				span[i] = char.IsAsciiLetterOrDigit(text[i]) ? text[i] : '_';
			}
		});
		var marker = new ColoredMarker(markerId, markerId + "_" + id, paint, EdgeMarkers.FindArrowType(arrowType)?.Fill == true);
		colored.Add(marker);
		return marker.Id;
	}

	private string? ColorId(FlowNode node) =>
		_measure.ColorSlots > 0 && node.ColorIndex is { } index
			? "color-" + (index % _measure.ColorSlots).ToString(System.Globalization.CultureInfo.InvariantCulture)
			: null;

	private static string StrokeClasses(FlowEdge edge) =>
		edge.Stroke switch
		{
			FlowchartStroke.Thick => "edge-thickness-thick",
			FlowchartStroke.Invisible => "edge-thickness-invisible",
			_ => "edge-thickness-normal",
		}
		+ (edge.Pattern == "dashed" ? " edge-pattern-dashed"
			: edge.Pattern == "dotted" || edge.Stroke == FlowchartStroke.Dotted ? " edge-pattern-dotted"
			: " edge-pattern-solid");

	private static bool HasIntersect(FlowNode node) => node.IsFrame || node.Geometry is not null;

	private static PathPoint Intersect(FlowNode node, PathPoint point)
	{
		var center = new ShapePoint(node.X, node.Y);
		var target = new ShapePoint(point.X, point.Y);
		var found = node.Geometry is { } geometry && !node.IsFrame
			? geometry.IntersectFrom(center, target)
			: ShapeIntersect.RectPoint(center, node.Width, node.Height, target);
		return new PathPoint(found.X, found.Y);
	}

	private static bool OutsideNode(FlowNode node, PathPoint point) =>
		Math.Abs(point.X - node.X) >= node.Width / 2 || Math.Abs(point.Y - node.Y) >= node.Height / 2;

	/// <summary><c>intersection</c> in <c>edges.js</c>: where the segment from the inside point to the outside point leaves the node's box.</summary>
	private static PathPoint Intersection(FlowNode node, PathPoint outside, PathPoint inside)
	{
		var x = node.X;
		var y = node.Y;
		var w = node.Width / 2;
		var h = node.Height / 2;
		var q = Math.Abs(outside.Y - inside.Y);
		var r = Math.Abs(outside.X - inside.X);
		if (Math.Abs(y - outside.Y) * w > Math.Abs(x - outside.X) * h)
		{
			var qq = inside.Y < outside.Y ? outside.Y - h - y : y - h - outside.Y;
			var rr = r * qq / q;
			var resX = inside.X < outside.X ? inside.X + rr : inside.X - r + rr;
			var resY = inside.Y < outside.Y ? inside.Y + q - qq : inside.Y - q + qq;
			if (rr == 0)
			{
				resX = outside.X;
				resY = outside.Y;
			}

			if (r == 0)
			{
				resX = outside.X;
			}

			if (q == 0)
			{
				resY = outside.Y;
			}

			return new PathPoint(resX, resY);
		}

		var side = inside.X < outside.X ? outside.X - w - x : x - w - outside.X;
		var dq = q * side / r;
		var sx = inside.X < outside.X ? inside.X + r - side : inside.X - r + side;
		var sy = inside.Y < outside.Y ? inside.Y + dq : inside.Y - dq;
		if (side == 0)
		{
			sx = outside.X;
			sy = outside.Y;
		}

		if (r == 0)
		{
			sx = outside.X;
		}

		if (q == 0)
		{
			sy = outside.Y;
		}

		return new PathPoint(sx, sy);
	}

	/// <summary><c>cutPathAtIntersect</c>: the points up to where the path enters the node, ending at the node's border.</summary>
	private static List<PathPoint> CutPathAtIntersect(List<PathPoint> input, FlowNode boundary)
	{
		var points = new List<PathPoint>();
		if (input.Count == 0)
		{
			return points;
		}

		var lastOutside = input[0];
		var isInside = false;
		foreach (var point in input)
		{
			if (!OutsideNode(boundary, point) && !isInside)
			{
				var inter = Intersection(boundary, lastOutside, point);
				if (!points.Exists(p => p.X == inter.X && p.Y == inter.Y))
				{
					points.Add(inter);
				}

				isInside = true;
			}
			else
			{
				lastOutside = point;
				if (!isInside)
				{
					points.Add(point);
				}
			}
		}

		return points;
	}

	/// <summary><c>utils.calcLabelPosition</c>: the point half way along the points.</summary>
	internal static PathPoint CalcLabelPosition(IReadOnlyList<PathPoint> points)
	{
		if (points.Count == 1)
		{
			return points[0];
		}

		var total = 0.0;
		for (var i = 1; i < points.Count; i++)
		{
			total += Distance(points[i], points[i - 1]);
		}

		var remaining = total / 2;
		for (var i = 1; i < points.Count; i++)
		{
			var previous = points[i - 1];
			var point = points[i];
			var vector = Distance(point, previous);
			if (vector == 0)
			{
				return previous;
			}

			if (vector < remaining)
			{
				remaining -= vector;
				continue;
			}

			var ratio = remaining / vector;
			if (ratio <= 0)
			{
				return previous;
			}

			if (ratio >= 1)
			{
				return point;
			}

			return new PathPoint(Round5(((1 - ratio) * previous.X) + (ratio * point.X)), Round5(((1 - ratio) * previous.Y) + (ratio * point.Y)));
		}

		return points.Count > 0 ? points[^1] : default;
	}

	private static double Distance(PathPoint a, PathPoint b) => Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));

	private static double Round5(double value) => JsRound(value * 100000) / 100000;

	/// <summary>JavaScript's <c>Math.round</c>: halves go up.</summary>
	private static double JsRound(double value) => Math.Floor(value + 0.5);

	/// <summary>
	/// <c>utils.isLabelCoordinateInPath</c>: whether the path's data, with each decimal rounded, holds the point's
	/// rounded x or y.
	/// </summary>
	internal static bool IsLabelCoordinateInPath(PathPoint point, string data)
	{
		var rounded = new System.Text.StringBuilder(data.Length);
		var i = 0;
		while (i < data.Length)
		{
			if (!char.IsAsciiDigit(data[i]))
			{
				rounded.Append(data[i]);
				i++;
				continue;
			}

			var start = i;
			while (i < data.Length && char.IsAsciiDigit(data[i]))
			{
				i++;
			}

			if (i + 1 < data.Length && data[i] == '.' && char.IsAsciiDigit(data[i + 1]))
			{
				i++;
				while (i < data.Length && char.IsAsciiDigit(data[i]))
				{
					i++;
				}

				var value = double.Parse(data.AsSpan(start, i - start), System.Globalization.CultureInfo.InvariantCulture);
				rounded.Append(JsNumber.ToString(JsRound(value)));
			}
			else
			{
				rounded.Append(data, start, i - start);
			}
		}

		var text = rounded.ToString();
		return text.Contains(JsNumber.ToString(JsRound(point.X)), StringComparison.Ordinal)
			|| text.Contains(JsNumber.ToString(JsRound(point.Y)), StringComparison.Ordinal);
	}
}
