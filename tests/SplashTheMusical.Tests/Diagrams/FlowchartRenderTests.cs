using AwesomeAssertions;
using SplashTheMusical.Config;
using SplashTheMusical.Diagrams;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Icons;
using SplashTheMusical.Tests.Rendering;
using SplashTheMusical.Text;

namespace SplashTheMusical.Tests.Diagrams;

public class FlowchartRenderTests
{
	private const string Source = """
		flowchart LR
		  A[Start] -->|go| B("`a *Markdown* label`")
		  subgraph S [Group]
		    B --> C{Choice}
		  end
		  C --> A
		""";

	[Test]
	public void TheLayoutHasABoxForEveryNodeSubgraphEdgeAndEdgeLabel()
	{
		var layout = Layout(Source);

		layout.Boxes().Select(b => b.Kind + " " + b.Name).Order(StringComparer.Ordinal).Should().Equal(
			"edge A --> B",
			"edge B --> C",
			"edge C --> A",
			"edge-label A --> B",
			"group S",
			"node A",
			"node B",
			"node C");
	}

	[Test]
	public void ASubgraphsFrameHoldsItsNodes()
	{
		var boxes = Layout(Source).Boxes().ToDictionary(b => b.Kind + " " + b.Name);
		var group = boxes["group S"];

		foreach (var name in new[] { "node B", "node C" })
		{
			var node = boxes[name];
			node.X.Should().BeGreaterThanOrEqualTo(group.X);
			node.Y.Should().BeGreaterThanOrEqualTo(group.Y);
			(node.X + node.Width).Should().BeLessThanOrEqualTo(group.X + group.Width);
			(node.Y + node.Height).Should().BeLessThanOrEqualTo(group.Y + group.Height);
		}

		var outside = boxes["node A"];
		(outside.X + outside.Width <= group.X || outside.X >= group.X + group.Width).Should().BeTrue();
	}

	[Test]
	public void TheViewBoxPadsTheDrawingByTheDiagramPadding()
	{
		var layout = Layout(Source);
		var boxes = layout.Boxes();
		var viewBox = layout.ViewBox!.Value;

		viewBox.X.Should().BeApproximately(boxes.Min(b => b.X) - 8, 1e-9);
		viewBox.Y.Should().BeApproximately(boxes.Min(b => b.Y) - 8, 1e-9);
		layout.Width.Should().Be(viewBox.Width);
	}

	[Test]
	public void TheSvgDrawsTheLabelsAndPassesTheSanitizer()
	{
		var svg = SvgDocuments.Checked(Mermaid.Default.RenderSvg(Source));

		svg.Should().Contain("aria-roledescription=\"flowchart-v2\"");
		foreach (var text in new[] { "Start", "Markdown", "Choice", "Group", "go" })
		{
			svg.Should().Contain(">" + text + "<");
		}
	}

	private static FlowchartLayout Layout(string source)
	{
		var document = (FlowchartDocument)Mermaid.Default.Parse(source);
		var config = MermaidConfig.Resolve(null, document, document.DiagramType);
		var context = new DiagramRenderContext(config, "mermaid", TextMeasurer.Default, NoIcons.Instance, [], ResourceLimits.Unlimited);
		return new FlowchartModule().Layout(document, context);
	}
}
