using System.Globalization;
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
			facts.Add(Line("preamble-title", document.Title));
		}

		Flatten(facts, "", PreambleConfig.Resolve(document, type));
		return facts;
	}

	private static void Flatten(List<string> facts, string prefix, MermaidValue value)
	{
		switch (value.Kind)
		{
			case MermaidValueKind.Mapping:
				if (value.Entries.Count == 0 && prefix.Length > 0)
				{
					facts.Add(Line("preamble-config", prefix, "{}"));
				}

				foreach (var (key, item) in value.Entries)
				{
					Flatten(facts, prefix.Length > 0 ? prefix + "." + key : key, item);
				}

				break;
			case MermaidValueKind.Sequence:
				if (value.Items.Count == 0)
				{
					facts.Add(Line("preamble-config", prefix, "[]"));
				}

				for (var i = 0; i < value.Items.Count; i++)
				{
					Flatten(facts, prefix + "." + i.ToString(CultureInfo.InvariantCulture), value.Items[i]);
				}

				break;
			default:
				facts.Add(Line("preamble-config", prefix, Json(value)));
				break;
		}
	}

	/// <summary>JavaScript's <c>JSON.stringify</c> of a scalar.</summary>
	private static string Json(MermaidValue value)
	{
		switch (value.Kind)
		{
			case MermaidValueKind.Null:
				return "null";
			case MermaidValueKind.Boolean:
				return value.Boolean ? "true" : "false";
			case MermaidValueKind.Number:
				return double.IsFinite(value.Number) ? JsNumber.ToString(value.Number) : "null";
			default:
				var builder = new StringBuilder("\"");
				foreach (var c in value.String)
				{
					builder.Append(c switch
					{
						'"' => "\\\"",
						'\\' => "\\\\",
						'\b' => "\\b",
						'\f' => "\\f",
						'\n' => "\\n",
						'\r' => "\\r",
						'\t' => "\\t",
						< ' ' => "\\u" + ((int)c).ToString("x4", CultureInfo.InvariantCulture),
						_ => c.ToString(),
					});
				}

				return builder.Append('"').ToString();
		}
	}

	/// <summary>A fact line as extract.mjs's <c>tab</c> writes it: each field with its space folded and trimmed, and no trailing empty fields.</summary>
	private static string Line(params string[] fields) =>
		string.Join('\t', fields.Select(Fold)).TrimEnd('\t');

	private static string Fold(string text)
	{
		var builder = new StringBuilder(text.Length);
		var space = false;
		foreach (var c in text)
		{
			if (PreambleParser.IsSpace(c))
			{
				space = true;
				continue;
			}

			if (space && builder.Length > 0)
			{
				builder.Append(' ');
			}

			space = false;
			builder.Append(c);
		}

		return builder.ToString();
	}

	private sealed class PreambleDocument : MermaidDocument;
}
