using SplashTheMusical.Config;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Parsing;
using SplashTheMusical.Rendering;

namespace SplashTheMusical.Diagrams.State;

/// <summary>
/// The state diagram's styles: the rules of <c>getStyles</c> in <c>diagrams/state/styles.js</c>, and the classes of
/// <c>StateDB.getClasses</c>.
/// </summary>
/// <remarks>
/// The template's <c>rx</c> and <c>ry</c> declarations are not in the stylesheet's property list, so the renderer writes
/// them on the frames as attributes. Its <c>url(&lt;id&gt;-gradient)</c> stroke and <c>url(&lt;id&gt;-drop-shadow)</c>
/// filter have no <c>#</c>, so a browser drops them, and STM leaves them out. The stylesheet takes no <c>$=</c>
/// attribute selector, so <c>[id$="-barbEnd"]</c> is the marker's own id, and the <c>dependencyStart</c> and
/// <c>dependencyEnd</c> rule, for markers a state diagram never draws, is left out.
/// </remarks>
internal static class StateStyles
{
	/// <summary><c>getStyles</c> with the options <c>createUserStyles</c> passes: the theme variables with the config's <c>theme</c> and <c>look</c>.</summary>
	/// <param name="svgId">The SVG's id, which names the <c>barbEnd</c> marker the template selects by the end of its id.</param>
	public static List<CssRule> Rules(MermaidConfig config, string svgId)
	{
		ArgumentNullException.ThrowIfNull(config);
		var options = config.ThemeVariables;
		MermaidValue? Option(string name) => options is { Kind: MermaidValueKind.Mapping } ? options[name] : null;
		MermaidValue? Or(string first, string second) => Option(first) is { } value && PreambleParser.IsTruthy(value) ? value : Option(second);
		CssDeclaration? Theme(string property, string name) => CssDeclaration.FromTheme(property, Option(name));
		CssDeclaration? Either(string property, string first, string second) => CssDeclaration.FromTheme(property, Or(first, second));
		CssDeclaration? Fixed(string property, string value) => CssDeclaration.FromTheme(property, value);
		MermaidValue StrokeWidth() => Option("strokeWidth") is { } width && PreambleParser.IsTruthy(width) ? width : MermaidValue.FromNumber(1);
		CssDeclaration? Width(string unit = "") => CssDeclaration.FromTheme("stroke-width", StrokeWidth(), unit);
		var altBackground = Option("altBackground") is { } alt && PreambleParser.IsTruthy(alt) ? alt : MermaidValue.FromString("#efefef");

		var rules = new List<CssRule>();
		AddColorSlots(rules, config, Option("borderColorArray"), Option("bkgColorArray"));
		var barbEnd = "#" + svgId + "_" + FlowchartDagre.StateDiagramType + "-barbEnd";
		rules.Add(Rule(["defs " + barbEnd], Theme("fill", "transitionColor"), Theme("stroke", "transitionColor")));
		rules.Add(Rule(["g.stateGroup text"], Theme("fill", "nodeBorder"), Fixed("stroke", "none"), Fixed("font-size", "10px")));
		rules.Add(Rule(["g.stateGroup text"], Theme("fill", "textColor"), Fixed("stroke", "none"), Fixed("font-size", "10px")));
		rules.Add(Rule(["g.stateGroup .state-title"], Fixed("font-weight", "bolder"), Theme("fill", "stateLabelColor")));
		rules.Add(Rule(["g.stateGroup rect"], Theme("fill", "mainBkg"), Theme("stroke", "nodeBorder")));
		rules.Add(Rule(["g.stateGroup line"], Theme("stroke", "lineColor"), Width()));
		rules.Add(Rule([".transition"], Theme("stroke", "transitionColor"), Width(), Fixed("fill", "none")));
		rules.Add(Rule([".stateGroup .composit"], Theme("fill", "background")));
		rules.Add(Rule([".stateGroup .alt-composit"], Fixed("fill", "#e0e0e0")));
		rules.Add(Rule([".state-note"], Theme("stroke", "noteBorderColor"), Theme("fill", "noteBkgColor")));
		rules.Add(Rule([".state-note text"], Theme("fill", "noteTextColor"), Fixed("stroke", "none"), Fixed("font-size", "10px")));
		rules.Add(Rule([".stateLabel .box"], Fixed("stroke", "none"), Fixed("stroke-width", "0"), Theme("fill", "mainBkg"), Fixed("opacity", "0.5")));
		rules.Add(Rule([".edgeLabel .label rect"], Theme("fill", "labelBackgroundColor"), Fixed("opacity", "0.5")));
		rules.Add(Rule([".edgeLabel"], Theme("background-color", "edgeLabelBackground"), Fixed("text-align", "center")));
		rules.Add(Rule([".edgeLabel p"], Theme("background-color", "edgeLabelBackground")));
		rules.Add(Rule([".edgeLabel rect"], Fixed("opacity", "0.5"), Theme("background-color", "edgeLabelBackground"), Theme("fill", "edgeLabelBackground")));
		rules.Add(Rule([".edgeLabel .label text"], Either("fill", "transitionLabelColor", "tertiaryTextColor")));
		rules.Add(Rule([".label div .edgeLabel"], Either("color", "transitionLabelColor", "tertiaryTextColor")));
		rules.Add(Rule([".stateLabel text"], Theme("fill", "stateLabelColor"), Fixed("font-size", "10px"), Fixed("font-weight", "bold")));
		rules.Add(Rule([".node circle.state-start"], Theme("fill", "specialStateColor"), Theme("stroke", "specialStateColor")));
		rules.Add(Rule([".node .fork-join"], Theme("fill", "specialStateColor"), Theme("stroke", "specialStateColor")));
		rules.Add(Rule([".node circle.state-end"], Theme("fill", "innerEndBackground"), Theme("stroke", "background"), Fixed("stroke-width", "1.5")));
		rules.Add(Rule([".end-state-inner"], Either("fill", "compositeBackground", "background"), Fixed("stroke-width", "1.5")));
		rules.Add(Rule([".node rect"], Either("fill", "stateBkg", "mainBkg"), Either("stroke", "stateBorder", "nodeBorder"), Width("px")));
		rules.Add(Rule([".node polygon"], Theme("fill", "mainBkg"), Either("stroke", "stateBorder", "nodeBorder"), Width("px")));
		rules.Add(Rule([barbEnd], Theme("fill", "lineColor")));
		rules.Add(Rule([".statediagram-cluster rect"], Theme("fill", "compositeTitleBackground"), Either("stroke", "stateBorder", "nodeBorder"), Width("px")));
		rules.Add(Rule([".cluster-label", ".nodeLabel"], Theme("color", "stateLabelColor")));
		rules.Add(Rule([".statediagram-state .divider"], Either("stroke", "stateBorder", "nodeBorder")));
		rules.Add(Rule([".statediagram-cluster.statediagram-cluster .inner"], Either("fill", "compositeBackground", "background")));
		rules.Add(Rule([".statediagram-cluster.statediagram-cluster-alt .inner"], CssDeclaration.FromTheme("fill", altBackground)));
		rules.Add(Rule([".statediagram-state rect.divider"], Fixed("stroke-dasharray", "10,10"), CssDeclaration.FromTheme("fill", altBackground)));
		rules.Add(Rule([".note-edge"], Fixed("stroke-dasharray", "5")));
		for (var i = 0; i < 2; i++)
		{
			rules.Add(Rule([".statediagram-note rect"], Theme("fill", "noteBkgColor"), Theme("stroke", "noteBorderColor"), Fixed("stroke-width", "1px")));
		}

		rules.Add(Rule([".statediagram-note text"], Theme("fill", "noteTextColor")));
		rules.Add(Rule([".statediagram-note .nodeLabel"], Theme("color", "noteTextColor")));
		rules.Add(Rule([".statediagram .edgeLabel"], Fixed("color", "red")));
		rules.Add(Rule([".statediagramTitleText"], Fixed("text-anchor", "middle"), Fixed("font-size", "18px"), Theme("fill", "textColor")));
		var gradient = Option("useGradient") is { } useGradient && PreambleParser.IsTruthy(useGradient);
		var neoWidth = Option("strokeWidth") is { Kind: not MermaidValueKind.Null } strokeWidth ? strokeWidth : MermaidValue.FromNumber(1);
		rules.Add(Rule(
			["[data-look=\"neo\"].statediagram-cluster rect"],
			Theme("fill", "mainBkg"),
			gradient ? null : Either("stroke", "stateBorder", "nodeBorder"),
			CssDeclaration.FromTheme("stroke-width", neoWidth)));
		var shadow = Option("dropShadow") is { } dropShadow && PreambleParser.IsTruthy(dropShadow) ? PreambleParser.ToJsString(dropShadow) : "none";
		rules.Add(Rule(["[data-look=\"neo\"].statediagram-cluster rect.outer"], CssDeclaration.FromTheme("filter", shadow)));
		return rules;
	}

