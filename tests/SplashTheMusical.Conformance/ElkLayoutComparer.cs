using System.Globalization;

namespace SplashTheMusical.Conformance;

/// <summary>A box with its top left corner in the canvas's coordinates.</summary>
internal readonly record struct LayoutBox(double X, double Y, double Width, double Height);

/// <summary>
/// A layout of a recorded ELK input graph, from any engine. Nodes and edges are keyed by their ELK ids.
/// </summary>
/// <param name="Boxes">The box of each node, leaves and clusters, in the canvas's coordinates.</param>
/// <param name="Edges">Each edge's route from its start to its end, in the canvas's coordinates.</param>
/// <param name="Layers">
/// The leaves of each layer in order, for a layout that assigns layers without coordinates. When null, the layers and
/// their order are read from <paramref name="Boxes"/> as they are from ELK's result.
/// </param>
internal sealed record CandidateLayout(
	double Width,
	double Height,
	IReadOnlyDictionary<string, LayoutBox> Boxes,
	IReadOnlyDictionary<string, IReadOnlyList<(double X, double Y)>> Edges,
	IReadOnlyList<IReadOnlyList<string>>? Layers = null);

/// <summary>A layout's score on the four measures. Each failure is null when the measure passes.</summary>
internal sealed record ElkLayoutScore(string? LayerFailure, string? OrderFailure, string? BoxFailure, string? BendFailure)
{
	public bool SameLayers => LayerFailure is null;

	public bool SameOrder => OrderFailure is null;

	public bool BoxesOverlap => BoxFailure is null;

	public bool SameBends => BendFailure is null;

	public bool All => SameLayers && SameOrder && BoxesOverlap && SameBends;

	/// <summary>The score of a layout that could not be made, which fails every measure.</summary>
	public static ElkLayoutScore Failed(string reason) => new(reason, reason, reason, reason);
}

/// <summary>
/// Scores a layout of a recorded ELK input graph against the layout ELK returned for it, on four measures:
/// <list type="number">
/// <item>every leaf is in the layer ELK put it in;</item>
/// <item>the leaves of each of ELK's layers are in ELK's order;</item>
/// <item>each node's box overlaps ELK's box by at least <see cref="MinOverlap"/>, and the canvas is within <see cref="CanvasTolerance"/>;</item>
/// <item>each edge has as many bends as ELK's.</item>
/// </list>
/// ELK gives no layers, so they are read from its coordinates. Along the layout direction, a layer is a maximal run of
/// leaves whose extents overlap, and its order is the leaves' order across the layout direction by their centres.
/// </summary>
internal static class ElkLayoutComparer
{
	public const double MinOverlap = 0.8;

	public const double CanvasTolerance = 0.05;

	private const double Epsilon = 0.01;

	public static ElkLayoutScore Score(ElkRecord record, CandidateLayout actual)
	{
		var graph = new ElkInputGraph(record.Input);
		return Score(graph, ElkLayout(record, graph), actual);
	}

	/// <summary>ELK's own result as a <see cref="CandidateLayout"/>, with every box and route in the root's coordinates.</summary>
	public static CandidateLayout ElkLayout(ElkRecord record, ElkInputGraph graph)
	{
		var places = record.Output.Places.ToDictionary(p => p.Id, StringComparer.Ordinal);
		var origins = new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal);

		(double X, double Y) Origin(string id)
		{
			if (origins.TryGetValue(id, out var origin))
			{
				return origin;
			}

			var place = places[id];
			var parent = place.Parent is { } p ? Origin(p) : (0, 0);
			origin = (parent.X + place.X, parent.Y + place.Y);
			origins[id] = origin;
			return origin;
		}

		var boxes = new Dictionary<string, LayoutBox>(StringComparer.Ordinal);
		foreach (var node in graph.Nodes)
		{
			if (places.TryGetValue(node.Id, out var place))
			{
				var origin = Origin(node.Id);
				boxes[node.Id] = new LayoutBox(origin.X, origin.Y, place.Width, place.Height);
			}
		}

		var edges = new Dictionary<string, IReadOnlyList<(double X, double Y)>>(StringComparer.Ordinal);
		foreach (var route in record.Output.Routes)
		{
			var offset = route.ElkContainer is { } container && places.ContainsKey(container) ? Origin(container) : (0, 0);
			var points = new List<(double X, double Y)>();
			foreach (var section in route.Sections)
			{
				if (section.Start is { } start)
				{
					points.Add(start);
				}

				points.AddRange(section.BendPoints);
				if (section.End is { } end)
				{
					points.Add(end);
				}
			}

			edges[route.Id] = [.. points.Select(p => (p.X + offset.X, p.Y + offset.Y))];
		}

