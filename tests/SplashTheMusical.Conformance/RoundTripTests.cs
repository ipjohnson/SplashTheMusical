using SplashTheMusical.Diagrams;

namespace SplashTheMusical.Conformance;

/// <summary>
/// Section 7's round trip for every diagram module of <see cref="Mermaid.CoreModules"/>, so a new module is checked
/// without test code: for every accepted corpus case of the module's type, STM writes the parsed document, and the
/// written text must parse, write back to the same text, and give the same facts and boxes as the original.
/// </summary>
/// <remarks>
/// The facts and boxes are <see cref="StmFactsProvider"/>'s, the ones the conformance levels compare with the oracle,
/// so they hold what the type draws and what mermaid.js's model has. A type's own model dump, such as
/// <see cref="FlowchartRoundTripTests"/>, may check more.
/// </remarks>
public class RoundTripTests
{
	[Test]
	public void EveryAcceptedCaseOfEveryModuleRoundTrips()
	{
		var result = RoundTrip.Run(CorpusCheck.CorpusRoot, Mermaid.CoreModules);

		Verify(!result.Problems.Any(), $"{string.Join("\n\n", result.Problems)}");
		var missing = Mermaid.CoreModules.Select(m => m.DiagramType).Where(t => result.Checked.GetValueOrDefault(t) == 0).ToList();
		Verify(!missing.Any(), $"{string.Join(", ", missing)}");
	}

	[Test]
	public void ChecksTheCasesOfAnAddedModule()
	{
		var result = RoundTrip.Run(CorpusCheck.CorpusRoot, [.. Mermaid.CoreModules, new LossyInfoModule()]);

		Verify(() => result.Checked.GetValueOrDefault("info") > 0);
		Verify(() => result.Problems.Count == result.Checked["info"]);
	}

	/// <summary>An <c>info</c> module whose writer adds an accessibility title, which the round trip must notice.</summary>
	private sealed class LossyInfoModule : IDiagramModule
	{
		private readonly IDiagramModule _inner = Mermaid.CoreModules.First(m => m.DiagramType == "info");

		public string DiagramType => _inner.DiagramType;

		public bool Detect(ReadOnlySpan<char> text) => _inner.Detect(text);

		public MermaidDocument? Parse(ReadOnlySpan<char> source, DiagramParseContext context) => _inner.Parse(source, context);

		public void Write(MermaidDocument document, System.Text.StringBuilder output) => output.Append("info\naccTitle: added by the writer\n");

		public DiagramLayout Layout(MermaidDocument document, DiagramRenderContext context) => _inner.Layout(document, context);

		public void Render(MermaidDocument document, DiagramLayout layout, Rendering.SvgWriter svg, DiagramRenderContext context) =>
			_inner.Render(document, layout, svg, context);
	}
}

/// <summary>The generic round trip of <see cref="RoundTripTests"/> over the cases of a corpus folder.</summary>
public static class RoundTrip
{
	public sealed record Result(IReadOnlyDictionary<string, int> Checked, IReadOnlyList<string> Problems);

	/// <summary>
	/// Every accepted case whose document is of one of <paramref name="modules"/>' types, with the modules in a
	/// <see cref="Mermaid"/> like <see cref="StmFactsProvider"/>'s. The result counts the cases of each type.
	/// </summary>
	public static Result Run(string corpus, IReadOnlyList<IDiagramModule> modules)
	{
		var types = new HashSet<string>(modules.Select(m => m.DiagramType), StringComparer.Ordinal);
		var mermaid = new Mermaid(new MermaidOptions { SiteConfig = StmFactsProvider.OracleSite, Limits = ResourceLimits.Unlimited }, modules);
		var provider = new StmFactsProvider(mermaid);
		var counts = new Dictionary<string, int>(StringComparer.Ordinal);
		var problems = new List<string>();
		foreach (var name in CorpusCheck.Cases(corpus))
		{
			if (!OracleFiles.Read(corpus, name).Accepted)
			{
				continue;
			}

			var source = CorpusComparison.Source(corpus, name);
			if (!mermaid.TryParse(source, out var document, out _) || !types.Contains(document!.DiagramType))
			{
				continue;
			}

			counts[document.DiagramType] = counts.GetValueOrDefault(document.DiagramType) + 1;
			if (Problem(mermaid, provider, source, document) is { } problem)
			{
				problems.Add($"{name}: {problem}");
			}
		}

		return new Result(counts, problems);
	}

	/// <summary>Why the written text of <paramref name="document"/> does not round trip, or <c>null</c> when it does.</summary>
	public static string? Problem(Mermaid mermaid, IFactsProvider provider, string source, MermaidDocument document)
	{
		var written = mermaid.Write(document);
		if (!mermaid.TryParse(written, out var again, out var diagnostics))
		{
			return $"the written text did not parse: {string.Join("; ", diagnostics)}\n{written}";
		}

		if (again!.DiagramType != document.DiagramType)
		{
			return $"the written text is a {again.DiagramType} diagram, not {document.DiagramType}\n{written}";
		}

		var rewritten = mermaid.Write(again);
		if (rewritten != written)
		{
			return $"writing the written text's document gives other text\n{FirstDifference(Lines(written), Lines(rewritten))}\n{written}";
		}

		if (FirstDifference(Shared(document), Shared(again)) is { } shared)
		{
			return $"the written text gives another preamble or accessibility text\n{shared}\n{written}";
		}

		var expected = provider.Facts(source);
		var actual = provider.Facts(written);
		var facts = FirstDifference([.. expected.Facts, .. expected.Boxes, expected.Canvas ?? ""], [.. actual.Facts, .. actual.Boxes, actual.Canvas ?? ""]);
		if (facts is not null)
		{
			return $"the written text gives other facts\n{facts}\n{written}";
		}

		return expected.Svg == actual.Svg ? null
			: $"the written text draws another SVG\n{FirstDifference(Lines(expected.Svg ?? ""), Lines(actual.Svg ?? ""))}\n{written}";
	}

	/// <summary>The values every <see cref="MermaidDocument"/> has, as lines.</summary>
	private static List<string> Shared(MermaidDocument document)
	{
		var lines = new List<string>
		{
			$"title {document.Title}",
			$"displayMode {document.DisplayMode}",
			$"config {document.FrontMatterConfig}",
			$"accTitle {document.AccessibilityTitle}",
			$"accDescr {document.AccessibilityDescription}",
		};
		foreach (var directive in document.Directives)
		{
			lines.Add($"directive {directive.Type} {directive.Arguments} {directive.InvalidArguments}");
		}

		return lines;
	}

	private static List<string> Lines(string text) => [.. text.Split('\n', '>')];

	private static string? FirstDifference(List<string> expected, List<string> actual)
	{
		for (var i = 0; i < Math.Max(expected.Count, actual.Count); i++)
		{
			var e = i < expected.Count ? expected[i] : "(none)";
			var a = i < actual.Count ? actual[i] : "(none)";
			if (e != a)
			{
				return $"expected {e}\n  actual {a}";
			}
		}

		return null;
	}
}
