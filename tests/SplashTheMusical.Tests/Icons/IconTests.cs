using System.Text;
using AwesomeAssertions;
using SplashTheMusical.Icons;
using SplashTheMusical.Tests.Rendering;

namespace SplashTheMusical.Tests.Icons;

public class IconTests
{
	private const string Pack = """
		{
		  "prefix": "test",
		  "info": { "name": "Test icons" },
		  "width": 24,
		  "height": 24,
		  "icons": {
		    "star": { "body": "<path d=\"M12 2l3 7h7l-6 4 2 7-6-4-6 4 2-7-6-4h7z\" fill=\"currentColor\"/>" },
		    "wide": { "body": "<rect width=\"32\" height=\"16\"/>", "width": 32, "height": 16, "left": 1 }
		  },
		  "aliases": {
		    "star-mirrored": { "parent": "star", "hFlip": true },
		    "star-again": { "parent": "star-mirrored" },
		    "loop": { "parent": "loop" },
		    "orphan": { "parent": "missing" }
		  }
		}
		""";

	[Test]
	public void AnIconifyPackIsReadWithItsDefaultsAndAliases()
	{
		var pack = IconPack.FromIconifyJson(Pack);

		pack.Prefix.Should().Be("test");
		pack.Names.Order(StringComparer.Ordinal).Should().Equal("star", "star-again", "star-mirrored", "wide");
		var star = pack.GetIcon("star")!;
		(star.Width, star.Height, star.Left).Should().Be((24, 24, 0));
		star.Body.Should().Be("""<path d="M12 2l3 7h7l-6 4 2 7-6-4-6 4 2-7-6-4h7z" fill="currentColor"/>""");
		star.Removed.Should().BeEmpty();
		var wide = pack.GetIcon("wide")!;
		(wide.Width, wide.Height, wide.Left).Should().Be((32, 16, 1));
		pack.GetIcon("star-again")!.HorizontalFlip.Should().BeTrue();
		pack.GetIcon("star-again")!.Body.Should().Be(star.Body);
	}

	[Test]
	public void ARegisteredIconifyIconIsDrawnAsInlineSvgColouredByTheTheme()
	{
		var mermaid = new Mermaid(icons: IconRegistry.Default.With(IconPack.FromIconifyJson(Encoding.UTF8.GetBytes(Pack))));
		var diagnostics = new List<MermaidDiagnostic>();

		var svg = SvgDocuments.Checked(mermaid.RenderSvg("""
			flowchart TD
			  A@{ icon: "test:star", label: "Star" }
			""", new RenderOptions { Diagnostics = diagnostics }));

		diagnostics.Should().BeEmpty();
		svg.Should().Contain("""<svg width="48" height="48" viewBox="0 0 24 24"><path d="M12 2l3 7h7l-6 4 2 7-6-4-6 4 2-7-6-4h7z" fill="currentColor"/></svg>""");
		svg.Should().Contain("""<g class="icon" transform="translate(-24, -""");
		svg.Should().Contain("""color="#28253D"><svg width="48" """);
	}

	[Test]
	public void AFlippedAliasIsDrawnInAMirroringGroup()
	{
		var mermaid = new Mermaid(icons: new IconRegistry([IconPack.FromIconifyJson(Pack)]));

		var svg = SvgDocuments.Checked(mermaid.RenderSvg("flowchart TD\n  A@{ icon: \"test:star-mirrored\" }"));

		svg.Should().Contain("""viewBox="0 0 24 24"><g transform="translate(24 0) scale(-1 1)"><path""");
	}

	[Test]
	public void AScriptInAnIconIsStrippedWhenTheIconIsRegistered()
	{
		const string json = """
			{ "prefix": "bad", "icons": { "x": { "body": "<script>alert(1)</script><path d=\"M0 0h4v4z\" onclick=\"alert(2)\" style=\"fill: red; background: url(https://example.com/a.png)\"/><foreignObject><div>hi</div></foreignObject><a href=\"javascript:alert(3)\"><rect width=\"2\" height=\"2\"/></a>" } } }
			""";

		var icon = IconPack.FromIconifyJson(json).GetIcon("x")!;

		icon.Body.Should().Be("""<path d="M0 0h4v4z" fill="red"/>""");
		icon.Body.Should().NotContain("script").And.NotContain("alert").And.NotContain("url(");
		icon.Removed.Should().Contain(new SvgViolation("element", "script"));
		icon.Removed.Should().Contain(new SvgViolation("attribute", "onclick", "path"));
		icon.Removed.Should().Contain(new SvgViolation("element", "foreignObject"));

		var svg = SvgDocuments.Checked(new Mermaid(icons: new IconRegistry([IconPack.FromIconifyJson(json)])).RenderSvg("flowchart TD\n  A@{ icon: \"bad:x\" }"));
		svg.Should().NotContain("<script").And.NotContain("alert").And.Contain("""<path d="M0 0h4v4z" fill="red"/>""");
	}

