namespace SplashTheMusical.Conformance;

/// <summary>
/// What a renderer reports for one source, in the line formats of the oracle's facts and boxes files.
/// tools/oracle/README.md describes them.
/// </summary>
/// <param name="Accepted">Whether the source is accepted.</param>
/// <param name="Error">The first line of the error message when the source is refused.</param>
/// <param name="Facts">The fact lines, without the header and the accept line.</param>
/// <param name="Boxes">The <c>box</c> lines of the boxes file.</param>
public sealed record CaseFacts(bool Accepted, string? Error, IReadOnlyList<string> Facts, IReadOnlyList<string> Boxes)
{
	public static CaseFacts Refused(string error) => new(false, error, [], []);
}
