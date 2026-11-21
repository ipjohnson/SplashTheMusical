using System.Globalization;

namespace SplashTheMusical.Benchmarks;

/// <summary>Measures rows again: fresh STM and Mermaider rows of the cases, stages and jobs of the STM rows it is given.</summary>
/// <param name="slow">STM's rows that were slower than their Mermaider rows.</param>
public delegate (IReadOnlyList<ResultRow> Stm, IReadOnlyList<ResultRow> Baseline) Remeasure(IReadOnlyList<ResultRow> slow);

/// <summary>
/// Holds STM's numbers to the Mermaider baseline. Section 6 of the parity plan sets the budget: no slower and no more
/// allocation than Mermaider on the same case, job and stage.
/// </summary>
public static class Budget
{
	/// <summary>The stages the budget holds. Mermaider's <c>render</c> row is the budget of STM's <c>render</c>, which also parses.</summary>
	public static readonly IReadOnlySet<string> Stages = new HashSet<string>(StringComparer.Ordinal) { "parse" };

	/// <summary>
	/// Writes one line per STM row of a held stage that is slower or allocates more than its Mermaider row, and one per
	/// Mermaider row of a measured type that STM's results lack.
	/// </summary>
	/// <param name="remeasure">
	/// When given, the rows that are slower are measured again, STM's and Mermaider's back to back, and fail only when STM's
	/// is still slower. A shared runner can slow one benchmark process for its whole run, and the two files are measured
	/// minutes apart. Allocation does not vary between runs, so a row that allocates more fails at once.
	/// </param>
	/// <returns>0 when every held row is within the budget, and 1 otherwise.</returns>
	public static int Run(string baselinePath, string stmPath, IReadOnlySet<string> types, TextWriter output, Remeasure? remeasure = null)
	{
		var stm = ResultFile.Read(stmPath).ToDictionary(r => r.Key, StringComparer.Ordinal);
		var failures = 0;
		var held = 0;
		var slow = new List<(ResultRow Stm, ResultRow Budget)>();
		foreach (var budget in ResultFile.Read(baselinePath))
		{
			if (!Stages.Contains(budget.Stage) || !types.Contains(TypeOf(budget.Case)))
			{
				continue;
			}

			held++;
			if (!stm.TryGetValue(budget.Key, out var actual))
			{
				output.WriteLine($"{budget.Key}: missing from {stmPath}");
				failures++;
				continue;
			}

			if (actual.Nanoseconds > budget.Nanoseconds)
			{
				if (remeasure is null)
				{
					output.WriteLine(Over(actual, budget));
					failures++;
				}
				else
				{
					slow.Add((actual, budget));
				}
			}

			if (actual.Bytes > budget.Bytes)
			{
				output.WriteLine($"{budget.Key}: allocation {actual.Bytes} B is over Mermaider's {budget.Bytes} B");
				failures++;
			}
		}

		if (slow.Count > 0)
		{
			failures += Recheck(slow, remeasure!, output);
		}

		output.WriteLine(failures == 0
			? $"{held} rows within the budget of {baselinePath}"
			: $"{failures} rows over the budget of {baselinePath}");
		return failures == 0 ? 0 : 1;
	}

	/// <summary>Measures the slow rows again and writes a line for each, counting the ones still over.</summary>
	private static int Recheck(List<(ResultRow Stm, ResultRow Budget)> slow, Remeasure remeasure, TextWriter output)
	{
		output.WriteLine($"{slow.Count} rows are slower than Mermaider's. Measuring them again.");
		var (stmRows, baselineRows) = remeasure(slow.Select(s => s.Stm).ToList());
		var stm = stmRows.ToDictionary(r => r.Key, StringComparer.Ordinal);
		var baseline = baselineRows.ToDictionary(r => r.Key, StringComparer.Ordinal);
		var failures = 0;
		foreach (var (actual, budget) in slow)
		{
			var first = Over(actual, budget);
			if (!stm.TryGetValue(actual.Key, out var again) || !baseline.TryGetValue(actual.Key, out var againBudget))
			{
				output.WriteLine($"{first}, and measuring it again gave no row");
				failures++;
			}
			else if (again.Nanoseconds > againBudget.Nanoseconds)
			{
				output.WriteLine(first + string.Create(CultureInfo.InvariantCulture,
					$", and again {again.Nanoseconds:F1} ns over {againBudget.Nanoseconds:F1} ns"));
				failures++;
			}
			else
			{
				output.WriteLine(first + string.Create(CultureInfo.InvariantCulture,
					$", but within it when measured again, {again.Nanoseconds:F1} ns to {againBudget.Nanoseconds:F1} ns"));
			}
		}

		return failures;
	}

	private static string Over(ResultRow actual, ResultRow budget) =>
		string.Create(CultureInfo.InvariantCulture, $"{actual.Key}: time {actual.Nanoseconds:F1} ns is over Mermaider's {budget.Nanoseconds:F1} ns");

	/// <summary>The corpus folder of a case id such as <c>flowchart-small</c>.</summary>
	private static string TypeOf(string id) => id[..id.LastIndexOf('-')];
}
