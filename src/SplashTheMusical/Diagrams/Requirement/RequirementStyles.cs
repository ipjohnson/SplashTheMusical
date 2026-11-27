// Upstream: packages/mermaid/src/diagrams/requirement/styles.js
using SplashTheMusical.Config;
using SplashTheMusical.Diagrams.Er;
using SplashTheMusical.Rendering;

namespace SplashTheMusical.Diagrams.Requirement;

/// <summary>
/// The requirement diagram's styles: the rules of <c>getStyles</c> in <c>diagrams/requirement/styles.js</c>, and the
/// classes of <c>classDef</c>.
/// </summary>
/// <remarks>
/// The template's <c>.label text,span</c> is the two selectors <c>.label text</c> and <c>span</c>. A declaration whose
/// theme variable is missing or outside <see cref="CssDeclaration"/>'s grammar is left out, as mermaid.js's stylis drops
/// an empty one.
/// </remarks>
internal static class RequirementStyles
{
	/// <summary><c>getStyles</c> with the options <c>createUserStyles</c> passes: the theme variables with the config's <c>look</c>.</summary>
	/// <param name="htmlEdgeLabels">
	/// Whether mermaid.js draws the edge labels as HTML, where the <c>.edgeLabel .label rect</c> and
	/// <c>.edgeLabel .label text</c> rules match nothing. STM draws every label as SVG text over a <c>rect</c>, so it
	/// leaves the two rules out for HTML labels.
	/// </param>
	public static List<CssRule> Rules(MermaidConfig config, bool htmlEdgeLabels)
	{
		ArgumentNullException.ThrowIfNull(config);
		var options = config.ThemeVariables;
		MermaidValue? Option(string name) => options is { Kind: MermaidValueKind.Mapping } ? options[name] : null;
		MermaidValue? Or(string first, string second) => Option(first) is { Kind: not MermaidValueKind.Null } value && (value.Kind != MermaidValueKind.String || value.String.Length > 0) ? value : Option(second);
		CssDeclaration? Theme(string property, string name) => CssDeclaration.FromTheme(property, Option(name));
		CssDeclaration? Either(string property, string first, string second) => CssDeclaration.FromTheme(property, Or(first, second));
		CssDeclaration? Fixed(string property, string value) => CssDeclaration.FromTheme(property, value);
		var text = (Func<string, CssDeclaration?>)(property => Either(property, "nodeTextColor", "textColor"));

		var rules = new List<CssRule>();
		ErStyles.AddColorSlots(rules, config, Option("borderColorArray"), Option("bkgColorArray"));
		rules.Add(Rule(["marker"], Theme("fill", "relationColor"), Theme("stroke", "relationColor")));
		rules.Add(Rule(["marker.cross"], Theme("stroke", "lineColor")));
		rules.Add(Rule(["svg"], Theme("font-family", "fontFamily"), Theme("font-size", "fontSize")));
		rules.Add(Rule([".reqBox"], Theme("fill", "requirementBackground"), Fixed("fill-opacity", "1.0"), Theme("stroke", "requirementBorderColor"), Theme("stroke-width", "requirementBorderSize")));
		rules.Add(Rule([".reqTitle", ".reqLabel"], Theme("fill", "requirementTextColor")));
		rules.Add(Rule([".reqLabelBox"], Theme("fill", "relationLabelBackground"), Fixed("fill-opacity", "1.0")));
		rules.Add(Rule([".req-title-line"], Theme("stroke", "requirementBorderColor"), Theme("stroke-width", "requirementBorderSize")));
		rules.Add(Rule([".relationshipLine"], Theme("stroke", "relationColor"), config.Look == "neo" ? Theme("stroke-width", "strokeWidth") : Fixed("stroke-width", "1px")));
		rules.Add(Rule([".relationshipLabel"], Theme("fill", "relationLabelColor")));
		rules.Add(Rule([".edgeLabel"], Theme("background-color", "edgeLabelBackground")));
		if (!htmlEdgeLabels)
		{
			rules.Add(Rule([".edgeLabel .label rect"], Theme("fill", "edgeLabelBackground")));
			rules.Add(Rule([".edgeLabel .label text"], Theme("fill", "relationLabelColor")));
		}

		rules.Add(Rule([".divider"], Theme("stroke", "nodeBorder"), Fixed("stroke-width", "1")));
		rules.Add(Rule([".label"], Theme("font-family", "fontFamily"), text("color")));
		rules.Add(Rule([".label text", "span"], text("fill"), text("color")));
		rules.Add(Rule([".labelBkg"], CssDeclaration.FromTheme("background-color", Option("requirementEdgeLabelBackground") is { Kind: not MermaidValueKind.Null } background ? background : Option("edgeLabelBackground"))));
		return rules;
	}

	/// <summary>The <c>classDef</c> classes, in the order the source first defines them.</summary>
	public static List<StyleClass> Classes(RequirementDb db)
	{
		ArgumentNullException.ThrowIfNull(db);
		return [.. db.Classes.Select(c => new StyleClass(c.Id, c.Styles, c.TextStyles))];
	}

	private static CssStyleRule Rule(string[] selectors, params ReadOnlySpan<CssDeclaration?> declarations) =>
		new([.. selectors.Select(CssSelector.Parse)], declarations);
}
