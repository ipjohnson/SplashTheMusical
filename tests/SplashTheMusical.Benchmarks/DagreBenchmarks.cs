using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using SplashTheMusical.Layout.Dagre;

namespace SplashTheMusical.Benchmarks;

/// <summary>The dagre port's whole layout of the <see cref="DagreGraphs.Flowchart"/> graphs.</summary>
public class DagreBenchmarks
{
	private DagreInput _input = null!;

	[Params(10, 100, 1_000)]
	public int Nodes { get; set; }

	[GlobalSetup]
	public void Setup() => _input = DagreGraphs.Flowchart(Nodes);

	[Benchmark(Description = "layout")]
	public object Layout() => DagreGraphs.Layout(_input);

	/// <summary>Runs the benchmarks and writes the result file.</summary>
	public static void Write(string path, IReadOnlyList<string> jobs)
	{
		var summary = Runs.Execute<DagreBenchmarks>(jobs);
		var text = new StringBuilder();
		text.Append("""
			# Dagre benchmark results

			The time and allocation of the dagre port's whole layout, from `DagreLayout.BuildLayoutGraph` through
			`RunPositionPhases`, on the seeded flowchart-shaped graphs of `DagreGraphs.Flowchart` in
			`tests/SplashTheMusical.Benchmarks`. `ns` is the mean time per layout and `Bytes` the bytes allocated per layout.
			Written by `mise run bench-dagre`.


			""");
		text.Append("| Property | Value |\n|---|---|\n");
		foreach (var (name, value) in Runs.Properties(summary, jobs))
		{
			text.Append("| ").Append(name).Append(" | ").Append(value).Append(" |\n");
		}

		text.Append("\n## Results\n\n| Job | Nodes | Edges | ns | Bytes | ns/node | Bytes/node |\n|---|---:|---:|---:|---:|---:|---:|\n");
		var rows = Runs.Results(summary)
			.Select(r => (Job: r.Benchmark.Job.Id, Nodes: (int)r.Benchmark.Parameters["Nodes"], r.Nanoseconds, r.Bytes))
			.OrderBy(r => r.Job, StringComparer.Ordinal)
			.ThenBy(r => r.Nodes);
		foreach (var (job, nodes, nanoseconds, bytes) in rows)
		{
			var edges = DagreGraphs.Flowchart(nodes).Edges.Count;
			text.Append(CultureInfo.InvariantCulture,
				$"| {job} | {nodes} | {edges} | {nanoseconds:F0} | {bytes} | {nanoseconds / nodes:F0} | {(double)bytes / nodes:F0} |\n");
		}

		Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
		File.WriteAllText(path, text.ToString());
	}
}
