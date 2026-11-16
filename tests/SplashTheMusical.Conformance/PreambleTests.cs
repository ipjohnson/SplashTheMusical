using System.Text;
using SplashTheMusical.Parsing;
using SplashTheMusical.Writing;

namespace SplashTheMusical.Conformance;

/// <summary>
/// The preamble parser on every accepted corpus case: the title and config it reads match the oracle's
/// <c>preamble-title</c> and <c>preamble-config</c> facts, and a parse of the written preamble gives the same model.
/// </summary>
public class PreambleTests
{
	private const string Prefix = "preamble-";

	[Test]
	public async Task EveryCaseReadsTheTitleAndConfigTheOracleRecorded()
	{
		var checkedCases = 0;
		var problems = new List<string>();
		foreach (var name in CorpusCheck.Cases(CorpusCheck.CorpusRoot))
		{
			var facts = OracleFiles.Read(CorpusCheck.CorpusRoot, name);
			if (!facts.Accepted)
			{
				continue;
			}

			var source = ReadSource(name);
			var document = new PreambleDocument();
			var diagnostics = new List<MermaidDiagnostic>();
			if (!PreambleParser.TryParse(source, document, diagnostics, ResourceLimits.Unlimited, out _))
			{
				problems.Add($"{name}: the preamble did not parse: {string.Join("; ", diagnostics)}");
				continue;
			}

			var type = facts.Facts.First(f => f.StartsWith("type\t", StringComparison.Ordinal))["type\t".Length..];
			var expected = facts.Facts.Where(f => f.StartsWith(Prefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal);
			var actual = Facts(document, type).Order(StringComparer.Ordinal);
			var expectedText = string.Join('\n', expected);
			var actualText = string.Join('\n', actual);
			if (expectedText != actualText)
			{
				problems.Add($"{name}:\n  oracle:\n    {expectedText.Replace("\n", "\n    ", StringComparison.Ordinal)}\n  stm:\n    {actualText.Replace("\n", "\n    ", StringComparison.Ordinal)}");
			}

			if (expectedText.Length > 0)
			{
				checkedCases++;
			}
		}

		await Assert.That(string.Join('\n', problems)).IsEmpty();
		await Assert.That(checkedCases).IsGreaterThanOrEqualTo(1546);
	}

	[Test]
	public async Task EveryCaseWithAPreambleRoundTrips()
	{
		var checkedCases = 0;
		var problems = new List<string>();
		foreach (var name in CorpusCheck.Cases(CorpusCheck.CorpusRoot))
		{
			var source = ReadSource(name);
			var document = new PreambleDocument();
			if (!PreambleParser.TryParse(source, document, [], ResourceLimits.Unlimited, out _)
				|| (document.Title is null && document.DisplayMode is null && document.FrontMatterConfig is null && document.Directives.Count == 0))
			{
				continue;
			}

			checkedCases++;
			var builder = new StringBuilder();
			PreambleWriter.Write(builder, document);
			var written = builder.ToString();
			var again = new PreambleDocument();
			var diagnostics = new List<MermaidDiagnostic>();
			if (!PreambleParser.TryParse(written, again, diagnostics, ResourceLimits.Unlimited, out var bodyStart))
			{
				problems.Add($"{name}: the written preamble did not parse: {string.Join("; ", diagnostics)}\n{written}");
			}
			else if (!Same(document, again) || bodyStart != written.Length)
			{
				problems.Add($"{name}: the written preamble reads back differently\n{written}");
			}
		}

		await Assert.That(string.Join('\n', problems)).IsEmpty();
		await Assert.That(checkedCases).IsGreaterThanOrEqualTo(1606);
	}

	/// <summary>The source as the oracle reads it, keeping a byte order mark, which mermaid.js sees as a space.</summary>
	private static string ReadSource(string name) =>
		Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(CorpusCheck.CorpusRoot, name + ".mmd")));

	private static bool Same(MermaidDocument a, MermaidDocument b) =>
		a.Title == b.Title
		&& a.DisplayMode == b.DisplayMode
		&& Equals(a.FrontMatterConfig, b.FrontMatterConfig)
		&& a.Directives.SequenceEqual(b.Directives);

	/// <summary>The preamble facts of a document, written as tools/oracle/extract.mjs writes them.</summary>
	private static List<string> Facts(MermaidDocument document, string type)
	{
		var facts = new List<string>();
		if (document.Title is not null)
		{
			facts.Add(ConfigLines.Line("preamble-title", document.Title));
		}

		ConfigLines.Flatten(facts, "preamble-config", "", PreambleConfig.Resolve(document, type));
		return facts;
	}

	private sealed class PreambleDocument : MermaidDocument
	{
		public override string DiagramType => "";
	}
}
