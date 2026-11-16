using AwesomeAssertions;
using SplashTheMusical.Config;
using SplashTheMusical.Parsing;

namespace SplashTheMusical.Tests.Config;

public class MermaidConfigResolveTests
{
	[Test]
	public void EachLevelOverridesTheOneBefore()
	{
		const string frontMatter = "---\nconfig:\n  pie:\n    textPosition: 0.5\n---\n";
		const string directive = "%%{init: {\"pie\": {\"textPosition\": 0.4}}}%%\n";
		var site = Site("{\"pie\": {\"textPosition\": 0.6, \"donutHole\": 0.2}}");

		Resolve(null, "pie\n").Pie.TextPosition.Should().Be(0.75);
		Resolve(site, "pie\n").Pie.TextPosition.Should().Be(0.6);
		Resolve(site, frontMatter + "pie\n").Pie.TextPosition.Should().Be(0.5);

		var all = Resolve(site, frontMatter + directive + "pie\n");
		all.Pie.TextPosition.Should().Be(0.4);
		all.Pie.DonutHole.Should().Be(0.2);
		all.Pie.LegendPosition.Should().Be("right");
	}

	[Test]
	public void ADiagramCannotChangeALockedKey()
	{
		var config = Resolve(null, "---\nconfig:\n  maxTextSize: 10\n---\n%%{init: {\"securityLevel\": \"loose\", \"secure\": [], \"theme\": \"dark\"}}%%\npie\n");

		config.SecurityLevel.Should().Be("strict");
		config.MaxTextSize.Should().Be(50000);
		config.Secure.Should().Equal("secure", "securityLevel", "startOnLoad", "maxTextSize", "suppressErrorRendering", "maxEdges");
		config.Theme.Should().Be("dark");
	}

	[Test]
	public void TheSiteConfigAddsLockedKeysAtEveryDepth()
	{
		var site = Site("{\"secure\": [\"fontFamily\", \"messageFontSize\"], \"fontFamily\": \"Arial\"}");
		var config = Resolve(site, "%%{init: {\"fontFamily\": \"Courier\", \"sequence\": {\"messageFontSize\": 20, \"noteFontSize\": 20}}}%%\nsequenceDiagram\n", "sequence");

		config.FontFamily.Should().Be("Arial");
		config.Sequence.MessageFontSize.Should().Be(MermaidValue.FromNumber(16));
		config.Sequence.NoteFontSize.Should().Be(MermaidValue.FromNumber(20));
		config.Secure.Should().Contain(["maxTextSize", "fontFamily", "messageFontSize"]);
	}

	[Test]
	public void ADiagramTypesOwnThemeAppliesUnlessTheSiteOrTheDiagramSetsOne()
	{
		Resolve(null, "flowchart LR\n", "flowchart-v2").Theme.Should().Be("redux-color");
		Resolve(Site("{\"theme\": \"default\"}"), "flowchart LR\n", "flowchart-v2").Theme.Should().Be("default");
		Resolve(Site("{\"theme\": \"default\"}"), "%%{init: {\"theme\": \"forest\"}}%%\nflowchart LR\n", "flowchart-v2").Theme.Should().Be("forest");
		Resolve(Site("{\"theme\": \"no-such-theme\"}"), "pie\n").Theme.Should().Be("default");
	}

	[Test]
	public void ADirectiveDropsKeysThatAreNotConfigKeysAndUnsafeValues()
	{
		var config = Resolve(null, "%%{init: {\"noSuchKey\": 1, \"themeCSS\": \"} .a {\", \"fontFamily\": \"<b>\", \"flowchart\": {\"curve\": \"linear\", \"__proto__\": {}}}}%%\nflowchart LR\n", "flowchart-v2");

		config.ThemeCSS.Should().Be("{ /* ERROR: Unbalanced CSS */ }");
		config.FontFamily.Should().Be(new MermaidConfig().FontFamily);
		config.Flowchart.Curve.Should().Be("linear");
	}

	[Test]
	public void ANumberKeyReadsANumericString()
	{
		var config = Resolve(null, "%%{init: {\"flowchart\": {\"rankSpacing\": \"100\", \"nodeSpacing\": \"wide\"}}}%%\nflowchart LR\n", "flowchart-v2");

		config.Flowchart.RankSpacing.Should().Be(100);
		config.Flowchart.NodeSpacing.Should().BeNull();
	}

	private static MermaidConfig Resolve(MermaidValue? site, string source, string diagramType = "pie")
	{
		var document = new TestDocument();
		PreambleParser.TryParse(source, document, [], ResourceLimits.Default, out _).Should().BeTrue();
		return MermaidConfig.Resolve(site, document, diagramType);
	}

	private static MermaidValue Site(string json)
	{
		var document = new TestDocument();
		PreambleParser.TryParse($"%%{{init: {json}}}%%\npie\n", document, [], ResourceLimits.Default, out _).Should().BeTrue();
		return document.Directives[0].Arguments!;
	}

	private sealed class TestDocument : MermaidDocument
	{
		public override string DiagramType => "";
	}
}
