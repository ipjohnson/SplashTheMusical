using System.Text;
using AwesomeAssertions;
using SplashTheMusical.Parsing;
using SplashTheMusical.Writing;

namespace SplashTheMusical.Tests.Parsing;

public class PreambleParserTests
{
	[Test]
	public void ReadsTheFrontMatterAndTheDirectives()
	{
		const string source = """
			---
			title: Sales
			displayMode: compact
			config:
			  theme: dark
			  flowchart:
			    curve: basis
			---
			%% a comment
			%%{init: {"look": "neo"}}%%
			flowchart TD
			  A --> B %%{wrap}%%
			""";
		var (document, bodyStart, diagnostics) = Parse(source);

		diagnostics.Should().BeEmpty();
		document.Title.Should().Be("Sales");
		document.DisplayMode.Should().Be("compact");
		ValueJson.Write(document.FrontMatterConfig!).Should().Be("""{"theme":"dark","flowchart":{"curve":"basis"}}""");
		document.Directives.Select(d => d.Type).Should().Equal("init", "wrap");
		source[bodyStart..].Should().StartWith("flowchart TD");
	}

	[Test]
	public void WithoutAPreambleTheBodyStartsAtTheFirstStatement()
	{
		var (document, bodyStart, _) = Parse("\n  \n%% note\npie\n");
		document.Title.Should().BeNull();
		document.Directives.Should().BeEmpty();
		bodyStart.Should().Be("\n  \n%% note\n".Length);
	}

	[Test]
	[Arguments("title: 42", "42")]
	[Arguments("title: 1e21", "1e+21")]
	[Arguments("title: true", "true")]
	[Arguments("title: [a, null, 2]", "a,,2")]
	[Arguments("title: {a: 1}", "[object Object]")]
	[Arguments("title: ''", null)]
	[Arguments("title: 0", null)]
	[Arguments("title: false", null)]
	[Arguments("title: null", null)]
	public void TheTitleIsTheStringFormOfATruthyValue(string yaml, string? title) =>
		Parse($"---\n{yaml}\n---\npie\n").Document.Title.Should().Be(title);

	[Test]
	public void AnIndentedFenceTakesItsIndentOffTheBody()
	{
		var (document, bodyStart, _) = Parse("  ---\n  title: Indented\n  ---\npie\n");
		document.Title.Should().Be("Indented");
		bodyStart.Should().Be("  ---\n  title: Indented\n  ---\n".Length);
	}

	[Test]
	[Arguments("---\ntitle: A\n---")]
	[Arguments("--- x\ntitle: A\n---\npie")]
	[Arguments("---\n---\npie")]
	[Arguments("pie\n---\ntitle: A\n---\n")]
	public void WithoutAClosingFenceOnItsOwnLineThereIsNoFrontMatter(string source) =>
		Parse(source).Document.Title.Should().BeNull();

	[Test]
	public void TheOpeningLineBreakIsTheLastBeforeTheLastClosingFence()
	{
		var (document, _, _) = Parse("---\r\n\r\ntitle: CRLF\r\n---\r\npie\r\n");
		document.Title.Should().Be("CRLF");
	}

	[Test]
	public void AFrontMatterThatIsNotAMappingIsIgnored()
	{
		var (document, _, diagnostics) = Parse("---\n- a\n---\npie\n");
		document.Title.Should().BeNull();
		diagnostics.Should().BeEmpty();
	}

	[Test]
	public void AConfigThatIsNotAMappingIsIgnoredWithAWarning()
	{
		var (document, _, diagnostics) = Parse("---\nconfig: dark\n---\npie\n");
		document.FrontMatterConfig.Should().BeNull();
		diagnostics.Should().ContainSingle().Which.Severity.Should().Be(DiagnosticSeverity.Warning);
	}

	[Test]
	public void InvalidYamlIsAnError()
	{
		var document = new TestDocument();
		var diagnostics = new List<MermaidDiagnostic>();
		PreambleParser.TryParse("---\ntitle: [a\n---\npie\n", document, diagnostics, ResourceLimits.Default, out _).Should().BeFalse();
		var error = diagnostics.Should().ContainSingle().Subject;
		error.Severity.Should().Be(DiagnosticSeverity.Error);
		error.Line.Should().BeGreaterThanOrEqualTo(2);
	}

	[Test]
	public void AnInvalidDirectiveIsKept()
	{
		var (document, _, diagnostics) = Parse("%%{init: {theme: dark}}%%\npie\n");
		document.Directives.Should().Equal(new MermaidDirective("init", null) { InvalidArguments = "{theme: dark}" });
		diagnostics.Should().ContainSingle().Which.Severity.Should().Be(DiagnosticSeverity.Warning);
	}

	[Test]
	public void APercentBraceWithoutATypeStartsTheBody()
	{
		var (document, bodyStart, _) = Parse("%%{}%%\npie\n");
		document.Directives.Should().BeEmpty();
		bodyStart.Should().Be(0);
	}

