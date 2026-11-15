namespace SplashTheMusical.Conformance;

/// <summary>Why a difference from mermaid.js is accepted. Section 7 of docs/mermaid-parity-plan.md lists them.</summary>
public enum LedgerCategory
{
	Bug,
	ByDesign,
	OutOfScope,
}

/// <param name="Case">The case's path below its type folder, without the .mmd extension.</param>
/// <param name="Issue">The issue a <see cref="LedgerCategory.Bug"/> entry names, as #N or owner/repo#N.</param>
public sealed record LedgerEntry(string Case, Level Level, LedgerCategory Category, string? Issue);

/// <summary>
/// The accepted differences of one diagram type: each entry is a case that fails one level. corpus/ledger/README.md
/// describes the file.
/// </summary>
public sealed class Ledger(string type, IReadOnlyList<LedgerEntry> entries)
{
	private const string ByDesign = "by-design";
	private const string OutOfScope = "out-of-scope";
	private const string BugPrefix = "bug ";

	public string Type { get; } = type;

	public IReadOnlyList<LedgerEntry> Entries { get; } = entries;

	public int Bugs => Entries.Count(e => e.Category == LedgerCategory.Bug);

	/// <summary>The file name of a type's ledger in the ledger folder.</summary>
	public static string FileName(string type) => type + ".txt";

	/// <param name="file">The name problems are reported against.</param>
	public static Ledger Parse(string type, string file, IEnumerable<string> lines, List<string> problems)
	{
		var entries = new List<LedgerEntry>();
		var seen = new HashSet<(string, Level)>();
		LedgerCategory? category = null;
		string? issue = null;
		var number = 0;
		foreach (var line in lines)
		{
			number++;
			if (line.Length == 0 || line[0] == '#')
			{
				continue;
			}

			var tab = line.IndexOf('\t');
			if (tab < 0)
			{
				(category, issue) = ParseCategory(line);
				if (category is null)
				{
					problems.Add($"{file}:{number}: {line} is not by-design, out-of-scope or bug with an issue such as #13");
				}

				continue;
			}

			var name = line[..tab];
			var level = ParseLevel(line[(tab + 1)..]);
			if (level is null)
			{
				problems.Add($"{file}:{number}: {line[(tab + 1)..]} is not L0, L1 or L2");
			}
			else if (category is null)
			{
				problems.Add($"{file}:{number}: {name} is not under a category");
			}
			else if (!seen.Add((name, level.Value)))
			{
				problems.Add($"{file}:{number}: {name} {level} is listed twice");
			}
			else
			{
				entries.Add(new LedgerEntry(name, level.Value, category.Value, issue));
			}
		}

		return new Ledger(type, entries);
	}

	/// <summary>The file's lines: bug entries grouped by issue, then by-design, then out-of-scope.</summary>
	public IEnumerable<string> Lines()
	{
		yield return $"# The parity ledger of {Type}. corpus/ledger/README.md describes it.";
		var groups = Entries
			.GroupBy(e => (e.Category, e.Issue))
			.OrderBy(g => g.Key.Category)
			.ThenBy(g => g.Key.Issue, StringComparer.Ordinal);
		foreach (var group in groups)
		{
			yield return group.Key.Category switch
			{
				LedgerCategory.Bug => BugPrefix + group.Key.Issue,
				LedgerCategory.ByDesign => ByDesign,
				_ => OutOfScope,
			};

			foreach (var entry in group.OrderBy(e => e.Case, StringComparer.Ordinal).ThenBy(e => e.Level))
			{
				yield return $"{entry.Case}\t{entry.Level}";
			}
		}
	}

	/// <summary>Whether text is an issue reference: #N or owner/repo#N.</summary>
	public static bool IsIssue(string text)
	{
		var hash = text.IndexOf('#');
		if (hash < 0 || hash == text.Length - 1 || text.AsSpan(hash + 1).ContainsAnyExceptInRange('0', '9'))
		{
			return false;
		}

		if (hash == 0)
		{
			return true;
		}

		var repository = text.AsSpan(0, hash);
		var slash = repository.IndexOf('/');
		return slash > 0
			&& slash < repository.Length - 1
			&& repository[(slash + 1)..].IndexOf('/') < 0
			&& repository.IndexOfAny(' ', '\t') < 0;
	}

	private static (LedgerCategory?, string?) ParseCategory(string line)
	{
		if (line == ByDesign)
		{
			return (LedgerCategory.ByDesign, null);
		}

		if (line == OutOfScope)
		{
			return (LedgerCategory.OutOfScope, null);
		}

		if (line.StartsWith(BugPrefix, StringComparison.Ordinal) && IsIssue(line[BugPrefix.Length..]))
		{
			return (LedgerCategory.Bug, line[BugPrefix.Length..]);
		}

		return (null, null);
	}

	private static Level? ParseLevel(string text) =>
		text switch
		{
			"L0" => Level.L0,
			"L1" => Level.L1,
			"L2" => Level.L2,
			_ => null,
		};
}
