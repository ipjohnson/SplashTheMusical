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

		SequenceAssert.Equal(layout.Boxes().Select(b => b.Kind + " " + b.Name).Order(StringComparer.Ordinal), "edge A --> B", "edge B --> C", "edge C --> A", "edge-label A --> B", "group S", "node A", "node B", "node C");
	}

	[Test]
	public void ASubgraphsFrameHoldsItsNodes()
	{
		var boxes = Layout(Source).Boxes().ToDictionary(b => b.Kind + " " + b.Name);
		var group = boxes["group S"];

		foreach (var name in new[] { "node B", "node C" })
		{
			var node = boxes[name];
			Verify(node.X >= group.X);
			Verify(node.Y >= group.Y);
			Verify((node.X + node.Width) <= group.X + group.Width);
			Verify((node.Y + node.Height) <= group.Y + group.Height);
		}

		var outside = boxes["node A"];
		Verify((outside.X + outside.Width <= group.X || outside.X >= group.X + group.Width));
	}

	[Test]
	public void TheViewBoxPadsTheDrawingByTheDiagramPadding()
	{
		var layout = Layout(Source);
		var boxes = layout.Boxes();
		var viewBox = layout.ViewBox!.Value;

		Verify(Math.Abs(viewBox.X - (boxes.Min(b => b.X) - 8)) <= 1e-9);
		Verify(Math.Abs(viewBox.Y - (boxes.Min(b => b.Y) - 8)) <= 1e-9);
		Verify(Math.Abs(layout.Width - viewBox.Width) <= 1e-9);
	}

	[Test]
	public void TheSvgDrawsTheLabelsAndPassesTheSanitizer()
	{
		var svg = SvgDocuments.Checked(Mermaid.Default.RenderSvg(Source));

		Verify(svg.Contains("aria-roledescription=\"flowchart-v2\"", StringComparison.Ordinal));
		foreach (var text in new[] { "Start", "Markdown", "Choice", "Group", "go" })
		{
			Verify(svg.Contains(">" + text + "<", StringComparison.Ordinal));
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
