// Upstream: packages/mermaid/src/diagrams/sequence/styles.js
using System.Globalization;
using SplashTheMusical.Config;
using SplashTheMusical.Parsing;
using SplashTheMusical.Rendering;

namespace SplashTheMusical.Diagrams.Sequence;

/// <summary>
/// The sequence diagram's styles: the rules of <c>getStyles</c> in <c>diagrams/sequence/styles.js</c>, and the rules of
/// the palette colours <c>svgDraw.js</c> sets as inline styles under the colour themes.
/// </summary>
/// <remarks>
/// The template's <c>//stroke</c> line in <c>.note</c> is a comment to stylis, so it is left out. A
/// <c>dropShadow</c> that is a <c>url()</c> names a filter the sequence SVG does not have, so the browser draws no
/// shadow, and STM leaves the declaration out. <c>box-shadow</c> is outside the allowlist of
/// <see cref="CssDeclaration"/> and is left out too.
/// <para>
/// <c>drawMessage</c> gives every dashed message, which is every <c>messageLine1</c>, the inline style
/// <c>stroke-dasharray: 3, 3</c>, and <c>drawLoop</c> gives each branch line the same. An inline style outranks the
/// template's <c>2, 2</c>, so the <c>.messageLine1</c> rule has <c>3, 3</c>, and a rule for the <c>loopLine</c>s after
/// the four of a block's frame stands in for the branch lines' style.
/// </para>
/// </remarks>
internal static class SequenceStyles
{
	private static readonly string[] ColorThemes = ["redux-color", "redux-dark-color"];

	/// <summary><c>getStyles</c> with the options <c>createUserStyles</c> passes: the theme variables and the config's <c>look</c>.</summary>
	public static List<CssRule> Rules(MermaidConfig config)
	{
		ArgumentNullException.ThrowIfNull(config);
		var options = config.ThemeVariables;
		MermaidValue? Option(string name) => options is { Kind: MermaidValueKind.Mapping } ? options[name] : null;
		CssDeclaration? Theme(string property, string name) => CssDeclaration.FromTheme(property, Option(name));
		CssDeclaration? Fixed(string property, string value) => CssDeclaration.FromTheme(property, value);
		var shadow = Shadow(Option("dropShadow"));
		var strokeWidth = Option("strokeWidth") is { Kind: not MermaidValueKind.Null } width ? width : MermaidValue.FromNumber(1);
		var neo = config.Look == "neo";
		return
		[
			Rule([".actor"], Theme("stroke", "actorBorder"), Theme("fill", "actorBkg"), CssDeclaration.FromTheme("stroke-width", strokeWidth)),
			Rule(["rect.actor.outer-path[data-look=\"neo\"]"], shadow),
			Rule(["rect.note[data-look=\"neo\"]"], Theme("stroke", "noteBorderColor"), Theme("fill", "noteBkgColor"), shadow),
			Rule(["text.actor > tspan"], Theme("fill", "actorTextColor"), Fixed("stroke", "none")),
			Rule([".actor-line"], Theme("stroke", "actorLineColor")),
			Rule([".innerArc"], Fixed("stroke-width", "1.5"), Fixed("stroke-dasharray", "none")),
			Rule([".messageLine0"], Fixed("stroke-width", "1.5"), Fixed("stroke-dasharray", "none"), Theme("stroke", "signalColor")),
			Rule([".messageLine1"], Fixed("stroke-width", "1.5"), Fixed("stroke-dasharray", "3, 3"), Theme("stroke", "signalColor")),
			Rule(["[id$=\"-arrowhead\"] path"], Theme("fill", "signalColor"), Theme("stroke", "signalColor")),
			Rule([".sequenceNumber"], Theme("fill", "sequenceNumberColor")),
			Rule(["[id$=\"-sequencenumber\"]"], Theme("fill", "signalColor")),
			Rule(["[id$=\"-crosshead\"] path"], Theme("fill", "signalColor"), Theme("stroke", "signalColor")),
			Rule([".messageText"], Theme("fill", "signalTextColor"), Fixed("stroke", "none")),
			Rule([".labelBox"], Theme("stroke", "labelBoxBorderColor"), Theme("fill", "labelBoxBkgColor"), neo ? shadow : Fixed("filter", "none")),
			Rule([".labelText", ".labelText > tspan"], Theme("fill", "labelTextColor"), Fixed("stroke", "none")),
			Rule([".loopText", ".loopText > tspan"], Theme("fill", "loopTextColor"), Fixed("stroke", "none")),
			Rule([".sectionTitle", ".sectionTitle > tspan"], Theme("fill", "loopTextColor"), Fixed("stroke", "none")),
			Rule([".loopLine"], Fixed("stroke-width", "2px"), Fixed("stroke-dasharray", "2, 2"), Theme("stroke", "labelBoxBorderColor"), Theme("fill", "labelBoxBorderColor")),
			Rule([".note"], Theme("stroke", "noteBorderColor"), Theme("fill", "noteBkgColor")),
			Rule([".noteText", ".noteText > tspan"], Theme("fill", "noteTextColor"), Fixed("stroke", "none")),
			Rule([".activation0"], Theme("fill", "activationBkgColor"), Theme("stroke", "activationBorderColor")),
			Rule([".activation1"], Theme("fill", "activationBkgColor"), Theme("stroke", "activationBorderColor")),
			Rule([".activation2"], Theme("fill", "activationBkgColor"), Theme("stroke", "activationBorderColor")),
			Rule([".actorPopupMenu"], Fixed("position", "absolute")),
			Rule([".actorPopupMenuPanel"], Fixed("position", "absolute"), Theme("fill", "actorBkg"), Fixed("filter", "drop-shadow(3px 5px 2px rgb(0 0 0 / 0.4))")),
			Rule([".actor-man circle", "line"], Theme("fill", "actorBkg"), Fixed("stroke-width", "2px")),
			Rule(["g rect.rect"], shadow, Theme("stroke", "nodeBorder")),
			Rule(["line.loopLine:nth-of-type(n+5)"], Fixed("stroke-dasharray", "3, 3")),
		];
	}

