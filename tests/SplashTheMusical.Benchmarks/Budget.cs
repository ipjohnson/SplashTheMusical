using System.Globalization;

namespace SplashTheMusical.Benchmarks;

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
	/// <returns>0 when every held row is within the budget, and 1 otherwise.</returns>
	public static int Run(string baselinePath, string stmPath, IReadOnlySet<string> types, TextWriter output)
	{
		var stm = ResultFile.Read(stmPath).ToDictionary(r => r.Key, StringComparer.Ordinal);
		var failures = 0;
		var held = 0;
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
				output.WriteLine(string.Create(CultureInfo.InvariantCulture,
					$"{budget.Key}: time {actual.Nanoseconds:F1} ns is over Mermaider's {budget.Nanoseconds:F1} ns"));
				failures++;
			}

			if (actual.Bytes > budget.Bytes)
			{
				output.WriteLine($"{budget.Key}: allocation {actual.Bytes} B is over Mermaider's {budget.Bytes} B");
				failures++;
			}
		}

		output.WriteLine(failures == 0
			? $"{held} rows within the budget of {baselinePath}"
			: $"{failures} rows over the budget of {baselinePath}");
		return failures == 0 ? 0 : 1;
	}

	/// <summary>The corpus folder of a case id such as <c>flowchart-small</c>.</summary>
	private static string TypeOf(string id) => id[..id.LastIndexOf('-')];
}
