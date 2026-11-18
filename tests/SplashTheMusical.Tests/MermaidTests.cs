using System.Buffers;
using System.Text;
using AwesomeAssertions;
using SplashTheMusical.Diagrams;
using SplashTheMusical.Diagrams.Info;
using SplashTheMusical.Rendering;
using SplashTheMusical.Tests.Rendering;
using SplashTheMusical.Text;
using SplashTheMusical.Themes;

namespace SplashTheMusical.Tests;

public class MermaidTests
{
	[Test]
	public void TheDefaultInstanceRendersInfo()
	{
		var svg = Mermaid.Default.RenderSvg("info");

		SvgDocuments.Checked(svg).Should().StartWith("<svg xmlns=\"http://www.w3.org/2000/svg\" id=\"mermaid\" width=\"400\"");
		svg.Should().Contain("aria-roledescription=\"info\"");
		svg.Should().Contain("<style>#mermaid{font-family:");
		svg.Should().EndWith("<g/><g><text x=\"100\" y=\"40\" class=\"version\" font-size=\"32\" text-anchor=\"middle\">v12.3.0</text></g></svg>");
	}

	[Test]
	public void RenderingToABufferWritesTheSameUtf8()
	{
		var buffer = new ArrayBufferWriter<byte>();
		var document = Mermaid.Default.Parse("info");

		Mermaid.Default.RenderSvg(document, buffer);

		Encoding.UTF8.GetString(buffer.WrittenSpan).Should().Be(Mermaid.Default.RenderSvg(document));
	}

	[Test]
	public void AnAddedModuleAndAReplacedMeasurerAreUsed()
	{
		var measurer = new FixedMeasurer(123);
		var mermaid = new Mermaid(modules: [new BoxModule()], textMeasurer: measurer);

		var document = mermaid.Parse("%% a comment\nbox Hello");
		var svg = SvgDocuments.Checked(mermaid.RenderSvg(document));

		document.Should().BeOfType<BoxDocument>().Which.Label.Should().Be("Hello");
		svg.Should().Contain("<rect width=\"123\" height=\"20\"/>");
		svg.Should().Contain("aria-roledescription=\"box\"");
		measurer.Calls.Should().Be(1);
		mermaid.Write(document).Should().Be("box Hello\n");
		mermaid.RenderSvg("info").Should().Be(Mermaid.Default.RenderSvg("info"));
	}

	[Test]
	public void AnAddedModuleReplacesTheCoreModuleOfItsType()
	{
		var mermaid = new Mermaid(modules: [new BoxModule("info")]);

		mermaid.Parse("info").Should().BeOfType<BoxDocument>();
	}

	[Test]
	public void AReplacedThemeProviderComputesTheThemeVariables()
	{
		var themes = new CountingThemes();
		var mermaid = new Mermaid(themes: themes);

		mermaid.RenderSvg("%%{init: {\"theme\": \"forest\"}}%%\ninfo");

		themes.Names.Should().Contain("forest");
	}

	[Test]
	public async Task SixteenThreadsGetTheSameOutputFromOneInstance()
	{
		IMermaid mermaid = new Mermaid();
		string[] sources =
		[
			"info",
			"---\ntitle: T\nconfig:\n  theme: dark\n---\ninfo showInfo",
			"%%{init: {\"theme\": \"forest\", \"themeVariables\": {\"primaryColor\": \"#ff0000\"}}}%%\ninfo\naccTitle: A\naccDescr: B\n",
		];
		var expected = sources.Select(s => mermaid.RenderSvg(s)).ToArray();
		using var start = new Barrier(16);
		var threads = Enumerable.Range(0, 16)
			.Select(_ => Task.Factory.StartNew(
				() =>
				{
					start.SignalAndWait();
					var outputs = new List<string>();
					for (var i = 0; i < 20; i++)
					{
						outputs.AddRange(sources.Select(s => mermaid.RenderSvg(s)));
					}

					return outputs;
				},
				TaskCreationOptions.LongRunning))
			.ToArray();

		var results = await Task.WhenAll(threads);

		foreach (var outputs in results)
		{
			outputs.Should().Equal(Enumerable.Repeat(expected, 20).SelectMany(e => e));
		}
	}

