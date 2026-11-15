namespace SplashTheMusical;

public enum DiagnosticSeverity
{
	Error,
	Warning,
	Info,
}

/// <summary>A problem found in Mermaid source. <see cref="Line"/> and <see cref="Column"/> are 1-based, and 0 when unknown.</summary>
public readonly record struct MermaidDiagnostic(DiagnosticSeverity Severity, string Message, int Line, int Column)
{
	public override string ToString() =>
		Line > 0 ? $"{Line}:{Column}: {Severity.ToString().ToLowerInvariant()}: {Message}" : $"{Severity.ToString().ToLowerInvariant()}: {Message}";
}
