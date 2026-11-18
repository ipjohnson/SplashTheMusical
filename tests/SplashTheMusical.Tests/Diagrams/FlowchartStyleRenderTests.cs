using AwesomeAssertions;
using SplashTheMusical.Tests.Rendering;

namespace SplashTheMusical.Tests.Diagrams;

public class FlowchartStyleRenderTests
{
	private const string Body = """
		flowchart LR
		  classDef hex fill:#f9f,stroke:#333,color:#00f
		  subgraph S [Group]
		    A:::hex --> B
		  end
		  B -->|go| C
		  style B fill:#0f0
		  style S stroke:#f00
		  linkStyle 1 stroke:#f00,color:#00f
		""";

	[Test]
	[Arguments("base")]
	[Arguments("dark")]
	[Arguments("default")]
	[Arguments("forest")]
	[Arguments("neo")]
	[Arguments("neo-dark")]
	[Arguments("neutral")]
	[Arguments("redux")]
	[Arguments("redux-color")]
	[Arguments("redux-dark")]
	[Arguments("redux-dark-color")]
	public void EveryThemeDrawsAStyledFlowchart(string theme)
	{
		var svg = SvgDocuments.Checked(Mermaid.Default.RenderSvg($"---\nconfig:\n  theme: {theme}\n---\n{Body}", new RenderOptions { SvgId = "s" }));

		svg.Should().Contain("<g class=\"node default hex stm-style-");
		svg.Should().Contain("<g class=\"cluster stm-style-");
		svg.Should().Contain("#s .node rect,#s .node circle,#s .node ellipse,#s .node polygon,#s .node path{fill:");
		svg.Should().Contain("#s .hex&gt;*{fill:rgb(255, 153, 255)!important;stroke:rgb(51, 51, 51)!important;color:rgb(0, 0, 255)!important;}");
		svg.Should().Contain("&gt;:not(.label),#s .stm-style-");
		svg.Should().Contain("{fill:rgb(0, 255, 0)!important;");
		svg.Should().Contain("&gt;rect{stroke:rgb(255, 0, 0)!important;}");
		svg.Should().Contain("marker-end=\"url(#s_flowchart-v2-pointEnd-margin__f00)\"");
		svg.Should().Contain("<marker id=\"s_flowchart-v2-pointEnd-margin__f00\"");
		svg.Should().Contain(" text{fill:rgb(0, 0, 255);}");
	}

	[Test]
	public void AColourThemeGivesEachSubgraphAColourSlot()
	{
		var svg = Mermaid.Default.RenderSvg(Body, new RenderOptions { SvgId = "s" });

		svg.Should().Contain("data-color-id=\"color-0\"");
		svg.Should().Contain("#s [data-look=\"neo\"][data-color-id=\"color-0\"].cluster:not(.swimlane) rect{stroke:#E879F9;fill:#FDF4FF;}");
	}

	[Test]
	public void AGradientThemeDefinesTheGradientAndShadowsItsRulesReferTo()
	{
		var svg = Mermaid.Default.RenderSvg("---\nconfig:\n  theme: forest\n---\n" + Body, new RenderOptions { SvgId = "s" });

		svg.Should().Contain("stroke:url(#s-gradient)");
		svg.Should().Contain("<linearGradient id=\"s-gradient\"");
		svg.Should().Contain("<filter id=\"s-drop-shadow\" height=\"130%\" width=\"130%\"><feDropShadow dx=\"4\" dy=\"4\" stdDeviation=\"0\" flood-opacity=\"0.06\" flood-color=\"#000000\"/>");
		svg.Should().Contain("<filter id=\"s-drop-shadow-small\"");
	}

	[Test]
	public void AThemeWithoutAGradientDefinesNone()
	{
		var svg = Mermaid.Default.RenderSvg("---\nconfig:\n  theme: redux-dark\n---\n" + Body, new RenderOptions { SvgId = "s" });

		svg.Should().NotContain("linearGradient");
		svg.Should().Contain("flood-color=\"#FFFFFF\"");
	}

	[Test]
	public void AClassicThemeGivesNoColourSlot()
	{
		var svg = Mermaid.Default.RenderSvg("---\nconfig:\n  theme: default\n---\n" + Body, new RenderOptions { SvgId = "s" });

		svg.Should().NotContain("data-color-id");
		svg.Should().NotContain("[data-color-id=");
	}
}