	[Test]
	[Arguments("")]
	[Arguments("not a diagram")]
	[Arguments("info unsupported")]
	[Arguments("info: key: value")]
	[Arguments("info showInfo showInfo")]
	[Arguments("pie\n\"a\": 1")]
	[Arguments("---\ntitle: [unclosed\n---\ninfo")]
	[Arguments("---\nnot closed\ninfo")]
	[Arguments("%%{init: {\"theme\": \"dark\"\ninfo")]
	public void TryParseReturnsFalseWithAnErrorAndDoesNotThrow(string source)
	{
		var parsed = Mermaid.Default.TryParse(source, out var document, out var diagnostics);

		parsed.Should().BeFalse();
		document.Should().BeNull();
		diagnostics.Should().Contain(d => d.Severity == DiagnosticSeverity.Error);
	}

	[Test]
	public void TryParseReportsAnExceededLimitAsADiagnostic()
	{
		var options = new ParseOptions { Limits = ResourceLimits.Default with { MaxInputLength = 3 } };

		var parsed = Mermaid.Default.TryParse("info", out _, out var diagnostics, options);

		parsed.Should().BeFalse();
		diagnostics.Should().ContainSingle().Which.Message.Should().Contain("MaxInputLength");
	}

	[Test]
	public void ParseThrowsWithTheDiagnostics()
	{
		var parse = () => Mermaid.Default.Parse("pie\n\"a\": 1");

		parse.Should().Throw<MermaidParseException>().Which.Diagnostics.Should().ContainSingle()
			.Which.Message.Should().Be("STM does not support the pie diagram yet.");
	}

	[Test]
	public void RenderRefusesAThemeColourMermaidJsCannotRead()
	{
		const string Source = "%%{init: {\"theme\": \"base\", \"themeVariables\": {\"primaryColor\": \"411d4e\"}}}%%\nflowchart LR\n  A --> B";

		var render = () => Mermaid.Default.RenderSvg(Source);

		render.Should().Throw<MermaidParseException>().Which.Diagnostics.Should().ContainSingle()
			.Which.Message.Should().Be("Unsupported color format: \"411d4e\"");
	}

	[Test]
	public void RenderRefusesAFlowchartMermaidJssDagreCannotRank()
	{
		// mermaid.js 12.3.0 with layout: dagre throws in dagre's ranking, because the edge ends at subgraph B.
		const string Source = "flowchart\nb-->B\nsubgraph O\nA\nend\nsubgraph B\nc\nend\nsubgraph A\nb\nB\nend\n";

		var render = () => Mermaid.Default.RenderSvg(Source);

		render.Should().Throw<MermaidParseException>().Which.Diagnostics.Should().ContainSingle()
			.Which.Message.Should().Be("Cannot set properties of undefined (setting 'rank')");
	}

	[Test]
	public void RenderRefusesAFlowchartWhoseSubgraphHoldsItsOwnParent()
	{
		// TOP is a member of B2, which is a member of TOP, and graphlib's setParent refuses the cycle.
		const string Source = "flowchart\nsubgraph TOP\nsubgraph B2\nTOP-->f2\nend\nend\n";

		var render = () => Mermaid.Default.RenderSvg(Source);

		render.Should().Throw<MermaidParseException>().Which.Diagnostics.Should().ContainSingle()
			.Which.Message.Should().Be("Setting TOP as parent of B2 would create a cycle");
	}

	[Test]
	public void TheOptionsOfACallOverrideTheInstancesOptions()
	{
		var mermaid = new Mermaid(new MermaidOptions { SiteConfig = Site("theme", "dark") });

		var dark = mermaid.RenderSvg("info");
		var forest = mermaid.RenderSvg("info", new RenderOptions { SvgId = "chart", SiteConfig = Site("theme", "forest") });

		dark.Should().Contain("#mermaid{font-family:\"trebuchet ms\",verdana,arial,sans-serif;font-size:16px;fill:#ccc;}");
		forest.Should().Contain("id=\"chart\"").And.Contain("#chart{font-family:\"trebuchet ms\",verdana,arial,sans-serif;font-size:16px;fill:#000000;}");
	}