	[Test]
	public void AnIconMadeDirectlyIsSanitizedToo()
	{
		var icon = new MermaidIcon("<g><script>alert(1)</script><circle r=\"3\" fill=\"currentColor\"/></g>", 8, 8);

		icon.Body.Should().Be("""<g><circle r="3" fill="currentColor"/></g>""");
		icon.Removed.Should().Equal(new SvgViolation("element", "script"));
	}

	[Test]
	public void AMissingIconDrawsMermaidsUnknownIconAndGivesAWarning()
	{
		var diagnostics = new List<MermaidDiagnostic>();

		var svg = SvgDocuments.Checked(Mermaid.Default.RenderSvg("flowchart TD\n  A@{ icon: \"fa:user\", label: \"User\" }", new RenderOptions { Diagnostics = diagnostics }));

		diagnostics.Should().ContainSingle().Which.Should().Be(new MermaidDiagnostic(
			DiagnosticSeverity.Warning,
			"The icon 'fa:user' of node 'A' is in no registered icon pack, so it is drawn as mermaid's unknown icon.",
			0,
			0));
		svg.Should().Contain("""viewBox="0 0 80 80"><g><rect width="80" height="80" fill="#087ebf" stroke-width="0px"/><text transform="translate(21.16 64.67)" fill="#fff" font-family="ArialMT, Arial" font-size="67.75px"><tspan x="0" y="0">?</tspan></text></g></svg>""");
	}

	[Test]
	public void TheBuiltInArchitectureIconsAreRegisteredByDefault()
	{
		IconRegistry.Default.Prefixes.Should().Equal("mermaid-architecture");
		var database = IconRegistry.Default.GetIcon("mermaid-architecture:database")!;

		database.Removed.Should().BeEmpty();
		database.Body.Should().StartWith("""<g><rect width="80" height="80" fill="#087ebf" stroke-width="0px"/><path id="b" data-name="4" d="m20,57.86c0,3.94,8.95,7.14,20,7.14s20-3.2,20-7.14" fill="none" stroke="#fff" stroke-miterlimit="10" stroke-width="2px"/>""");
	}

	[Test]
	public void IconIdsArePrefixedPerDrawnIcon()
	{
		var pack = new IconPack("g", [new("grad", new MermaidIcon("""<linearGradient id="a"><stop offset="0" stop-color="red"/></linearGradient><rect width="4" height="4" fill="url(#a)"/>""", 4, 4))]);

		var svg = SvgDocuments.Checked(new Mermaid(icons: new IconRegistry([pack])).RenderSvg("flowchart TD\n  A@{ icon: \"g:grad\" }\n  B@{ icon: \"g:grad\" }"));

		svg.Should().Contain("""<linearGradient id="mermaid-icon0-a">""").And.Contain("""fill="url(#mermaid-icon0-a)""");
		svg.Should().Contain("""<linearGradient id="mermaid-icon1-a">""").And.Contain("""fill="url(#mermaid-icon1-a)""");
	}

	[Test]
	[Arguments("g:x", "g", "x")]
	[Arguments("@local:g:x", "g", "x")]
	[Arguments("g-x-y", "g", "x-y")]
	[Arguments("x", null, null)]
	[Arguments("a:b:c:d", null, null)]
	[Arguments(":x", null, null)]
	public void NamesAreSplitAsIconifyDoes(string name, string? prefix, string? icon)
	{
		var split = IconRegistry.Split(name);

		(split?.Prefix, split?.Name).Should().Be((prefix, icon));
	}

	[Test]
	[Arguments("not json")]
	[Arguments("[]")]
	[Arguments("""{ "icons": {} }""")]
	public void AMalformedPackIsAFormatException(string json)
	{
		var read = () => IconPack.FromIconifyJson(json);

		read.Should().Throw<FormatException>();
	}

	[Test]
	public void APackWithoutAPrefixTakesTheRegisteredName()
	{
		IconPack.FromIconifyJson("""{ "icons": { "a": { "body": "<path d=\"M0 0\"/>" } } }""", "named").Prefix.Should().Be("named");
	}
}
