using System.Globalization;

namespace SplashTheMusical.Diagrams.Flowchart;

/// <summary>
/// A short fluent way to build a <see cref="FlowchartDocument"/> for the common case: nodes, edges, subgraphs, classes
/// and styles. Each call does what the matching statement does in mermaid.js's parser, so the document reads back the
/// same after <see cref="IMermaidWriter.Write"/>. For the rest of the model, set the document's properties.
/// </summary>
/// <example>
/// <code>
/// var document = new FlowchartBuilder("LR")
///     .Node("A", "Start", "stadium")
///     .Edge("A", "B", "next")
///     .Subgraph("group", "A group", "B")
///     .Build();
/// </code>
/// </example>
public sealed class FlowchartBuilder
{
	private readonly FlowchartDocument _document = new();
	private readonly Dictionary<string, FlowchartNode> _nodes = new(StringComparer.Ordinal);
	private readonly Dictionary<string, FlowchartEdge> _edges = new(StringComparer.Ordinal);
	private readonly Dictionary<string, FlowchartSubgraph> _subgraphs = new(StringComparer.Ordinal);
	private readonly Dictionary<string, FlowchartClass> _classes = new(StringComparer.Ordinal);

	// The members some subgraph holds, which a later subgraph leaves out as mermaid.js does.
	private readonly HashSet<string> _held = new(StringComparer.Ordinal);

	// The subgraphs no other subgraph holds yet, in the order they were added.
	private readonly List<string> _roots = [];

	/// <param name="direction"><c>TB</c>, <c>TD</c>, <c>BT</c>, <c>LR</c> or <c>RL</c>. <c>TD</c> is read as <c>TB</c>, as mermaid.js reads it.</param>
	public FlowchartBuilder(string direction = "TB")
	{
		ArgumentNullException.ThrowIfNull(direction);
		_document.Direction = direction switch
		{
			"TB" or "TD" => "TB",
			"BT" or "LR" or "RL" => direction,
			_ => throw new ArgumentException($"A flowchart's direction is TB, TD, BT, LR or RL, not '{direction}'.", nameof(direction)),
		};
	}

	/// <summary>
	/// Adds a node, or changes the label and shape of one that exists. A node with a label and no shape is a
	/// <c>square</c>, as <c>A[label]</c> is.
	/// </summary>
	/// <param name="shape">A bracket shape, such as <c>square</c>, <c>round</c> or <c>diamond</c>, or a shape of mermaid.js's shape table, such as <c>hex</c>.</param>
	public FlowchartBuilder Node(string id, string? label = null, string? shape = null)
	{
		var node = AddNode(id);
		if (shape is not null)
		{
			if (!IsShape(shape))
			{
				throw new ArgumentException($"No such shape: {shape}.", nameof(shape));
			}

			node.Shape = shape;
		}

		if (label is not null)
		{
			node.Label = label;
			node.LabelType = LabelTypeOf(label);
			node.Shape ??= "square";
		}

		return this;
	}

	/// <summary>
	/// Adds an edge from <paramref name="start"/> to <paramref name="end"/>, and the nodes it names that do not exist.
	/// The edge's id is mermaid.js's <c>L_start_end_n</c>, or <paramref name="id"/> when it is given and not used.
	/// </summary>
	/// <param name="length">The extra length, from 1 to 10, as the count of the link's dashes gives it.</param>
	public FlowchartBuilder Edge(
		string start,
		string end,
		string? label = null,
		FlowchartEdgeType type = FlowchartEdgeType.Point,
		FlowchartStroke stroke = FlowchartStroke.Normal,
		int length = 1,
		string? id = null)
	{
		if (type == FlowchartEdgeType.Invalid || stroke == FlowchartStroke.Invalid)
		{
			throw new ArgumentException("An edge built here has a valid type and stroke.", type == FlowchartEdgeType.Invalid ? nameof(type) : nameof(stroke));
		}

		if (stroke == FlowchartStroke.Invisible && type != FlowchartEdgeType.Open)
		{
			throw new ArgumentException("An invisible edge has no arrowheads.", nameof(type));
		}

		ArgumentOutOfRangeException.ThrowIfLessThan(length, 1);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(length, 10);
		AddNode(start);
		AddNode(end);
		var userDefined = !string.IsNullOrEmpty(id) && !_edges.ContainsKey(id);
		if (!userDefined)
		{
			var existing = 0;
			foreach (var edge in _document.Edges)
			{
				if (edge.Start == start && edge.End == end)
				{
					existing++;
				}
			}

			id = string.Create(CultureInfo.InvariantCulture, $"L_{start}_{end}_{(existing == 0 ? 0 : existing + 1)}");
		}

		var added = new FlowchartEdge(start, end, id!)
		{
			IsUserDefinedId = userDefined,
			Type = type,
			Stroke = stroke,
			Length = length,
			Label = label ?? "",
			LabelType = label is null ? FlowchartLabelType.Text : LabelTypeOf(label),
		};
		_document.Edges.Add(added);
		_edges.TryAdd(added.Id, added);
		return this;
	}

