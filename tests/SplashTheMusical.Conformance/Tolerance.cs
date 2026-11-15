using System.Globalization;
using System.Text.Json;

namespace SplashTheMusical.Conformance;

/// <summary>
/// The L3 and L4 tolerances of one diagram type. corpus/tolerances/README.md describes the file.
/// </summary>
/// <param name="Canvas">How far the canvas width and height may differ, as a fraction of mermaid.js's.</param>
/// <param name="Overlap">The least intersection over union of a box and its counterpart.</param>
/// <param name="Raster">The least structural similarity of STM's raster to mermaid.js's at L4.</param>
/// <param name="RasterReportOnly">True when L4 reports the type's scores and does not judge them.</param>
/// <param name="Reason">Why the type is looser than <see cref="Start"/>.</param>
public sealed record Tolerance(double Canvas, double Overlap, double Raster, bool RasterReportOnly = false, string? Reason = null)
{
	/// <summary>The starting tolerances of section 7 of docs/mermaid-parity-plan.md. A type may only be tighter without a reason.</summary>
	public static Tolerance Start { get; } = new(0.05, 0.8, 0.9);

	public static string Root => Path.Combine(CorpusCheck.CorpusRoot, "tolerances");

	public static string FileName(string type) => type + ".json";

	public bool IsLooserThanStart => Canvas > Start.Canvas || Overlap < Start.Overlap || Raster < Start.Raster || RasterReportOnly;

	/// <param name="file">The name problems are reported against.</param>
	public static Tolerance? Parse(string file, string json, List<string> problems)
	{
		JsonDocument document;
		try
		{
			document = JsonDocument.Parse(json);
		}
		catch (JsonException e)
		{
			problems.Add($"{file}: {e.Message}");
			return null;
		}

		using (document)
		{
			if (document.RootElement.ValueKind != JsonValueKind.Object)
			{
				problems.Add($"{file}: is not a JSON object");
				return null;
			}

			double? canvas = null;
			double? overlap = null;
			double? raster = null;
			var reportOnly = false;
			string? reason = null;
			foreach (var property in document.RootElement.EnumerateObject())
			{
				switch (property.Name)
				{
					case "canvas":
						canvas = Fraction(file, property, problems);
						break;
					case "overlap":
						overlap = Fraction(file, property, problems);
						break;
					case "raster":
						raster = Fraction(file, property, problems);
						break;
					case "rasterReportOnly" when property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False:
						reportOnly = property.Value.GetBoolean();
						break;
					case "rasterReportOnly":
						problems.Add($"{file}: rasterReportOnly is not true or false");
						break;
					case "reason" when property.Value.ValueKind == JsonValueKind.String:
						reason = property.Value.GetString();
						break;
					case "reason":
						problems.Add($"{file}: reason is not a string");
						break;
					default:
						problems.Add($"{file}: {property.Name} is not canvas, overlap, raster, rasterReportOnly or reason");
						break;
				}
			}

			if (canvas is null || overlap is null || raster is null)
			{
				problems.Add($"{file}: needs canvas, overlap and raster");
				return null;
			}

			var tolerance = new Tolerance(canvas.Value, overlap.Value, raster.Value, reportOnly, string.IsNullOrWhiteSpace(reason) ? null : reason);
			if (tolerance.IsLooserThanStart && tolerance.Reason is null)
			{
				problems.Add(
					string.Create(
						CultureInfo.InvariantCulture,
						$"{file}: canvas {tolerance.Canvas}, overlap {tolerance.Overlap}, raster {tolerance.Raster} and rasterReportOnly {(tolerance.RasterReportOnly ? "true" : "false")} are looser than {Start.Canvas}, {Start.Overlap}, {Start.Raster} and false, and it has no reason"
					)
				);
			}

			return tolerance;
		}
	}

	/// <summary>The tolerance of every type that has a valid file in the folder.</summary>
	public static IReadOnlyDictionary<string, Tolerance> Read(string folder, List<string> problems)
	{
		var tolerances = new Dictionary<string, Tolerance>(StringComparer.Ordinal);
		if (!Directory.Exists(folder))
		{
			return tolerances;
		}

		foreach (var path in Directory.EnumerateFiles(folder, "*.json").Order(StringComparer.Ordinal))
		{
			var file = Path.GetFileName(path);
			if (Parse(file, File.ReadAllText(path), problems) is { } tolerance)
			{
				tolerances[file[..^".json".Length]] = tolerance;
			}
		}

		return tolerances;
	}

	/// <summary>One line per invalid file, per type without a file and per file without a type.</summary>
	public static IReadOnlyList<string> Problems(string folder, IEnumerable<string> types)
	{
		var problems = new List<string>();
		_ = Read(folder, problems);
		var known = types.ToHashSet(StringComparer.Ordinal);
		var files = Directory.Exists(folder)
			? Directory.EnumerateFiles(folder, "*.json").Select(p => Path.GetFileName(p)[..^".json".Length]).ToHashSet(StringComparer.Ordinal)
			: [];
		foreach (var type in known.Where(t => !files.Contains(t)).Order(StringComparer.Ordinal))
		{
			problems.Add($"{type}: no {FileName(type)}");
		}

		foreach (var type in files.Where(t => !known.Contains(t)).Order(StringComparer.Ordinal))
		{
			problems.Add($"{FileName(type)}: the corpus has no {type} cases");
		}

		return problems;
	}

	private static double? Fraction(string file, JsonProperty property, List<string> problems)
	{
		if (property.Value.ValueKind == JsonValueKind.Number && property.Value.GetDouble() is var value and >= 0 and <= 1)
		{
			return value;
		}

		problems.Add($"{file}: {property.Name} is not a number from 0 to 1");
		return null;
	}
}