	/// <summary><c>StateDB.getClasses</c>: the top-level <c>classDef</c> classes, in the order the source first defines them.</summary>
	public static List<StyleClass> Classes(StateData data)
	{
		ArgumentNullException.ThrowIfNull(data);
		return [.. data.Classes.Select(c => new StyleClass(c.Id, c.Styles, c.TextStyles))];
	}

	// genColor: a colour theme's palettes on the composite states and regions, by the data-color-id stampColorSlot gives them.
	private static void AddColorSlots(List<CssRule> rules, MermaidConfig config, MermaidValue? borders, MermaidValue? backgrounds)
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
			CssDeclaration? Stroke() => CssDeclaration.FromTheme("stroke", border);
			CssDeclaration? Fill() => fill is null ? null : CssDeclaration.FromTheme("fill", fill);
			var slot = $"[data-look=\"{look}\"][data-color-id=\"color-{i}\"]";
			rules.Add(Rule([slot + ".statediagram-cluster rect.outer"], Stroke(), Fill()));
			rules.Add(Rule([slot + ".statediagram-cluster rect.inner"], Stroke()));
			rules.Add(Rule([slot + ".statediagram-cluster rect.divider"], Stroke(), Fill()));
			rules.Add(Rule([slot + ".statediagram-cluster .outer path[stroke=\"none\"]"], Fill()));
			rules.Add(Rule([slot + ".statediagram-cluster .outer path[fill=\"none\"]"], Stroke()));
			rules.Add(Rule([slot + ".statediagram-cluster .divider path[stroke=\"none\"]"], Fill()));
			rules.Add(Rule([slot + ".statediagram-cluster .divider path[fill=\"none\"]"], Stroke()));
		}
	}

	private static CssStyleRule Rule(string[] selectors, params ReadOnlySpan<CssDeclaration?> declarations) =>
		new([.. selectors.Select(CssSelector.Parse)], declarations);
}