	/// <summary>
	/// Adds a subgraph that holds <paramref name="members"/>: nodes, which are added when they do not exist, and
	/// subgraphs added before it. As in mermaid.js, a node an earlier subgraph holds is left out. The subgraphs it holds
	/// must be the last ones added that no subgraph holds yet, in the order they were added, because mermaid.js orders
	/// subgraphs by where they end.
	/// </summary>
	public FlowchartBuilder Subgraph(string id, string title, params IEnumerable<string> members)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentNullException.ThrowIfNull(title);
		ArgumentNullException.ThrowIfNull(members);
		if (_subgraphs.ContainsKey(id))
		{
			throw new ArgumentException($"The subgraph '{id}' exists.", nameof(id));
		}

		var list = members.ToList();
		var nested = list.Where(member => _subgraphs.ContainsKey(member) && !_nodes.ContainsKey(member)).Distinct(StringComparer.Ordinal).ToList();
		if (nested.Count > _roots.Count || !nested.SequenceEqual(_roots.Skip(_roots.Count - nested.Count), StringComparer.Ordinal))
		{
			throw new ArgumentException(
				"The subgraphs a subgraph holds are the last ones added that no subgraph holds yet, in the order they were added.",
				nameof(members));
		}

		var subgraph = new FlowchartSubgraph(id, title) { LabelType = LabelTypeOf(title) };
		var seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (var member in list)
		{
			if (!_subgraphs.ContainsKey(member))
			{
				AddNode(member);
			}

			if (seen.Add(member) && member != id && !_held.Contains(member))
			{
				subgraph.Nodes.Add(member);
			}
		}

		foreach (var member in subgraph.Nodes)
		{
			_held.Add(member);
		}

		_roots.RemoveRange(_roots.Count - nested.Count, nested.Count);
		_roots.Add(id);
		_document.Subgraphs.Add(subgraph);
		_subgraphs.Add(id, subgraph);
		return this;
	}

	/// <summary>Adds the styles of <c>classDef</c> to a class, with the text styles mermaid.js derives from them.</summary>
	public FlowchartBuilder ClassDef(string id, params IEnumerable<string> styles)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentNullException.ThrowIfNull(styles);
		if (!_classes.TryGetValue(id, out var definition))
		{
			definition = new FlowchartClass(id);
			_classes.Add(id, definition);
			_document.Classes.Add(definition);
		}

		foreach (var style in styles)
		{
			CheckStyle(style, nameof(styles));
			if (style.Contains("color", StringComparison.Ordinal))
			{
				var fill = style.IndexOf("fill", StringComparison.Ordinal);
				definition.TextStyles.Add(fill < 0 ? style : string.Concat(style.AsSpan(0, fill), "bgFill", style.AsSpan(fill + 4)));
			}

			definition.Styles.Add(style);
		}

		return this;
	}

	/// <summary>Gives the class to the node, the edge and the subgraph of the id, as <c>class</c> does.</summary>
	public FlowchartBuilder Class(string id, string className)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentException.ThrowIfNullOrWhiteSpace(className);
		_nodes.GetValueOrDefault(id)?.Classes.Add(className);
		_edges.GetValueOrDefault(id)?.Classes.Add(className);
		_subgraphs.GetValueOrDefault(id)?.Classes.Add(className);
		return this;
	}

	/// <summary>Adds the declarations of <c>style</c> to a node, which is added when it does not exist.</summary>
	public FlowchartBuilder Style(string id, params IEnumerable<string> styles)
	{
		ArgumentNullException.ThrowIfNull(styles);
		var node = AddNode(id);
		foreach (var style in styles)
		{
			CheckStyle(style, nameof(styles));
			if (style.Contains(',', StringComparison.Ordinal))
			{
				throw new ArgumentException("A style statement splits its declarations at commas.", nameof(styles));
			}

			node.Styles.Add(style);
		}

		return this;
	}

	/// <summary>The document. Later calls change it.</summary>
	public FlowchartDocument Build() => _document;

	private FlowchartNode AddNode(string id)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		if (!_nodes.TryGetValue(id, out var node))
		{
			node = new FlowchartNode(id);
			_nodes.Add(id, node);
			_document.Nodes.Add(node);
		}

		return node;
	}

	/// <summary>Plain text when it can be written in brackets as it is, and a string otherwise.</summary>
	private static FlowchartLabelType LabelTypeOf(string label) =>
		FlowchartWriter.IsPlainText(label) || !FlowchartWriter.IsStringText(label) ? FlowchartLabelType.Text : FlowchartLabelType.String;

	private static bool IsShape(string shape) =>
		shape is "square" or "round" or "circle" or "doublecircle" or "stadium" or "subroutine" or "cylinder" or "diamond" or "hexagon"
			or "odd" or "trapezoid" or "inv_trapezoid" or "lean_right" or "lean_left"
		|| (FlowchartShapes.IsValid(shape) && !shape.Contains('_') && shape.Equals(shape.ToLowerInvariant(), StringComparison.Ordinal));

	private static void CheckStyle(string style, string parameter)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(style, parameter);
		if (style.AsSpan().IndexOfAny(";\n\r") >= 0)
		{
			throw new ArgumentException("A style is one declaration, without ';' or a line break.", parameter);
		}
	}
}
