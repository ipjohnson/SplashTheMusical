using BenchmarkDotNet.Attributes;

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
	/// <summary>
	/// The stages. A diagram type adds its parse, layout, render and write stages here when it lands. The core has no stage yet,
	/// so <c>mise run bench</c> writes a result file with no rows.
	/// </summary>
	public static readonly StmStage[] Stages = [];

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
