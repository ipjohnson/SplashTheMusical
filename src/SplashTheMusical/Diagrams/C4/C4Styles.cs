// Upstream: packages/mermaid/src/diagrams/c4/styles.js
using SplashTheMusical.Config;
using SplashTheMusical.Rendering;

namespace SplashTheMusical.Diagrams.C4;

/// <summary>
/// The C4 diagram's styles: the rules of <c>getStyles</c> in <c>diagrams/c4/styles.js</c>, with the font rule of each
/// element type that <c>elementFontStyles</c> builds from the <c>c4</c> config.
/// </summary>
/// <remarks>
/// <c>elementFontStyles</c> sets each property through the CSSOM, which drops a value outside the property's grammar,
/// and leaves out a rule with no property. <see cref="CssDeclaration"/> drops such a value too.
/// </remarks>
internal static class C4Styles
{
	/// <summary>The element types in the order of <c>C4_ELEMENT_TYPES</c>: each type, then its external form.</summary>
	internal static readonly string[] ElementTypes =
	[
		"person", "external_person", "system", "external_system", "system_db", "external_system_db", "system_queue", "external_system_queue",
		"container", "external_container", "container_db", "external_container_db", "container_queue", "external_container_queue",
		"component", "external_component", "component_db", "external_component_db", "component_queue", "external_component_queue",
	];

	public static List<CssRule> Rules(MermaidConfig config)
	{
		ArgumentNullException.ThrowIfNull(config);
		var options = config.ThemeVariables;
		MermaidValue? Option(string name) => options is { Kind: MermaidValueKind.Mapping } ? options[name] : null;
		var rules = new List<CssRule>
		{
			Rule([".person"], CssDeclaration.FromTheme("stroke", Option("personBorder")), CssDeclaration.FromTheme("fill", Option("personBkg"))),
		};

		var c4 = config.C4.ToValue();
		foreach (var type in ElementTypes)
		{
			var declarations = new List<CssDeclaration?>(3);
			if (c4[type + "FontFamily"] is { } family && Truthy(family))
			{
				declarations.Add(CssDeclaration.FromTheme("font-family", family));
			}

			if (c4[type + "FontSize"] is { } size && Truthy(size))
			{
				declarations.Add(CssDeclaration.FromTheme("font-size", size, size.Kind == MermaidValueKind.Number ? "px" : ""));
			}

			if (c4[type + "FontWeight"] is { } weight && Truthy(weight))
			{
				declarations.Add(CssDeclaration.FromTheme("font-weight", weight));
			}

			if (declarations.Any(d => d is not null))
			{
				rules.Add(Rule([".c4-shape.c4-" + type + " .label"], [.. declarations]));
			}
		}

		rules.Add(Rule([".c4-shape .label", ".c4-shape .label text"], Fixed("color", "inherit"), Fixed("fill", "currentColor")));
		rules.Add(Rule([".c4-shape .label .c4-name"], Fixed("font-weight", "bold")));
		rules.Add(Rule([".c4-shape .label .c4-type"], Fixed("font-size", "0.75em")));
		rules.Add(Rule([".c4-shape .label .c4-descr"], Fixed("font-size", "0.82em")));
		rules.Add(Rule(
			[".c4-shape .basic", ".c4-shape rect", ".c4-shape path", ".c4-shape circle", ".c4-shape ellipse", ".c4-shape line"],
			Fixed("stroke-width", "2px")));
		return rules;
	}

	private static bool Truthy(MermaidValue value) => value.Kind switch
	{
		MermaidValueKind.String => value.String.Length > 0,
		MermaidValueKind.Number => value.Number != 0 && !double.IsNaN(value.Number),
		MermaidValueKind.Boolean => value.Boolean,
		MermaidValueKind.Null => false,
		_ => true,
	};

	private static CssDeclaration? Fixed(string property, string value) => CssDeclaration.FromTheme(property, value);

	private static CssStyleRule Rule(string[] selectors, params ReadOnlySpan<CssDeclaration?> declarations) =>
		new([.. selectors.Select(CssSelector.Parse)], declarations);
}
