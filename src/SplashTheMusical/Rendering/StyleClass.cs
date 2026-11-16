namespace SplashTheMusical.Rendering;

/// <summary>
/// A class a diagram defines with <c>classDef</c>, as mermaid's diagram databases return it from <c>getClasses</c>.
/// Each style is one declaration from the source, such as <c>fill:#f9f</c>. The text styles are the ones the
/// diagram also applies to label text.
/// </summary>
internal sealed record StyleClass(string Id, IReadOnlyList<string> Styles, IReadOnlyList<string> TextStyles);
