using System.Text;
using SplashTheMusical.Layout.Markers;

namespace SplashTheMusical.Conformance;

/// <summary>
/// <see cref="EdgeMarkers"/> against corpus/markers.txt, which tools/oracle/curves.mjs writes from mermaid's
/// <c>insertMarkers</c> and <c>insertEdge</c> for the diagram id ID and type TYPE, and against the edge ends the
/// oracle's structure facts name.
/// </summary>
public class EdgeMarkerTests
{
	private static readonly string[] Lines = File.ReadAllLines(Path.Combine(CorpusCheck.CorpusRoot, "markers.txt"));

	// The mermaid arrow type behind each end name the facts write, per diagram. The flowchart, state and class names
	// are those of `end` in tools/oracle/extract.mjs, and the ER names are its cardinality names, which mermaid
	// lower-cases into arrow types. Mermaid has no marker for md_parent.
	private static readonly Dictionary<string, (MarkerDiagram Diagram, Dictionary<string, string?> Ends)> Diagrams = new(StringComparer.Ordinal)
	{
		["flowchart-v2"] = (MarkerDiagram.Flowchart, FlowchartEnds()),
		["flowchart-elk"] = (MarkerDiagram.Flowchart, FlowchartEnds()),
		["swimlane"] = (MarkerDiagram.Flowchart, FlowchartEnds()),
		["stateDiagram"] = (MarkerDiagram.State, new(StringComparer.Ordinal)
		{
			["none"] = null,
			["arrow"] = "arrow_barb",
			["arrow_barb_neo"] = "arrow_barb_neo",
		}),
		["classDiagram"] = (MarkerDiagram.Class, new(StringComparer.Ordinal)
		{
			["none"] = null,
			["arrow"] = "dependency",
			["triangle"] = "extension",
			["diamond"] = "aggregation",
			["diamond-filled"] = "composition",
			["lollipop"] = "lollipop",
		}),
		["er"] = (MarkerDiagram.Er, new(StringComparer.Ordinal)
		{
			["one"] = "only_one",
			["zero-or-one"] = "zero_or_one",
			["one-or-more"] = "one_or_more",
			["zero-or-more"] = "zero_or_more",
			["parent"] = "md_parent",
		}),
	};

	[Test]
	public void TheFileIsFromThePinnedMermaid() =>
		Verify(Lines[1] == "# mermaid " + CorpusCheck.PinnedMermaid(), $"was {Lines[1]}");

	[Test]
	public void TheDefinitionsAreMermaids()
	{
		var expected = Lines.Where(line => line.StartsWith("element\t", StringComparison.Ordinal)).ToArray();
		var actual = new List<string>();
		foreach (var set in EdgeMarkers.Sets)
		{
			foreach (var marker in set.Markers)
			{
				var depth = 0;
				if (marker.InDefs)
				{
					actual.Add($"element\t{set.Name}\t0\tdefs");
					depth = 1;
				}

				var fields = new List<string> { "id=" + EdgeMarkers.Id("ID", "TYPE", marker) };
				if (marker.Class is { } markerClass)
				{
					fields.Add($"class={markerClass} TYPE");
				}

				fields.AddRange(Fields(marker.Attributes));
				actual.Add($"element\t{set.Name}\t{depth}\tmarker\t{string.Join('\t', fields)}");
				foreach (var child in marker.Children)
				{
					Write(set.Name, child, depth + 1, actual);
				}
			}
		}

		Verify(!Diff(expected, actual).Any(), $"{string.Join('\n', Diff(expected, actual))}");
	}

	[Test]
	public void EveryArrowTypeGetsMermaidsMarkerId()
	{
		var problems = new List<string>();
		foreach (var fields in Lines.Select(line => line.Split('\t')).Where(fields => fields[0] == "arrow"))
		{
			var (type, look, start, end) = (fields[1], fields[2], fields[3], fields[4]);
			var neo = look == "neo";
			var actualStart = Url(EdgeMarkers.MarkerId("ID", "TYPE", type, MarkerEnd.Start, neo));
			var actualEnd = Url(EdgeMarkers.MarkerId("ID", "TYPE", type, MarkerEnd.End, neo));
			if (actualStart != start || actualEnd != end)
			{
				problems.Add($"{type} {look}: expected {start} {end}, got {actualStart} {actualEnd}");
			}
		}

		Verify(!problems.Any(), $"{string.Join('\n', problems)}");
		Verify(Lines.Count(line => line.StartsWith("arrow\t", StringComparison.Ordinal)) == (EdgeMarkers.ArrowTypes.Length + 4) * 2, $"was {Lines.Count(line => line.StartsWith("arrow\t", StringComparison.Ordinal))}");
	}

