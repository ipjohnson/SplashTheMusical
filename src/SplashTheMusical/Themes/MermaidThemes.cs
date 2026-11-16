namespace SplashTheMusical.Themes;

/// <summary>The 11 themes of mermaid 12.3.0: <c>base</c>, <c>dark</c>, <c>default</c>, <c>forest</c>, <c>neo</c>, <c>neo-dark</c>, <c>neutral</c>, <c>redux</c>, <c>redux-color</c>, <c>redux-dark</c> and <c>redux-dark-color</c>.</summary>
public sealed class MermaidThemes : IThemeProvider
{
	/// <summary>The keys of <c>packages/mermaid/src/themes/index.js</c>, sorted.</summary>
	private static readonly string[] Names =
		["base", "dark", "default", "forest", "neo", "neo-dark", "neutral", "redux", "redux-color", "redux-dark", "redux-dark-color"];

	private MermaidThemes()
	{
	}

	public static MermaidThemes Default { get; } = new();

	public bool HasTheme(string name)
	{
		ArgumentNullException.ThrowIfNull(name);
		return Array.BinarySearch(Names, name, StringComparer.Ordinal) >= 0;
	}

	/// <inheritdoc/>
	/// <exception cref="FormatException">A colour the theme passes to khroma does not parse.</exception>
	public MermaidValue GetThemeVariables(string name, MermaidValue? overrides)
	{
		ArgumentNullException.ThrowIfNull(name);
		return Theme.Variables(name, overrides) ?? throw new ArgumentException($"No theme is named '{name}'.", nameof(name));
	}
}
