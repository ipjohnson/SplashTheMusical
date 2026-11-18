using System.Text;
using SplashTheMusical.Conformance.Fuzzing;
using SplashTheMusical.Diagrams;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Rendering;

namespace SplashTheMusical.Conformance;

/// <summary>
/// The differential runner on flowchart, against mermaid.js, with STM's rule for thick links broken so <c>==&gt;</c>
/// reads as <c>--&gt;</c>. mermaid.js's answers come from <c>Fixtures/differential-thick-rule.txt</c>. With
/// <c>STM_FUZZ_RECORD</c> set, the test asks the live oracle instead and rewrites the file. Rewrite it when the
/// mutator or the corpus changes the run's sources.
/// </summary>
public class BrokenRuleReplayTests
{
	public const string RecordVariable = "STM_FUZZ_RECORD";

	private const long Seed = 1;
	private const int MaxSources = 20_000;

	private static string FixturePath => Path.Combine(CorpusCheck.RepositoryRoot, "tests", "SplashTheMusical.Conformance", "Fixtures", "differential-thick-rule.txt");

	[Test]
	public void FindsAndMinimizesAThickLinkRuleBrokenInTheFlowchartParser()
	{
		var stm = new StmFactsProvider(new Mermaid(new MermaidOptions { SiteConfig = StmFactsProvider.OracleSite, Limits = ResourceLimits.Unlimited }, [new BrokenThickRule()]));
		var corpus = CorpusCheck.CorpusRoot;
		using var process = Environment.GetEnvironmentVariable(RecordVariable) is { Length: > 0 } ? new OracleProcess(CorpusCheck.RepositoryRoot, TimeSpan.FromSeconds(30)) : null;
		var oracle = process is null
			? RecordedOracle.Read(FixturePath)
			: RecordedOracle.Recording(DifferentialRunner.SupportedTypes(process), DifferentialRunner.AgreeingCases(corpus, "flowchart", stm.Facts));
		// The finds before the thick edge's are differences STM has without the break.
		var runner = new DifferentialRunner(oracle.Facts, stm.Facts) { StopAfter = ShowsTheBreak };

		var run = runner.Run("flowchart", DifferentialRunner.Seeds(corpus, "flowchart", oracle.Seeds), null, Seed, MaxSources, TimeSpan.MaxValue);
		if (process is not null)
		{
			oracle.Write(FixturePath);
		}

		Verify(oracle.Misses == 0, $"the run asked for sources the fixture lacks. Rewrite it with {RecordVariable}=1, was {oracle.Misses}");
		var find = run.Finds.FirstOrDefault(ShowsTheBreak);
		Verify(find is not null);
		Console.WriteLine($"Source {find!.Index} of {run.Sources}, minimized from {SourceTokens.Lines(find.Original).Count} lines in {find.Tests} tests: {Mutator.Escape(find.Minimized)}");
		Verify(find.Disagreement.Signature == "L1 edge", $"was {find.Disagreement.Signature}");
		Verify(find.Minimized.Contains("=="), $"{find.Minimized}");
		Verify(SourceTokens.Lines(find.Minimized).Count < SourceTokens.Lines(find.Original).Count, $"was {SourceTokens.Lines(find.Minimized).Count}");
		Verify(SourceTokens.Lines(find.Minimized).Count <= 3, $"was {SourceTokens.Lines(find.Minimized).Count}");
		Verify(runner.Disagree(find.Minimized)?.Shows("L1 edge") == true);
		var unbroken = new DifferentialRunner(oracle.Facts, new StmFactsProvider().Facts).Disagree(find.Minimized);
		Verify(unbroken is null, $"was {unbroken}");
	}

	/// <summary>Whether mermaid.js has a thick edge STM lacks.</summary>
	private static bool ShowsTheBreak(DifferentialFind find) =>
		find.Disagreement.Lines.Any(l => l.Split('\t') is ["- edge", _, _, _, _, "thick", ..]);

	/// <summary><see cref="FlowchartModule"/> with <c>destructEndLink</c>'s thick stroke read as the normal one.</summary>
	private sealed class BrokenThickRule : IDiagramModule
	{
		private readonly IDiagramModule _flowchart = new FlowchartModule();

		public string DiagramType => _flowchart.DiagramType;

		public bool Detect(ReadOnlySpan<char> text) => _flowchart.Detect(text);

		public MermaidDocument? Parse(ReadOnlySpan<char> source, DiagramParseContext context)
		{
			var document = _flowchart.Parse(source, context);
			if (document is FlowchartDocument flowchart)
			{
				foreach (var edge in flowchart.Edges.Where(e => e.Stroke == FlowchartStroke.Thick))
				{
					edge.Stroke = FlowchartStroke.Normal;
				}
			}

			return document;
		}

		public void Write(MermaidDocument document, StringBuilder output) => _flowchart.Write(document, output);

		public DiagramLayout Layout(MermaidDocument document, DiagramRenderContext context) => _flowchart.Layout(document, context);

		public void Render(MermaidDocument document, DiagramLayout layout, SvgWriter svg, DiagramRenderContext context) =>
			_flowchart.Render(document, layout, svg, context);
	}
}
