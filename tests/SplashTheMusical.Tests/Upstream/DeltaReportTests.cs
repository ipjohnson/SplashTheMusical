using AwesomeAssertions;
using SplashTheMusical.Upstream;

namespace SplashTheMusical.Tests.Upstream;

public class DeltaReportTests
{
	private const string PieGrammar = "packages/mermaid/src/diagrams/pie/parser/pie.jison";

	private static readonly Dictionary<string, List<string>> Parsers = new() { [PieGrammar] = ["src/SplashTheMusical/Diagrams/Pie/PieParser.cs"] };

	[Test]
	public void TheSameVersionReportsNothingChanged()
	{
		using var fixture = new UpstreamFixture();
		WriteSide(fixture, "old", newer: false);
		WriteSide(fixture, "new", newer: false);

		var report = DeltaReport.Write(fixture.Side("old", "1.0.0"), fixture.Side("new", "1.0.0"), Parsers);

		report.Should().StartWith("# Upstream delta: mermaid 1.0.0 to 1.0.0\n\nNothing changed.\n");
		report.Split("No change.").Should().HaveCount(8, "each of the seven sections says it has no change");
	}

	[Test]
	public void EachCategoryListsItsDifferences()
	{
		using var fixture = new UpstreamFixture();
		WriteSide(fixture, "old", newer: false);
		WriteSide(fixture, "new", newer: true);

		var report = DeltaReport.Write(fixture.Side("old", "1.0.0"), fixture.Side("new", "1.1.0"), Parsers);

		report.Should().Contain("Changed: Diagram types, Grammar files, Config keys, Theme variables, Shapes, edge types and layout algorithms, Corpus cases, Conformance.");
		report.Should().NotContain("No change.");
		Section(report, "Diagram types").Should().Contain("Diagram folders added, 1:\n\n- `venn`").And.Contain("Detector ids added, 1:\n\n- `venn`");
		Section(report, "Grammar files")
			.Should()
			.Contain($"### `{PieGrammar}`, changed\n\nSTM parser: `src/SplashTheMusical/Diagrams/Pie/PieParser.cs`")
			.And.Contain("-slice\n+slices\n")
			.And.Contain("### `packages/mermaid/src/diagrams/venn/parser/venn.jison`, added\n\nNo STM parser names it.");
		Section(report, "Config keys")
			.Should()
			.Contain("Added, 1:\n\n- `key pie.legend`: `true`")
			.And.Contain("Changed, 1:\n\n- `key pie.textPosition`: `0.75` to `0.8`");
		Section(report, "Theme variables").Should().Contain("- `theme default pie1`: `\"#a\"` to `\"#b\"`");
		Section(report, "Shapes, edge types and layout algorithms")
			.Should()
			.Contain("- `key grid-box`: `gridBox`")
			.And.Contain("Edge markers added, 1:\n\n- `cross`")
			.And.Contain("Registered layout algorithms added, 1:\n\n- `elk.box`")
			.And.Contain("Layout folders added, 1:\n\n- `grid`");
		Section(report, "Corpus cases")
			.Should()
			.Contain("Cases added, 1:\n\n- `pie/c`")
			.And.Contain("Cases removed, 1:\n\n- `pie/b`")
			.And.Contain("Cases whose oracle facts changed, 1:\n\n- `pie/a`\n  - `- label 1.0.0`\n  - `+ label 1.1.0`\n");
		Section(report, "Conformance")
			.Should()
			.Contain("Corpus levels that newly fail, 1:\n\n- `pie/a L1`")
			.And.Contain("Corpus levels that newly pass, 1:\n\n- `pie/a L0`")
			.And.Contain("Tests that newly fail, 1:\n\n- `ThemeTests.EveryVariable`");
	}

