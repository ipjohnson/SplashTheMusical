// Upstream: packages/mermaid/src/diagrams/class/styles.js
using SplashTheMusical.Config;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Parsing;
using SplashTheMusical.Rendering;

namespace SplashTheMusical.Diagrams.Class;

/// <summary>
/// The class diagram's styles: the rules of <c>getStyles</c> in <c>diagrams/class/styles.js</c>, and the
/// <c>classDef</c> classes of <c>ClassDB.getClasses</c>.
/// </summary>
/// <remarks>
/// The stylesheet takes no <c>$=</c> attribute selector, so <c>[id$="-compositionStart"]</c> and the other marker rules
/// select the markers' own ids. The template's nested rules, <c>.title</c> inside <c>g.classGroup text</c> and
/// <c>p</c> and <c>rect</c> inside <c>.edgeLabel[data-look="neo"]</c>, are written as the descendant rules a browser
/// reads them as.
/// </remarks>
internal static class ClassStyles
{
	private static readonly string[] Markers = ["composition", "dependency", "extension", "aggregation", "lollipop"];

	/// <summary><c>getStyles</c> with the options <c>createUserStyles</c> passes: the theme variables with the config's <c>theme</c> and <c>look</c>.</summary>
	/// <param name="svgId">The SVG's id, which names the markers the template selects by the end of their ids.</param>
	public static List<CssRule> Rules(MermaidConfig config, string svgId)
	{
		ArgumentNullException.ThrowIfNull(config);
		var options = config.ThemeVariables;
		MermaidValue? Option(string name) => options is { Kind: MermaidValueKind.Mapping } ? options[name] : null;
		MermaidValue? Or(string first, string second) => Option(first) is { } value && PreambleParser.IsTruthy(value) ? value : Option(second);
		string Text(string name) => Option(name) is { } value ? PreambleParser.ToJsString(value) : "undefined";
		CssDeclaration? Theme(string property, string name) => CssDeclaration.FromTheme(property, Option(name));
		CssDeclaration? Important(string property, string name) => CssDeclaration.FromTheme(property, Text(name), important: true);
		CssDeclaration? Fixed(string property, string value) => CssDeclaration.FromTheme(property, value);

		var rules = new List<CssRule>();
		AddColorSlots(rules, config, Option("borderColorArray"), Option("bkgColorArray"));
		rules.Add(Rule(["g.classGroup text"], CssDeclaration.FromTheme("fill", Or("nodeBorder", "classText")), Fixed("stroke", "none"), Theme("font-family", "fontFamily"), Fixed("font-size", "10px")));
		rules.Add(Rule(["g.classGroup text .title"], Fixed("font-weight", "bolder")));
		rules.Add(Rule([".cluster-label text"], Theme("fill", "titleColor")));
		rules.Add(Rule([".cluster-label span"], Theme("color", "titleColor")));
		rules.Add(Rule([".cluster-label span p"], Fixed("background-color", "transparent")));
		rules.Add(Rule([".cluster rect"], Theme("fill", "clusterBkg"), Theme("stroke", "clusterBorder"), Fixed("stroke-width", "1px")));
		rules.Add(Rule([".cluster text"], Theme("fill", "titleColor")));
		rules.Add(Rule([".cluster span"], Theme("color", "titleColor")));
		rules.Add(Rule([".nodeLabel", ".edgeLabel"], Theme("color", "classText")));
		rules.Add(Rule([".noteLabel .nodeLabel", ".noteLabel .edgeLabel"], Theme("color", "noteTextColor")));
		rules.Add(Rule([".edgeLabel .label rect"], Theme("fill", "mainBkg")));
		rules.Add(Rule([".label text"], Theme("fill", "classText")));
		rules.Add(Rule([".labelBkg"], Theme("background", "mainBkg")));
		rules.Add(Rule([".edgeLabel .label span"], Theme("background", "mainBkg")));
		rules.Add(Rule([".classTitle"], Fixed("font-weight", "bolder")));
		rules.Add(Rule(
			[".node rect", ".node circle", ".node ellipse", ".node polygon", ".node path"],
			Theme("fill", "mainBkg"),
			Theme("stroke", "nodeBorder"),
			Theme("stroke-width", "strokeWidth")));
		rules.Add(Rule([".divider"], Theme("stroke", "nodeBorder"), Fixed("stroke-width", "1")));
		rules.Add(Rule(["g.clickable"], Fixed("cursor", "pointer")));
		rules.Add(Rule(["g.classGroup rect"], Theme("fill", "mainBkg"), Theme("stroke", "nodeBorder")));
		rules.Add(Rule(["g.classGroup line"], Theme("stroke", "nodeBorder"), Fixed("stroke-width", "1")));
		rules.Add(Rule([".classLabel .box"], Fixed("stroke", "none"), Fixed("stroke-width", "0"), Theme("fill", "mainBkg"), Fixed("opacity", "0.5")));
		rules.Add(Rule([".classLabel .label"], Theme("fill", "nodeBorder"), Fixed("font-size", "10px")));
		rules.Add(Rule([".relation"], Theme("stroke", "lineColor"), Theme("stroke-width", "strokeWidth"), Fixed("fill", "none")));
		rules.Add(Rule([".dashed-line"], Fixed("stroke-dasharray", "3")));
		rules.Add(Rule([".dotted-line"], Fixed("stroke-dasharray", "1 2")));
		foreach (var marker in Markers)
		{
			var fill = marker switch
			{
				"composition" or "dependency" => Important("fill", "lineColor"),
				"lollipop" => Important("fill", "mainBkg"),
				_ => CssDeclaration.FromTheme("fill", "transparent", important: true),
			};
			foreach (var end in (ReadOnlySpan<string>)["Start", "End"])
			{
				var id = "#" + svgId + "_" + FlowchartDagre.ClassDiagramType + "-" + marker + end;
				rules.Add(Rule([id, "." + marker], fill, Important("stroke", "lineColor"), Fixed("stroke-width", "1")));
			}
		}

		rules.Add(Rule([".edgeTerminals"], Fixed("font-size", "11px"), Fixed("line-height", "initial")));
		rules.Add(Rule([".classTitleText"], Fixed("text-anchor", "middle"), Fixed("font-size", "18px"), Theme("fill", "textColor")));
		rules.Add(Rule([".edgeLabel[data-look=\"neo\"]"], Theme("background-color", "edgeLabelBackground"), Fixed("text-align", "center")));
		rules.Add(Rule([".edgeLabel[data-look=\"neo\"] p"], Theme("background-color", "edgeLabelBackground")));
		rules.Add(Rule(
			[".edgeLabel[data-look=\"neo\"] rect"],
			Fixed("opacity", "0.5"),
			Theme("background-color", "edgeLabelBackground"),
			Theme("fill", "edgeLabelBackground")));

		// getIconStyles of diagrams/globalStyles.ts.
		rules.Add(Rule(
			[".label-icon"],
			Fixed("display", "inline-block"),
			Fixed("height", "1em"),
			Fixed("overflow", "visible"),
			Fixed("vertical-align", "-0.125em")));
		rules.Add(Rule([".node .label-icon path"], Fixed("fill", "currentColor"), Fixed("stroke", "revert"), Fixed("stroke-width", "revert")));
		return rules;
	}

