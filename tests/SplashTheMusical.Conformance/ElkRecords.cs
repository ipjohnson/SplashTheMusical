using System.Globalization;

namespace SplashTheMusical.Conformance;

/// <summary>One ELK call of an <c>.elk.txt</c> file: the graph mermaid.js gave ELK and the graph ELK returned.</summary>
internal sealed record ElkRecord(int Number, ElkInput Input, ElkOutput Output);

/// <summary>The graph mermaid.js gave ELK, in the order the oracle walked it.</summary>
internal sealed record ElkInput(
	IReadOnlyList<ElkNodeInput> Nodes,
	IReadOnlyList<ElkPortInput> Ports,
	IReadOnlyList<ElkEdgeInput> Edges,
	IReadOnlyList<ElkLabelInput> Labels,
	IReadOnlyList<ElkOption> Options);

/// <param name="Parent">The parent's id, or null for the root.</param>
internal sealed record ElkNodeInput(string Id, string? Parent, double? Width, double? Height, double? X, double? Y);

internal sealed record ElkPortInput(string Id, string Node, double? Width, double? Height, double? X, double? Y);

/// <param name="Container">The id of the node whose <c>edges</c> holds the edge.</param>
internal sealed record ElkEdgeInput(string Id, string Container, IReadOnlyList<string> Sources, IReadOnlyList<string> Targets);

/// <param name="OwnerKind"><c>node</c>, <c>port</c> or <c>edge</c>.</param>
internal sealed record ElkLabelInput(string OwnerKind, string Owner, int Index, string Text, double? Width, double? Height, double? X, double? Y);

/// <param name="IsProperty">Whether the key is of the owner's <c>properties</c> rather than its <c>layoutOptions</c>.</param>
/// <param name="OwnerKind"><c>node</c>, <c>port</c>, <c>edge</c>, <c>node-label</c>, <c>port-label</c> or <c>edge-label</c>.</param>
/// <param name="LabelIndex">The label's index for a label's option, otherwise null.</param>
internal sealed record ElkOption(bool IsProperty, string OwnerKind, string Owner, int? LabelIndex, string Key, string Value);

/// <summary>The graph ELK returned, in the order the oracle walked it.</summary>
internal sealed record ElkOutput(
	IReadOnlyList<ElkPlace> Places,
	IReadOnlyList<ElkPortPlace> Ports,
	IReadOnlyList<ElkLabelPlace> Labels,
	IReadOnlyList<ElkRoute> Routes);

/// <summary>A node's box, relative to its parent as ELK gives it. A missing number is NaN.</summary>
internal sealed record ElkPlace(string Id, string? Parent, double X, double Y, double Width, double Height);

internal sealed record ElkPortPlace(string Id, string Node, double X, double Y, double Width, double Height);

internal sealed record ElkLabelPlace(string OwnerKind, string Owner, int Index, double X, double Y, double Width, double Height);

/// <param name="Container">The id of the node whose <c>edges</c> holds the edge.</param>
/// <param name="ElkContainer">The edge's <c>container</c> as ELK set it, or null.</param>
internal sealed record ElkRoute(
	string Id,
	string Container,
	string? ElkContainer,
	IReadOnlyList<(double X, double Y)> JunctionPoints,
	IReadOnlyList<ElkSection> Sections);

internal sealed record ElkSection(
	string Id,
	(double X, double Y)? Start,
	(double X, double Y)? End,
	IReadOnlyList<(double X, double Y)> BendPoints,
	IReadOnlyList<string> IncomingShapes,
	IReadOnlyList<string> OutgoingShapes,
	IReadOnlyList<string> IncomingSections,
	IReadOnlyList<string> OutgoingSections);

/// <summary>Reads the ELK files the oracle writes. <c>tools/oracle/README.md</c> describes the format.</summary>
internal static class ElkRecords
{
	/// <summary>Every ELK file of the corpus, relative to the corpus folder, in ordinal order.</summary>
	public static IEnumerable<string> Files(string corpus) =>
		Directory.EnumerateFiles(corpus, "*.elk.txt", SearchOption.AllDirectories)
			.Select(path => Path.GetRelativePath(corpus, path).Replace('\\', '/'))
			.Order(StringComparer.Ordinal);

	public static List<ElkRecord> Read(string path) => Parse(File.ReadLines(path), path);

	/// <summary>The calls of a file's lines. <paramref name="name"/> names the file in a <see cref="FormatException"/>.</summary>
	public static List<ElkRecord> Parse(IEnumerable<string> lines, string name)
	{
		var records = new List<ElkRecord>();
		Builder? current = null;
		foreach (var line in lines)
		{
			if (line.Length == 0 || line[0] == '#')
			{
				continue;
			}

			var fields = line.Split('\t');
			if (fields[0] == "layout")
			{
				if (current is not null)
				{
					records.Add(current.Build(name));
				}

				current = new Builder(int.Parse(fields[1], CultureInfo.InvariantCulture));
				continue;
			}

			if (current is null)
			{
				throw new FormatException($"{name}: a '{fields[0]}' line comes before the first layout line.");
			}

			if (fields[0] == "result")
			{
				current.HasResult = true;
				continue;
			}

			if (current.HasResult ? !ReadOutput(current, fields) : !ReadInput(current, fields))
			{
				throw new FormatException($"{name}: unknown {(current.HasResult ? "output" : "input")} line '{fields[0]}'.");
			}
		}

		if (current is not null)
		{
			records.Add(current.Build(name));
		}

		return records;
	}

