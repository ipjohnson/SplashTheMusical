using System.Text;
using SplashTheMusical.Diagrams;
using SplashTheMusical.Parsing;
using SplashTheMusical.Rendering;

namespace SplashTheMusical.Conformance;

/// <summary>Detection on every corpus case mermaid.js accepts against the oracle's <c>type</c> fact.</summary>
public class DetectionTests
{
	[Test]
	public async Task EveryAcceptedCaseIsDetectedAsTheTypeTheOracleRecorded()
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

		await Assert.That(string.Join('\n', problems)).IsEmpty();
		await Assert.That(checkedCases).IsEqualTo(4411);
	}

	[Test]
	public async Task ASourceOfNoKnownTypeGivesMermaidsError()
	{
		var parsed = Mermaid.Default.TryParse("%%{init: {}}%%\n%% a comment\nnot a diagram %% trailing\nsecond line", out var document, out var diagnostics);

		await Assert.That(parsed).IsFalse();
		await Assert.That(document).IsNull();
		await Assert.That(diagnostics.Count).IsEqualTo(1);
		await Assert.That(diagnostics[0].Message).IsEqualTo("No diagram type detected matching given configuration for text: not a diagram\nsecond line");
		await Assert.That(diagnostics[0].Line).IsEqualTo(3);
	}

	[Test]
	public async Task TheC4KeywordsAfterTheFirstMatchAnywhereOutsideComments()
	{
		await Assert.That(Detect("flowchart LR\n  a[C4Container] --> b")).IsEqualTo("c4");
		await Assert.That(Detect("flowchart LR\n  %% C4Container\n  a --> b")).IsEqualTo("flowchart-v2");
		await Assert.That(Detect("%%{init: {\"x\": \"C4Dynamic\"}}%%\nflowchart LR")).IsEqualTo("flowchart-v2");
		await Assert.That(Detect("mindmap\n  C4Container")).IsEqualTo("mindmap");
	}

	[Test]
	public async Task KeywordsMatchAsMermaidsExpressionsDo()
	{
		await Assert.That(Detect("graphs")).IsEqualTo("flowchart-v2");
		await Assert.That(Detect("flowchart-elk TD")).IsEqualTo("flowchart-elk");
		await Assert.That(Detect("agentflow-betax")).IsNull();
		await Assert.That(Detect("agentflow-beta%% c\n")).IsEqualTo("agentflow");
		await Assert.That(Detect("ISHIKAWA-beta")).IsEqualTo("ishikawa");
		await Assert.That(Detect("ishikawas")).IsNull();
		await Assert.That(Detect("Railroad-EBNF-beta")).IsEqualTo("railroadEbnf");
		await Assert.That(Detect("usecase-beta:")).IsNull();
		await Assert.That(Detect("cynefin-beta:")).IsEqualTo("cynefin");
		await Assert.That(Detect("  Error  \n")).IsEqualTo("error");
		await Assert.That(Detect("---\nx")).IsEqualTo("---");
		await Assert.That(Detect("%%\ninfo")).IsEqualTo("info");
		await Assert.That(Detect("%%{} not a directive\ninfo")).IsEqualTo("info");
		await Assert.That(Detect("%%{ word as a directive\ninfo")).IsNull();
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
