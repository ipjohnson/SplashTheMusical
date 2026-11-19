using SplashTheMusical.Config;
using SplashTheMusical.Parsing;
using SplashTheMusical.Rendering;
using SplashTheMusical.Themes;

namespace SplashTheMusical.Diagrams.Flowchart;

/// <summary>
/// The flowchart's styles: the rules of <c>getStyles</c> in <c>diagrams/flowchart/styles.ts</c>, the classes of
/// <c>FlowDB.getClasses</c>, and the style text mermaid.js's renderer writes on each node, cluster and edge.
/// </summary>
internal static class FlowchartStyles
{
	/// <summary><c>COLOR_THEMES</c> of <c>diagrams/common/colorThemeGate.ts</c>.</summary>
	private static readonly string[] ColorThemes = ["redux-color", "redux-dark-color"];

	/// <summary>
	/// <c>getStyles</c> with the options <c>createUserStyles</c> passes: the theme variables with the config's
	/// <c>theme</c> and <c>look</c>.
	/// </summary>
	public static List<CssRule> Rules(MermaidConfig config)
	{
		ArgumentNullException.ThrowIfNull(config);
		var options = config.ThemeVariables;
		MermaidValue? Option(string name) => options is { Kind: MermaidValueKind.Mapping } ? options[name] : null;
		string Text(string name) => Option(name) is { } value ? PreambleParser.ToJsString(value) : "undefined";
		MermaidValue? Or(string first, string second) => Option(first) is { } value && PreambleParser.IsTruthy(value) ? value : Option(second);
		CssDeclaration? Theme(string property, string name) => CssDeclaration.FromTheme(property, Option(name));
		CssDeclaration? Fixed(string property, string value) => CssDeclaration.FromTheme(property, value);
		MermaidValue Width(double fallback) => Option("strokeWidth") is { Kind: not MermaidValueKind.Null } width ? width : MermaidValue.FromNumber(fallback);

		var rules = new List<CssRule>();
		AddColorSlots(rules, config, Option("borderColorArray"), Option("bkgColorArray"));
		var textColor = Or("nodeTextColor", "textColor");
		rules.Add(Rule([".label"], Theme("font-family", "fontFamily"), CssDeclaration.FromTheme("color", textColor)));
		rules.Add(Rule([".cluster-label text"], Theme("fill", "titleColor")));
		rules.Add(Rule([".cluster-label span"], Theme("color", "titleColor")));
		rules.Add(Rule([".cluster-label span p"], Fixed("background-color", "transparent")));
		rules.Add(Rule([".label text", "span"], CssDeclaration.FromTheme("fill", textColor), CssDeclaration.FromTheme("color", textColor)));
		rules.Add(Rule(
			[".node rect", ".node circle", ".node ellipse", ".node polygon", ".node path"],
			Theme("fill", "mainBkg"),
			Theme("stroke", "nodeBorder"),
			CssDeclaration.FromTheme("stroke-width", Width(1), "px")));
		rules.Add(Rule([".rough-node .label text", ".node .label text", ".image-shape .label", ".icon-shape .label"], Fixed("text-anchor", "middle")));
		rules.Add(Rule([".node .katex path"], Fixed("fill", "#000"), Fixed("stroke", "#000"), Fixed("stroke-width", "1px")));
		rules.Add(Rule([".rough-node .label", ".node .label", ".image-shape .label", ".icon-shape .label"], Fixed("text-align", "center")));
		rules.Add(Rule([".node.clickable"], Fixed("cursor", "pointer")));
		rules.Add(Rule(
			[".root .anchor path"],
			CssDeclaration.FromTheme("fill", Text("lineColor"), important: true),
			Fixed("stroke-width", "0"),
			Theme("stroke", "lineColor")));
		rules.Add(Rule([".arrowheadPath"], Theme("fill", "arrowheadColor")));
		rules.Add(Rule([".edgePaths .path"], Theme("stroke", "lineColor"), CssDeclaration.FromTheme("stroke-width", Width(2), "px")));
		rules.Add(Rule([".flowchart-link"], Theme("stroke", "lineColor"), Fixed("fill", "none")));
		rules.Add(Rule([".edgeLabel"], Theme("background-color", "edgeLabelBackground"), Fixed("text-align", "center")));
		rules.Add(Rule([".edgeLabel p"], Theme("background-color", "edgeLabelBackground")));
		rules.Add(Rule([".edgeLabel rect"], Fixed("opacity", "0.5"), Theme("background-color", "edgeLabelBackground"), Theme("fill", "edgeLabelBackground")));
		rules.Add(Rule([".labelBkg"], Fade(Text("edgeLabelBackground"))));
		rules.Add(Rule([".cluster rect"], Theme("fill", "clusterBkg"), Theme("stroke", "clusterBorder"), Fixed("stroke-width", "1px")));
		rules.Add(Rule([".cluster text"], Theme("fill", "titleColor")));
		rules.Add(Rule([".cluster span"], Theme("color", "titleColor")));
		rules.Add(Rule([".node .collapsed-indicator"], Theme("fill", "clusterBorder"), Fixed("stroke", "none"), Fixed("opacity", "0.6")));
		rules.Add(Rule([".node .collapsed-separator"], Theme("stroke", "clusterBorder"), Fixed("stroke-width", "0.75px")));
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
		rules.Add(Rule(["rect.text"], Fixed("fill", "none"), Fixed("stroke-width", "0")));
		rules.Add(Rule([".icon-shape", ".image-shape"], Theme("background-color", "edgeLabelBackground"), Fixed("text-align", "center")));
		rules.Add(Rule([".icon-shape p", ".image-shape p"], Theme("background-color", "edgeLabelBackground"), Fixed("padding", "2px")));
		rules.Add(Rule(
			[".icon-shape .label rect", ".image-shape .label rect"],
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

	/// <summary>Whether <c>stampColorSlot</c> gives a node a colour slot: the theme is a colour theme with a border palette.</summary>
	public static bool IsColorTheme(MermaidConfig config)
	{
		ArgumentNullException.ThrowIfNull(config);
		return PaletteLength(config) > 0 && Array.IndexOf(ColorThemes, config.Theme) >= 0;
	}

	/// <summary>The length of <c>borderColorArray</c>, or 0 when it is not a list.</summary>
	public static int PaletteLength(MermaidConfig config)
	{
		ArgumentNullException.ThrowIfNull(config);
		return config.ThemeVariables is { Kind: MermaidValueKind.Mapping } options && options["borderColorArray"] is { Kind: MermaidValueKind.Sequence } palette
			? palette.Items.Count
			: 0;
	}

	/// <summary><c>FlowDB.getClasses</c>: the <c>classDef</c> classes, in the order the source first defines them.</summary>
	public static List<StyleClass> Classes(FlowchartDocument document)
	{
		ArgumentNullException.ThrowIfNull(document);
		return [.. document.Classes.Select(c => new StyleClass(c.Id, c.Styles, c.TextStyles))];
	}

	/// <summary>The value <c>compileStyles</c>' <c>stylesMap</c> gives <paramref name="property"/>: the last of the class styles and the node's own, or <c>null</c>.</summary>
	public static string? StyleValue(IReadOnlyList<string> compiled, IReadOnlyList<string> own, string property)
	{
		string? value = null;
		foreach (var style in compiled.Concat(own))
		{
			var parts = style.Split(':');
			if (parts[0].Trim() == property)
			{
				value = parts.Length > 1 ? parts[1].Trim() : null;
			}
		}

		return value;
	}

	/// <summary>
	/// <c>styles2String</c>: the compiled styles, then the node's own, then the label style, keyed by property in the
	/// order each first appears, each kept with its last value. Label properties go to the label's style and the rest to
	/// the node's, each with <c>!important</c>, joined by <c>;</c>.
	/// </summary>
	public static (string NodeStyles, string LabelStyles) NodeStyles(IReadOnlyList<string> compiled, IReadOnlyList<string> own)
	{
		ArgumentNullException.ThrowIfNull(compiled);
		ArgumentNullException.ThrowIfNull(own);
		var map = new OrderedDictionary<string, string?>(StringComparer.Ordinal);
		foreach (var style in compiled.Concat(own))
		{
			// styles2Map keeps the text between the first and second colons as the value.
			var parts = style.Split(':');
			map[parts[0].Trim()] = parts.Length > 1 ? parts[1].Trim() : null;
		}

		var label = new List<string>();
		var node = new List<string>();
		foreach (var (key, value) in map)
		{
			(IsLabelStyle(key) ? label : node).Add(key + ":" + value + " !important");
		}

		return (string.Join(';', node), string.Join(';', label));
	}

	/// <summary>
	/// The <c>style</c> attribute <c>insertEdge</c> writes on an edge's path in the classic look, which the neo look
	/// prefixes with a dash array: the class styles, then the edge's styles, each ended by <c>;</c>, then the edge's
	/// styles again, each after a <c>;</c>.
	/// </summary>
	public static string EdgeStyle(IReadOnlyList<string> compiled, IReadOnlyList<string> styles)
	{
		ArgumentNullException.ThrowIfNull(compiled);
		ArgumentNullException.ThrowIfNull(styles);
		var fromClasses = string.Join(';', compiled);
		var own = string.Concat(styles.Select(s => s + ";"));
		var again = string.Concat(styles.Select(s => ";" + s));
		return (fromClasses.Length > 0 ? fromClasses + ";" + own + ";" : own) + ";" + again;
	}

	/// <summary>The stroke colour <c>insertEdge</c> reads from the path's style: the text after its first <c>stroke:</c>, up to a <c>;</c>.</summary>
	public static string? StrokeColor(string edgeStyle)
	{
		ArgumentNullException.ThrowIfNull(edgeStyle);
		var at = edgeStyle.IndexOf("stroke:", StringComparison.Ordinal);
		if (at < 0)
		{
			return null;
		}

		var value = edgeStyle.AsSpan(at + "stroke:".Length);
		var end = value.IndexOf(';');
		return end == 0 ? null : (end < 0 ? value : value[..end]).ToString();
	}

	/// <summary>The declarations of a style text, split at each <c>;</c>, as checked rules, with <c>color</c> read as <c>fill</c> when <paramref name="colorAsFill"/>.</summary>
	public static CssDeclaration?[] Declarations(string styles, bool important, bool colorAsFill)
	{
		ArgumentNullException.ThrowIfNull(styles);
		var declarations = new List<CssDeclaration?>();
		foreach (var range in styles.AsSpan().Split(';'))
		{
			var style = styles.AsSpan()[range].Trim();
			if (style.EndsWith("!important", StringComparison.Ordinal))
			{
				style = style[..^"!important".Length].TrimEnd();
			}

			if (style.IsEmpty)
			{
				continue;
			}

			if (colorAsFill && style.StartsWith("color", StringComparison.Ordinal) && style[5..].TrimStart().StartsWith(':'))
			{
				declarations.Add(CssDeclaration.FromSource(string.Concat("fill", style[5..]), important));
			}
			else
			{
				declarations.Add(CssDeclaration.FromSource(style, important));
			}
		}

		return [.. declarations];
	}

	/// <summary>
	/// Rules that stand in for the <c>style</c> attributes mermaid.js writes, which the SVG writer does not write. Each
	/// node, cluster, edge and edge label that has styles gets a class <c>stm-style-N</c>, numbered in drawing order, and
	/// a rule for it. A node's or cluster's styles keep their <c>!important</c>, as the attribute does, and a label's
	/// colour paints its SVG text.
	/// </summary>
	public static List<CssRule> ElementRules(FlowchartDrawing root)
	{
		ArgumentNullException.ThrowIfNull(root);
		var rules = new List<CssRule>();
		var count = 0;
		string Next() => "stm-style-" + (count++).ToString(System.Globalization.CultureInfo.InvariantCulture);
		void Add(string cls, string[] selectors, string styles, bool important, bool colorAsFill) =>
			rules.Add(new CssStyleRule([.. selectors.Select(s => CssSelector.Parse("." + cls + s))], Declarations(styles, important, colorAsFill)));

		void Walk(FlowchartDrawing drawing)
		{
			foreach (var cluster in drawing.Clusters)
			{
				if (cluster.NodeStyles.Length > 0 || cluster.LabelStyles.Length > 0)
				{
					var cls = cluster.StyleClass = Next();
					Add(cls, [">rect"], cluster.NodeStyles, important: true, colorAsFill: false);
					Add(cls, [">.cluster-label text"], cluster.LabelStyles, important: true, colorAsFill: true);
				}
			}

			foreach (var edge in drawing.Edges)
			{
				if (edge.PathStyle.Trim(';').Length > 0)
				{
					Add(edge.StyleClass = Next(), [""], edge.PathStyle, important: false, colorAsFill: false);
				}

				var label = string.Join(';', edge.LabelStyles.Split(';').Where(s => s.Split(':')[0].Trim() is var key && IsLabelStyle(key)));
				if (label.Length > 0 && edge.Label is { HasText: true })
				{
					Add(edge.LabelStyleClass = Next(), [" text"], label, important: false, colorAsFill: true);
				}
			}

			foreach (var node in drawing.Nodes)
			{
				if (node.Subgraph is { } subgraph)
				{
					Walk(subgraph);
				}
				else if (node.NodeStyles.Length > 0 || node.LabelStyles.Length > 0)
				{
					var cls = node.StyleClass = Next();
					Add(cls, [">:not(.label)", ">:not(.label) *"], node.NodeStyles, important: true, colorAsFill: false);
					Add(cls, [">.label text"], node.LabelStyles, important: true, colorAsFill: true);
				}
			}
		}

		Walk(root);
		return rules;
	}

	/// <summary><c>isLabelStyle</c> of <c>handDrawnShapeStyles.ts</c>.</summary>
	private static bool IsLabelStyle(string key) => key is "color" or "font-size" or "font-family" or "font-weight" or "font-style"
		or "text-decoration" or "text-align" or "text-transform" or "line-height" or "letter-spacing" or "word-spacing" or "text-shadow"
		or "text-overflow" or "white-space" or "word-wrap" or "word-break" or "overflow-wrap" or "hyphens";

	// genColor: a colour theme's border and background palettes, by the data-color-id stampColorSlot gives clusters.
	private static void AddColorSlots(List<CssRule> rules, MermaidConfig config, MermaidValue? borders, MermaidValue? backgrounds)
	{
		if (!IsColorTheme(config) || borders is not { Kind: MermaidValueKind.Sequence })
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
			string[] Collapsed(string suffix) => [$"{slot}.node {suffix}", $"{slot}.rough-node {suffix}"];
			string[] Lane(string suffix) => [$"{slot}.swimlane.cluster .swimlane-title{suffix}", $"{slot}.swimlane.cluster .swimlane-body{suffix}"];
			rules.Add(Rule([slot + ".cluster:not(.swimlane) rect"], Stroke(), Fill()));
			rules.Add(Rule([slot + ".cluster:not(.swimlane) path"], Stroke(), Fill()));
			rules.Add(Rule([slot + ".swimlane.cluster rect.swimlane-title", slot + ".swimlane.cluster rect.swimlane-body"], Stroke(), Fill()));
			rules.Add(Rule(Lane(" path:nth-of-type(2)"), Stroke()));
			if (fill is not null)
			{
				rules.Add(Rule(Lane(" path:first-of-type"), CssDeclaration.FromTheme("stroke", fill)));
			}

			rules.Add(Rule([.. Collapsed(".collapsed-group"), .. Collapsed(".collapsed-group path")], Stroke(), Fill()));
			rules.Add(Rule(Collapsed(".collapsed-indicator"), CssDeclaration.FromTheme("fill", border)));
			rules.Add(Rule(Collapsed(".collapsed-separator"), Stroke()));
		}
	}

	// fade: rgba() of the colour's channels at half opacity. khroma throws on a colour it cannot read, and the rule is
	// left without the declaration.
	private static CssDeclaration? Fade(string color)
	{
		try
		{
			return CssDeclaration.FromTheme("background-color", Khroma.Fade(color, 0.5));
		}
		catch (FormatException)
		{
			return null;
		}
	}

	private static CssStyleRule Rule(string[] selectors, params ReadOnlySpan<CssDeclaration?> declarations) =>
		new([.. selectors.Select(CssSelector.Parse)], declarations);
}
