namespace SplashTheMusical.Conformance;

public class ElkStageSpikeTests
{
	/// <summary>The folder <see cref="RunsWhenAsked"/> writes <c>report.md</c> to. Without it the test does nothing.</summary>
	public const string FolderVariable = "STM_ELK_SPIKES_DIR";

	[Test]
	public void RunsWhenAsked()
	{
		if (Environment.GetEnvironmentVariable(FolderVariable) is not { Length: > 0 } folder)
		{
			return;
		}

		var run = ElkStageSpikes.Measure(CorpusCheck.CorpusRoot);
		var report = ElkStageSpikes.Report(run);
		Directory.CreateDirectory(folder);
		File.WriteAllText(Path.Combine(folder, "report.md"), report);
		Console.Write(report);
		Verify(() => run.Outcomes.Count > 0);
	}
}
