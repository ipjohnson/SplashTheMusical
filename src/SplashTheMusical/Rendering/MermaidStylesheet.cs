using SplashTheMusical.Config;
using SplashTheMusical.Parsing;

namespace SplashTheMusical.Rendering;

/// <summary>
/// The stylesheet of mermaid's <c>createUserStyles</c>: the rules every diagram shares, built from the theme variables
/// by <c>getStyles</c> in <c>packages/mermaid/src/styles.ts</c>, the diagram's own rules, the rules for looks, and the
/// rules <c>createCssStyles</c> in <c>mermaidAPI.ts</c> writes for the font families and each <c>classDef</c>.
/// </summary>
/// <remarks>
/// <c>themeCSS</c> is not written: it is CSS text that only a full parser could check. A theme variable or
/// <c>classDef</c> declaration that <see cref="CssDeclaration"/> refuses is left out, where mermaid.js copies it.
/// </remarks>
internal static class MermaidStylesheet
{
	private const string DropShadow = "url(#drop-shadow)";

	/// <param name="config">The resolved config, whose <see cref="MermaidConfig.ThemeVariables"/> fill the templates.</param>
	/// <param name="diagramRules">The rules of the diagram type's own <c>styles</c> function.</param>
	/// <param name="classes">The classes the diagram defines.</param>
	public static List<CssRule> Build(MermaidConfig config, IReadOnlyList<CssRule> diagramRules, IReadOnlyList<StyleClass> classes)
	{
		ArgumentNullException.ThrowIfNull(config);
		ArgumentNullException.ThrowIfNull(diagramRules);
		ArgumentNullException.ThrowIfNull(classes);
		var options = config.ThemeVariables;
		MermaidValue? Option(string name) => options?[name];
		var strokeWidth = Option("strokeWidth") is { Kind: not MermaidValueKind.Null } width ? width : MermaidValue.FromNumber(1);
		var rules = new List<CssRule>
		{
			new CssStyleRule(
				CssSelector.Root,
				CssDeclaration.FromTheme("font-family", Option("fontFamily")),
				CssDeclaration.FromTheme("font-size", Option("fontSize")),
				CssDeclaration.FromTheme("fill", Option("textColor"))),
			new CssKeyframesRule("edge-animation-frame", ("from", [CssDeclaration.FromTheme("stroke-dashoffset", "0")])),
			new CssKeyframesRule("dash", ("to", [CssDeclaration.FromTheme("stroke-dashoffset", "0")])),
			EdgeAnimation(".edge-animation-slow", "dash 50s linear infinite"),
			EdgeAnimation(".edge-animation-fast", "dash 20s linear infinite"),
			Rule(".error-icon", CssDeclaration.FromTheme("fill", Option("errorBkgColor"))),
			Rule(".error-text", CssDeclaration.FromTheme("fill", Option("errorTextColor")), CssDeclaration.FromTheme("stroke", Option("errorTextColor"))),
			Rule(".edge-thickness-normal", CssDeclaration.FromTheme("stroke-width", strokeWidth, "px")),
			Rule(".edge-thickness-thick", CssDeclaration.FromTheme("stroke-width", "3.5px")),
			Rule(".edge-pattern-solid", CssDeclaration.FromTheme("stroke-dasharray", "0")),
			Rule(".edge-thickness-invisible", CssDeclaration.FromTheme("stroke-width", "0"), CssDeclaration.FromTheme("fill", "none")),
			Rule(".edge-pattern-dashed", CssDeclaration.FromTheme("stroke-dasharray", "3")),
			Rule(".edge-pattern-dotted", CssDeclaration.FromTheme("stroke-dasharray", "2")),
			Rule(".marker", CssDeclaration.FromTheme("fill", Option("lineColor")), CssDeclaration.FromTheme("stroke", Option("lineColor"))),
			Rule(".marker.cross", CssDeclaration.FromTheme("stroke", Option("lineColor"))),
			Rule("svg", CssDeclaration.FromTheme("font-family", Option("fontFamily")), CssDeclaration.FromTheme("font-size", Option("fontSize"))),
			Rule("p", CssDeclaration.FromTheme("margin", "0")),
		};
		rules.AddRange(diagramRules);

		var nodeBorder = Option("nodeBorder");
		var useGradient = Option("useGradient") is { } gradient && PreambleParser.IsTruthy(gradient);
		CssDeclaration? Border(string property) =>
			useGradient ? CssDeclaration.WithReference(property, string.Empty, "-gradient", string.Empty) : CssDeclaration.FromTheme(property, nodeBorder);
		var shadow = Shadow(Option("dropShadow"));
		const string Neo = "[data-look=\"neo\"]";
		rules.Add(Rule(".node .neo-node", CssDeclaration.FromTheme("stroke", nodeBorder)));
		rules.Add(new CssStyleRule(
			[CssSelector.Parse(Neo + ".node rect"), CssSelector.Parse(Neo + ".cluster rect"), CssSelector.Parse(Neo + ".node polygon")],
			Border("stroke"),
			shadow));
		rules.Add(Rule(Neo + ".swimlane.cluster rect", CssDeclaration.FromTheme("filter", "none")));
		rules.Add(Rule(Neo + ".node path", Border("stroke"), CssDeclaration.FromTheme("stroke-width", strokeWidth, "px")));
		rules.Add(Rule(Neo + ".node .outer-path", shadow));
		rules.Add(Rule(Neo + ".node .neo-line path", CssDeclaration.FromTheme("stroke", nodeBorder), CssDeclaration.FromTheme("filter", "none")));
		rules.Add(Rule(Neo + ".node circle", Border("stroke"), shadow));
		rules.Add(Rule(Neo + ".node circle .state-start", CssDeclaration.FromTheme("fill", "#000000")));
		rules.Add(Rule(Neo + ".icon-shape .icon", Border("fill"), shadow));
		rules.Add(Rule(Neo + ".icon-shape .icon-neo path", Border("stroke"), shadow));

		AddUserRules(rules, config, classes);
		return rules;
	}

