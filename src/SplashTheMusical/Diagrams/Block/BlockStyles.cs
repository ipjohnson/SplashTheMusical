// Upstream: packages/mermaid/src/diagrams/block/styles.ts
using SplashTheMusical.Config;
using SplashTheMusical.Parsing;
using SplashTheMusical.Rendering;
using SplashTheMusical.Themes;

namespace SplashTheMusical.Diagrams.Block;

/// <summary>
/// The block diagram's styles: the rules of <c>getStyles</c> in <c>diagrams/block/styles.ts</c>, and the classes of
/// <c>classDef</c>.
/// </summary>
/// <remarks>
/// The template's <c>.label text,span</c> is the two selectors <c>.label text</c> and <c>span</c>, and its nested
/// <c>.edgeLabel</c> rules are <c>.edgeLabel p</c> and <c>.edgeLabel rect</c>. Its <c>//</c> lines are not CSS
/// comments, and the browser drops the rules and declarations they start, so STM leaves them out. A declaration whose
/// theme variable is missing or outside <see cref="CssDeclaration"/>'s grammar is left out, as is <c>box-shadow</c>,
/// which does not paint SVG.
/// </remarks>
internal static class BlockStyles
{
	/// <summary><c>getStyles</c> with the options <c>createUserStyles</c> passes: the theme variables with the config's <c>theme</c> and <c>look</c>.</summary>
	public static List<CssRule> Rules(MermaidConfig config)
	{
		ArgumentNullException.ThrowIfNull(config);
		var options = config.ThemeVariables;
		MermaidValue? Option(string name) => options is { Kind: MermaidValueKind.Mapping } ? options[name] : null;
		string Text(string name) => Option(name) is { } value ? PreambleParser.ToJsString(value) : "undefined";
		MermaidValue? Or(string first, string second) => Option(first) is { } value && PreambleParser.IsTruthy(value) ? value : Option(second);
		CssDeclaration? Theme(string property, string name) => CssDeclaration.FromTheme(property, Option(name));
		CssDeclaration? Fixed(string property, string value) => CssDeclaration.FromTheme(property, value);
		var textColor = Or("nodeTextColor", "textColor");

		var rules = new List<CssRule>();
		AddColorSlots(rules, config, Option("borderColorArray"), Option("bkgColorArray"));
		rules.Add(Rule([".label"], Theme("font-family", "fontFamily"), CssDeclaration.FromTheme("color", textColor)));
		rules.Add(Rule([".cluster-label text"], Theme("fill", "titleColor")));
		rules.Add(Rule([".cluster-label span"], Theme("color", "titleColor")));
		rules.Add(Rule([".label text", "span"], CssDeclaration.FromTheme("fill", textColor), CssDeclaration.FromTheme("color", textColor)));
		rules.Add(Rule(
			[".node rect", ".node circle", ".node ellipse", ".node polygon", ".node path"],
			Theme("fill", "mainBkg"),
			Theme("stroke", "nodeBorder"),
			CssDeclaration.FromTheme("stroke-width", Option("strokeWidth") is { Kind: not MermaidValueKind.Null } width ? width : MermaidValue.FromNumber(1), "px")));
		rules.Add(Rule([".flowchart-label text"], Fixed("text-anchor", "middle")));
		rules.Add(Rule([".node .label"], Fixed("text-align", "center")));
		rules.Add(Rule([".node.clickable"], Fixed("cursor", "pointer")));
		rules.Add(Rule([".arrowheadPath"], Theme("fill", "arrowheadColor")));
		rules.Add(Rule([".edgePaths .path"], Theme("stroke", "lineColor"), Fixed("stroke-width", "2.0px")));
		rules.Add(Rule([".flowchart-link"], Theme("stroke", "lineColor"), Fixed("fill", "none")));
		rules.Add(Rule([".edgeLabel"], Theme("background-color", "edgeLabelBackground"), Fixed("text-align", "center")));
		rules.Add(Rule([".edgeLabel p"], Fixed("margin", "0"), Fixed("padding", "0"), Fixed("display", "inline")));
		rules.Add(Rule([".edgeLabel rect"], Fixed("opacity", "0.5"), Theme("background-color", "edgeLabelBackground"), Theme("fill", "edgeLabelBackground")));
		rules.Add(Rule([".labelBkg"], Theme("background-color", "edgeLabelBackground")));
		rules.Add(Rule([".node .cluster"], Fade("fill", Text("clusterBkg"), 0.5), Fade("stroke", Text("clusterBorder"), 0.2), Fixed("stroke-width", "1px")));
		rules.Add(Rule([".cluster text"], Theme("fill", "titleColor")));
		rules.Add(Rule([".cluster span"], Theme("color", "titleColor")));
		rules.Add(Rule(
			["div.mermaidTooltip"],
			Fixed("position", "absolute"),
			Fixed("text-align", "center"),
			Fixed("max-width", "200px"),
			Fixed("padding", "2px"),
			Theme("font-family", "fontFamily"),
			Fixed("font-size", "12px"),
			Theme("background", "tertiaryColor"),
			CssDeclaration.FromTheme("border", "1px solid " + Text("border2")),
			Fixed("border-radius", "2px"),
			Fixed("pointer-events", "none"),
			Fixed("z-index", "100")));
		rules.Add(Rule([".flowchartTitleText"], Fixed("text-anchor", "middle"), Fixed("font-size", "18px"), Theme("fill", "textColor")));

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

	/// <summary><c>getClasses</c>: the <c>classDef</c> classes, in the order the source first defines them.</summary>
	public static List<StyleClass> Classes(BlockDb db)
	{
		ArgumentNullException.ThrowIfNull(db);
		return [.. db.Classes.Values.Select(c => new StyleClass(c.Id, c.Styles, c.TextStyles))];
	}

	/// <summary>
	/// <c>genColor</c>: on a colour theme, each slot of the border palette on the composites <c>stampColorSlot</c> gives
	/// it, with the background palette's fill when there is one.
	/// </summary>
	private static void AddColorSlots(List<CssRule> rules, MermaidConfig config, MermaidValue? borders, MermaidValue? backgrounds)
	{
		if (!Flowchart.FlowchartStyles.IsColorTheme(config) || borders is not { Kind: MermaidValueKind.Sequence })
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
			var slot = $"[data-look=\"{look}\"][data-color-id=\"color-{i}\"].node rect.composite";
			rules.Add(Rule([slot], CssDeclaration.FromTheme("stroke", border), fill is null ? null : CssDeclaration.FromTheme("fill", fill)));
		}
	}

	/// <summary>The template's <c>fade</c>: the colour's channels at <paramref name="alpha"/>, or nothing for a colour khroma cannot read.</summary>
	private static CssDeclaration? Fade(string property, string color, double alpha)
	{
		try
		{
			return CssDeclaration.FromTheme(property, Khroma.Fade(color, alpha));
		}
		catch (FormatException)
		{
			return null;
		}
	}

	private static CssStyleRule Rule(string[] selectors, params ReadOnlySpan<CssDeclaration?> declarations) =>
		new([.. selectors.Select(CssSelector.Parse)], declarations);
}
