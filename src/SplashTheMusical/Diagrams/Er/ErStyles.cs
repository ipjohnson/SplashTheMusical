// Upstream: packages/mermaid/src/diagrams/er/styles.ts
using SplashTheMusical.Config;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Parsing;
using SplashTheMusical.Rendering;

namespace SplashTheMusical.Diagrams.Er;

/// <summary>
/// The entity relationship diagram's styles: the rules of <c>getStyles</c> in <c>diagrams/er/styles.ts</c>, and the
/// classes of <c>classDef</c>.
/// </summary>
/// <remarks>
/// The template nests a <c>rect</c> rule inside <c>.relationshipLabelBox</c>, which STM writes as the descendant rule
/// <c>.relationshipLabelBox rect</c>. Where the template fades <c>tertiaryColor</c> to half opacity for the
/// <c>.labelBkg</c> background, STM leaves the declaration out.
/// </remarks>
internal static class ErStyles
{
	private static readonly string[] ColorThemes = ["redux-color", "redux-dark-color"];

	/// <summary><c>getStyles</c> with the options <c>createUserStyles</c> passes: the theme variables with the config's <c>theme</c> and <c>look</c>.</summary>
	public static List<CssRule> Rules(MermaidConfig config)
	{
		ArgumentNullException.ThrowIfNull(config);
		var options = config.ThemeVariables;
		MermaidValue? Option(string name) => options is { Kind: MermaidValueKind.Mapping } ? options[name] : null;
		MermaidValue? Or(string first, string second) => Option(first) is { Kind: not MermaidValueKind.Null } value ? value : Option(second);
		CssDeclaration? Theme(string property, string name) => CssDeclaration.FromTheme(property, Option(name));
		CssDeclaration? Either(string property, string first, string second) => CssDeclaration.FromTheme(property, Or(first, second));
		CssDeclaration? Fixed(string property, string value, bool important = false) => CssDeclaration.FromTheme(property, value, important);
		var neo = config.Look == "neo";
		CssDeclaration? Width() => neo ? CssDeclaration.FromTheme("stroke-width", Option("strokeWidth")) : Fixed("stroke-width", "1px");
		var colorTheme = Array.IndexOf(ColorThemes, config.Theme) >= 0 && Option("erEdgeLabelBackground") is { } erBackground && PreambleParser.IsTruthy(erBackground);
		CssDeclaration? EdgeBackground(string property) => colorTheme ? Theme(property, "erEdgeLabelBackground") : Theme(property, "edgeLabelBackground");

		var rules = new List<CssRule>();
		AddColorSlots(rules, config, Option("borderColorArray"), Option("bkgColorArray"));
		rules.Add(Rule([".entityBox"], Theme("fill", "mainBkg"), Theme("stroke", "nodeBorder")));
		rules.Add(Rule([".relationshipLabelBox"], Theme("fill", "tertiaryColor"), Fixed("opacity", "0.7"), Theme("background-color", "tertiaryColor")));
		rules.Add(Rule([".relationshipLabelBox rect"], Fixed("opacity", "0.5")));
		if (colorTheme)
		{
			rules.Add(Rule([".labelBkg"], Theme("background-color", "erEdgeLabelBackground")));
		}

		rules.Add(Rule([".edgeLabel"], EdgeBackground("background-color")));
		rules.Add(Rule([".edgeLabel .label rect"], EdgeBackground("fill")));
		rules.Add(Rule([".edgeLabel .label text"], Theme("fill", "textColor")));
		rules.Add(Rule([".edgeLabel .label"], Theme("fill", "nodeBorder"), Fixed("font-size", "14px")));
		rules.Add(Rule([".label"], Theme("font-family", "fontFamily"), Either("color", "nodeTextColor", "textColor")));
		rules.Add(Rule([".edge-pattern-dashed"], Fixed("stroke-dasharray", "8,8")));
		rules.Add(Rule([".node rect", ".node circle", ".node ellipse", ".node polygon"], Theme("fill", "mainBkg"), Theme("stroke", "nodeBorder"), Width()));
		rules.Add(Rule([".relationshipLine"], Theme("stroke", "lineColor"), Width(), Fixed("fill", "none")));
		rules.Add(Rule([".marker"], Fixed("fill", "none", true), CssDeclaration.FromTheme("stroke", Option("lineColor") is { Kind: MermaidValueKind.String } line ? line.String : "", true), Fixed("stroke-width", "1")));
		rules.Add(Rule([".cluster rect"], Either("fill", "clusterBkg", "mainBkg"), Either("stroke", "clusterBorder", "nodeBorder"), Fixed("stroke-width", "1px")));
		rules.Add(Rule([".cluster text"], Either("fill", "titleColor", "textColor")));
		rules.Add(Rule([".cluster-label text"], Either("fill", "titleColor", "textColor")));
		return rules;
	}

	/// <summary>The <c>classDef</c> classes, in the order the source first defines them.</summary>
	public static List<StyleClass> Classes(ErDb db)
	{
		ArgumentNullException.ThrowIfNull(db);
		return [.. db.Classes.Select(c => new StyleClass(c.Id, c.Styles, c.TextStyles))];
	}

	// genColor: a colour theme's palettes on the entities, by the data-color-id erBox gives them. The requirement
	// diagram's genColor is the same.
	internal static void AddColorSlots(List<CssRule> rules, MermaidConfig config, MermaidValue? borders, MermaidValue? backgrounds)
	{
		if (!FlowchartStyles.IsColorTheme(config) || borders is not { Kind: MermaidValueKind.Sequence })
		{
			return;
		}

		var look = config.Look is { Length: > 0 } name && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-') ? name : "classic";
		var fills = backgrounds is { Kind: MermaidValueKind.Sequence } list && list.Items.Count > 0 ? list.Items : null;
		var palette = borders.Items;
		for (var i = 0; i < palette.Count; i++)
		{
			var border = PreambleParser.ToJsString(palette[i]);
			var fill = fills is null ? null : PreambleParser.ToJsString(fills[i % fills.Count]);
			var slot = $"[data-look=\"{look}\"][data-color-id=\"color-{i}\"].node";
			rules.Add(Rule([slot + " path"], CssDeclaration.FromTheme("stroke", border), fill is null ? null : CssDeclaration.FromTheme("fill", fill)));
			rules.Add(Rule([slot + " rect"], CssDeclaration.FromTheme("stroke", border), fill is null ? null : CssDeclaration.FromTheme("fill", fill)));
		}
	}

	private static CssStyleRule Rule(string[] selectors, params ReadOnlySpan<CssDeclaration?> declarations) =>
		new([.. selectors.Select(CssSelector.Parse)], declarations);
}
