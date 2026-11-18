using AwesomeAssertions;
using CsCheck;
using SplashTheMusical.Diagrams.Flowchart;

namespace SplashTheMusical.Tests.Generation;

/// <summary>The coverage of <see cref="FlowchartGenerator"/>: every node shape, every edge type and stroke and every label type.</summary>
public class FlowchartGeneratorTests
{
	// 5,000 documents hold each shape about a hundred times, so a random sample covers every one.
	private static readonly Lazy<List<FlowchartDocument>> Sample = new(() =>
	{
		var documents = new List<FlowchartDocument>();
		FlowchartGenerator.Document.Sample(d => { lock (documents) { documents.Add(d); } }, iter: 5_000);
		return documents;
	});

	[Test]
	public void TheShapesAreEveryShapeTheParserAccepts()
	{
		// The keys of mermaid.js's shapes map a @{ shape } block accepts: lowercase and without _.
		var keys = File.ReadLines(Path.Combine(Repository(), "corpus", "shapes.txt"))
			.Select(line => line.Split('\t'))
			.Where(fields => fields is ["key", _, _])
			.Select(fields => fields[1])
			.Where(name => name == name.ToLowerInvariant() && !name.Contains('_'));

		FlowchartGenerator.BlockShapes.Should().BeEquivalentTo(keys);
		foreach (var shape in FlowchartGenerator.Shapes)
		{
			var source = FlowchartGenerator.BracketShapes.Contains(shape)
				? Mermaid.Default.Write(new FlowchartDocument { Nodes = { new FlowchartNode("A") { Shape = shape } } })
				: $"flowchart TB\n    A@{{ shape: {shape} }}\n";
			Mermaid.Default.TryParse(source, out var document, out var diagnostics).Should().BeTrue($"{shape}: {string.Join("; ", diagnostics)}");
			((FlowchartDocument)document!).Nodes[0].Shape.Should().Be(shape);
		}
	}

	[Test]
	public void TheDocumentsUseEveryNodeShape() =>
		Sample.Value.SelectMany(d => d.Nodes).Select(n => n.Shape).OfType<string>().Distinct()
			.Should().BeEquivalentTo(FlowchartGenerator.Shapes);

	[Test]
	public void TheDocumentsUseEveryEdgeTypeAndStroke()
	{
		var edges = Sample.Value.SelectMany(d => d.Edges).ToList();
		edges.Select(e => e.Type).Distinct().Should().BeEquivalentTo(Enum.GetValues<FlowchartEdgeType>());
		edges.Select(e => e.Stroke).Distinct().Should().BeEquivalentTo(Enum.GetValues<FlowchartStroke>());
		edges.Select(e => (e.Type, e.Stroke)).Distinct().Should().BeEquivalentTo(FlowchartGenerator.EdgeKinds);
		edges.Select(e => e.Length).Distinct().Should().Contain([1, 10, null]);
		edges.Select(e => e.IsUserDefinedId).Distinct().Should().HaveCount(2);
	}

	[Test]
	public void TheDocumentsUseEveryLabelTypeAndHardText()
	{
		var nodes = Sample.Value.SelectMany(d => d.Nodes).ToList();
		var edges = Sample.Value.SelectMany(d => d.Edges).ToList();
		var subgraphs = Sample.Value.SelectMany(d => d.Subgraphs).ToList();
		nodes.Select(n => n.LabelType).Distinct().Should().BeEquivalentTo(Enum.GetValues<FlowchartLabelType>());
		edges.Where(e => e.Label.Length > 0).Select(e => e.LabelType).Distinct().Should().BeEquivalentTo(Enum.GetValues<FlowchartLabelType>());
		subgraphs.Where(s => s.Title != s.Id).Select(s => s.LabelType).Distinct().Should().BeEquivalentTo(Enum.GetValues<FlowchartLabelType>());

		var labels = nodes.Select(n => n.Label).Concat(edges.Select(e => e.Label)).ToList();
		foreach (var text in new[] { "\"", "'", "[", "]", "(", ")", "{", "}", "#quot;", "#35;", "&amp;", "<br>", "\n", "日本語", "😀" })
		{
			labels.Should().Contain(label => label.Contains(text, StringComparison.Ordinal), $"a label holds {text}");
		}
	}

	[Test]
	public void TheDocumentsNestSubgraphsAndUseTheStatements()
	{
		var documents = Sample.Value;
		documents.Select(d => d.DiagramType).Distinct().Should().BeEquivalentTo(["flowchart-v2", "flowchart-elk"]);
		documents.Should().Contain(d => d.Subgraphs.Any(s => s.Nodes.Any(m => d.Subgraphs.Any(t => t.Id == m))), "a subgraph holds a subgraph");
		documents.Should().Contain(d => d.Nodes.Any(n => n.Styles.Count > 0 && n.Classes.Count > 0));
		documents.Should().Contain(d => d.Edges.Any(e => e.Styles != null && e.Interpolate != null && e.Classes.Count > 0 && e.Animate != null));
		documents.Should().Contain(d => d.Classes.Any(c => c.TextStyles.Count > 0));
		documents.Should().Contain(d => d.DefaultInterpolate != null && d.DefaultEdgeStyles != null);
		documents.Should().Contain(d => d.AccessibilityDescription != null && d.AccessibilityDescription.Contains('\n'));
	}

	private static string Repository()
	{
		for (var folder = AppContext.BaseDirectory; folder is not null; folder = Path.GetDirectoryName(folder))
		{
			if (File.Exists(Path.Combine(folder, "SplashTheMusical.slnx")))
			{
				return folder;
			}
		}

		throw new InvalidOperationException("SplashTheMusical.slnx not found above " + AppContext.BaseDirectory);
	}
}
