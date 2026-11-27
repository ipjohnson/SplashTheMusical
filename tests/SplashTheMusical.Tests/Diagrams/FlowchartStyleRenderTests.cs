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

		Verify(svg.Contains("<g class=\"node default hex stm-style-", StringComparison.Ordinal));
		Verify(svg.Contains("<g class=\"cluster stm-style-", StringComparison.Ordinal));
		Verify(svg.Contains("#s .node rect,#s .node circle,#s .node ellipse,#s .node polygon,#s .node path{fill:", StringComparison.Ordinal));
		Verify(svg.Contains("#s .hex&gt;*{fill:rgb(255, 153, 255)!important;stroke:rgb(51, 51, 51)!important;color:rgb(0, 0, 255)!important;}", StringComparison.Ordinal));
		Verify(svg.Contains("&gt;:not(.label),#s .stm-style-", StringComparison.Ordinal));
		Verify(svg.Contains("{fill:rgb(0, 255, 0)!important;", StringComparison.Ordinal));
		Verify(svg.Contains("&gt;rect{stroke:rgb(255, 0, 0)!important;}", StringComparison.Ordinal));
		Verify(svg.Contains("marker-end=\"url(#s_flowchart-v2-pointEnd-margin__f00)\"", StringComparison.Ordinal));
		Verify(svg.Contains("<marker id=\"s_flowchart-v2-pointEnd-margin__f00\"", StringComparison.Ordinal));
		Verify(svg.Contains(" text{fill:rgb(0, 0, 255);}", StringComparison.Ordinal));
	}

	[Test]
	public void AColourThemeGivesEachSubgraphAColourSlot()
	{
		var svg = Mermaid.Default.RenderSvg(Body, new RenderOptions { SvgId = "s" });

		Verify(svg.Contains("data-color-id=\"color-0\"", StringComparison.Ordinal));
		Verify(svg.Contains("#s [data-look=\"neo\"][data-color-id=\"color-0\"].cluster:not(.swimlane) rect{stroke:#E879F9;fill:#FDF4FF;}", StringComparison.Ordinal));
	}

	[Test]
	public void AGradientThemeDefinesTheGradientAndShadowsItsRulesReferTo()
	{
		var svg = Mermaid.Default.RenderSvg("---\nconfig:\n  theme: forest\n---\n" + Body, new RenderOptions { SvgId = "s" });

		Verify(svg.Contains("stroke:url(#s-gradient)", StringComparison.Ordinal));
		Verify(svg.Contains("<linearGradient id=\"s-gradient\"", StringComparison.Ordinal));
		Verify(svg.Contains("<filter id=\"s-drop-shadow\" height=\"130%\" width=\"130%\"><feDropShadow dx=\"4\" dy=\"4\" stdDeviation=\"0\" flood-opacity=\"0.06\" flood-color=\"#000000\"/>", StringComparison.Ordinal));
		Verify(svg.Contains("<filter id=\"s-drop-shadow-small\"", StringComparison.Ordinal));
	}

	[Test]
	public void AThemeWithoutAGradientDefinesNone()
	{
		var svg = Mermaid.Default.RenderSvg("---\nconfig:\n  theme: redux-dark\n---\n" + Body, new RenderOptions { SvgId = "s" });

		Verify(!svg.Contains("linearGradient", StringComparison.Ordinal));
		Verify(svg.Contains("flood-color=\"#FFFFFF\"", StringComparison.Ordinal));
	}

	[Test]
	public void AClassicThemeGivesNoColourSlot()
	{
		var svg = Mermaid.Default.RenderSvg("---\nconfig:\n  theme: default\n---\n" + Body, new RenderOptions { SvgId = "s" });

		Verify(!svg.Contains("data-color-id", StringComparison.Ordinal));
		Verify(!svg.Contains("[data-color-id=", StringComparison.Ordinal));
	}
}
