using SplashTheMusical.Parsing;
using SplashTheMusical.Themes;

namespace SplashTheMusical.Config;

public sealed partial class MermaidConfig
{
	/// <summary>
	/// The config mermaid.js draws <paramref name="document"/> with. The default config comes first, then
	/// <paramref name="siteConfig"/>, then the front matter, then the directives. A key of the site config's
	/// <c>secure</c> list, and <c>secure</c> itself, keep their site value, so a diagram cannot change them.
	/// </summary>
	/// <param name="siteConfig">The options the host gives, as mermaid.js's <c>initialize</c> takes them, or <c>null</c>.</param>
	/// <param name="document">The diagram, whose front matter and directives are read.</param>
	/// <param name="diagramType">The id of mermaid.js's detector for the diagram, such as <c>flowchart-v2</c> or <c>pie</c>.</param>
	/// <remarks>
	/// A value whose type does not fit its property reads as <c>null</c>. Keys that config.schema.yaml does not have, such
	/// as <c>treemap</c>, are dropped. <see cref="ThemeVariables"/> holds the variables the theme computes, as mermaid.js
	/// computes them.
	/// </remarks>
	/// <exception cref="FormatException">A theme variable that the theme passes to a colour function is not a colour.</exception>
	public static MermaidConfig Resolve(MermaidValue? siteConfig, MermaidDocument document, string diagramType) =>
		Resolve(siteConfig, document, diagramType, MermaidThemes.Default);

	/// <inheritdoc cref="Resolve(MermaidValue?, MermaidDocument, string)"/>
	/// <param name="themes">The themes a <c>theme</c> can name.</param>
	public static MermaidConfig Resolve(MermaidValue? siteConfig, MermaidDocument document, string diagramType, IThemeProvider themes)
	{
		ArgumentNullException.ThrowIfNull(document);
		ArgumentNullException.ThrowIfNull(diagramType);
		ArgumentNullException.ThrowIfNull(themes);
		return FromValue(ConfigMerge.Apply(siteConfig, PreambleConfig.Resolve(document, diagramType), diagramType, themes));
	}
}
