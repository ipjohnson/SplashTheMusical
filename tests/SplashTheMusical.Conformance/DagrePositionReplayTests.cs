using System.Globalization;
using System.Text;
using SplashTheMusical.Layout.Dagre;

namespace SplashTheMusical.Conformance;

/// <summary>
/// Replays every dagre layout the oracle recorded that has no cluster through all of the port's phases, and compares
/// the graph's size, each node's box, each edge's points and each edge label's position with the recorded output.
/// </summary>
public class DagrePositionReplayTests
{
	private const double Tolerance = 0.01;

	private const int MaxReported = 20;

	[Test]
	public async Task PositionsMatchEveryRecordedLayoutWithoutClusters()
	{
		var layouts = 0;
		var failures = new List<string>();
		foreach (var file in DagreRecords.Files(CorpusCheck.CorpusRoot))
		{
			foreach (var record in DagreRecords.Read(Path.Combine(CorpusCheck.CorpusRoot, file)))
			{
				if (record.Input.Nodes.Any(node => node.Parent is not null))
				{
					continue;
				}

				layouts++;
				var failure = Compare(record);
				if (failure is not null)
				{
					failures.Add($"{file} layout {record.Number}: {failure}");
				}
			}
		}

		var report = new StringBuilder();
		foreach (var failure in failures.Take(MaxReported))
		{
			report.Append(failure).Append('\n');
		}

		if (failures.Count > MaxReported)
		{
			report.Append(CultureInfo.InvariantCulture, $"and {failures.Count - MaxReported} more of {layouts} layouts\n");
		}

		await Assert.That(layouts).IsGreaterThan(0);
		await Assert.That(report.ToString()).IsEqualTo("");
	}

	private static string? Compare(DagreRecord record)
	{
		LayoutGraph g;
		try
		{
			g = DagreLayout.BuildLayoutGraph(record.Input);
			DagreLayout.RunRankPhases(g);
			DagreLayout.RunOrderPhases(g);
			DagreLayout.RunPositionPhases(g);
		}
		catch (InvalidOperationException exception)
		{
			return exception.Message;
		}

		if (Differs(g.Label.Width, record.Width) || Differs(g.Label.Height, record.Height))
		{
			return Text($"the size is {g.Label.Width} x {g.Label.Height}, recorded {record.Width} x {record.Height}");
		}

		foreach (var place in record.Places)
		{
			var node = g.Node(g.NodeHandle(place.Id));
			if (Differs(node.X, place.X) || Differs(node.Y, place.Y) || Differs(node.Width, place.Width) || Differs(node.Height, place.Height))
			{
				return Text($"node '{place.Id}' is at {node.X},{node.Y} sized {node.Width} x {node.Height}, recorded {place.X},{place.Y} sized {place.Width} x {place.Height}");
			}
		}

		foreach (var route in record.Routes)
		{
			var e = g.FindEdge(g.NodeHandle(route.V), g.NodeHandle(route.W), route.Name);
			if (e < 0)
			{
				return $"the edge '{route.V}' to '{route.W}' named '{route.Name}' is missing";
			}

			var edge = g.Edge(e);
			if (edge.HasPosition != route.LabelX.HasValue)
			{
				return Text($"the edge '{route.V}' to '{route.W}' label position is {(edge.HasPosition ? "set" : "unset")}, recorded {(route.LabelX.HasValue ? "set" : "unset")}");
			}

			if (route.LabelX is { } labelX && (Differs(edge.X, labelX) || Differs(edge.Y, route.LabelY ?? double.NaN)))
			{
				return Text($"the edge '{route.V}' to '{route.W}' label is at {edge.X},{edge.Y}, recorded {labelX},{route.LabelY}");
			}

			var points = edge.Points ?? [];
			var matches = points.Count == route.Points.Count;
			for (var i = 0; matches && i < points.Count; i++)
			{
				matches = !Differs(points[i].X, route.Points[i].X) && !Differs(points[i].Y, route.Points[i].Y);
			}

			if (!matches)
			{
				return $"the edge '{route.V}' to '{route.W}' has points {Points(points)}, recorded {Points(route.Points)}";
			}
		}

		return null;
	}

	/// <summary>
	/// Whether the values are further apart than the tolerance. Two NaNs match, as both mean dagre gave no value, and so
	/// do two equal infinities, which an empty graph's size is.
	/// </summary>
	private static bool Differs(double actual, double expected) =>
		actual != expected && !(double.IsNaN(actual) && double.IsNaN(expected)) && !(Math.Abs(actual - expected) <= Tolerance);

	private static string Points(IReadOnlyList<(double X, double Y)> points) =>
		string.Join(' ', points.Select(p => Text($"{p.X},{p.Y}")));

	private static string Text(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
