using AwesomeAssertions;
using SplashTheMusical.Tests.Rendering;

namespace SplashTheMusical.Tests.Icons;

public class ImageNodeTests
{
	private const string Remote = """
		flowchart TD
		  A --> C@{ img: "https://example.com/logo.svg", label: "Logo", w: 60, h: 60 }
		""";

	// A 1 by 1 PNG.
	private const string Png = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

	[Test]
	public void ARemoteImageIsNotDrawnByDefaultAndGivesAWarning()
	{
		var diagnostics = new List<MermaidDiagnostic>();

		var svg = SvgDocuments.Checked(Mermaid.Default.RenderSvg(Remote, new RenderOptions { Diagnostics = diagnostics }));

		svg.Should().NotContain("<image").And.NotContain("example.com");
		diagnostics.Should().ContainSingle().Which.Should().Be(new MermaidDiagnostic(
			DiagnosticSeverity.Warning,
			"The image 'https://example.com/logo.svg' of node 'C' is not drawn, because remote images are off. Set AllowRemoteImages to draw it; STM does not fetch it.",
			0,
			0));
	}

	[Test]
	public void ARemoteImageIsDrawnWhenTheHostAllowsIt()
	{
		var diagnostics = new List<MermaidDiagnostic>();
		var mermaid = new Mermaid(new MermaidOptions { AllowRemoteImages = true });

		var svg = mermaid.RenderSvg(Remote, new RenderOptions { Diagnostics = diagnostics });

		diagnostics.Should().BeEmpty();
		svg.Should().Contain("""<image href="https://example.com/logo.svg" width="120" height="60" preserveAspectRatio="none" transform="translate(-60, -46.5)"/>""");
		Mermaid.Default.RenderSvg(Remote, new RenderOptions { AllowRemoteImages = true }).Should().Contain("<image href=\"https://example.com/logo.svg\"");
	}

	[Test]
	public void ADataUriImageIsEmbedded()
	{
		var diagnostics = new List<MermaidDiagnostic>();

		var svg = SvgDocuments.Checked(Mermaid.Default.RenderSvg(
			"flowchart TD\n  P@{ img: \"" + Png + "\", w: 40, h: 40 }",
			new RenderOptions { Diagnostics = diagnostics }));

		diagnostics.Should().BeEmpty();
		svg.Should().Contain("<image href=\"" + Png + "\" width=\"40\" height=\"40\" preserveAspectRatio=\"none\" transform=\"translate(-20, -20)\"/>");
	}

	[Test]
	[Arguments("javascript:alert(1)")]
	[Arguments("data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==")]
	[Arguments("file:///etc/passwd")]
	public void AnImageThatIsNeitherADataImageNorAnHttpUrlIsNeverDrawn(string url)
	{
		var diagnostics = new List<MermaidDiagnostic>();

		var svg = new Mermaid(new MermaidOptions { AllowRemoteImages = true }).RenderSvg(
			"flowchart TD\n  P@{ img: \"" + url + "\" }",
			new RenderOptions { Diagnostics = diagnostics });

		svg.Should().NotContain("<image");
		diagnostics.Should().ContainSingle().Which.Message.Should().Contain("neither a base64 data: PNG or SVG image nor an http or https URL");
	}
}
