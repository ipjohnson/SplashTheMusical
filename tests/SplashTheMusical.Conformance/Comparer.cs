namespace SplashTheMusical.Conformance;

/// <summary>The exact levels of section 1 of docs/mermaid-parity-plan.md.</summary>
public enum Level
{
	/// <summary>Acceptance: the <c>accept</c> line.</summary>
	L0,

	/// <summary>Model: every fact except <c>drawn</c> and <c>label</c>.</summary>
	L1,

	/// <summary>Structure: the <c>drawn</c> and <c>label</c> facts, and the kind and name of every box.</summary>
	L2,
}

public enum Outcome
{
	Pass,
	Fail,

	/// <summary>Both sides refuse the source, so it has no facts to compare at L1 or L2.</summary>
	NotApplicable,
}

/// <param name="Missing">True for a line the oracle has and the provider lacks, false for a line only the provider has.</param>
public sealed record Difference(Level Level, bool Missing, string Line)
{
	public override string ToString() => $"{Level} {(Missing ? '-' : '+')} {Line}";
}

public sealed record LevelResult(Level Level, Outcome Outcome, IReadOnlyList<Difference> Differences);

/// <param name="Name">The case's path relative to the corpus folder, without the .mmd extension.</param>
/// <param name="Type">The corpus folder of the case's diagram type.</param>
/// <param name="Levels">One result per <see cref="Level"/>, in order.</param>
public sealed record CaseComparison(string Name, string Type, IReadOnlyList<LevelResult> Levels)
{
	public LevelResult this[Level level] => Levels[(int)level];
}

/// <summary>Diffs a provider's facts with the oracle's, per level. Lines are compared as multisets.</summary>
public static class Comparer
{
	public static IReadOnlyList<Level> Levels { get; } = [Level.L0, Level.L1, Level.L2];

	public static CaseComparison Compare(string name, CaseFacts oracle, CaseFacts actual)
	{
		var type = name.Split('/', '\\')[0];
		var levels = new List<LevelResult>(Levels.Count);
		foreach (var level in Levels)
		{
			if (level != Level.L0 && !oracle.Accepted && !actual.Accepted)
			{
				levels.Add(new LevelResult(level, Outcome.NotApplicable, []));
				continue;
			}

			var differences = Diff(level, Lines(level, oracle), Lines(level, actual));
			levels.Add(new LevelResult(level, differences.Count == 0 ? Outcome.Pass : Outcome.Fail, differences));
		}

		return new CaseComparison(name, type, levels);
	}

	/// <summary>The lines of a provider's facts that a level compares.</summary>
	public static IReadOnlyList<string> Lines(Level level, CaseFacts facts) =>
		level switch
		{
			Level.L0 => [facts.Accepted ? "accept\tyes" : "accept\tno"],
			Level.L1 => [.. facts.Facts.Where(f => !IsStructure(f))],
			Level.L2 => [.. facts.Facts.Where(IsStructure), .. facts.Boxes.Select(BoxIdentity)],
			_ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
		};

	private static bool IsStructure(string fact) =>
		fact.StartsWith("drawn\t", StringComparison.Ordinal) || fact.StartsWith("label\t", StringComparison.Ordinal);

	// A box line is box, kind, name, x, y, width and height. L2 keeps the first three fields and L3 compares the rest.
	private static string BoxIdentity(string box)
	{
		var end = -1;
		for (var field = 0; field < 3; field++)
		{
			end = box.IndexOf('\t', end + 1);
			if (end < 0)
			{
				return box;
			}
		}

		return box[..end];
	}

	private static List<Difference> Diff(Level level, IReadOnlyList<string> expected, IReadOnlyList<string> actual)
	{
		var counts = new Dictionary<string, int>(StringComparer.Ordinal);
		foreach (var line in expected)
		{
			counts[line] = counts.GetValueOrDefault(line) + 1;
		}

		foreach (var line in actual)
		{
			counts[line] = counts.GetValueOrDefault(line) - 1;
		}

		var differences = new List<Difference>();
		foreach (var (line, count) in counts.OrderBy(c => c.Key, StringComparer.Ordinal))
		{
			for (var i = 0; i < Math.Abs(count); i++)
			{
				differences.Add(new Difference(level, count > 0, line));
			}
		}

		return differences;
	}
}
