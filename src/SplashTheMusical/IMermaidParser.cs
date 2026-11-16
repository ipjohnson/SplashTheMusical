namespace SplashTheMusical;

/// <summary>Parses Mermaid source into the document of its diagram type.</summary>
/// <remarks>An implementation is stateless and thread safe.</remarks>
public interface IMermaidParser
{
	/// <summary>Parses <paramref name="source"/>.</summary>
	/// <exception cref="MermaidParseException">The source is not a diagram mermaid.js accepts, or a type STM does not support.</exception>
	/// <exception cref="MermaidResourceLimitException">The source exceeds one of the <see cref="ResourceLimits"/>.</exception>
	MermaidDocument Parse(ReadOnlySpan<char> source, ParseOptions? options = null);

	/// <summary>
	/// Parses <paramref name="source"/>. Returns <c>false</c>, with at least one error in <paramref name="diagnostics"/>,
	/// when it cannot, including when the source exceeds a limit. It does not throw on any source.
	/// </summary>
	bool TryParse(
		ReadOnlySpan<char> source,
		out MermaidDocument? document,
		out IReadOnlyList<MermaidDiagnostic> diagnostics,
		ParseOptions? options = null);
}
