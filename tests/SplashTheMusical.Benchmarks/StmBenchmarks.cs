using BenchmarkDotNet.Attributes;
using SplashTheMusical.Config;
using SplashTheMusical.Diagrams;
using SplashTheMusical.Icons;
using SplashTheMusical.Text;

namespace SplashTheMusical.Benchmarks;

/// <summary>One step of STM's pipeline, measured on its own.</summary>
/// <param name="Name"><c>parse</c>, <c>layout</c>, <c>render</c> or <c>write</c>.</param>
/// <param name="Types">The corpus folders of the diagram types the stage handles.</param>
/// <param name="Prepare">Makes the stage's input from the source outside the measurement, such as the parsed model for <c>layout</c>.</param>
/// <param name="Run">The measured call.</param>
public sealed record StmStage(string Name, IReadOnlySet<string> Types, Func<string, object> Prepare, Func<object, object?> Run);

/// <summary>STM's stages on the benchmark set.</summary>
public class StmBenchmarks
{
	/// <summary>The corpus folders of the diagram types that have stages. A diagram type adds its folder when it lands.</summary>
	public static readonly IReadOnlySet<string> Measured = new HashSet<string>(StringComparer.Ordinal) { "flowchart", "state", "class", "er" };

	/// <summary>
	/// The stages. <c>render</c> starts from the source, as Mermaider's <c>render</c> does, so it is parse, layout and render
	/// together.
	/// </summary>
	public static readonly StmStage[] Stages =
	[
		new("parse", Measured, source => source, source => Mermaid.Default.Parse((string)source)),
		new("layout", Measured, PrepareLayout, layout => ((Func<DiagramLayout>)layout)()),
		new("render", Measured, source => source, source => Mermaid.Default.RenderSvg((string)source)),
		new("write", Measured, source => Mermaid.Default.Parse(source), document => Mermaid.Default.Write((MermaidDocument)document)),
	];

	/// <summary>Parses the source and resolves its config, as <c>Mermaid.RenderSvg</c> does before it calls the module's layout.</summary>
	private static Func<DiagramLayout> PrepareLayout(string source)
	{
		var document = Mermaid.Default.Parse(source);
		var module = Mermaid.CoreModules.First(m => m.DiagramType == document.DiagramType);
		var config = MermaidConfig.Resolve(null, document, document.DiagramType);
		var context = new DiagramRenderContext(config, "mermaid", TextMeasurer.Default, NoIcons.Instance, [], ResourceLimits.Default);
		return () => module.Layout(document, context);
	}

	private StmStage _stage = null!;
	private object _input = null!;

	/// <summary>Each case and stage pair, as <c>case/stage</c>.</summary>
	public static IEnumerable<string> Cases() =>
		from c in BenchmarkSet.Selected()
		from s in Stages
		where s.Types.Contains(c.Type)
		select c.Id + "/" + s.Name;

	/// <summary>The case id and the stage, as <c>case/stage</c>.</summary>
	[ParamsSource(nameof(Cases))]
	public string Case { get; set; } = "";

	[GlobalSetup]
	public void Setup()
	{
		var slash = Case.IndexOf('/', StringComparison.Ordinal);
		_stage = Stages.Single(s => s.Name == Case[(slash + 1)..]);
		_input = _stage.Prepare(BenchmarkSet.Find(Case[..slash]).Source);
	}

	[Benchmark(Description = "stage")]
	public object? Run() => _stage.Run(_input);
}