	[Test]
	public void ALockedKeyKeepsItsSiteValue()
	{
		var locked = new Mermaid(new MermaidOptions { LockedKeys = ["theme"] });
		const string Source = "%%{init: {\"theme\": \"dark\"}}%%\ninfo";

		locked.RenderSvg(Source).Should().Be(Mermaid.Default.RenderSvg("info"));
		Mermaid.Default.RenderSvg(Source).Should().NotBe(Mermaid.Default.RenderSvg("info"));
	}

	[Test]
	public void ADocumentOfATypeNoModuleHandlesIsRefused()
	{
		var render = () => Mermaid.Default.RenderSvg(new BoxDocument("x"));
		var write = () => Mermaid.Default.Write(new BoxDocument("x"));

		render.Should().Throw<NotSupportedException>();
		write.Should().Throw<NotSupportedException>();
	}

	[Test]
	public void TheAccessibilityTextIsTheSvgsTitleAndDescription()
	{
		var svg = SvgDocuments.Checked(Mermaid.Default.RenderSvg("info\naccTitle: The version\naccDescr: Of mermaid"));

		svg.Should().Contain("aria-labelledby=\"chart-title-mermaid\" aria-describedby=\"chart-desc-mermaid\"");
		svg.Should().Contain("<title id=\"chart-title-mermaid\">The version</title><desc id=\"chart-desc-mermaid\">Of mermaid</desc><style>");
	}

	[Test]
	public void WriteUsesTheNewLineOfTheOptions()
	{
		var document = Mermaid.Default.Parse("info\nshowInfo");

		Mermaid.Default.Write(document, new WriteOptions { NewLine = "\r\n" }).Should().Be("info\r\nshowInfo\r\n");
	}

	private static MermaidValue Site(string key, string value) =>
		MermaidValue.FromEntries(new OrderedDictionary<string, MermaidValue> { [key] = MermaidValue.FromString(value) });

	internal sealed class FixedMeasurer(double width) : ITextMeasurer
	{
		private int _calls;

		public int Calls => _calls;

		public TextMetrics Measure(ReadOnlySpan<char> text, in FontSpec font)
		{
			Interlocked.Increment(ref _calls);
			return new TextMetrics(width, 15, 5);
		}
	}

	private sealed class CountingThemes : IThemeProvider
	{
		private readonly List<string> _names = [];

		public IReadOnlyList<string> Names
		{
			get
			{
				lock (_names)
				{
					return [.. _names];
				}
			}
		}

		public bool HasTheme(string name) => MermaidThemes.Default.HasTheme(name);

		public MermaidValue GetThemeVariables(string name, MermaidValue? overrides)
		{
			lock (_names)
			{
				_names.Add(name);
			}

			return MermaidThemes.Default.GetThemeVariables(name, overrides);
		}
	}

	internal sealed class BoxDocument(string label, string type = "box") : MermaidDocument
	{
		public override string DiagramType => type;

		public string Label { get; } = label;
	}

	/// <summary>A host's diagram: <c>box</c> and a label, drawn as a rectangle as wide as the label.</summary>
	internal sealed class BoxModule(string type = "box") : DiagramModule<BoxDocument, DiagramLayout>
	{
		public override string DiagramType => type;

		public override bool Detect(ReadOnlySpan<char> text) => text.StartsWith("box", StringComparison.Ordinal);

		public override BoxDocument? Parse(ReadOnlySpan<char> source, DiagramParseContext context)
		{
			var body = source[context.BodyStart..];
			var keyword = body.IndexOf(' ');
			return new BoxDocument(keyword < 0 ? "" : body[(keyword + 1)..].Trim().ToString(), type);
		}

		public override void Write(BoxDocument document, StringBuilder output) => output.Append("box ").Append(document.Label).Append('\n');

		public override DiagramLayout Layout(BoxDocument document, DiagramRenderContext context)
		{
			var width = context.TextMeasurer.Measure(document.Label, new FontSpec("arial", 16)).Width;
			return new DiagramLayout(width, 20);
		}

		public override void Render(BoxDocument document, DiagramLayout layout, SvgWriter svg, DiagramRenderContext context)
		{
			svg.StartElement("rect");
			svg.WriteAttribute("width", layout.Width);
			svg.WriteAttribute("height", layout.Height!.Value);
			svg.EndElement();
		}
	}
}
