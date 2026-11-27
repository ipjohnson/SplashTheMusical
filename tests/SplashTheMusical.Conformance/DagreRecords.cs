using System.Globalization;
using SplashTheMusical.Layout.Dagre;

namespace SplashTheMusical.Conformance;

/// <summary>One dagre layout of a <c>.dagre.txt</c> file: the input mermaid.js gave dagre and what dagre wrote back.</summary>
internal sealed record DagreRecord(
	int Number,
	DagreInput Input,
	double Width,
	double Height,
	IReadOnlyList<DagrePlace> Places,
	IReadOnlyList<DagreRoute> Routes);

internal sealed record DagrePlace(string Id, double X, double Y, double Width, double Height);

/// <param name="LabelX">The label's x, or null when dagre gave the edge no label position.</param>
internal sealed record DagreRoute(string V, string W, string? Name, double? LabelX, double? LabelY, IReadOnlyList<(double X, double Y)> Points);

/// <summary>Reads the dagre files the oracle writes. <c>tools/oracle/README.md</c> describes the format.</summary>
internal static class DagreRecords
{
	/// <summary>Every dagre file of the corpus, relative to the corpus folder, in ordinal order.</summary>
	public static IEnumerable<string> Files(string corpus) =>
		Directory.EnumerateFiles(corpus, "*.dagre.txt", SearchOption.AllDirectories)
			.Select(path => Path.GetRelativePath(corpus, path).Replace('\\', '/'))
			.Order(StringComparer.Ordinal);

	public static List<DagreRecord> Read(string path)
	{
		var records = new List<DagreRecord>();
		Builder? current = null;
		foreach (var line in File.ReadLines(path))
		{
			if (line.Length == 0 || line[0] == '#')
			{
				continue;
			}

			var fields = line.Split('\t');
			switch (fields[0])
			{
				case "layout":
					if (current is not null)
					{
						records.Add(current.Build());
					}

					current = new Builder(int.Parse(fields[1], CultureInfo.InvariantCulture));
					break;
				case "graph":
					Require(current).Options = new DagreOptions(
						RecordFields.Text(fields, 1),
						RecordFields.Number(fields, 2),
						RecordFields.Number(fields, 3),
						RecordFields.Number(fields, 4),
						RecordFields.Number(fields, 5),
						RecordFields.Number(fields, 6),
						RecordFields.Text(fields, 7),
						RecordFields.Text(fields, 8),
						RecordFields.Text(fields, 9));
					break;
				case "node":
					Require(current).Nodes.Add(new DagreNodeInput(RecordFields.Field(fields, 1), RecordFields.Text(fields, 2), RecordFields.Number(fields, 3) ?? 0, RecordFields.Number(fields, 4) ?? 0));
					break;
				case "edge":
					Require(current).Edges.Add(new DagreEdgeInput(
						RecordFields.Field(fields, 1),
						RecordFields.Field(fields, 2),
						RecordFields.Text(fields, 3),
						RecordFields.Number(fields, 4) ?? 1,
						RecordFields.Number(fields, 5) ?? 1,
						RecordFields.Number(fields, 6) ?? 0,
						RecordFields.Number(fields, 7) ?? 0,
						RecordFields.Number(fields, 8) ?? 10,
						RecordFields.Text(fields, 9) ?? "r"));
					break;
				case "size":
					Require(current).Width = RecordFields.Number(fields, 1) ?? double.NaN;
					current!.Height = RecordFields.Number(fields, 2) ?? double.NaN;
					break;
				case "place":
					Require(current).Places.Add(new DagrePlace(
						RecordFields.Field(fields, 1),
						RecordFields.Number(fields, 2) ?? double.NaN,
						RecordFields.Number(fields, 3) ?? double.NaN,
						RecordFields.Number(fields, 4) ?? double.NaN,
						RecordFields.Number(fields, 5) ?? double.NaN));
					break;
				case "route":
					Require(current).Routes.Add(new DagreRoute(
						RecordFields.Field(fields, 1),
						RecordFields.Field(fields, 2),
						RecordFields.Text(fields, 3),
						RecordFields.Number(fields, 4),
						RecordFields.Number(fields, 5),
						RecordFields.Points(RecordFields.Field(fields, 6))));
					break;
				default:
					throw new FormatException($"{path}: unknown line '{fields[0]}'.");
			}
		}

		if (current is not null)
		{
			records.Add(current.Build());
		}

		return records;
	}

	private static Builder Require(Builder? builder) => builder ?? throw new FormatException("A line comes before the first layout line.");

	private sealed class Builder(int number)
	{
		public DagreOptions Options { get; set; } = new();

		public List<DagreNodeInput> Nodes { get; } = [];

		public List<DagreEdgeInput> Edges { get; } = [];

		public double Width { get; set; } = double.NaN;

		public double Height { get; set; } = double.NaN;

		public List<DagrePlace> Places { get; } = [];

		public List<DagreRoute> Routes { get; } = [];

		public DagreRecord Build() => new(number, new DagreInput(Options, Nodes, Edges), Width, Height, Places, Routes);
	}
}
