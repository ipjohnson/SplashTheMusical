namespace SplashTheMusical.Themes;

/// <summary>
/// The themes a diagram's <c>theme</c> config can name, as <c>packages/mermaid/src/themes/index.js</c> lists mermaid's.
/// A host that adds or replaces a theme supplies its own provider. It answers to <c>default</c>, which stands in for a
/// theme name no theme answers to.
/// </summary>
/// <remarks>An implementation is stateless and thread safe.</remarks>
public interface IThemeProvider
{
	/// <summary>Whether a theme answers to <paramref name="name"/>.</summary>
	bool HasTheme(string name);

	/// <summary>
	/// The variables the theme <paramref name="name"/> computes from <paramref name="overrides"/>, the
	/// <c>themeVariables</c> of the config, as mermaid's <c>getThemeVariables</c> does.
	/// </summary>
	/// <exception cref="ArgumentException">No theme answers to <paramref name="name"/>.</exception>
	MermaidValue GetThemeVariables(string name, MermaidValue? overrides);
}
