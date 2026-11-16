namespace SplashTheMusical.Diagrams;

/// <summary>What a module's parser gets besides the source.</summary>
public sealed class DiagramParseContext
{
	private readonly List<MermaidDiagnostic> _diagnostics;

	/// <param name="diagramType">The detected type.</param>
	/// <param name="bodyStart">The offset of the diagram's first keyword in the source.</param>
	/// <param name="limits">The limits of the parse.</param>
	/// <param name="diagnostics">The list the parse adds its diagnostics to.</param>
	public DiagramParseContext(string diagramType, int bodyStart, ResourceLimits limits, List<MermaidDiagnostic> diagnostics)
	{
		ArgumentNullException.ThrowIfNull(diagramType);
		ArgumentNullException.ThrowIfNull(limits);
		ArgumentNullException.ThrowIfNull(diagnostics);
		DiagramType = diagramType;
		BodyStart = bodyStart;
		Limits = limits;
		_diagnostics = diagnostics;
	}

	/// <summary>The id of the detector that picked the module.</summary>
	public string DiagramType { get; }

	/// <summary>
	/// The offset of the diagram's first keyword in the source, after the front matter and the space, directives and
	/// comments before it.
	/// </summary>
	public int BodyStart { get; }

	public ResourceLimits Limits { get; }

	public IReadOnlyList<MermaidDiagnostic> Diagnostics => _diagnostics;

	public void Add(MermaidDiagnostic diagnostic) => _diagnostics.Add(diagnostic);

	/// <summary>Adds an error at a 1-based line and column.</summary>
	public void AddError(string message, int line, int column) =>
		_diagnostics.Add(new MermaidDiagnostic(DiagnosticSeverity.Error, message, line, column));
}