	/// <summary>
	/// The styling the layout carries: the rules, the palette rules of <paramref name="palette"/>, and the theme values the
	/// renderer writes as attributes.
	/// </summary>
	public static SequenceStyling Styling(MermaidConfig config, SequencePalette palette)
	{
		ArgumentNullException.ThrowIfNull(config);
		ArgumentNullException.ThrowIfNull(palette);
		var options = config.ThemeVariables;
		string? Option(string name) => options is { Kind: MermaidValueKind.Mapping } && options[name] is { Kind: not MermaidValueKind.Null } value && PreambleParser.IsTruthy(value)
			? PreambleParser.ToJsString(value)
			: null;
		return new SequenceStyling(
			Rules(config),
			palette.Rules,
			config.Theme ?? "default",
			Option("rectBkgColor") ?? Option("actorBkg") ?? "rgba(128, 128, 128, 0.5)",
			Option("actorBorder"),
			Option("actorBkg"));
	}

	/// <summary><c>options.dropShadow ?? 'none'</c>, without a <c>url()</c>, which names no filter of this SVG.</summary>
	private static CssDeclaration? Shadow(MermaidValue? dropShadow)
	{
		if (dropShadow is null || dropShadow.Kind == MermaidValueKind.Null)
		{
			return CssDeclaration.FromTheme("filter", "none");
		}

		var text = PreambleParser.ToJsString(dropShadow);
		return text.Contains("url(", StringComparison.Ordinal) ? null : CssDeclaration.FromTheme("filter", text);
	}

	private static CssStyleRule Rule(string[] selectors, params ReadOnlySpan<CssDeclaration?> declarations) =>
		new([.. selectors.Select(CssSelector.Parse)], declarations);

	/// <summary>Whether <c>svgDraw.js</c>'s <c>COLOR_THEMES</c> holds the config's theme.</summary>
	internal static bool IsColorTheme(MermaidConfig config) => Array.IndexOf(ColorThemes, config.Theme) >= 0;
}

/// <summary>
/// The palette colours of <c>paletteColor</c>, which <c>svgDraw.js</c> sets as inline <c>stroke</c> and <c>fill</c>
/// styles on a participant's shapes and on an activation under the colour themes. STM gives each element a class
/// <c>stm-style-N</c> instead, with a rule that goes after the diagram's rules. A palette that is missing or empty sets
/// nothing, so the stylesheet decides, except that an activation falls back to <c>mainBkg</c>.
/// </summary>
internal sealed class SequencePalette
{
	private readonly IReadOnlyList<MermaidValue>? _borders;
	private readonly IReadOnlyList<MermaidValue>? _backgrounds;
	private readonly MermaidValue? _mainBackground;
	private readonly Dictionary<(int Index, bool Activation), string?> _classes = [];
	private readonly List<CssRule> _rules = [];

	public SequencePalette(MermaidConfig config)
	{
		ArgumentNullException.ThrowIfNull(config);
		var options = config.ThemeVariables;
		MermaidValue? Option(string name) => options is { Kind: MermaidValueKind.Mapping } ? options[name] : null;
		if (!SequenceStyles.IsColorTheme(config))
		{
			return;
		}

		_borders = Option("borderColorArray") is { Kind: MermaidValueKind.Sequence, Items.Count: > 0 } borders ? borders.Items : null;
		_backgrounds = Option("bkgColorArray") is { Kind: MermaidValueKind.Sequence, Items.Count: > 0 } backgrounds ? backgrounds.Items : null;
		_mainBackground = Option("mainBkg");
		Enabled = true;
	}

	/// <summary>Whether the theme is a colour theme, so the shapes get palette classes.</summary>
	public bool Enabled { get; }

	public IReadOnlyList<CssRule> Rules => _rules;

	/// <summary>The class of the <paramref name="index"/>th participant's colours, or <c>null</c> when it has none.</summary>
	public string? Actor(int index) => ClassOf(index, activation: false);

	/// <summary>The class of the colours of an activation of the <paramref name="index"/>th participant, or <c>null</c>.</summary>
	public string? Activation(int index) => ClassOf(index, activation: true);

	private string? ClassOf(int index, bool activation)
	{
		if (!Enabled)
		{
			return null;
		}

		if (_classes.TryGetValue((index, activation), out var known))
		{
			return known;
		}

		var stroke = _borders is { } borders ? CssDeclaration.FromTheme("stroke", borders[index % borders.Count]) : null;
		var fillValue = _backgrounds is { } backgrounds ? backgrounds[index % backgrounds.Count] : activation ? _mainBackground : null;
		var fill = fillValue is null ? null : CssDeclaration.FromTheme("fill", fillValue);
		string? name = null;
		if (stroke is not null || fill is not null)
		{
			name = "stm-style-" + _rules.Count.ToString(CultureInfo.InvariantCulture);
			_rules.Add(new CssStyleRule(CssSelector.Parse("." + name), stroke, fill));
		}

		_classes[(index, activation)] = name;
		return name;
	}
}
