using BenchmarkDotNet.Attributes;
using Mermaider;

namespace SplashTheMusical.Benchmarks;

/// <summary>
/// The baseline: the Mermaider package on the benchmark set. Mermaider's public API parses, or parses, lays out and renders in one
/// call, so its stages are <c>parse</c> and <c>render</c>, and <c>render</c> includes the other two. <c>MermaidRenderer.Parse</c>
/// handles only some types, so a case has a <c>parse</c> row only when it succeeds.
/// </summary>
public class MermaiderBenchmarks
{
	private Func<string, object> _stage = null!;
	private string _source = "";

	/// <summary>Each case and stage Mermaider runs without an exception, as <c>case/stage</c>.</summary>
	public static IEnumerable<string> Cases() =>
		from c in BenchmarkSet.Selected()
		from stage in new[] { "parse", "render" }
		where Runs(stage, c.Source)
		select c.Id + "/" + stage;

	/// <summary>The case id and the stage, as <c>case/stage</c>.</summary>
	[ParamsSource(nameof(Cases))]
	public string Case { get; set; } = "";

	/// <summary>Whether a stage runs on a source without an exception.</summary>
	public static bool Runs(string stage, string source)
	{
		try
		{
			_ = Stage(stage)(source);
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	[GlobalSetup]
	public void Setup()
	{
		var slash = Case.IndexOf('/', StringComparison.Ordinal);
		_stage = Stage(Case[(slash + 1)..]);
		_source = BenchmarkSet.Find(Case[..slash]).Source;
	}

	[Benchmark(Description = "stage")]
	public object Run() => _stage(_source);

	private static Func<string, object> Stage(string name) => name switch
	{
		"parse" => source => MermaidRenderer.Parse(source),
		"render" => source => MermaidRenderer.RenderSvg(source),
		_ => throw new ArgumentException($"Unknown stage {name}", nameof(name)),
	};
}
