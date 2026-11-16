namespace SplashTheMusical.Diagrams.Info;

/// <summary>
/// The <c>info</c> diagram, which draws the version of mermaid.js. Its grammar is
/// <c>packages/parser/src/language/info/info.langium</c>: <c>info</c>, then <c>showInfo</c> if given, then title and
/// accessibility statements.
/// </summary>
public sealed class InfoDocument : MermaidDocument
{
	public override string DiagramType => "info";

	/// <summary>Whether the source has the <c>showInfo</c> keyword. mermaid.js draws the same either way.</summary>
	public bool ShowInfo { get; set; }

	/// <summary>The text of the <c>title</c> statement, or <c>null</c> when there is none. mermaid.js does not draw it.</summary>
	public string? DiagramTitle { get; set; }
}
