using System.Buffers;
using System.Text;
using System.Text.Json;
using SplashTheMusical.Config;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Parsing;
using SplashTheMusical.Rendering;

namespace SplashTheMusical.Conformance;

/// <summary>
/// <see cref="MermaidStylesheet"/> written by <see cref="SvgWriter"/> against corpus/styles.txt, the style element
/// mermaid.js writes for a quadrant chart and a flowchart in each theme, and for flowcharts with classDef, style, class
/// and linkStyle statements.
/// </summary>
public class StylesheetTests
{
	private const string SvgId = "styles";

	private static readonly Mermaid Stm = new(new MermaidOptions { SiteConfig = Site(), Limits = ResourceLimits.Unlimited });

	/// <summary>
	/// A quadrant chart has no rules of its own, so its stylesheet is the shared rules alone. A flowchart's adds
	/// <see cref="FlowchartStyles.Rules"/> and its classes, which STM's parser must read as mermaid.js's
	/// <c>getClasses</c> returns them. Every stylesheet must match in full.
	/// </summary>
	[Test]
	public void EveryCaseGetsTheOracleStylesheet()
	{
		var lines = File.ReadAllLines(Path.Combine(CorpusCheck.CorpusRoot, "styles.txt"));
		var cases = lines.Where(line => line.StartsWith("source\t", StringComparison.Ordinal)).Select(line => line.Split('\t')[1]).ToList();
		var problems = new List<string>();
		foreach (var name in cases)
		{
			var source = JsonString(Field(lines, "source", name)[2]);
			var oracleClasses = lines
				.Where(line => line.StartsWith("class\t" + name + "\t", StringComparison.Ordinal))
				.Select(line => line.Split('\t'))
				.Select(f => ClassLine(f[3], JsonStrings(f[4]), JsonStrings(f[5])))
				.ToList();
			var oracle = Field(lines, "style", name)[2];
			string stm;
			if (name.StartsWith("theme-", StringComparison.Ordinal))
			{
				var document = new PreambleDocument();
				if (!PreambleParser.TryParse(source, document, [], ResourceLimits.Unlimited, out _))
				{
					problems.Add($"{name}: the preamble does not parse");
					continue;
				}

				stm = Write(MermaidStylesheet.Build(MermaidConfig.Resolve(Site(), document, "quadrantChart"), [], []));
			}
			else
			{
				if (!Stm.TryParse(source, out var parsed, out _) || parsed is not FlowchartDocument flowchart)
				{
					problems.Add($"{name}: STM refuses the flowchart");
					continue;
				}

				var config = MermaidConfig.Resolve(Site(), flowchart, flowchart.DiagramType);
				var classes = FlowchartStyles.Classes(flowchart);
				var stmClasses = classes.Select(c => ClassLine(c.Id, c.Styles, c.TextStyles)).ToList();
				if (!stmClasses.SequenceEqual(oracleClasses))
				{
					problems.Add($"{name}: classes\n  stm    {string.Join(" | ", stmClasses)}\n  oracle {string.Join(" | ", oracleClasses)}");
				}

				stm = Write(MermaidStylesheet.Build(config, FlowchartStyles.Rules(config), classes));
			}

			if (stm != oracle)
			{
				var at = 0;
				while (at < stm.Length && at < oracle.Length && stm[at] == oracle[at])
				{
					at++;
				}

				var from = Math.Max(0, at - 80);
				problems.Add($"{name}: differs at {at}\n  stm    …{stm[from..Math.Min(stm.Length, at + 120)]}\n  oracle …{oracle[from..Math.Min(oracle.Length, at + 120)]}");
			}
		}

		Verify(!problems.Any(), $"{string.Join('\n', problems)}");
		Verify(() => lines[1] == "# mermaid " + CorpusCheck.PinnedMermaid());
		Verify(() => cases.Count == 26);
	}

	private static string ClassLine(string id, IReadOnlyList<string> styles, IReadOnlyList<string> textStyles) =>
		$"{id} [{string.Join(", ", styles)}] [{string.Join(", ", textStyles)}]";

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
