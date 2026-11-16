using System.Buffers;
using System.Text;
using System.Text.Json;
using SplashTheMusical.Config;
using SplashTheMusical.Parsing;
using SplashTheMusical.Rendering;

namespace SplashTheMusical.Conformance;

/// <summary>
/// <see cref="MermaidStylesheet"/> written by <see cref="SvgWriter"/> against corpus/styles.txt, the style element
/// mermaid.js writes for a quadrant chart in each theme and for flowcharts with classDef statements.
/// </summary>
public class StylesheetTests
{
	private const string SvgId = "styles";

	/// <summary>
	/// A quadrant chart has no rules of its own, so its stylesheet is the shared rules alone and must match in full.
	/// mermaid.js puts a flowchart's own rules between the shared rules and the look rules, and STM has not ported them, so
	/// a flowchart's stylesheet must start with the text STM writes before them and end with the text STM writes after.
	/// </summary>
	[Test]
	public async Task EveryCaseGetsTheOracleStylesheet()
	{
		var lines = File.ReadAllLines(Path.Combine(CorpusCheck.CorpusRoot, "styles.txt"));
		var cases = lines.Where(line => line.StartsWith("source\t", StringComparison.Ordinal)).Select(line => line.Split('\t')[1]).ToList();
		var problems = new List<string>();
		foreach (var name in cases)
		{
			var source = JsonString(Field(lines, "source", name)[2]);
			var document = new PreambleDocument();
			if (!PreambleParser.TryParse(source, document, [], ResourceLimits.Unlimited, out _))
			{
				problems.Add($"{name}: the preamble does not parse");
				continue;
			}

			var flowchart = name.StartsWith("classdef-", StringComparison.Ordinal);
			var config = MermaidConfig.Resolve(Site(), document, flowchart ? "flowchart-v2" : "quadrantChart");
			var classes = lines
				.Where(line => line.StartsWith("class\t" + name + "\t", StringComparison.Ordinal))
				.Select(line => line.Split('\t'))
				.Select(f => new StyleClass(f[3], JsonStrings(f[4]), JsonStrings(f[5])))
				.ToList();
			var oracle = Field(lines, "style", name)[2];
			var marker = new CssStyleRule(CssSelector.Parse(".stm-diagram-rules"), CssDeclaration.FromTheme("fill", "red"));
			var stm = Write(MermaidStylesheet.Build(config, flowchart ? [marker] : [], classes));
			if (!flowchart)
			{
				if (stm != oracle)
				{
					problems.Add($"{name}:\n  stm    {stm}\n  oracle {oracle}");
				}

				continue;
			}

			var markerText = SvgWriter.StyleText(SvgId, [marker]);
			var before = stm[..stm.IndexOf(markerText, StringComparison.Ordinal)];
			var after = stm[(stm.IndexOf(markerText, StringComparison.Ordinal) + markerText.Length)..];
			if (!oracle.StartsWith(before, StringComparison.Ordinal) || !oracle.EndsWith(after, StringComparison.Ordinal))
			{
				problems.Add($"{name}:\n  stm    {before} … {after}\n  oracle {oracle}");
			}
		}

		await Assert.That(string.Join('\n', problems)).IsEmpty();
		await Assert.That(lines[1]).IsEqualTo("# mermaid " + CorpusCheck.PinnedMermaid());
		await Assert.That(cases.Count).IsEqualTo(13);
	}

	// The style element's text, unescaped. The sanitizer must keep the document unchanged.
	private static string Write(IEnumerable<CssRule> rules)
	{
		var buffer = new ArrayBufferWriter<byte>();
		var writer = new SvgWriter(buffer);
		writer.StartElement("svg");
		writer.WriteAttribute("id", SvgId);
		writer.WriteStyle(rules);
		writer.EndElement();
		var xml = Encoding.UTF8.GetString(buffer.WrittenSpan);
		var sanitized = SvgSanitizer.SanitizeRendererOutput(xml);
		if (sanitized.HasViolations)
		{
			throw new InvalidOperationException($"The sanitizer changed the stylesheet: {string.Join(", ", sanitized.Violations)}");
		}

		var start = xml.IndexOf("<style>", StringComparison.Ordinal) + "<style>".Length;
		return xml[start..xml.IndexOf("</style>", StringComparison.Ordinal)].Replace("&gt;", ">", StringComparison.Ordinal)
			.Replace("&lt;", "<", StringComparison.Ordinal).Replace("&amp;", "&", StringComparison.Ordinal);
	}

	private static string[] Field(string[] lines, string kind, string name) =>
		lines.First(line => line.StartsWith(kind + "\t" + name + "\t", StringComparison.Ordinal)).Split('\t', 3);

	// The initialize options of tools/oracle/facts.mjs.
	private static MermaidValue Site() => MermaidValue.FromEntries(new OrderedDictionary<string, MermaidValue>
	{
		["startOnLoad"] = MermaidValue.False,
		["securityLevel"] = MermaidValue.FromString("strict"),
	});

	private static string JsonString(string json)
	{
		using var document = JsonDocument.Parse(json);
		return document.RootElement.GetString()!;
	}

	private static List<string> JsonStrings(string json)
	{
		using var document = JsonDocument.Parse(json);
		return [.. document.RootElement.EnumerateArray().Select(item => item.GetString()!)];
	}

	private sealed class PreambleDocument : MermaidDocument
	{
		public override string DiagramType => "";
	}
}