		var root = places.TryGetValue(graph.Root, out var r) ? r : new ElkPlace(graph.Root, null, 0, 0, double.NaN, double.NaN);
		return new CandidateLayout(root.Width, root.Height, boxes, edges);
	}

	public static ElkLayoutScore Score(ElkInputGraph graph, CandidateLayout expected, CandidateLayout actual)
	{
		var expectedLayers = Layers(graph, expected);
		var missing = graph.Leaves.FirstOrDefault(id => !actual.Boxes.ContainsKey(id) && (actual.Layers is null || !actual.Layers.Any(l => l.Contains(id))));
		if (missing is not null)
		{
			return ElkLayoutScore.Failed($"the layout has no node '{missing}'");
		}

		var actualLayers = Layers(graph, actual);
		return new ElkLayoutScore(
			LayerFailure(expectedLayers, actualLayers),
			OrderFailure(expectedLayers, actualLayers, actual, graph.Direction),
			BoxFailure(graph, expected, actual),
			BendFailure(expected, actual));
	}

	/// <summary>The leaves of each layer in order: the layout's own when it gives them, otherwise read from its boxes.</summary>
	public static IReadOnlyList<IReadOnlyList<string>> Layers(ElkInputGraph graph, CandidateLayout layout)
	{
		if (layout.Layers is { } given)
		{
			return given;
		}

		var leaves = graph.Leaves
			.Where(layout.Boxes.ContainsKey)
			.Select(id => (Id: id, Along: Along(layout.Boxes[id], graph.Direction)))
			.OrderBy(l => l.Along.Start)
			.ThenBy(l => l.Along.End)
			.ToList();
		var layers = new List<List<string>>();
		var layerStart = double.NegativeInfinity;
		var layerEnd = double.NegativeInfinity;
		foreach (var (id, along) in leaves)
		{
			if (layers.Count == 0 || (along.Start >= layerEnd - Epsilon && Math.Abs(along.Start - layerStart) > Epsilon))
			{
				layers.Add([]);
				layerStart = along.Start;
				layerEnd = along.End;
			}

			layers[^1].Add(id);
			layerEnd = Math.Max(layerEnd, along.End);
		}

		return [.. layers.Select(layer => (IReadOnlyList<string>)[.. layer.OrderBy(id => Across(layout.Boxes[id], graph.Direction))])];
	}

	/// <summary>
	/// The number of bends of a route: the points where it turns, after repeated points and points on a straight run
	/// are dropped.
	/// </summary>
	public static int Bends(IReadOnlyList<(double X, double Y)> points)
	{
		var kept = new List<(double X, double Y)>(points.Count);
		foreach (var point in points)
		{
			if (kept.Count > 0 && Distance(kept[^1], point) <= Epsilon)
			{
				continue;
			}

			while (kept.Count >= 2 && Straight(kept[^2], kept[^1], point))
			{
				kept.RemoveAt(kept.Count - 1);
			}

			kept.Add(point);
		}

		return Math.Max(0, kept.Count - 2);
	}

	/// <summary>The share of the larger box that the two boxes' intersection covers. A side under 1 counts as 1.</summary>
	public static double Overlap(LayoutBox a, LayoutBox b)
	{
		static LayoutBox Sized(LayoutBox box) => new(
			box.X - (Math.Max(box.Width, 1) - box.Width) / 2,
			box.Y - (Math.Max(box.Height, 1) - box.Height) / 2,
			Math.Max(box.Width, 1),
			Math.Max(box.Height, 1));

		a = Sized(a);
		b = Sized(b);
		var width = Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X);
		var height = Math.Min(a.Y + a.Height, b.Y + b.Height) - Math.Max(a.Y, b.Y);
		return width <= 0 || height <= 0 ? 0 : width * height / Math.Max(a.Width * a.Height, b.Width * b.Height);
	}

	private static string? LayerFailure(IReadOnlyList<IReadOnlyList<string>> expected, IReadOnlyList<IReadOnlyList<string>> actual)
	{
		var actualLayer = LayerOf(actual);
		for (var layer = 0; layer < expected.Count; layer++)
		{
			foreach (var id in expected[layer])
			{
				var other = actualLayer.TryGetValue(id, out var a) ? a : -1;
				if (other != layer)
				{
					return Text($"node '{id}' is in layer {other}, ELK's is {layer}");
				}
			}
		}

		return null;
	}

	private static string? OrderFailure(
		IReadOnlyList<IReadOnlyList<string>> expected,
		IReadOnlyList<IReadOnlyList<string>> actual,
		CandidateLayout layout,
		string direction)
	{
		var position = new Dictionary<string, double>(StringComparer.Ordinal);
		foreach (var layer in actual)
		{
			for (var i = 0; i < layer.Count; i++)
			{
				position[layer[i]] = layout.Layers is null ? Across(layout.Boxes[layer[i]], direction) : i;
			}
		}

		for (var layer = 0; layer < expected.Count; layer++)
		{
			var order = expected[layer].OrderBy(id => position[id]).ToList();
			if (!order.SequenceEqual(expected[layer], StringComparer.Ordinal))
			{
				return $"layer {layer} is in the order {string.Join(' ', order)}, ELK's is {string.Join(' ', expected[layer])}";
			}
		}

		return null;
	}

	private static string? BoxFailure(ElkInputGraph graph, CandidateLayout expected, CandidateLayout actual)
	{
		if (!Within(actual.Width, expected.Width) || !Within(actual.Height, expected.Height))
		{
			return Text($"the canvas is {actual.Width} x {actual.Height}, ELK's is {expected.Width} x {expected.Height}");
		}

		foreach (var node in graph.Nodes)
		{
			if (!expected.Boxes.TryGetValue(node.Id, out var box))
			{
				continue;
			}

			if (!actual.Boxes.TryGetValue(node.Id, out var other))
			{
				return $"the layout has no box for '{node.Id}'";
			}

			var overlap = Overlap(box, other);
			if (overlap < MinOverlap)
			{
				return Text($"the box of '{node.Id}' overlaps ELK's by {overlap * 100:0}%");
			}
		}

		return null;
	}

	private static string? BendFailure(CandidateLayout expected, CandidateLayout actual)
	{
		foreach (var (id, points) in expected.Edges)
		{
			if (!actual.Edges.TryGetValue(id, out var route))
			{
				return $"the layout has no route for edge '{id}'";
			}

			var bends = Bends(route);
			var elk = Bends(points);
			if (bends != elk)
			{
				return Text($"edge '{id}' has {bends} bends, ELK's has {elk}");
			}
		}

		return null;
	}

	private static Dictionary<string, int> LayerOf(IReadOnlyList<IReadOnlyList<string>> layers)
	{
		var layerOf = new Dictionary<string, int>(StringComparer.Ordinal);
		for (var layer = 0; layer < layers.Count; layer++)
		{
			foreach (var id in layers[layer])
			{
				layerOf[id] = layer;
			}
		}

		return layerOf;
	}

	/// <summary>The box's extent along the layout direction, negated for <c>UP</c> and <c>LEFT</c> so that layers run from low to high.</summary>
	private static (double Start, double End) Along(LayoutBox box, string direction) => direction switch
	{
		"UP" => (-(box.Y + box.Height), -box.Y),
		"RIGHT" => (box.X, box.X + box.Width),
		"LEFT" => (-(box.X + box.Width), -box.X),
		_ => (box.Y, box.Y + box.Height),
	};

	private static double Across(LayoutBox box, string direction) =>
		direction is "RIGHT" or "LEFT" ? box.Y + box.Height / 2 : box.X + box.Width / 2;

	private static bool Within(double actual, double expected) =>
		Math.Abs(actual - expected) <= CanvasTolerance * Math.Abs(expected);

	private static double Distance((double X, double Y) a, (double X, double Y) b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));

	/// <summary>Whether <paramref name="b"/> is on the straight run from <paramref name="a"/> to <paramref name="c"/>, within <see cref="Epsilon"/>.</summary>
	private static bool Straight((double X, double Y) a, (double X, double Y) b, (double X, double Y) c)
	{
		var length = Distance(a, c);
		if (length <= Epsilon)
		{
			return false;
		}

		var cross = ((c.X - a.X) * (b.Y - a.Y)) - ((c.Y - a.Y) * (b.X - a.X));
		var along = ((b.X - a.X) * (c.X - a.X)) + ((b.Y - a.Y) * (c.Y - a.Y));
		return Math.Abs(cross) / length <= Epsilon && along >= 0 && along <= length * length;
	}

	private static string Text(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
