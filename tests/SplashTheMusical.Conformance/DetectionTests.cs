using System.Text;
using SplashTheMusical.Diagrams;
using SplashTheMusical.Parsing;
using SplashTheMusical.Rendering;

namespace SplashTheMusical.Conformance;

/// <summary>Detection on every corpus case mermaid.js accepts against the oracle's <c>type</c> fact.</summary>
public class DetectionTests
{
	[Test]
	public void EveryAcceptedCaseIsDetectedAsTheTypeTheOracleRecorded()
	{
		// tools/oracle registers ZenUML as an external diagram, as a host adds a module.
		var mermaid = new Mermaid(modules: [new ZenUmlDetector()]);
		var checkedCases = 0;
		var problems = new List<string>();
		foreach (var name in CorpusCheck.Cases(CorpusCheck.CorpusRoot))
		{
			var oracle = OracleFiles.Read(CorpusCheck.CorpusRoot, name);
			if (!oracle.Accepted)
			{
				continue;
			}

			var source = CorpusComparison.Source(CorpusCheck.CorpusRoot, name);
			var expected = oracle.Facts.First(f => f.StartsWith("type\t", StringComparison.Ordinal))["type\t".Length..];
			if (!PreambleParser.TryParse(source, new Preamble(), [], ResourceLimits.Unlimited, out var bodyStart))
			{
				problems.Add($"{name}: the preamble did not parse");
				continue;
			}

			var detected = mermaid.DetectType(source, bodyStart, out _);
			if (detected != expected)
			{
				problems.Add($"{name}: oracle {expected}, stm {detected ?? "none"}");
			}

			checkedCases++;
		}

		Verify(!problems.Any(), $"{string.Join('\n', problems)}");
		Verify(() => checkedCases == 4411);
	}

	[Test]
	public void ASourceOfNoKnownTypeGivesMermaidsError()
	{
		var parsed = Mermaid.Default.TryParse("%%{init: {}}%%\n%% a comment\nnot a diagram %% trailing\nsecond line", out var document, out var diagnostics);

		Verify(() => !parsed);
		Verify(() => document is null);
		Verify(() => diagnostics.Count == 1);
		Verify(() => diagnostics[0].Message == "No diagram type detected matching given configuration for text: not a diagram\nsecond line");
		Verify(() => diagnostics[0].Line == 3);
	}

	[Test]
	public void TheC4KeywordsAfterTheFirstMatchAnywhereOutsideComments()
	{
		Verify(() => Detect("flowchart LR\n  a[C4Container] --> b") == "c4");
		Verify(() => Detect("flowchart LR\n  %% C4Container\n  a --> b") == "flowchart-v2");
		Verify(() => Detect("%%{init: {\"x\": \"C4Dynamic\"}}%%\nflowchart LR") == "flowchart-v2");
		Verify(() => Detect("mindmap\n  C4Container") == "mindmap");
	}

	[Test]
	public void KeywordsMatchAsMermaidsExpressionsDo()
	{
		Verify(() => Detect("graphs") == "flowchart-v2");
		Verify(() => Detect("flowchart-elk TD") == "flowchart-elk");
		Verify(() => Detect("agentflow-betax") is null);
		Verify(() => Detect("agentflow-beta%% c\n") == "agentflow");
		Verify(() => Detect("ISHIKAWA-beta") == "ishikawa");
		Verify(() => Detect("ishikawas") is null);
		Verify(() => Detect("Railroad-EBNF-beta") == "railroadEbnf");
		Verify(() => Detect("usecase-beta:") is null);
		Verify(() => Detect("cynefin-beta:") == "cynefin");
		Verify(() => Detect("  Error  \n") == "error");
		Verify(() => Detect("---\nx") == "---");
		Verify(() => Detect("%%\ninfo") == "info");
		Verify(() => Detect("%%{} not a directive\ninfo") == "info");
		Verify(() => Detect("%%{ word as a directive\ninfo") is null);
	}

	private static string? Detect(string source)
	{
		var bodyStart = PreambleParser.TryParse(source, new Preamble(), [], ResourceLimits.Unlimited, out var start) ? start : 0;
		return Mermaid.Default.DetectType(source, bodyStart, out _);
	}

	private sealed class Preamble : MermaidDocument
	{
		public override string DiagramType => "";
	}

	/// <summary>The detector of <c>@mermaid-js/mermaid-zenuml</c>, <c>/^\s*zenuml/</c>. The other parts are not used.</summary>
	private sealed class ZenUmlDetector : IDiagramModule
	{
		public string DiagramType => "zenuml";

		public bool Detect(ReadOnlySpan<char> text) => text.StartsWith("zenuml", StringComparison.Ordinal);

		public MermaidDocument? Parse(ReadOnlySpan<char> source, DiagramParseContext context) => throw new NotSupportedException();

		public void Write(MermaidDocument document, StringBuilder output) => throw new NotSupportedException();

		public DiagramLayout Layout(MermaidDocument document, DiagramRenderContext context) => throw new NotSupportedException();

		public void Render(MermaidDocument document, DiagramLayout layout, SvgWriter svg, DiagramRenderContext context) =>
			throw new NotSupportedException();
	}
}
