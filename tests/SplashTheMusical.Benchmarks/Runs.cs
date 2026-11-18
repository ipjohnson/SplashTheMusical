using System.Runtime.InteropServices;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.NativeAot;
using Perfolizer.Horology;

namespace SplashTheMusical.Benchmarks;

/// <summary>Runs a benchmark class with BenchmarkDotNet and turns its summary into result rows.</summary>
public static class Runs
{
	/// <summary>The jobs a run takes when it names none.</summary>
	public static readonly string[] AllJobs = ["JIT", "AOT"];

	/// <summary>The warmup iterations of the JIT job. The AOT job takes 3.</summary>
	public const int JitWarmupCount = 20;

	/// <summary>
	/// Runs the benchmarks of <typeparamref name="T"/> under the named jobs, with <see cref="MemoryDiagnoser"/>. The class has a
	/// <c>Case</c> parameter of the form <c>case/stage</c>.
	/// </summary>
	/// <param name="jobs"><c>JIT</c>, <c>AOT</c> or both.</param>
	public static (Summary Summary, IReadOnlyList<ResultRow> Rows) Run<T>(IReadOnlyList<string> jobs)
	{
		var summary = Execute<T>(jobs);
		var chars = BenchmarkSet.Load().ToDictionary(c => c.Id, c => c.Source.Length, StringComparer.Ordinal);
		var rows = new List<ResultRow>();
		foreach (var (benchmark, nanoseconds, bytes) in Results(summary))
		{
			var parameter = (string)benchmark.Parameters["Case"];
			var slash = parameter.IndexOf('/', StringComparison.Ordinal);
			var (id, stage) = (parameter[..slash], parameter[(slash + 1)..]);
			rows.Add(new ResultRow(benchmark.Job.Id, id, stage, chars[id], nanoseconds, bytes));
		}

		rows.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
		return (summary, rows);
	}

	/// <summary>Runs the benchmarks of <typeparamref name="T"/> under the named jobs, with <see cref="MemoryDiagnoser"/>.</summary>
	/// <param name="jobs"><c>JIT</c>, <c>AOT</c> or both.</param>
	public static Summary Execute<T>(IReadOnlyList<string> jobs)
	{
		var job = Job.Default
			.WithWarmupCount(3)
			.WithIterationCount(10)
			.WithIterationTime(TimeInterval.FromMilliseconds(100))
			.WithEnvironmentVariable(Repository.RootVariable, Repository.Root());
		var config = ManualConfig.Create(DefaultConfig.Instance)
			.AddDiagnoser(MemoryDiagnoser.Default)
			.WithBuildTimeout(TimeSpan.FromMinutes(30))
			.WithArtifactsPath(Repository.Resolve(".artifacts/bench/BenchmarkDotNet"));
		foreach (var id in jobs)
		{
			config.AddJob(id switch
			{
				// Tier-1 code can arrive a second or more into a run, so 3 warmups of 100 ms would measure tier-0 code.
				"JIT" => job.WithWarmupCount(JitWarmupCount).WithId("JIT"),
				"AOT" => job.WithToolchain(NativeAotToolchain.Net10_0).WithId("AOT"),
				_ => throw new ArgumentException($"Unknown job {id}. Use JIT or AOT.", nameof(jobs)),
			});
		}

		return BenchmarkRunner.Run<T>(config);
	}

	/// <summary>Each benchmark of the summary with its mean time and its bytes allocated per call.</summary>
	public static IEnumerable<(BenchmarkDotNet.Running.BenchmarkCase Benchmark, double Nanoseconds, long Bytes)> Results(Summary summary)
	{
		foreach (var report in summary.Reports)
		{
			var benchmark = report.BenchmarkCase;
			if (report.ResultStatistics is not { } statistics || report.GcStats.GetBytesAllocatedPerOperation(benchmark) is not { } bytes)
			{
				throw new InvalidOperationException($"{benchmark.DisplayInfo} has no result. See the log under .artifacts/bench/BenchmarkDotNet.");
			}

			yield return (benchmark, statistics.Mean, bytes);
		}
	}

	/// <summary>The machine, the runtime and the tool versions of a run.</summary>
	public static IEnumerable<(string Name, string Value)> Properties(Summary summary, IReadOnlyList<string> jobs)
	{
		yield return ("Date", DateTime.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
		yield return ("Machine", $"{summary.HostEnvironmentInfo.Cpu.Value.ProcessorName}, {Environment.ProcessorCount} logical cores");
		yield return ("OS", $"{RuntimeInformation.OSDescription} ({RuntimeInformation.RuntimeIdentifier})");
		yield return ("Runtime", RuntimeInformation.FrameworkDescription);
		yield return ("BenchmarkDotNet", summary.HostEnvironmentInfo.BenchmarkDotNetVersion);
		yield return ("Jobs", string.Join(", ", jobs.Select(j => j == "JIT" ? $"JIT: {JitWarmupCount} warmup" : j + ": 3 warmup"))
			+ ", and 10 measured iterations, of about 100 ms each");
	}
}
