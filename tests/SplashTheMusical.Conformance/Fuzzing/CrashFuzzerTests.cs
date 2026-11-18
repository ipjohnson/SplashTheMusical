using SplashTheMusical.Conformance.Fuzzing;

namespace SplashTheMusical.Conformance;

public class CrashFuzzerTests
{
	private static readonly string[] Seeds = ["flowchart LR\n  A((a)) --> B[b]\n", "stateDiagram-v2\n  [*] --> S\n"];

	[Test]
	public async Task KeepsEachSourceTheTargetThrowsOnWithAnExceptionOtherThanAParseException()
	{
		var run = CrashFuzzer.Run("test", Seeds, 2000, 3, () => source =>
		{
			if (source.Contains("((", StringComparison.Ordinal) && source.Contains("[b]", StringComparison.Ordinal))
			{
				throw new InvalidOperationException("broken");
			}

			throw new MermaidParseException(new MermaidDiagnostic(DiagnosticSeverity.Error, "refused", 1, 1));
		}, TimeSpan.FromSeconds(5), threads: 4);

		await Assert.That(run.Finds.Count).IsEqualTo(CrashFuzzer.MaxFinds);
		foreach (var find in run.Finds)
		{
			await Assert.That(find.Problem).StartsWith("System.InvalidOperationException: broken");
			await Assert.That(find.Source).IsEqualTo(CrashFuzzer.Source(Seeds, 3, find.Index));
		}
	}

	[Test]
	public async Task KeepsASourceThatTakesLongerThanTheDeadline()
	{
		var run = CrashFuzzer.Run("test", Seeds, 200, 3, () => source =>
		{
			if (source.StartsWith("state", StringComparison.Ordinal) && source.Contains("-->", StringComparison.Ordinal))
			{
				Thread.Sleep(60);
			}
		}, TimeSpan.FromMilliseconds(30), threads: 2);

		await Assert.That(run.Finds.Count).IsGreaterThan(0);
		await Assert.That(run.Finds[0].Problem).Contains("past the 0.03 s deadline");
	}

	[Test]
	public async Task EndsTheRunOnASourceThatDoesNotReturn()
	{
		using var release = new ManualResetEventSlim();
		var run = CrashFuzzer.Run("test", Seeds, 1_000_000, 3, () => source =>
		{
			if (source.Contains("((", StringComparison.Ordinal))
			{
				release.Wait();
			}
		}, TimeSpan.FromMilliseconds(100), threads: 2);
		release.Set();

		await Assert.That(run.Finds.Count).IsGreaterThan(0);
		await Assert.That(run.Finds[^1].Problem).IsEqualTo("did not return within 0.2 s");
		await Assert.That(run.Sources).IsLessThan(1_000_000);
	}

	[Test]
	public async Task RunsMutatedCorpusCasesOfEveryFuzzedTypeThroughStmWithoutACrash()
	{
		var corpus = CorpusCheck.CorpusRoot;
		var folders = FuzzTypes.Folders(corpus);

		await Assert.That(string.Join(",", folders)).IsEqualTo("flowchart,info,state");
		foreach (var folder in folders)
		{
			var run = CrashFuzzer.Run(folder, FuzzTypes.Sources(corpus, folder), 300, 1, CrashFuzzer.Stm, ResourceLimits.Default.RenderDeadline!.Value, Environment.ProcessorCount);

			await Assert.That(string.Join("\n", run.Finds.Select(f => $"{f.Index}: {f.Problem}\n{Mutator.Escape(f.Source)}"))).IsEmpty();
			await Assert.That(run.Sources).IsEqualTo(300);
		}
	}
}
