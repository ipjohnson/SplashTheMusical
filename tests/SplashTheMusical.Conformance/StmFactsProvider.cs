using System.Xml;
using SplashTheMusical.Config;
using SplashTheMusical.Diagrams.Info;
using SplashTheMusical.Parsing;
using SplashTheMusical.Text;

namespace SplashTheMusical.Conformance;

/// <summary>
/// STM through its public services, with the site config tools/oracle/facts.mjs initializes mermaid.js with. The facts
/// come from the parsed document and the boxes from its layout, in the formats of tools/oracle/README.md.
/// </summary>
public sealed class StmFactsProvider : IFactsProvider
{
	public static readonly MermaidValue OracleSite = MermaidValue.FromEntries(new OrderedDictionary<string, MermaidValue>
	{
		["startOnLoad"] = MermaidValue.False,
		["securityLevel"] = MermaidValue.FromString("strict"),
	});

	private static readonly Mermaid Stm = new(new MermaidOptions { SiteConfig = OracleSite, Limits = ResourceLimits.Unlimited });

	public string Name => "stm";

	public CaseFacts Facts(string source)
	{
		if (!Stm.TryParse(source, out var document, out var diagnostics))
		{
			var error = diagnostics.First(d => d.Severity == DiagnosticSeverity.Error).Message;
			var lineEnd = error.IndexOf('\n', StringComparison.Ordinal);
			return CaseFacts.Refused(lineEnd < 0 ? error : error[..lineEnd]);
		}

		var type = document!.DiagramType;
		var svg = Stm.RenderSvg(document);
		var preamble = PreambleConfig.Resolve(document, type);
		var facts = new List<string> { ConfigLines.Line("type", type), ConfigLines.Line("drawn", Drawn(svg) ? "yes" : "no") };
		facts.AddRange(ConfigLines.ConfigFacts(ConfigMerge.Site(OracleSite), ConfigMerge.Apply(OracleSite, preamble, type)));
		if (document.Title is not null)
		{
			facts.Add(ConfigLines.Line("preamble-title", document.Title));
		}

		ConfigLines.Flatten(facts, "preamble-config", "", preamble);
		var boxes = new List<string>();
		string? canvas = null;
		if (document is InfoDocument)
		{
			canvas = Info(facts, boxes, MermaidConfig.Resolve(OracleSite, document, type));
		}

		return new CaseFacts(true, null, facts, boxes, canvas, svg);
	}

	/// <summary>
	/// <c>extract.mjs</c>'s info extractor: the version as a label, and the box of the version text. The root has no
	/// viewBox and no height, so the canvas is its width and the 150 a browser gives an SVG without a height.
	/// </summary>
	private static string Info(List<string> facts, List<string> boxes, MermaidConfig config)
	{
		const double FontSize = 32;
		var version = MermaidCompatibility.MermaidJsVersion;
		facts.Add(ConfigLines.Line("label", version));
		var family = config.ThemeVariables?["fontFamily"] is { Kind: MermaidValueKind.String } font ? font.String : "sans-serif";
		var metrics = TextMeasurer.Default.Measure("v" + version, new FontSpec(family, FontSize));
		boxes.Add(Box("version", version, 100 - (metrics.Width / 2), 40 - metrics.Ascent, metrics.Width, metrics.Height));
		return "canvas\t0\t0\t400\t150";
	}

	private static string Box(string kind, string name, double x, double y, double width, double height) =>
		string.Join('\t', "box", kind, name, Number(x), Number(y), Number(width), Number(height));

	private static string Number(double value) => JsNumber.ToString(Math.Round(value, 3, MidpointRounding.AwayFromZero));

	/// <summary>
	/// <c>drawn</c>: whether the SVG has a shape, or a text with something other than space, outside <c>defs</c> and
	/// markers.
	/// </summary>
	private static bool Drawn(string svg)
	{
		using var reader = XmlReader.Create(new StringReader(svg), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
		var hidden = 0;
		var inText = 0;
		while (reader.Read())
		{
			switch (reader.NodeType)
			{
				case XmlNodeType.Element:
					var isEmpty = reader.IsEmptyElement;
					switch (reader.LocalName)
					{
						case "defs" or "marker" or "style" or "title" or "desc":
							hidden += isEmpty ? 0 : 1;
							break;
						case "rect" or "circle" or "ellipse" or "polygon" or "polyline" or "line" or "path" or "image" when hidden == 0:
							return true;
						case "text" when !isEmpty:
							inText++;
							break;
					}

					break;
				case XmlNodeType.EndElement:
					switch (reader.LocalName)
					{
						case "defs" or "marker" or "style" or "title" or "desc":
							hidden--;
							break;
						case "text":
							inText--;
							break;
					}

					break;
				case XmlNodeType.Text when hidden == 0 && inText > 0 && !string.IsNullOrWhiteSpace(reader.Value):
					return true;
			}
		}

		return false;
	}
}