	[Test]
	public void ConfigMergesTheFrontMatterTheDisplayModeAndTheInitDirectives()
	{
		var (document, _, _) = Parse("""
			---
			displayMode: compact
			config:
			  theme: dark
			  gantt:
			    barHeight: 20
			---
			%%{init: {"theme": "forest", "config": {"curve": "basis"}}}%%
			%%{wrap}%%
			flowchart TD
			""");
		ValueJson.Write(PreambleConfig.Resolve(document, "flowchart-v2")).Should().Be(
			"""{"theme":"forest","gantt":{"barHeight":20,"displayMode":"compact"},"flowchart":{"curve":"basis"},"wrap":true}""");
	}

	[Test]
	public void OneInvalidInitDropsEveryInit()
	{
		var (document, _, _) = Parse("%%{init: {\"theme\": \"dark\"}}%%\n%%{init: {bad}}%%\npie\n");
		ValueJson.Write(PreambleConfig.Resolve(document, "pie")).Should().Be("{}");
	}

	[Test]
	public void SeveralInitsAreMergedTwoLevelsDeep()
	{
		var (document, _, _) = Parse("""
			%%{init: {"theme": "dark", "flowchart": {"curve": "basis", "a": {"b": 1}}}}%%
			%%{initialize: {"theme": "forest", "flowchart": {"padding": 8, "a": {"c": 2}}}}%%
			pie
			""");
		ValueJson.Write(PreambleConfig.Resolve(document, "pie")).Should().Be(
			"""{"theme":"forest","flowchart":{"curve":"basis","a":{"b":1,"c":2},"padding":8}}""");
	}

	[Test]
	public void SeveralWrapDirectivesSetWrapToWhetherAnyIsWrap()
	{
		var (document, _, _) = Parse("%%{nowrap}%%\n%%{nowrap}%%\npie\n");
		ValueJson.Write(PreambleConfig.Resolve(document, "pie")).Should().Be("""{"wrap":false}""");
	}

	[Test]
	[Arguments("---\ntitle: Sales\n---\n")]
	[Arguments("---\ntitle: \"42\"\ndisplayMode: compact\n---\n")]
	[Arguments("---\ntitle: \"a: b # c\"\nconfig:\n  theme: dark\n  list:\n    - 1\n    - x: 1\n      y: [1, 2]\n    - [3, 4]\n    - {}\n  \"odd key\": null\n  inf: .inf\n  empty: []\n---\n")]
	[Arguments("%%{init: {\"title\": \"it\\u0027s 100%\", \"n\": 1e21}}%%\n%%{wrap}%%\n%%{init: word}%%\n%%{init: {bad}}%%\n")]
	[Arguments("---\ntitle: \"line\\nbreak\\ttab\"\n---\n")]
	public void AWrittenPreambleReadsBackTheSame(string preamble)
	{
		var (document, _, _) = Parse(preamble + "pie\n");
		var builder = new StringBuilder();
		PreambleWriter.Write(builder, document);
		var written = builder.ToString();

		var (again, bodyStart, diagnostics) = Parse(written);
		diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
		bodyStart.Should().Be(written.Length);
		again.Title.Should().Be(document.Title);
		again.DisplayMode.Should().Be(document.DisplayMode);
		again.FrontMatterConfig.Should().Be(document.FrontMatterConfig);
		again.Directives.Should().Equal(document.Directives);
	}

	[Test]
	public void TheWriterWritesOneCanonicalForm()
	{
		var document = new TestDocument
		{
			Title = "Sales: 2024",
			FrontMatterConfig = MermaidValue.FromEntries(new() { ["theme"] = MermaidValue.FromString("dark"), ["fontSize"] = MermaidValue.FromNumber(16) }),
			Directives = { new MermaidDirective("init", MermaidValue.FromEntries(new() { ["look"] = MermaidValue.FromString("it's") })), new MermaidDirective("wrap", null) },
		};
		var builder = new StringBuilder();
		PreambleWriter.Write(builder, document);
		builder.ToString().Should().Be("""
			---
			title: "Sales: 2024"
			config:
			  theme: dark
			  fontSize: 16
			---
			%%{init: {"look": "itQUOTEs"}}%%
			%%{wrap}%%

			""".Replace("\r\n", "\n", StringComparison.Ordinal).Replace("QUOTE", "\\" + "u0027", StringComparison.Ordinal));
	}

	private static (TestDocument Document, int BodyStart, List<MermaidDiagnostic> Diagnostics) Parse(string source)
	{
		var document = new TestDocument();
		var diagnostics = new List<MermaidDiagnostic>();
		PreambleParser.TryParse(source, document, diagnostics, ResourceLimits.Default, out var bodyStart).Should().BeTrue();
		return (document, bodyStart, diagnostics);
	}

	private sealed class TestDocument : MermaidDocument
	{
		public override string DiagramType => "";
	}
}
