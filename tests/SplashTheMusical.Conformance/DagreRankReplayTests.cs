using System.Globalization;
using System.Text;
using SplashTheMusical.Layout.Dagre;

namespace SplashTheMusical.Conformance;

/// <summary>
/// Replays every dagre layout the oracle recorded through the port's phases up to the ranks, and compares the ranks
/// with the recorded positions. The records hold no ranks. dagre gives every node of a rank the same coordinate on the
/// rank axis, and each later rank a larger one, so the leaf nodes sorted by rank and by that coordinate fall into the
/// same groups in the same order when the ranks match. Cluster nodes are left out, as dagre centres them on their
/// contents.
/// </summary>
public class DagreRankReplayTests
{
	private const int MaxReported = 20;

	[Test]
	public async Task RanksMatchEveryRecordedLayout()
	{
		var layouts = 0;
		var failures = new List<string>();
		foreach (var file in DagreRecords.Files(CorpusCheck.CorpusRoot))
		{
			foreach (var record in DagreRecords.Read(Path.Combine(CorpusCheck.CorpusRoot, file)))
			{
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
		}
		catch (InvalidOperationException exception)
		{
			return exception.Message;
		}

		var parents = new HashSet<string>(StringComparer.Ordinal);
		foreach (var node in record.Input.Nodes)
		{
			if (node.Parent is not null)
			{
				parents.Add(node.Parent);
			}
		}

		var axis = (record.Input.Options.RankDir ?? "tb").ToLowerInvariant();
		var ids = new List<string>();
		var ranks = new List<double>();
		var coordinates = new List<double>();
		foreach (var place in record.Places)
		{
			if (parents.Contains(place.Id))
			{
				continue;
			}

			ids.Add(place.Id);
			ranks.Add(g.Node(g.NodeHandle(place.Id)).Rank);
			coordinates.Add(axis switch
			{
				"bt" => -place.Y,
				"lr" => place.X,
				"rl" => -place.X,
				_ => place.Y,
			});
		}

		var rankOrder = DenseOrder(ranks);
		var recordedOrder = DenseOrder(coordinates);
		for (var i = 0; i < ids.Count; i++)
		{
			if (rankOrder[i] != recordedOrder[i])
			{
				return string.Create(CultureInfo.InvariantCulture, $"node '{ids[i]}' has rank {ranks[i]}, layer {rankOrder[i]} of the port's, and layer {recordedOrder[i]} of the recorded positions");
			}
		}

		return null;
	}

	/// <summary>Each value's position among the distinct values in ascending order.</summary>
	private static int[] DenseOrder(List<double> values)
	{
		var distinct = values.Distinct().Order().ToList();
		var result = new int[values.Count];
		for (var i = 0; i < values.Count; i++)
		{
			result[i] = distinct.BinarySearch(values[i]);
		}

		return result;
	}
}
