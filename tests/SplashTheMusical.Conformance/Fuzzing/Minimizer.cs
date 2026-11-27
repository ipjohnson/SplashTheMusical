namespace SplashTheMusical.Conformance.Fuzzing;

/// <summary>
/// Shrinks a source while a test holds, with Zeller's ddmin: first over its lines, then over its
/// <see cref="SourceTokens"/>, again until neither pass removes anything or the tests run out.
/// </summary>
public static class Minimizer
{
	/// <param name="keeps">Whether a smaller source still shows what the original does.</param>
	/// <param name="maxTests">The most calls of <paramref name="keeps"/>.</param>
	/// <param name="tests">The calls made.</param>
	public static string Minimize(string source, Func<string, bool> keeps, int maxTests, out int tests)
	{
		var budget = new Budget(maxTests);
		bool Joined(List<string> units) => keeps(string.Concat(units));
		while (true)
		{
			var before = source;
			source = string.Concat(DeltaDebug(SourceTokens.Lines(source), Joined, budget));
			source = string.Concat(DeltaDebug(SourceTokens.Split(source).ConvertAll(t => t.Text), Joined, budget));
			if (source == before || budget.Spent)
			{
				tests = budget.Used;
				return source;
			}
		}
	}

	/// <summary>Shrinks a list of units while a test holds, with one pass of ddmin.</summary>
	/// <param name="keeps">Whether a sublist, in the units' order, still shows what the whole list does.</param>
	/// <param name="maxTests">The most calls of <paramref name="keeps"/>.</param>
	/// <param name="tests">The calls made.</param>
	public static List<T> DeltaDebug<T>(List<T> units, Func<List<T>, bool> keeps, int maxTests, out int tests)
	{
		var budget = new Budget(maxTests);
		var reduced = DeltaDebug(units, keeps, budget);
		tests = budget.Used;
		return reduced;
	}

	private sealed class Budget(int max)
	{
		public int Used { get; private set; }

		public bool Spent => Used >= max;

		public bool Try<T>(Func<T, bool> keeps, T candidate)
		{
			Used++;
			return keeps(candidate);
		}
	}

	private static List<T> DeltaDebug<T>(List<T> units, Func<List<T>, bool> keeps, Budget budget)
	{
		var parts = 2;
		while (units.Count >= 2 && !budget.Spent)
		{
			var size = (units.Count + parts - 1) / parts;
			var reduced = false;
			for (var start = 0; start < units.Count && !budget.Spent; start += size)
			{
				var candidate = new List<T>(units.Count);
				candidate.AddRange(units.Take(start));
				candidate.AddRange(units.Skip(start + size));
				if (budget.Try(keeps, candidate))
				{
					units = candidate;
					parts = Math.Max(parts - 1, 2);
					reduced = true;
					break;
				}
			}

			if (!reduced)
			{
				if (parts >= units.Count)
				{
					break;
				}

				parts = Math.Min(parts * 2, units.Count);
			}
		}

		return units;
	}
}