	[Test]
	public void AMermaid13CheckoutIsReadFromItsDiagramPackages()
	{
		using var fixture = new UpstreamFixture();
		WriteSide(fixture, "old", newer: false);
		WriteSide(fixture, "new", newer: false);
		Directory.Delete(Path.Combine(fixture.Root, "new", "checkout"), recursive: true);
		void Write(string path, string text) => fixture.Write(Path.Combine("new", "checkout", path), text);
		Write(
			"packages/mermaid/src/generated/detectors.gen.ts",
			"export const DETECTORS = [\n  { type: 'pie', pattern: /^\\s*pie/, id: 'pie' },\n  { type: 'venn', pattern: /^\\s*venn-beta/, id: 'venn' },\n];\n"
		);
		Write("packages/diagram-pie/grammar/pie.ebnf", "pie = 'pie' ;\n");
		Write("packages/diagram-pie/grammar/generated/pie.grammar", "");
		Write("packages/diagram-venn/grammar/venn.ebnf", "venn = 'venn-beta' ;\n");
		Write("packages/core/src/layout/layered/index.ts", "");
		Write("packages/core/src/layout/hops/index.ts", "");
		Write("packages/layout-elk/package.json", "{}");

		var report = DeltaReport.Write(fixture.Side("old", "12.3.0"), fixture.Side("new", "13.0.0"), Parsers);

		Section(report, "Diagram types")
			.Should()
			.Be("\n## Diagram types\n\nDiagram folders added, 1:\n\n- `venn`\n\nDetector ids added, 1:\n\n- `venn`\n");
		var grammars = Section(report, "Grammar files");
		grammars
			.Should()
			.Contain($"### `{PieGrammar}`, removed\n\nSTM parser:")
			.And.Contain("### `packages/diagram-pie/grammar/pie.ebnf`, added")
			.And.Contain("### `packages/diagram-venn/grammar/venn.ebnf`, added")
			.And.NotContain("generated");
		Section(report, "Shapes, edge types and layout algorithms")
			.Should()
			.Contain($"Edge markers were not compared, because the new checkout has no `{UpstreamTree.MarkersFile}`.")
			.And.Contain($"Registered layout algorithms were not compared, because the new checkout has no `{UpstreamTree.RenderFile}`.")
			.And.Contain("Layout folders added, 1:\n\n- `layered`\n\nLayout folders removed, 1:\n\n- `dagre`", "12's elk folder and 13's layout-elk package are the same layout, and hops is shared");
	}

	private static string Section(string report, string title)
	{
		var start = report.IndexOf("\n## " + title + "\n", StringComparison.Ordinal);
		start.Should().BeGreaterThanOrEqualTo(0, $"the report has a {title} section");
		var end = report.IndexOf("\n## ", start + 1, StringComparison.Ordinal);
		return end < 0 ? report[start..] : report[start..end];
	}

	private static void WriteSide(UpstreamFixture fixture, string side, bool newer)
	{
		void Write(string path, string text) => fixture.Write(Path.Combine(side, path), text);

		var version = newer ? "1.1.0" : "1.0.0";
		Write("checkout/packages/mermaid/src/diagrams/pie/pieDetector.ts", "const id = 'pie';\n");
		Write("checkout/" + PieGrammar, newer ? "pie\nslices\n" : "pie\nslice\n");
		Write("checkout/packages/parser/src/language/info/info.langium", "grammar Info\n");
		Write("checkout/packages/mermaid/src/rendering-util/rendering-elements/markers.js", "const markers = {\n  point,\n  circle,\n" + (newer ? "  cross,\n" : "") + "};\n");
		Write("checkout/packages/mermaid/src/rendering-util/render.ts", "registerLayoutLoaders([{ name: 'dagre', loader }]);\n");
		Write(
			"checkout/packages/mermaid/src/rendering-util/layout-algorithms/elk/algorithms.ts",
			"export const ELK_ALGORITHMS = [\n  'elk.stress',\n" + (newer ? "  'elk.box',\n" : "") + "] as const;\n"
		);
		Write("checkout/packages/mermaid/src/rendering-util/layout-algorithms/dagre/index.ts", "");
		Write("checkout/packages/mermaid/src/rendering-util/layout-algorithms/common/index.ts", "");
		if (newer)
		{
			Write("checkout/packages/mermaid/src/diagrams/venn/vennDetector.ts", "const id = 'venn';\n");
			Write("checkout/packages/mermaid/src/diagrams/venn/parser/venn.jison", "venn\n");
			Write("checkout/packages/mermaid/src/rendering-util/layout-algorithms/grid/index.ts", "");
		}

		var header = $"# mermaid.js oracle\n# mermaid {version}\n";
		Write(
			"corpus/config.txt",
			header + (newer ? "key\tpie.legend\ttrue\n" : "") + $"key\tpie.textPosition\t{(newer ? "0.8" : "0.75")}\nname\ttextPosition\n"
		);
		Write("corpus/themes.txt", header + $"theme\tdefault\tpie1\t\"{(newer ? "#b" : "#a")}\"\n");
		Write("corpus/shapes.txt", header + (newer ? "key\tgrid-box\tgridBox\n" : "") + "key\trect\trect\n");
		Write("corpus/pie/a.mmd", "pie\n");
		Write("corpus/pie/a.facts.txt", header + $"# source 1\naccept\tyes\nlabel\t{version}\n");
		Write(newer ? "corpus/pie/c.mmd" : "corpus/pie/b.mmd", "pie\n");
		Write("results.txt", newer ? "pie/a\tL0\tpass\npie/a\tL1\tfail\n\tbox differs\n" : "pie/a\tL0\tfail\n\tparse failed\npie/a\tL1\tpass\n");
		Write(
			"results.trx",
			$"""
			<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
			  <Results>
			    <UnitTestResult testName="ThemeTests.EveryVariable" outcome="{(newer ? "Failed" : "Passed")}" />
			    <UnitTestResult testName="CorpusTests.Hashes" outcome="Failed" />
			  </Results>
			</TestRun>
			"""
		);
	}
}
