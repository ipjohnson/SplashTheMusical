using System.Globalization;
using System.Text;
using SplashTheMusical.Layout.Dagre;

namespace SplashTheMusical.Conformance;

/// <summary>
/// Replays every dagre layout the oracle recorded through the port's phases up to the order, and compares the order of
/// the nodes in each rank with the recorded positions. The records hold no orders. dagre places the nodes of a rank
/// one after another in their order along the axis across the ranks, which is x for TB and BT and y for LR and RL, so
/// the leaf nodes of a rank sorted by order have strictly increasing coordinates on that axis when the orders match.
/// Cluster nodes are left out, as dagre centres them on their contents.
/// </summary>
public class DagreOrderReplayTests
{
	private const int MaxReported = 20;

	[Test]
	public void OrdersMatchEveryRecordedLayout()
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

		Verify(layouts > 0, $"was {layouts}");
		Verify(report.ToString() == "", $"was {report.ToString()}");
	}

	private static string? Compare(DagreRecord record)
	{
		LayoutGraph g;
		try
		{
			g = DagreLayout.BuildLayoutGraph(record.Input);
			DagreLayout.RunRankPhases(g);
			DagreLayout.RunOrderPhases(g);
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
		var placed = new List<(string Id, double Rank, int Order, double Across)>();
		foreach (var place in record.Places)
		{
			if (parents.Contains(place.Id))
			{
				continue;
			}

			var node = g.Node(g.NodeHandle(place.Id));
			placed.Add((place.Id, node.Rank, node.Order, axis is "lr" or "rl" ? place.Y : place.X));
		}

		foreach (var rank in placed.GroupBy(p => p.Rank))
		{
			var inOrder = rank.OrderBy(p => p.Order).ToList();
			for (var i = 1; i < inOrder.Count; i++)
			{
				var before = inOrder[i - 1];
				var after = inOrder[i];
				if (!(before.Across < after.Across))
				{
					return string.Create(CultureInfo.InvariantCulture, $"rank {rank.Key}: the port puts '{before.Id}' (order {before.Order}) before '{after.Id}' (order {after.Order}), and the recorded positions put it at {before.Across} and '{after.Id}' at {after.Across}");
				}
			}
		}

		return null;
	}
}
