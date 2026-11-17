using System.Globalization;
using SplashTheMusical.Layout.Markers;
using SplashTheMusical.Layout.Paths;

namespace SplashTheMusical.Conformance;

/// <summary>
/// The curve ports against corpus/curves.txt, which tools/oracle/curves.mjs writes from d3-shape's <c>line()</c> and
/// mermaid's <c>insertEdge</c>.
/// </summary>
public class CurveTests
{
	private static readonly string[] Lines = File.ReadAllLines(Path.Combine(CorpusCheck.CorpusRoot, "curves.txt"));

	[Test]
	public async Task TheFileIsFromThePinnedMermaid() =>
		await Assert.That(Lines[1]).IsEqualTo("# mermaid " + CorpusCheck.PinnedMermaid());

	[Test]
	public async Task TheCurvesAreTheSchemasCurveValues()
	{
		var schema = Records("curve-name").Select(fields => fields[1]).ToArray();
		var ported = CurveNames.All.Select(entry => entry.Name).ToArray();
		await Assert.That(string.Join(' ', ported)).IsEqualTo(string.Join(' ', schema));
		await Assert.That(Enum.GetValues<Curve>().Length).IsEqualTo(schema.Length);
	}

	[Test]
	public async Task EveryCurveWritesD3sPath()
	{
		var problems = new List<string>();
		var counts = new Dictionary<string, int>(StringComparer.Ordinal);
		foreach (var fields in Records("curve"))
		{
			var (name, points, expected) = (fields[1], fields[2], fields[3]);
			counts[name] = counts.GetValueOrDefault(name) + 1;
			if (!CurveNames.TryParse(name, out var curve))
			{
				problems.Add($"{name}: no port");
				continue;
			}

			var actual = CurveWriter.ToString(curve, Points(points));
			if (actual != expected)
			{
				problems.Add($"{name} [{points}]\n  expected {expected}\n  got      {actual}");
			}
		}

		await Assert.That(string.Join('\n', problems.Take(20))).IsEmpty();
		foreach (var (name, _) in CurveNames.All)
		{
			await Assert.That(counts.GetValueOrDefault(name)).IsGreaterThanOrEqualTo(20);
		}
	}

	[Test]
	public async Task EveryEdgePathMatchesInsertEdge()
	{
		var problems = new List<string>();
		var count = 0;
		foreach (var fields in Records("edge"))
		{
			var (name, start, end, points, expected) = (fields[1], fields[2], fields[3], fields[4], fields[5]);
			count++;
			var actual = EdgePath.ToString(Points(points), CurveNames.Parse(name), start, end);
			if (actual != expected)
			{
				problems.Add($"{name} {start} {end} [{points}]\n  expected {expected}\n  got      {actual}");
			}
		}

		await Assert.That(string.Join('\n', problems.Take(20))).IsEmpty();
		await Assert.That(count).IsGreaterThanOrEqualTo(1000);
	}

	[Test]
	public async Task JsMathGivesV8sResults()
	{
		var problems = new List<string>();
		var counts = new Dictionary<string, int>(StringComparer.Ordinal);
		foreach (var fields in Records("math"))
		{
			var (name, arguments, expected) = (fields[1], fields[2].Split(',').Select(Number).ToArray(), Number(fields[3]));
			counts[name] = counts.GetValueOrDefault(name) + 1;
			var actual = name switch
			{
				"acos" => JsMath.Acos(arguments[0]),
				"atan" => JsMath.Atan(arguments[0]),
				"atan2" => JsMath.Atan2(arguments[0], arguments[1]),
				"sin" => JsMath.Sin(arguments[0]),
				"cos" => JsMath.Cos(arguments[0]),
				"hypot" => JsMath.Hypot(arguments[0], arguments[1]),
				_ => throw new InvalidOperationException($"curves.txt has a function CurveTests does not know: {name}"),
			};
			var same = double.IsNaN(expected) ? double.IsNaN(actual) : BitConverter.DoubleToInt64Bits(actual) == BitConverter.DoubleToInt64Bits(expected);
			if (!same)
			{
				problems.Add($"{name}({fields[2]}): expected {fields[3]}, got {actual.ToString("R", CultureInfo.InvariantCulture)}");
			}
		}

		await Assert.That(string.Join('\n', problems.Take(20))).IsEmpty();
		foreach (var name in new[] { "acos", "atan", "atan2", "sin", "cos", "hypot" })
		{
			await Assert.That(counts.GetValueOrDefault(name)).IsGreaterThan(1000);
		}
	}

	[Test]
	public async Task TheMarkerOffsetsAreMermaids()
	{
		await Assert.That(Offsets(MarkerOffsets.Line)).IsEqualTo(Offsets(Records("marker-offset")));
		await Assert.That(Offsets(MarkerOffsets.Dash)).IsEqualTo(Offsets(Records("dash-offset")));
	}

	private static IEnumerable<string[]> Records(string kind) =>
		Lines.Where(line => !line.StartsWith('#')).Select(line => line.Split('\t')).Where(fields => fields[0] == kind);

	private static string Offsets(IEnumerable<(string ArrowType, double Offset)> offsets) =>
		string.Join(' ', offsets.Select(entry => $"{entry.ArrowType}={entry.Offset.ToString(CultureInfo.InvariantCulture)}"));

	private static string Offsets(IEnumerable<string[]> records) =>
		Offsets(records.Select(fields => (fields[1], double.Parse(fields[2], CultureInfo.InvariantCulture))));

	// "x,y x,y", with each number as JavaScript's String() writes it.
	private static PathPoint[] Points(string text) =>
	[
		.. text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(pair =>
		{
			var comma = pair.IndexOf(',');
			return new PathPoint(Number(pair[..comma]), Number(pair[(comma + 1)..]));
		}),
	];

	private static double Number(string text) => double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
}
