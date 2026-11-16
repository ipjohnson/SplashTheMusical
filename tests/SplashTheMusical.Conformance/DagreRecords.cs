using System.Globalization;
using System.Text;
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
						Text(fields, 1),
						Number(fields, 2),
						Number(fields, 3),
						Number(fields, 4),
						Number(fields, 5),
						Number(fields, 6),
						Text(fields, 7),
						Text(fields, 8),
						Text(fields, 9));
					break;
				case "node":
					Require(current).Nodes.Add(new DagreNodeInput(Field(fields, 1), Text(fields, 2), Number(fields, 3) ?? 0, Number(fields, 4) ?? 0));
					break;
				case "edge":
					Require(current).Edges.Add(new DagreEdgeInput(
						Field(fields, 1),
						Field(fields, 2),
						Text(fields, 3),
						Number(fields, 4) ?? 1,
						Number(fields, 5) ?? 1,
						Number(fields, 6) ?? 0,
						Number(fields, 7) ?? 0,
						Number(fields, 8) ?? 10,
						Text(fields, 9) ?? "r"));
					break;
				case "size":
					Require(current).Width = Number(fields, 1) ?? double.NaN;
					current!.Height = Number(fields, 2) ?? double.NaN;
					break;
				case "place":
					Require(current).Places.Add(new DagrePlace(
						Field(fields, 1),
						Number(fields, 2) ?? double.NaN,
						Number(fields, 3) ?? double.NaN,
						Number(fields, 4) ?? double.NaN,
						Number(fields, 5) ?? double.NaN));
					break;
				case "route":
					Require(current).Routes.Add(new DagreRoute(
						Field(fields, 1),
						Field(fields, 2),
						Text(fields, 3),
						Number(fields, 4),
						Number(fields, 5),
						Points(Field(fields, 6))));
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

	/// <summary>The field, unescaped. A trailing empty field is dropped from the line, so a missing one is empty.</summary>
	private static string Field(string[] fields, int index) => index < fields.Length ? Unescape(fields[index]) : "";

	/// <summary>The field, or null when it is empty.</summary>
	private static string? Text(string[] fields, int index)
	{
		var text = Field(fields, index);
		return text.Length == 0 ? null : text;
	}

	/// <summary>A number written by JavaScript's <c>String</c>, or null when the field is empty.</summary>
	private static double? Number(string[] fields, int index) => Text(fields, index) is { } text ? ParseNumber(text) : null;

	private static double ParseNumber(string text) => text switch
	{
		"NaN" => double.NaN,
		"Infinity" => double.PositiveInfinity,
		"-Infinity" => double.NegativeInfinity,
		_ => double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture),
	};

	private static List<(double X, double Y)> Points(string text)
	{
		var points = new List<(double X, double Y)>();
		foreach (var pair in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
		{
			var comma = pair.IndexOf(',', StringComparison.Ordinal);
			points.Add((ParseNumber(pair[..comma]), ParseNumber(pair[(comma + 1)..])));
		}

		return points;
	}

	private static string Unescape(string text)
	{
		if (!text.Contains('\\', StringComparison.Ordinal))
		{
			return text;
		}

		var builder = new StringBuilder(text.Length);
		for (var i = 0; i < text.Length; i++)
		{
			if (text[i] == '\\' && i + 1 < text.Length)
			{
				i++;
				builder.Append(text[i] switch
				{
					't' => '\t',
					'n' => '\n',
					'r' => '\r',
					_ => text[i],
				});
			}
			else
			{
				builder.Append(text[i]);
			}
		}

		return builder.ToString();
	}

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
