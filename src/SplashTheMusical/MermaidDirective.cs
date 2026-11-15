namespace SplashTheMusical;

/// <summary>
/// A <c>%%{type: arguments}%%</c> directive. <see cref="Arguments"/> is the parsed JSON, a string when the arguments are
/// one word, or <c>null</c> when there are none.
/// </summary>
public sealed record MermaidDirective(string Type, MermaidValue? Arguments)
{
	/// <summary>
	/// The arguments as written when they are not valid JSON, otherwise <c>null</c>. mermaid.js ignores every
	/// <c>init</c> directive of a diagram when one of them is invalid, so the directive is kept for that rule and for
	/// the writer.
	/// </summary>
	public string? InvalidArguments { get; init; }
}
