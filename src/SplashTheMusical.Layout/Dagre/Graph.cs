namespace SplashTheMusical.Layout.Dagre;

/// <summary>
/// A port of graphlib's <c>Graph</c>, which dagre lays out. Nodes and edges are int handles into buffers. A node is
/// named by a string, as in graphlib, and a node that is removed and added again gets a new handle. Every method that
/// lists nodes or edges returns them in graphlib's order, which <see cref="KeyOrder"/> reproduces.
/// </summary>
internal class Graph<TNode, TEdge>
	where TNode : class
	where TEdge : class
{
	/// <summary>The parent handle of a node at the top of a compound graph.</summary>
	public const int Root = -1;

	private readonly Dictionary<string, int> _nodeHandles;
	private readonly Dictionary<(int V, int W, string? Name), int> _edgeHandles;
	private readonly KeyOrder _nodes = new();
	private readonly KeyOrder _edges = new();
	private readonly KeyOrder? _rootChildren;

	private string[] _names;
	private long[] _arrayIndexes;
	private TNode?[] _nodeLabels;
	private int[] _parents;
	private KeyOrder?[] _children;
	private KeyOrder?[] _in;
	private KeyOrder?[] _out;
	private KeyOrder?[] _preds;
	private KeyOrder?[] _sucs;
	private int _nodeHandleCount;

	private int[] _edgeV;
	private int[] _edgeW;
	private string?[] _edgeNames;
	private TEdge?[] _edgeLabels;
	private int _edgeHandleCount;

	/// <param name="capacity">The number of nodes and of edges to make room for. The graph grows past it.</param>
	public Graph(bool directed = true, bool multigraph = false, bool compound = false, int capacity = 16)
	{
		capacity = Math.Max(1, capacity);
		_nodeHandles = new(capacity, StringComparer.Ordinal);
		_edgeHandles = new(capacity);
		_names = new string[capacity];
		_arrayIndexes = new long[capacity];
		_nodeLabels = new TNode?[capacity];
		_parents = new int[capacity];
		_children = new KeyOrder?[capacity];
		_in = new KeyOrder?[capacity];
		_out = new KeyOrder?[capacity];
		_preds = new KeyOrder?[capacity];
		_sucs = new KeyOrder?[capacity];
		_edgeV = new int[capacity];
		_edgeW = new int[capacity];
		_edgeNames = new string?[capacity];
		_edgeLabels = new TEdge?[capacity];
		IsDirected = directed;
		IsMultigraph = multigraph;
		IsCompound = compound;
		if (compound)
		{
			_rootChildren = new KeyOrder();
		}
	}

	public bool IsDirected { get; }

	public bool IsMultigraph { get; }

	public bool IsCompound { get; }

	public int NodeCount => _nodes.Count;

	public int EdgeCount => _edges.Count;

	/// <summary>One more than the largest node handle the graph has given out.</summary>
	public int NodeHandleCount => _nodeHandleCount;

	// Nodes

	public int[] Nodes() => _nodes.ToArray();

	/// <summary>The first node of <see cref="Nodes"/>, or -1 for an empty graph.</summary>
	public int FirstNode() => _nodes.First();

	public int[] Sources()
	{
		var nodes = Nodes();
		var count = 0;
		foreach (var v in nodes)
		{
			if (_in[v]!.Count == 0)
			{
				nodes[count++] = v;
			}
		}

		return nodes[..count];
	}

	/// <summary>graphlib's <c>setNode(v, value)</c>: adds the node, or replaces the label of the node that has the name.</summary>
	public int SetNode(string name, TNode label)
	{
		var v = EnsureNode(name);
		_nodeLabels[v] = label;
		return v;
	}

	/// <summary>graphlib's <c>setNode(v)</c>: adds the node with no label, and leaves a present node alone.</summary>
	public int EnsureNode(string name)
	{
		if (_nodeHandles.TryGetValue(name, out var existing))
		{
			return existing;
		}

		var v = _nodeHandleCount++;
		if (v == _names.Length)
		{
			var size = v * 2;
			Array.Resize(ref _names, size);
			Array.Resize(ref _arrayIndexes, size);
			Array.Resize(ref _nodeLabels, size);
			Array.Resize(ref _parents, size);
			Array.Resize(ref _children, size);
			Array.Resize(ref _in, size);
			Array.Resize(ref _out, size);
			Array.Resize(ref _preds, size);
			Array.Resize(ref _sucs, size);
		}

		var arrayIndex = JsKeys.ArrayIndex(name);
		_names[v] = name;
		_arrayIndexes[v] = arrayIndex;
		_nodeLabels[v] = null;
		_parents[v] = Root;
		_in[v] = new KeyOrder();
		_out[v] = new KeyOrder();
		_preds[v] = new KeyOrder();
		_sucs[v] = new KeyOrder();
		if (IsCompound)
		{
			_children[v] = new KeyOrder();
			_rootChildren!.Add(v, arrayIndex);
		}

		_nodeHandles.Add(name, v);
		_nodes.Add(v, arrayIndex);
		return v;
	}

	public bool HasNode(string name) => _nodeHandles.ContainsKey(name);

	public bool HasNode(int v) => v >= 0 && v < _nodeHandleCount && _in[v] is not null;

	/// <summary>The handle of the node with the name, or -1.</summary>
	public int NodeHandle(string name) => _nodeHandles.TryGetValue(name, out var v) ? v : -1;

	public string Name(int v) => _names[v];

	/// <summary>The node's label. dagre never reads a node it has not labelled.</summary>
	public TNode Node(int v) => _nodeLabels[v] ?? throw new InvalidOperationException($"Node '{_names[v]}' has no label.");

	/// <summary>graphlib's <c>node(v)</c>: the node's label, or null for a node added without one or not in the graph.</summary>
	public TNode? NodeOrDefault(int v) => HasNode(v) ? _nodeLabels[v] : null;

	public void RemoveNode(int v)
	{
		if (!HasNode(v))
		{
			return;
		}

		_nodeHandles.Remove(_names[v]);
		_nodes.Remove(v);
		if (IsCompound)
		{
			ChildrenOf(_parents[v]).Remove(v);
			_parents[v] = Root;
			foreach (var child in _children[v]!.ToArray())
			{
				SetParent(child, Root);
			}

			_children[v] = null;
		}

		foreach (var e in _in[v]!.ToArray())
		{
			RemoveEdge(e);
		}

		foreach (var e in _out[v]!.ToArray())
		{
			RemoveEdge(e);
		}

		_in[v] = null;
		_out[v] = null;
		_preds[v] = null;
		_sucs[v] = null;
		_nodeLabels[v] = null;
	}

	/// <summary>graphlib's <c>setParent</c>. <see cref="Root"/> moves the node to the top.</summary>
	public void SetParent(int v, int parent)
	{
		if (!IsCompound)
		{
			throw new InvalidOperationException("Cannot set parent in a non-compound graph");
		}

		for (var ancestor = parent; ancestor != Root; ancestor = _parents[ancestor])
		{
			if (ancestor == v)
			{
				throw new InvalidOperationException($"Setting {_names[parent]} as parent of {_names[v]} would create a cycle");
			}
		}

		ChildrenOf(_parents[v]).Remove(v);
		_parents[v] = parent;
		ChildrenOf(parent).Add(v, _arrayIndexes[v]);
	}

	/// <summary>The node's parent, or <see cref="Root"/>.</summary>
	public int Parent(int v) => IsCompound ? _parents[v] : Root;

	/// <summary>graphlib's <c>children</c>. <see cref="Root"/> gives the nodes at the top.</summary>
	public int[] Children(int v = Root)
	{
		if (IsCompound)
		{
			return ChildrenOf(v).ToArray();
		}

		return v == Root ? Nodes() : [];
	}

	public bool HasChildren(int v) => IsCompound && _children[v]!.Count > 0;

	public int[] Predecessors(int v) => _preds[v]!.ToArray();

	public int[] Successors(int v) => _sucs[v]!.ToArray();

	/// <summary>graphlib's <c>neighbors</c>: <c>_.union</c> of the predecessors and the successors.</summary>
	public int[] Neighbors(int v)
	{
		var preds = _preds[v]!;
		var sucs = _sucs[v]!;
		var result = new int[preds.Count + sucs.Count];
		preds.CopyTo(result);
		var count = preds.Count;
		foreach (var w in sucs.ToArray())
		{
			if (!preds.Contains(w))
			{
				result[count++] = w;
			}
		}

		return count == result.Length ? result : result[..count];
	}

	// Edges

	public int[] Edges() => _edges.ToArray();

	/// <summary>graphlib's <c>setEdge</c> with a value: adds the edge, or replaces the label of the edge that is present.</summary>
	public int SetEdge(int v, int w, TEdge label, string? name = null)
	{
		if (name is not null && !IsMultigraph)
		{
			throw new InvalidOperationException("Cannot set a named edge when isMultigraph = false");
		}

		if (!IsDirected && string.CompareOrdinal(_names[v], _names[w]) > 0)
		{
			(v, w) = (w, v);
		}

		if (_edgeHandles.TryGetValue((v, w, name), out var existing))
		{
			_edgeLabels[existing] = label;
			return existing;
		}

		var e = _edgeHandleCount++;
		if (e == _edgeV.Length)
		{
			var size = e * 2;
			Array.Resize(ref _edgeV, size);
			Array.Resize(ref _edgeW, size);
			Array.Resize(ref _edgeNames, size);
			Array.Resize(ref _edgeLabels, size);
		}

		_edgeV[e] = v;
		_edgeW[e] = w;
		_edgeNames[e] = name;
		_edgeLabels[e] = label;
		_edgeHandles.Add((v, w, name), e);
		_edges.Add(e, -1);
		_preds[w]!.Increment(v, _arrayIndexes[v]);
		_sucs[v]!.Increment(w, _arrayIndexes[w]);
		_in[w]!.Add(e, -1);
		_out[v]!.Add(e, -1);
		return e;
	}

	/// <summary>The handle of the edge, or -1. An undirected graph finds the edge from either end.</summary>
	public int FindEdge(int v, int w, string? name = null)
	{
		if (!IsDirected && string.CompareOrdinal(_names[v], _names[w]) > 0)
		{
			(v, w) = (w, v);
		}

		return _edgeHandles.TryGetValue((v, w, name), out var e) ? e : -1;
	}

	public bool HasEdge(int v, int w, string? name = null) => FindEdge(v, w, name) >= 0;

	/// <summary>The edge's tail. An edge keeps its ends and name after it is removed.</summary>
	public int EdgeV(int e) => _edgeV[e];

	public int EdgeW(int e) => _edgeW[e];

	public string? EdgeName(int e) => _edgeNames[e];

	public TEdge Edge(int e) => _edgeLabels[e]!;

	public void RemoveEdge(int e)
	{
		var v = _edgeV[e];
		var w = _edgeW[e];
		if (!_edges.Remove(e))
		{
			return;
		}

		_edgeHandles.Remove((v, w, _edgeNames[e]));
		_preds[w]!.Decrement(v);
		_sucs[v]!.Decrement(w);
		_in[w]!.Remove(e);
		_out[v]!.Remove(e);
	}

	public int[] InEdges(int v) => _in[v]!.ToArray();

	public int[] OutEdges(int v) => _out[v]!.ToArray();

	/// <summary>graphlib's <c>inEdges(v, u)</c>: the edges from u to v.</summary>
	public int[] InEdges(int v, int u) => Filter(InEdges(v), _edgeV, u);

	/// <summary>graphlib's <c>outEdges(v, w)</c>: the edges from v to w.</summary>
	public int[] OutEdges(int v, int w) => Filter(OutEdges(v), _edgeW, w);

	/// <summary>graphlib's <c>nodeEdges</c>: the in-edges, then the out-edges.</summary>
	public int[] NodeEdges(int v)
	{
		var inEdges = _in[v]!;
		var outEdges = _out[v]!;
		var result = new int[inEdges.Count + outEdges.Count];
		inEdges.CopyTo(result);
		outEdges.CopyTo(result.AsSpan(inEdges.Count));
		return result;
	}

	public bool HasInEdges(int v) => _in[v]!.Count > 0;

	private KeyOrder ChildrenOf(int v) => v == Root ? _rootChildren! : _children[v]!;

	private static int[] Filter(int[] edges, int[] ends, int end)
	{
		var count = 0;
		foreach (var e in edges)
		{
			if (ends[e] == end)
			{
				edges[count++] = e;
			}
		}

		return edges[..count];
	}
}
