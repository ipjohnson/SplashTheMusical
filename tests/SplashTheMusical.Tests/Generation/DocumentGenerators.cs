using CsCheck;

namespace SplashTheMusical.Tests.Generation;

/// <summary>
/// The random document generator of each diagram type with a writer. <see cref="RoundTripPropertyTests"/> runs the
/// round trip on each, <c>OracleSampleTests</c> writes a sample of each for the oracle, and
/// <see cref="RoundTripPropertyTests.EveryCoreModuleHasAGenerator"/> fails when a module of
/// <see cref="Mermaid.CoreModules"/> has none.
/// </summary>
/// <remarks>One generator per line, so types added in parallel do not change the same line.</remarks>
public static class DocumentGenerators
{
	public static IReadOnlyList<DocumentGenerator> All { get; } =
	[
		new("info", InfoGenerator.Document.Select(d => (MermaidDocument)d)),
		new("flowchart", FlowchartGenerator.Document.Select(d => (MermaidDocument)d)),
		new("state", StateGenerator.Document.Select(d => (MermaidDocument)d)),
		new("class", ClassGenerator.Document.Select(d => (MermaidDocument)d)),
		new("er", ErGenerator.Document.Select(d => (MermaidDocument)d)),
	];

	/// <summary>The names of <see cref="All"/>, for TUnit's <c>MethodDataSource</c>.</summary>
	public static IEnumerable<string> Names() => All.Select(g => g.Name);

	public static DocumentGenerator Named(string name) => All.Single(g => g.Name == name);
}

/// <param name="Name">The generator's name, which names its oracle sample folder.</param>
/// <param name="Documents">Random documents the writer can write. A document may be of more than one diagram type, as a flowchart is <c>flowchart-v2</c> or <c>flowchart-elk</c>.</param>
public sealed record DocumentGenerator(string Name, Gen<MermaidDocument> Documents);
