namespace SplashTheMusical;

/// <summary>
/// The base of every diagram's document. It holds what all diagram types share: the front matter, the directives and
/// the accessibility text. The model keeps what the source said, so the writer can write it back.
/// </summary>
public abstract class MermaidDocument
{
	/// <summary>
	/// The id of mermaid.js's detector for the diagram, such as <c>info</c> or <c>flowchart-v2</c>. The config is resolved
	/// for it, and the <see cref="Diagrams.IDiagramModule"/> of the same <see cref="Diagrams.IDiagramModule.DiagramType"/>
	/// writes and renders the document.
	/// </summary>
	public abstract string DiagramType { get; }

	/// <summary>
	/// The <c>title</c> of the front matter, as mermaid.js reads it: the value's string form, or <c>null</c> when the value
	/// is missing, <c>false</c>, <c>0</c> or empty.
	/// </summary>
	public string? Title { get; set; }

	/// <summary>The <c>displayMode</c> of the front matter, read as <see cref="Title"/> is. mermaid.js sets it as <c>gantt.displayMode</c>.</summary>
	public string? DisplayMode { get; set; }

	/// <summary>The <c>config</c> mapping of the front matter, or <c>null</c> when there is none.</summary>
	public MermaidValue? FrontMatterConfig { get; set; }

	/// <summary>
	/// Every <c>%%{ }%%</c> directive in the source, in source order. mermaid.js reads directives anywhere in the text
	/// after the front matter, and the writer puts them before the diagram.
	/// </summary>
	public List<MermaidDirective> Directives { get; init; } = [];

	/// <summary>The text of the <c>accTitle</c> statement.</summary>
	public string? AccessibilityTitle { get; set; }

	/// <summary>The text of the <c>accDescr</c> statement, which may span lines.</summary>
	public string? AccessibilityDescription { get; set; }
}
