using AwesomeAssertions;
using SplashTheMusical.Cli;
using SplashTheMusical.Tests.Rendering;

namespace SplashTheMusical.Tests.Cli;

public class StmCommandTests
{
	[Test]
	public void RenderWritesTheSvgOfStdinToStdout()
	{
		var (code, stdout, stderr) = Run("info", "render");

		code.Should().Be(0);
		stderr.Should().BeEmpty();
		SvgDocuments.Checked(stdout.TrimEnd('\n')).Should().Be(Mermaid.Default.RenderSvg("info"));
	}

	[Test]
	public void RenderReadsAFileAndWritesTheOutputFile()
	{
		var folder = Directory.CreateTempSubdirectory("stm-cli-").FullName;
		try
		{
			var input = Path.Combine(folder, "a.mmd");
			var output = Path.Combine(folder, "a.svg");
			File.WriteAllText(input, "info");

			var (code, stdout, _) = Run("", "render", input, "-o", output);

			code.Should().Be(0);
			stdout.Should().BeEmpty();
			File.ReadAllText(output).Should().Be(Mermaid.Default.RenderSvg("info") + "\n");
		}
		finally
		{
			Directory.Delete(folder, recursive: true);
		}
	}

	[Test]
	public void TheThemeOptionOverridesTheThemeOfTheConfigFile()
	{
		var folder = Directory.CreateTempSubdirectory("stm-cli-").FullName;
		try
		{
			var config = Path.Combine(folder, "config.json");
			File.WriteAllText(config, """{ "theme": "forest", "fontFamily": "monospace" }""");
			var dark = Site(("theme", MermaidValue.FromString("dark")), ("fontFamily", MermaidValue.FromString("monospace")));
			var forest = Site(("theme", MermaidValue.FromString("forest")), ("fontFamily", MermaidValue.FromString("monospace")));

			Run("info", "render", "--config", config).Stdout.Should().Be(Render(forest));
			Run("info", "render", "--config", config, "--theme", "dark").Stdout.Should().Be(Render(dark));
			Run("info", "render", "--theme", "dark").Stdout.Should().Be(Render(Site(("theme", MermaidValue.FromString("dark")))));
			Render(dark).Should().NotBe(Render(forest));
		}
		finally
		{
			Directory.Delete(folder, recursive: true);
		}
	}

	[Test]
	public void AnUnknownThemeIsRefused()
	{
		var (code, stdout, stderr) = Run("info", "render", "--theme", "plaid");

		code.Should().Be(1);
		stdout.Should().BeEmpty();
		stderr.Should().Be("stm: no theme is named 'plaid'\n");
	}

	[Test]
	public void ParsePrintsTheDiagnosticsAndFailsOnAnError()
	{
		Run("info", "parse").Should().Be((0, "", ""));

		var (code, stdout, stderr) = Run("notADiagram", "parse");

		code.Should().Be(1);
		stderr.Should().BeEmpty();
		Mermaid.Default.TryParse("notADiagram", out _, out var diagnostics).Should().BeFalse();
		stdout.Should().Be(string.Concat(diagnostics.Select(d => d + "\n")));
		stdout.Should().NotBeEmpty();
	}

	[Test]
	public void RenderPrintsTheDiagnosticsToStderrWhenTheSourceDoesNotParse()
	{
		var (code, stdout, stderr) = Run("notADiagram", "render");

		code.Should().Be(1);
		stdout.Should().BeEmpty();
		stderr.Should().NotBeEmpty();
	}

	[Test]
	public void FormatWritesTheParsedDiagramBack()
	{
		const string Source = "---\ntitle: T\n---\ninfo";

		Run(Source, "format").Should().Be((0, Mermaid.Default.Write(Mermaid.Default.Parse(Source)), ""));
	}

	[Test]
	public void VersionPrintsTheStmAndMermaidJsVersions()
	{
		var (code, stdout, _) = Run("", "version");

		code.Should().Be(0);
		stdout.Split('\n')[1].Should().Be("mermaid.js 12.3.0");
		stdout.Should().StartWith("stm ");
	}

	[Test]
	[Arguments(new string[0], "")]
	[Arguments(new[] { "draw" }, "stm: unknown command 'draw'\n")]
	[Arguments(new[] { "render", "-o" }, "stm: -o needs a value\n")]
	[Arguments(new[] { "render", "--theme", "dark", "--theme", "dark" }, "stm: --theme is given twice\n")]
	[Arguments(new[] { "parse", "--theme", "dark" }, "stm: parse has no option --theme\n")]
	[Arguments(new[] { "format", "a.mmd", "b.mmd" }, "stm: format takes one input\n")]
	[Arguments(new[] { "version", "x" }, "stm: version takes no arguments\n")]
	public void AUsageErrorPrintsTheUsage(string[] args, string message)
	{
		var (code, stdout, stderr) = Run("info", args);

		code.Should().Be(2);
		stdout.Should().BeEmpty();
		stderr.Should().Be(message + StmCommand.Usage.ReplaceLineEndings("\n") + "\n");
	}

	private static (int Code, string Stdout, string Stderr) Run(string stdin, params string[] args)
	{
		using var stdout = new StringWriter();
		using var stderr = new StringWriter();
		var code = StmCommand.Run(args, () => new StringReader(stdin), stdout, stderr);
		return (code, stdout.ToString(), stderr.ToString());
	}

	private static MermaidValue Site(params (string Key, MermaidValue Value)[] entries) =>
		MermaidValue.FromEntries(new OrderedDictionary<string, MermaidValue>(entries.Select(e => KeyValuePair.Create(e.Key, e.Value))));

	private static string Render(MermaidValue site) => new Mermaid(new MermaidOptions { SiteConfig = site }).RenderSvg("info") + "\n";
}