	private static bool ReadInput(Builder b, string[] fields)
	{
		switch (fields[0])
		{
			case "node":
				b.Nodes.Add(new ElkNodeInput(
					RecordFields.Field(fields, 1),
					RecordFields.Text(fields, 2),
					RecordFields.Number(fields, 3),
					RecordFields.Number(fields, 4),
					RecordFields.Number(fields, 5),
					RecordFields.Number(fields, 6)));
				return true;
			case "port":
				b.Ports.Add(new ElkPortInput(
					RecordFields.Field(fields, 1),
					RecordFields.Field(fields, 2),
					RecordFields.Number(fields, 3),
					RecordFields.Number(fields, 4),
					RecordFields.Number(fields, 5),
					RecordFields.Number(fields, 6)));
				return true;
			case "edge":
				b.Edges.Add(new ElkEdgeInput(
					RecordFields.Field(fields, 1),
					RecordFields.Field(fields, 2),
					RecordFields.Ids(fields, 3),
					RecordFields.Ids(fields, 4)));
				return true;
			case "label":
				b.Labels.Add(new ElkLabelInput(
					RecordFields.Field(fields, 1),
					RecordFields.Field(fields, 2),
					Index(fields, 3),
					RecordFields.Field(fields, 4),
					RecordFields.Number(fields, 5),
					RecordFields.Number(fields, 6),
					RecordFields.Number(fields, 7),
					RecordFields.Number(fields, 8)));
				return true;
			case "option" or "property":
				b.Options.Add(new ElkOption(
					fields[0] == "property",
					RecordFields.Field(fields, 1),
					RecordFields.Field(fields, 2),
					RecordFields.Text(fields, 3) is { } index ? int.Parse(index, CultureInfo.InvariantCulture) : null,
					RecordFields.Field(fields, 4),
					RecordFields.Field(fields, 5)));
				return true;
			default:
				return false;
		}
	}

	private static bool ReadOutput(Builder b, string[] fields)
	{
		switch (fields[0])
		{
			case "place":
				b.Places.Add(new ElkPlace(
					RecordFields.Field(fields, 1),
					RecordFields.Text(fields, 2),
					Number(fields, 3),
					Number(fields, 4),
					Number(fields, 5),
					Number(fields, 6)));
				return true;
			case "place-port":
				b.PortPlaces.Add(new ElkPortPlace(
					RecordFields.Field(fields, 1),
					RecordFields.Field(fields, 2),
					Number(fields, 3),
					Number(fields, 4),
					Number(fields, 5),
					Number(fields, 6)));
				return true;
			case "place-label":
				b.LabelPlaces.Add(new ElkLabelPlace(
					RecordFields.Field(fields, 1),
					RecordFields.Field(fields, 2),
					Index(fields, 3),
					Number(fields, 4),
					Number(fields, 5),
					Number(fields, 6),
					Number(fields, 7)));
				return true;
			case "route":
				b.Routes.Add(new RouteBuilder(
					RecordFields.Field(fields, 1),
					RecordFields.Field(fields, 2),
					RecordFields.Text(fields, 3),
					RecordFields.Points(RecordFields.Field(fields, 4))));
				return true;
			case "section":
				var id = RecordFields.Field(fields, 1);
				var route = b.Routes.Count > 0 && b.Routes[^1].Id == id
					? b.Routes[^1]
					: throw new FormatException($"The section of edge '{id}' does not follow its route line.");
				route.Sections.Add(new ElkSection(
					RecordFields.Field(fields, 2),
					RecordFields.Point(fields, 3),
					RecordFields.Point(fields, 4),
					RecordFields.Points(RecordFields.Field(fields, 5)),
					RecordFields.Ids(fields, 6),
					RecordFields.Ids(fields, 7),
					RecordFields.Ids(fields, 8),
					RecordFields.Ids(fields, 9)));
				return true;
			default:
				return false;
		}
	}

	private static int Index(string[] fields, int index) => int.Parse(RecordFields.Field(fields, index), CultureInfo.InvariantCulture);

	private static double Number(string[] fields, int index) => RecordFields.Number(fields, index) ?? double.NaN;

	private sealed class RouteBuilder(string id, string container, string? elkContainer, IReadOnlyList<(double X, double Y)> junctionPoints)
	{
		public string Id => id;

		public List<ElkSection> Sections { get; } = [];

		public ElkRoute Build() => new(id, container, elkContainer, junctionPoints, Sections);
	}

	private sealed class Builder(int number)
	{
		public bool HasResult { get; set; }

		public List<ElkNodeInput> Nodes { get; } = [];

		public List<ElkPortInput> Ports { get; } = [];

		public List<ElkEdgeInput> Edges { get; } = [];

		public List<ElkLabelInput> Labels { get; } = [];

		public List<ElkOption> Options { get; } = [];

		public List<ElkPlace> Places { get; } = [];

		public List<ElkPortPlace> PortPlaces { get; } = [];

		public List<ElkLabelPlace> LabelPlaces { get; } = [];

		public List<RouteBuilder> Routes { get; } = [];

		public ElkRecord Build(string name) => HasResult
			? new(
				number,
				new ElkInput(Nodes, Ports, Edges, Labels, Options),
				new ElkOutput(Places, PortPlaces, LabelPlaces, [.. Routes.Select(r => r.Build())]))
			: throw new FormatException($"{name}: layout {number} has no result line.");
	}
}