	[Test]
	public void EveryEndTheFactsNameHasAMarker()
	{
		var problems = new SortedSet<string>(StringComparer.Ordinal);
		var seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (var file in Directory.EnumerateFiles(CorpusCheck.CorpusRoot, "*.facts.txt", SearchOption.AllDirectories))
		{
			var facts = File.ReadAllLines(file).Select(line => line.Split('\t')).ToArray();
			var type = facts.FirstOrDefault(fields => fields[0] == "type")?[1];
			if (type is null || !Diagrams.TryGetValue(type, out var diagram))
			{
				continue;
			}

			var kind = diagram.Diagram is MarkerDiagram.Class or MarkerDiagram.Er ? "relation" : "edge";
			foreach (var fields in facts.Where(fields => fields[0] == kind && fields.Length > 4))
			{
				foreach (var (name, end) in new[] { (fields[3], MarkerEnd.Start), (fields[4], MarkerEnd.End) })
				{
					seen.Add($"{type} {name}");
					if (!diagram.Ends.TryGetValue(name, out var arrowType))
					{
						problems.Add($"{type}: the facts name the end {name}, which the test does not map to an arrow type");
						continue;
					}

					if (arrowType is null or "md_parent")
					{
						if (EdgeMarkers.MarkerId("ID", "TYPE", arrowType, end, useMargin: false) is { } id)
						{
							problems.Add($"{type} {name}: mermaid draws no marker, STM gives {id}");
						}

						continue;
					}

					foreach (var neo in new[] { false, true })
					{
						var id = EdgeMarkers.MarkerId("ID", "TYPE", arrowType, end, neo);
						var inserted = EdgeMarkers.SetsFor(diagram.Diagram, neo)
							.Select(EdgeMarkers.FindSet)
							.SelectMany(set => set!.Markers)
							.Any(marker => EdgeMarkers.Id("ID", "TYPE", marker) == id);
						if (!inserted)
						{
							problems.Add($"{type} {name} {end} {(neo ? "neo" : "classic")}: {id ?? "no id"} is not among the markers the diagram inserts");
						}
					}
				}
			}
		}

		Verify(!problems.Any(), $"{string.Join('\n', problems)}");
		foreach (var type in new[] { "flowchart-v2", "stateDiagram", "classDiagram", "er" })
		{
			Verify(seen.Count(entry => entry.StartsWith(type + " ", StringComparison.Ordinal)) > 1, $"was {seen.Count(entry => entry.StartsWith(type + " ", StringComparison.Ordinal))}");
		}
	}

	private static Dictionary<string, string?> FlowchartEnds() => new(StringComparer.Ordinal)
	{
		["none"] = null,
		["arrow"] = "arrow_point",
		["circle"] = "arrow_circle",
		["cross"] = "arrow_cross",
	};

	private static void Write(string set, MarkerElement element, int depth, List<string> lines)
	{
		var fields = Fields(element.Attributes).ToList();
		lines.Add(fields.Count == 0
			? $"element\t{set}\t{depth}\t{element.Tag}"
			: $"element\t{set}\t{depth}\t{element.Tag}\t{string.Join('\t', fields)}");
		foreach (var child in element.Children)
		{
			Write(set, child, depth + 1, lines);
		}
	}

	// The attributes, then the styles, as curves.mjs writes them, with each theme variable as its name in braces.
	private static IEnumerable<string> Fields(IEnumerable<MarkerAttribute> attributes) =>
		attributes.Where(a => !a.IsStyle).Select(a => $"{a.Name}={Value(a)}")
			.Concat(attributes.Where(a => a.IsStyle).Select(a => $"style:{a.Name}={Value(a)}"));

	private static string Value(MarkerAttribute attribute) => attribute.Theme switch
	{
		MarkerThemeValue.StrokeWidth => "{strokeWidth}",
		MarkerThemeValue.MainBackground => "{mainBkg}",
		MarkerThemeValue.TransitionColor => "{transitionColor}",
		_ => attribute.Value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\t", "\\t", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal),
	};

	private static string Url(string? id) => id is null ? "" : $"url(#{id})";

	private static string Diff(IReadOnlyList<string> expected, IReadOnlyList<string> actual)
	{
		var report = new StringBuilder();
		for (var i = 0; i < Math.Max(expected.Count, actual.Count) && report.Length < 4000; i++)
		{
			var e = i < expected.Count ? expected[i] : "(none)";
			var a = i < actual.Count ? actual[i] : "(none)";
			if (e != a)
			{
				report.Append($"line {i}:\n  expected {e}\n  got      {a}\n");
			}
		}

		return report.ToString();
	}
}
