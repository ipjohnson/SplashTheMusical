using AwesomeAssertions;
using CsCheck;

namespace SplashTheMusical.Tests.Generation;

/// <summary>
/// Section 7's property tests: for random documents of every generator in <see cref="DocumentGenerators"/>, writing the
/// document and parsing the result gives an equal document. CsCheck shrinks a failure to a small document and prints
/// its written text, which goes into the type's corpus folder as a hand-written case.
/// </summary>
public class RoundTripPropertyTests
{
	/// <summary>The documents each generator checks in the default test run. <c>CsCheck_Iter</c> in the environment sets another count.</summary>
	public const int Iterations = 10_000;

	[Test]
	[MethodDataSource(typeof(DocumentGenerators), nameof(DocumentGenerators.Names))]
	public void RandomDocumentsRoundTrip(string generator) =>
		DocumentGenerators.Named(generator).Documents.Sample(
			AssertRoundTrips,
			iter: Environment.GetEnvironmentVariable("CsCheck_Iter") is null ? Iterations : -1,
			print: Print);

	[Test]
	public void EveryCoreModuleHasAGenerator()
	{
		var types = new HashSet<string>(StringComparer.Ordinal);
		foreach (var generator in DocumentGenerators.All)
		{
			generator.Documents.Sample(d => { lock (types) { _ = types.Add(d.DiagramType); } }, iter: 1_000, threads: 1);
		}

		Mermaid.CoreModules.Select(m => m.DiagramType).Should().BeSubsetOf(types, "every diagram type with a writer has a generator in DocumentGenerators");
	}

	public static void AssertRoundTrips(MermaidDocument document)
	{
		var written = Mermaid.Default.Write(document);
		if (!Mermaid.Default.TryParse(written, out var again, out var diagnostics))
		{
			throw new InvalidOperationException($"The written text did not parse: {string.Join("; ", diagnostics)}");
		}

		if (DocumentDump.FirstDifference(DocumentDump.Lines(document), DocumentDump.Lines(again!)) is { } difference)
		{
			throw new InvalidOperationException("The written text reads back as another document:\n" + difference);
		}
	}

	/// <summary>The written text, or why it could not be written.</summary>
	public static string Print(MermaidDocument document)
	{
		try
		{
			return "\n" + Mermaid.Default.Write(document);
		}
		catch (ArgumentException exception)
		{
			return "(not written: " + exception.Message + ")";
		}
	}
}
