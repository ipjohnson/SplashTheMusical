using System.Globalization;

namespace SplashTheMusical.Benchmarks;

/// <summary>Compares a run with the committed numbers.</summary>
public static class Comparison
{
	/// <summary>A run fails when a row's time or allocation is more than this fraction above the committed row.</summary>
	public const double Tolerance = 0.10;

	/// <summary>
	/// Writes one line per regression and per committed row the run lacks.
	/// </summary>
	/// <returns>0 when the run is within <see cref="Tolerance"/> of every committed row, and 1 otherwise.</returns>
	public static int Run(string committedPath, string runPath, TextWriter output)
	{
		var run = ResultFile.Read(runPath).ToDictionary(r => r.Key, StringComparer.Ordinal);
		var committed = ResultFile.Read(committedPath);
		var failures = 0;
		foreach (var expected in committed)
		{
			if (!run.TryGetValue(expected.Key, out var actual))
			{
				output.WriteLine($"{expected.Key}: missing from the run");
				failures++;
				continue;
			}

			if (Worse(expected.Nanoseconds, actual.Nanoseconds))
			{
				output.WriteLine(string.Create(CultureInfo.InvariantCulture,
					$"{expected.Key}: time {expected.Nanoseconds:F1} ns -> {actual.Nanoseconds:F1} ns ({Change(expected.Nanoseconds, actual.Nanoseconds)})"));
				failures++;
			}

			if (Worse(expected.Bytes, actual.Bytes))
			{
				output.WriteLine(string.Create(CultureInfo.InvariantCulture,
					$"{expected.Key}: allocation {expected.Bytes} B -> {actual.Bytes} B ({Change(expected.Bytes, actual.Bytes)})"));
				failures++;
			}
		}

		output.WriteLine(failures == 0
			? $"{committed.Count} rows within {Tolerance:0%} of {committedPath}"
			: $"{failures} regressions against {committedPath}");
		return failures == 0 ? 0 : 1;
	}

	private static bool Worse(double expected, double actual) => actual > expected * (1 + Tolerance);

	private static string Change(double expected, double actual) =>
		expected == 0 ? "was zero" : ((actual - expected) / expected).ToString("+0.0%", CultureInfo.InvariantCulture);
}
