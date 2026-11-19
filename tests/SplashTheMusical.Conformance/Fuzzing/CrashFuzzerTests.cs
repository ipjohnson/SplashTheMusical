using SplashTheMusical.Conformance.Fuzzing;

namespace SplashTheMusical.Conformance;

public class CrashFuzzerTests
{
	private static readonly string[] Seeds = ["flowchart LR\n  A((a)) --> B[b]\n", "stateDiagram-v2\n  [*] --> S\n"];

	[Test]
	public void KeepsEachSourceTheTargetThrowsOnWithAnExceptionOtherThanAParseException()
	{
		var run = CrashFuzzer.Run("test", Seeds, 2000, 3, () => source =>
		{
			if (source.Contains("((", StringComparison.Ordinal) && source.Contains("[b]", StringComparison.Ordinal))
			{
				throw new InvalidOperationException("broken");
			}

			throw new MermaidParseException(new MermaidDiagnostic(DiagnosticSeverity.Error, "refused", 1, 1));
		}, TimeSpan.FromSeconds(5), threads: 4);

		Verify(run.Finds.Count == CrashFuzzer.MaxFinds, $"was {run.Finds.Count}");
		foreach (var find in run.Finds)
		{
			Verify(find.Problem.StartsWith("System.InvalidOperationException: broken", StringComparison.Ordinal), $"was {find.Problem}");
			Verify(find.Source == CrashFuzzer.Source(Seeds, 3, find.Index), $"was {find.Source}");
		}
	}

	[Test]
	public void KeepsASourceThatTakesLongerThanTheDeadline()
	{
		var run = CrashFuzzer.Run("test", Seeds, 200, 3, () => source =>
		{
			if (source.StartsWith("state", StringComparison.Ordinal) && source.Contains("-->", StringComparison.Ordinal))
			{
				Thread.Sleep(250);
			}
		}, TimeSpan.FromMilliseconds(200), threads: 2);

		Verify(run.Finds.Count > 0, $"was {run.Finds.Count}");
		Verify(run.Finds[0].Problem.Contains("past the 0.2 s deadline"), $"{run.Finds[0].Problem}");
	}

	[Test]
	public void EndsTheRunOnASourceThatDoesNotReturn()
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

		Verify(run.Finds.Count > 0, $"was {run.Finds.Count}");
		Verify(run.Finds[^1].Problem == "did not return within 0.2 s", $"was {run.Finds[^1].Problem}");
		Verify(run.Sources < 1_000_000, $"was {run.Sources}");
	}

	[Test]
	public void RunsMutatedCorpusCasesOfEveryFuzzedTypeThroughStmWithoutACrash()
	{
		var corpus = CorpusCheck.CorpusRoot;
		var folders = FuzzTypes.Folders(corpus);

		Verify(string.Join(",", folders) == "class,er,flowchart,info,state", $"was {string.Join(",", folders)}");
		foreach (var folder in folders)
		{
			var run = CrashFuzzer.Run(folder, FuzzTypes.Sources(corpus, folder), 300, 1, CrashFuzzer.Stm, ResourceLimits.Default.RenderDeadline!.Value, Environment.ProcessorCount);

			Verify(!run.Finds.Select(f => $"{f.Index}: {f.Problem}\n{Mutator.Escape(f.Source)}").Any(), $"{(string.Join("\n", run.Finds.Select(f => $"{f.Index}: {f.Problem}\n{Mutator.Escape(f.Source)}")))}");
			Verify(run.Sources == 300, $"was {run.Sources}");
		}
	}
}