	/// <summary><c>ClassDB.getClasses</c>: the <c>classDef</c> classes, in the order the source first defines them.</summary>
	public static List<StyleClass> Classes(ClassData data)
	{
		ArgumentNullException.ThrowIfNull(data);
		return [.. data.StyleClasses.Values.Select(c => new StyleClass(c.Id, c.Styles, c.TextStyles))];
	}

	// genColor: a colour theme's palettes on the classes, by the data-color-id stampColorSlot gives them.
	private static void AddColorSlots(List<CssRule> rules, MermaidConfig config, MermaidValue? borders, MermaidValue? backgrounds)
	{
		if (!FlowchartStyles.IsColorTheme(config) || borders is not { Kind: MermaidValueKind.Sequence })
		{
			return;
		}

		var look = config.Look is { Length: > 0 } name && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-') ? name : "classic";
		var fills = backgrounds is { Kind: MermaidValueKind.Sequence } list && list.Items.Count > 0 ? list.Items : null;
		var palette = borders.Items;
		for (var i = 0; i < FlowchartStyles.PaletteLength(config); i++)
		{
			var border = PreambleParser.ToJsString(palette[i % palette.Count]);
			var fill = fills is null ? null : PreambleParser.ToJsString(fills[i % fills.Count]);
			var slot = $"[data-look=\"{look}\"][data-color-id=\"color-{i}\"]";
			rules.Add(Rule([slot + ".node .outer-path path"], CssDeclaration.FromTheme("stroke", border), fill is null ? null : CssDeclaration.FromTheme("fill", fill)));
			rules.Add(Rule([slot + ".node .divider path"], CssDeclaration.FromTheme("stroke", border)));
		}
	}

	private static CssStyleRule Rule(string[] selectors, params ReadOnlySpan<CssDeclaration?> declarations) =>
		new([.. selectors.Select(CssSelector.Parse)], declarations);
}