	private static CssStyleRule Rule(string selector, params ReadOnlySpan<CssDeclaration?> declarations) =>
		new(CssSelector.Parse(selector), declarations);

	private static CssStyleRule EdgeAnimation(string selector, string animation) => Rule(
		selector,
		CssDeclaration.FromTheme("stroke-dasharray", "9,5", important: true),
		CssDeclaration.FromTheme("stroke-dashoffset", "900"),
		CssDeclaration.FromTheme("animation", animation),
		CssDeclaration.FromTheme("stroke-linecap", "round"));

	// options.dropShadow, with its first url(#drop-shadow) pointed at this SVG's filter, or none when it is falsy. A
	// truthy value that is not a string has no replace method, so mermaid.js fails there.
	private static CssDeclaration? Shadow(MermaidValue? dropShadow)
	{
		if (dropShadow is null || !PreambleParser.IsTruthy(dropShadow))
		{
			return CssDeclaration.FromTheme("filter", "none");
		}

		if (dropShadow.Kind != MermaidValueKind.String)
		{
			return null;
		}

		var text = dropShadow.String;
		var at = text.IndexOf(DropShadow, StringComparison.Ordinal);
		return at < 0
			? CssDeclaration.FromTheme("filter", text)
			: CssDeclaration.WithReference("filter", text[..at], "-drop-shadow", text[(at + DropShadow.Length)..]);
	}

	// createCssStyles without themeCSS. The classDef rules go to the label's HTML elements, or to the shapes when labels
	// are SVG text, and text styles go to tspan with their first "color" read as "fill".
	private static void AddUserRules(List<CssRule> rules, MermaidConfig config, IReadOnlyList<StyleClass> classes)
	{
		if (config.FontFamily is not null)
		{
			rules.Add(Rule(":root", CssDeclaration.FromTheme("--mermaid-font-family", config.FontFamily)));
		}

		if (config.AltFontFamily is not null)
		{
			rules.Add(Rule(":root", CssDeclaration.FromTheme("--mermaid-alt-font-family", config.AltFontFamily)));
		}

		string[] elements = config.HtmlLabels ?? config.Flowchart.HtmlLabels ?? true
			? ["> *", "span"]
			: ["rect", "polygon", "ellipse", "circle", "path"];
		foreach (var styleClass in classes)
		{
			if (styleClass.Styles.Count > 0)
			{
				var declarations = Important(styleClass.Styles, toFill: false);
				foreach (var element in elements)
				{
					if (CssSelector.TryClass(styleClass.Id, element, out var selector))
					{
						rules.Add(new CssStyleRule(selector, declarations));
					}
				}
			}

			if (styleClass.TextStyles.Count > 0 && CssSelector.TryClass(styleClass.Id, "tspan", out var tspan))
			{
				rules.Add(new CssStyleRule(tspan, Important(styleClass.TextStyles, toFill: true)));
			}
		}
	}

	private static CssDeclaration?[] Important(IReadOnlyList<string> styles, bool toFill)
	{
		var declarations = new CssDeclaration?[styles.Count];
		for (var i = 0; i < styles.Count; i++)
		{
			var style = styles[i];
			var color = toFill ? style.IndexOf("color", StringComparison.Ordinal) : -1;
			declarations[i] = CssDeclaration.FromSource(color < 0 ? style : string.Concat(style.AsSpan(0, color), "fill", style.AsSpan(color + 5)), important: true);
		}

		return declarations;
	}
}
