namespace SplashTheMusical;

/// <summary>Thrown when Mermaid source cannot be parsed. It carries every diagnostic the parse collected.</summary>
public class MermaidParseException : Exception
{
	public MermaidParseException(IReadOnlyList<MermaidDiagnostic> diagnostics)
		: base(MessageFor(diagnostics)) => Diagnostics = diagnostics;

	public MermaidParseException(MermaidDiagnostic diagnostic)
		: this([diagnostic])
	{
	}

	public IReadOnlyList<MermaidDiagnostic> Diagnostics { get; }

	private static string MessageFor(IReadOnlyList<MermaidDiagnostic> diagnostics)
	{
		ArgumentNullException.ThrowIfNull(diagnostics);
		foreach (var diagnostic in diagnostics)
		{
			if (diagnostic.Severity == DiagnosticSeverity.Error)
			{
				return diagnostic.ToString();
			}
		}

		return diagnostics.Count > 0 ? diagnostics[0].ToString() : "The source could not be parsed.";
	}
}
