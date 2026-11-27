namespace SplashTheMusical.Conformance;

/// <summary>Reads every <c>.elk.txt</c> file of the corpus with <see cref="ElkRecords"/>.</summary>
public class ElkRecordsTests
{
	private const int MaxReported = 20;

	[Test]
	public void ReadsEveryCommittedFileWithEachNodePlacedAndEachEdgeRouted()
	{
		var files = 0;
		var layouts = 0;
		var problems = new List<string>();
		foreach (var file in ElkRecords.Files(CorpusCheck.CorpusRoot))
		{
			files++;
			var records = ElkRecords.Read(Path.Combine(CorpusCheck.CorpusRoot, file));
			if (records.Count == 0)
			{
				problems.Add($"{file}: no layout");
			}

			for (var i = 0; i < records.Count; i++)
			{
				layouts++;
				problems.AddRange(Check(records[i], i + 1).Select(p => $"{file} layout {records[i].Number}: {p}"));
			}
		}

		Verify(() => files > 0);
		Verify(() => layouts >= files);
		Verify(!problems.Any(), $"{string.Join('\n', problems.Take(MaxReported))}\n{problems.Count} problems");
	}

	[Test]
	public void NoCaseDrawnWithDagreHasAnElkFile()
	{
		var problems = new List<string>();
		foreach (var file in ElkRecords.Files(CorpusCheck.CorpusRoot))
		{
			var facts = Path.Combine(CorpusCheck.CorpusRoot, file[..^".elk.txt".Length] + ".facts.txt");
			var lines = File.ReadAllLines(facts);
			if (!lines.Contains("accept\tyes"))
			{
				problems.Add($"{file}: its case is refused");
			}

			if (lines.Contains("config\tlayout\t\"dagre\""))
			{
				problems.Add($"{file}: its case is drawn with dagre");
			}
		}

		Verify(!problems.Any(), $"{string.Join('\n', problems)}");
	}

	[Test]
	public void ParsesBothSidesOfACall()
	{
		string[] lines =
		[
			"# mermaid.js ELK layouts. Refresh with: mise run oracle",
			"layout\t1",
			"node\troot",
			"option\tnode\troot\t\telk.direction\tDOWN",
			"node\ta\\sb\troot\t10.5\t20",
			"port\tp\ta\\sb\t2\t2",
			"label\tnode\ta\\sb\t0\tline\\none\t8\t9",
			"edge\te\troot\ta\\sb\tp",
			"label\tedge\te\t0\t\t0\t0",
			"option\tedge-label\te\t0\tedgeLabels.placement\tCENTER",
			"property\tedge\te\t\tkey\tvalue",
			"result",
			"place\troot\t\t0\t0\t30\t40",
			"place\ta\\sb\troot\t12\t12\t10.5\t20",
			"place-port\tp\ta\\sb\t1\t2\t2\t2",
			"route\te\troot\troot\t1,2",
			"section\te\te_s0\t1,2\t3,4\t1,3 3,3\ta\\sb\tp",
			"place-label\tedge\te\t0\t5\t6\t0\t0",
			"layout\t2",
			"node\troot",
			"result",
			"place\troot\t\t0\t0\t0\t0",
		];

		var records = ElkRecords.Parse(lines, "inline");

		Verify(() => records.Count == 2);
		var first = records[0];
		Verify(() => first.Input.Nodes[1] == new ElkNodeInput("a b", "root", 10.5, 20, null, null));
		Verify(() => first.Input.Ports[0].Node == "a b");
		Verify(() => first.Input.Labels[0].Text == "line\none");
		Verify(() => first.Input.Edges[0].Sources.Single() == "a b" && first.Input.Edges[0].Targets.Single() == "p");
		Verify(() => first.Input.Options.Count == 3);
		Verify(() => first.Input.Options[0] == new ElkOption(false, "node", "root", null, "elk.direction", "DOWN"));
		Verify(() => first.Input.Options[1].LabelIndex == 0 && first.Input.Options[2].IsProperty);
		Verify(() => first.Output.Places[1] == new ElkPlace("a b", "root", 12, 12, 10.5, 20));
		Verify(() => first.Output.Ports[0].X == 1);
		var section = first.Output.Routes[0].Sections.Single();
		Verify(() => section.Start == (1, 2) && section.End == (3, 4) && section.BendPoints.Count == 2);
		Verify(() => section.IncomingShapes.Single() == "a b" && section.OutgoingShapes.Single() == "p");
		Verify(() => first.Output.Routes[0].JunctionPoints.Single() == (1, 2));
		Verify(() => first.Output.Labels[0].X == 5);
		Verify(() => records[1].Number == 2);
	}

	[Test]
	public void RefusesACallWithoutItsResult() =>
		Throws<FormatException>(() => ElkRecords.Parse(["layout\t1", "node\troot"], "inline"));

	private static IEnumerable<string> Check(ElkRecord record, int number)
	{
		if (record.Number != number)
		{
			yield return $"numbered {record.Number}, expected {number}";
		}

		var input = record.Input;
		var output = record.Output;
		if (input.Nodes.Count == 0 || input.Nodes[0].Parent is not null || input.Nodes.Skip(1).Any(n => n.Parent is null))
		{
			yield return "the first node is not the only root";
		}

		var inputNodes = input.Nodes.Select(n => (n.Id, n.Parent));
		var placedNodes = output.Places.Select(p => (p.Id, p.Parent));
		if (!inputNodes.SequenceEqual(placedNodes))
		{
			yield return "the placed nodes are not the input nodes in the same order";
		}

		foreach (var place in output.Places.Where(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y) || !double.IsFinite(p.Width) || !double.IsFinite(p.Height)))
		{
			yield return $"node {place.Id} is not placed";
		}

		var shapes = input.Nodes.Select(n => n.Id).Concat(input.Ports.Select(p => p.Id)).ToHashSet(StringComparer.Ordinal);
		foreach (var edge in input.Edges.Where(e => e.Sources.Count == 0 || e.Targets.Count == 0 || !e.Sources.Concat(e.Targets).All(shapes.Contains)))
		{
			yield return $"edge {edge.Id} has an end that is not a node or port";
		}

		if (!input.Edges.Select(e => e.Id).SequenceEqual(output.Routes.Select(r => r.Id)))
		{
			yield return "the routed edges are not the input edges in the same order";
		}

		// Only the layered algorithm routes edges. elk.box and elk.rectpacking leave them without sections.
		var layered = input.Options.Any(o => o is { OwnerKind: "node", Key: "elk.algorithm", Value: "elk.layered" } && o.Owner == input.Nodes[0].Id);
		foreach (var route in output.Routes.Where(r => (layered && r.Sections.Count == 0) || r.Sections.Any(s => s.Start is null || s.End is null)))
		{
			yield return $"edge {route.Id} has no section with both ends";
		}
	}
}
