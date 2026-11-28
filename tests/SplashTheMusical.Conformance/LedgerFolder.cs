namespace SplashTheMusical.Conformance;

/// <summary>Checks a provider's comparison results against the ledgers of a ledger folder.</summary>
public static class LedgerFolder
{
	/// <summary>The environment variable that rewrites the ledgers from the current differences. Its value is the issue new bug entries name.</summary>
	public const string WriteVariable = "STM_WRITE_LEDGER";

	public static string Root => Path.Combine(CorpusCheck.CorpusRoot, "ledger");

	/// <summary>The ledger of every type that has a file in the folder.</summary>
	public static IReadOnlyList<Ledger> Read(string folder, List<string> problems)
	{
		if (!Directory.Exists(folder))
		{
			return [];
		}

		return
		[
			.. Directory
				.EnumerateFiles(folder, "*.txt")
				.Select(Path.GetFileName)
				.Order(StringComparer.Ordinal)
				.Select(f => Ledger.Parse(f![..^".txt".Length], f, File.ReadLines(Path.Combine(folder, f)), problems)),
		];
	}

	/// <summary>One line per difference no ledger lists and per listed difference that no longer occurs.</summary>
	public static IReadOnlyList<string> Problems(string folder, IReadOnlyList<CaseComparison> results)
	{
		var problems = new List<string>();
		var ledgers = Read(folder, problems);
		var types = results.Select(r => r.Type).ToHashSet(StringComparer.Ordinal);
		foreach (var ledger in ledgers.Where(l => !types.Contains(l.Type)))
		{
			problems.Add($"{Ledger.FileName(ledger.Type)}: the corpus has no {ledger.Type} cases");
		}

		var listed = ledgers.SelectMany(l => l.Entries.Select(e => (l.Type, e.Case, e.Level))).ToHashSet();
		var failing = Failing(results).ToHashSet();
		foreach (var (type, name, level) in Failing(results).Where(f => !listed.Contains(f)))
		{
			problems.Add($"{type}/{name} {level}: differs from mermaid.js and {Ledger.FileName(type)} does not list it");
		}

		foreach (var ledger in ledgers)
		{
			foreach (var entry in ledger.Entries.Where(e => !failing.Contains((ledger.Type, e.Case, e.Level))))
			{
				problems.Add($"{ledger.Type}/{entry.Case} {entry.Level}: {Ledger.FileName(ledger.Type)} lists it and it no longer differs");
			}
		}

		return problems;
	}

	/// <summary>
	/// Rewrites the folder so its ledgers list exactly the current differences. A listed difference keeps its category,
	/// and a new one is a bug that names <paramref name="issue"/>.
	/// </summary>
	public static void Write(string folder, IReadOnlyList<CaseComparison> results, string issue)
	{
		if (!Ledger.IsIssue(issue))
		{
			throw new ArgumentException($"{issue} is not an issue such as #13 or owner/repo#13", nameof(issue));
		}

		var problems = new List<string>();
		var current = Read(folder, problems);
		if (problems.Count > 0)
		{
			throw new InvalidOperationException("Fix the ledger folder first:\n" + string.Join('\n', problems));
		}

		var known = current
			.SelectMany(l => l.Entries.Select(e => (Key: (l.Type, e.Case, e.Level), Entry: e)))
			.ToDictionary(p => p.Key, p => p.Entry);
		var ledgers = new List<Ledger>();
		foreach (var type in results.Select(r => r.Type).Distinct().Order(StringComparer.Ordinal))
		{
			var entries = Failing(results.Where(r => r.Type == type))
				.Select(f => known.TryGetValue(f, out var entry) ? entry : new LedgerEntry(f.Case, f.Level, LedgerCategory.Bug, issue))
				.ToList();
			ledgers.Add(new Ledger(type, entries));
		}

		_ = Directory.CreateDirectory(folder);
		foreach (var ledger in current.Where(c => !ledgers.Any(l => l.Type == c.Type)))
		{
			File.Delete(Path.Combine(folder, Ledger.FileName(ledger.Type)));
		}

		foreach (var ledger in ledgers)
		{
			WriteLines(Path.Combine(folder, Ledger.FileName(ledger.Type)), ledger.Lines());
		}
	}

	// Every case and level that fails, with the case's path below its type folder.
	private static IEnumerable<(string Type, string Case, Level Level)> Failing(IEnumerable<CaseComparison> results) =>
		results.SelectMany(r =>
			r.Levels.Where(l => l.Outcome == Outcome.Fail).Select(l => (r.Type, r.Name.Replace('\\', '/')[(r.Type.Length + 1)..], l.Level))
		);

	private static void WriteLines(string path, IEnumerable<string> lines) => File.WriteAllText(path, string.Join('\n', lines) + "\n");
}
