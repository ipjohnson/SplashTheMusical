namespace SplashTheMusical.Conformance;

/// <summary>
/// The committed count of <see cref="LedgerCategory.Bug"/> entries per type, and the mermaid version they were
/// counted against. A count may only go up when that version changes.
/// </summary>
/// <param name="Counts">The count per type. A type that is not listed has no bugs.</param>
public sealed record BugCounts(string Mermaid, IReadOnlyDictionary<string, int> Counts)
{
	public const string FileName = "bugs.txt";

	private const string MermaidPrefix = "# mermaid ";

	/// <summary>The counts in a file, or null when the file does not exist.</summary>
	public static BugCounts? Read(string path, List<string> problems)
	{
		if (!File.Exists(path))
		{
			return null;
		}

		string? mermaid = null;
		var counts = new Dictionary<string, int>(StringComparer.Ordinal);
		var number = 0;
		foreach (var line in File.ReadLines(path))
		{
			number++;
			if (line.StartsWith(MermaidPrefix, StringComparison.Ordinal))
			{
				mermaid = line[MermaidPrefix.Length..];
				continue;
			}

			if (line.Length == 0 || line[0] == '#')
			{
				continue;
			}

			var tab = line.IndexOf('\t');
			if (tab < 0 || !int.TryParse(line.AsSpan(tab + 1), out var count) || count < 0)
			{
				problems.Add($"{FileName}:{number}: {line} is not a type, a tab and a count");
			}
			else if (!counts.TryAdd(line[..tab], count))
			{
				problems.Add($"{FileName}:{number}: {line[..tab]} is listed twice");
			}
		}

		if (mermaid is null)
		{
			problems.Add($"{FileName}: no {MermaidPrefix}line");
		}

		return new BugCounts(mermaid ?? "", counts);
	}

	public IEnumerable<string> Lines()
	{
		yield return MermaidPrefix + Mermaid;
		yield return "# The bug entries of each ledger. corpus/ledger/README.md describes it.";
		foreach (var (type, count) in Counts.OrderBy(c => c.Key, StringComparer.Ordinal))
		{
			yield return $"{type}\t{count}";
		}
	}

	/// <summary>One line per type whose ledger holds another number of bugs than the file records.</summary>
	/// <param name="mermaid">The mermaid version tools/oracle pins.</param>
	public static IReadOnlyList<string> Problems(BugCounts? recorded, IEnumerable<Ledger> ledgers, string mermaid)
	{
		if (recorded is null)
		{
			return [$"{FileName} does not exist. Write it with STM_WRITE_LEDGER"];
		}

		if (recorded.Mermaid != mermaid)
		{
			return [$"{FileName} counts bugs for mermaid {recorded.Mermaid} and tools/oracle pins {mermaid}. Rewrite it with STM_WRITE_LEDGER"];
		}

		var problems = new List<string>();
		var actual = Actual(ledgers);
		foreach (var type in actual.Keys.Union(recorded.Counts.Keys).Order(StringComparer.Ordinal))
		{
			var now = actual.GetValueOrDefault(type);
			var then = recorded.Counts.GetValueOrDefault(type);
			if (now > then)
			{
				problems.Add($"{type}: {now} bugs, {FileName} allows {then}. The count may only go up when the mermaid version changes");
			}
			else if (now < then)
			{
				problems.Add($"{type}: {now} bugs, {FileName} records {then}. Lower it to {now}");
			}
		}

		return problems;
	}

	/// <summary>
	/// The counts to write: the ledgers' counts when the version changed, and otherwise each recorded count lowered to
	/// the ledger's, so a count that went up still fails.
	/// </summary>
	public static BugCounts Next(BugCounts? recorded, IEnumerable<Ledger> ledgers, string mermaid)
	{
		var actual = Actual(ledgers);
		if (recorded is null || recorded.Mermaid != mermaid)
		{
			return new BugCounts(mermaid, actual);
		}

		return new BugCounts(
			mermaid,
			actual.Keys.ToDictionary(t => t, t => Math.Min(actual[t], recorded.Counts.GetValueOrDefault(t)), StringComparer.Ordinal)
		);
	}

	private static Dictionary<string, int> Actual(IEnumerable<Ledger> ledgers) =>
		ledgers.ToDictionary(l => l.Type, l => l.Bugs, StringComparer.Ordinal);
}
