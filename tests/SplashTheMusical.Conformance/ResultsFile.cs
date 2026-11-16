using System.Text;

namespace SplashTheMusical.Conformance;

/// <summary>
/// The comparison results of every corpus case as text: one line per case and level with its outcome, and under it one
/// indented line per difference. CI writes it from the JIT run and the native run, and fails when the two differ.
/// </summary>
public static class ResultsFile
{
	/// <summary>The environment variable that names the file <see cref="LedgerTests"/> writes the corpus results to.</summary>
	public const string Variable = "STM_RESULTS_FILE";

	public static string Text(IEnumerable<CaseComparison> results)
	{
		var text = new StringBuilder();
		foreach (var result in results.OrderBy(r => r.Name.Replace('\\', '/'), StringComparer.Ordinal))
		{
			var name = result.Name.Replace('\\', '/');
			foreach (var level in result.Levels)
			{
				text.Append(name).Append('\t').Append(level.Level).Append('\t').Append(Outcome(level.Outcome)).Append('\n');
				foreach (var difference in level.Differences)
				{
					text.Append('\t').Append(difference).Append('\n');
				}
			}
		}

		return text.ToString();
	}

	/// <summary>Writes the results to the file <see cref="Variable"/> names, when it is set.</summary>
	public static void WriteIfRequested(IEnumerable<CaseComparison> results)
	{
		if (Environment.GetEnvironmentVariable(Variable) is not { Length: > 0 } path)
		{
			return;
		}

		_ = Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
		File.WriteAllText(path, Text(results));
	}

	private static string Outcome(Outcome outcome) =>
		outcome switch
		{
			Conformance.Outcome.Pass => "pass",
			Conformance.Outcome.Fail => "fail",
			Conformance.Outcome.NotApplicable => "n/a",
			Conformance.Outcome.ReportOnly => "report",
			_ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
		};
}
