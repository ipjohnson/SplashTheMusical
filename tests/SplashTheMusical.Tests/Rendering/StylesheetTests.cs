using System.Buffers;
using System.Text;
using AwesomeAssertions;
using CsCheck;
using SplashTheMusical.Config;
using SplashTheMusical.Rendering;
using SplashTheMusical.Themes;

namespace SplashTheMusical.Tests.Rendering;

public class StylesheetTests
{
	private const string Unsafe = "abcdefilnorstuw#:;{}()[]<>&!@/\\*\"' -.,%=`\n0123456789";

	private static MermaidConfig Config(params (string Key, string Value)[] overrides)
	{
		var entries = new OrderedDictionary<string, MermaidValue>(StringComparer.Ordinal);
		foreach (var (key, value) in overrides)
		{
			entries[key] = MermaidValue.FromString(value);
		}

		return new MermaidConfig { ThemeVariables = Theme.Variables("default", MermaidValue.FromEntries(entries)) };
	}

	private static string Css(MermaidConfig config, params StyleClass[] classes) =>
		SvgWriter.StyleText("svg-1", MermaidStylesheet.Build(config, [], classes));

	[Test]
	public void AClassDefValueThatClosesTheRuleOrLoadsAUrlIsLeftOut()
	{
		var css = Css(
			Config(),
			new StyleClass("evil", ["fill:red;} svg{background:url(https://x.test/a)", "stroke:url(#a)", "color:blue }", "stroke-width:2px"], ["color:red;}"]));

		css.Should().NotContain("url(").And.NotContain("x.test").And.NotContain("background").And.NotContain("red");
		css.Should().EndWith("#svg-1 .evil>*{stroke-width:2px!important;}#svg-1 .evil span{stroke-width:2px!important;}");
	}

	[Test]
	public void AClassDefWhoseIdIsNotAnIdentifierIsLeftOut()
	{
		var css = Css(Config(), new StyleClass("a{}b", ["fill:#f9f"], []), new StyleClass("1st", ["fill:#f9f"], []));

		css.Should().NotContain("rgb(255, 153, 255)");
	}

	[Test]
	public void AThemeVariableThatClosesTheRuleOrLoadsAUrlIsLeftOut()
	{
		var css = Css(Config(("lineColor", "red;} svg{fill:url(https://x.test/a)"), ("textColor", "url(https://x.test/b)"), ("errorBkgColor", "#123")));

		css.Should().NotContain("url(").And.NotContain("x.test");
		css.Should().NotContain(".marker").And.Contain("#svg-1 .error-icon{fill:#123;}");
		css.Should().StartWith("#svg-1{font-family:\"trebuchet ms\",verdana,arial,sans-serif;font-size:16px;}");
	}

	[Test]
	public void TheOnlyUrlIsAReferenceToTheSvgsOwnFilter()
	{
		var css = SvgWriter.StyleText("svg-1", MermaidStylesheet.Build(new MermaidConfig { ThemeVariables = Theme.Variables("redux", null) }, [], []));

		css.Should().Contain("filter:url(#svg-1-drop-shadow);");
		css.Replace("url(#svg-1-drop-shadow)", string.Empty, StringComparison.Ordinal).Should().NotContain("url(");
	}

	[Test]
	[Arguments("fill:#f9f", "fill:rgb(255, 153, 255)")]
	[Arguments("stroke:#0f08", "stroke:rgba(0, 255, 0, 0.533)")]
	[Arguments("FILL:Red", "fill:red")]
	[Arguments("stroke-dasharray: 5 5", "stroke-dasharray:5,5")]
	[Arguments("opacity:50%", "opacity:0.5")]
	[Arguments("font-family:Times New Roman, Sans-Serif", "font-family:\"Times New Roman\",sans-serif")]
	[Arguments("font-weight:Bold", "font-weight:bold")]
	[Arguments("stroke-width:2.50px", "stroke-width:2.5px")]
	public void AClassDefValueIsWrittenAsChromiumSerializesIt(string source, string css) =>
		Text(CssDeclaration.FromSource(source, important: false)).Should().Be(css);

	[Test]
	[Arguments("fill:#12345")]
	[Arguments("stroke-width:-1px")]
	[Arguments("font-size:12")]
	[Arguments("radius:10")]
	[Arguments("filter:url(#a)")]
	[Arguments("fill:red !important")]
	[Arguments("fill:expression(alert(1))")]
	[Arguments("font-family:\"a\\\"b\"")]
	[Arguments("animation:dash 1s")]
	public void AClassDefValueThePropertyDoesNotTakeIsRefused(string source) =>
		CssDeclaration.FromSource(source, important: false).Should().BeNull();

	[Test]
	public void NoSourceOrThemeValueCanEndTheDeclaration() =>
		Gen.String[Gen.Char[Unsafe], 0, 40].Sample(value =>
		{
			foreach (var declaration in new[] { CssDeclaration.FromSource("fill:" + value, true), CssDeclaration.FromSource("font-family:" + value, true), CssDeclaration.FromTheme("fill", value) })
			{
				if (declaration is not null)
				{
					var text = Text(declaration);
					text.IndexOfAny([';', '{', '}', '<', '&', '\\', '!', '@']).Should().Be(-1, text);
					text.Should().NotContain("url(").And.NotContain("/*");
				}
			}
		});

	[Test]
	public void TheWriterEscapesTheStyleElementsText()
	{
		var buffer = new ArrayBufferWriter<byte>();
		new SvgWriter(buffer).WriteStyle("s", [new CssStyleRule(CssSelector.Parse(".a > *"), CssDeclaration.FromTheme("fill", "red"))]);

		Encoding.UTF8.GetString(buffer.WrittenSpan).Should().Be("<style>#s .a&gt;*{fill:red;}</style>");
	}

	[Test]
	[Arguments("a{}")]
	[Arguments(".a, .b")]
	[Arguments("@import")]
	[Arguments("[x=y]")]
	[Arguments(".a</style>")]
	public void ASelectorOutsideTheGrammarIsRefused(string selector) =>
		FluentActions.Invoking(() => CssSelector.Parse(selector)).Should().Throw<ArgumentException>();

	private static string Text(CssDeclaration? declaration) =>
		declaration is null ? "(refused)" : declaration.Property + ":" + declaration.Value;
}
