using System.Globalization;
using System.Text;

namespace SplashTheMusical.Conformance;

/// <summary>
/// The parity report of section 7 of docs/mermaid-parity-plan.md: per diagram type, the pass rate of each level and
/// the ledger counts.
/// </summary>
public static class ParityReport
{
	/// <summary>The environment variable that rewrites the committed report when it is set to any value.</summary>
	public const string WriteVariable = "STM_WRITE_REPORT";

	public static string FilePath => Path.Combine(CorpusCheck.RepositoryRoot, "docs", "parity-report.md");

	/// <param name="provider">The name of the provider that produced the results.</param>
	/// <param name="mermaid">The mermaid version tools/oracle pins.</param>
	public static string Render(IReadOnlyList<CaseComparison> results, IReadOnlyList<Ledger> ledgers, string provider, string mermaid)
	{
		var text = new StringBuilder();
		text.Append("# Parity report\n\n");
		text.Append(
			CultureInfo.InvariantCulture,
			$"STM, as `{provider}`, against mermaid.js {mermaid} on the {Count(results.Count)} cases of `corpus/`. `mise run parity` writes\n"
		);
		text.Append("this file, and `ParityReportTests` in `tests/SplashTheMusical.Conformance` fails when it is out of date.\n\n");
		text.Append("A level's rate is the cases that pass it out of the cases it applies to. L1 and L2 do not apply to a case that both\n");
		text.Append("mermaid.js and STM refuse. Section 7 of [the parity plan](mermaid-parity-plan.md) describes the levels, and\n");
		text.Append("[the ledger README](../corpus/ledger/README.md) the categories of the ledger counts.\n\n");
		text.Append("| Type | Cases | Refused by mermaid.js | L0 | L1 | L2 | bug | by-design | out-of-scope |\n");
		text.Append("|---|---:|---:|---:|---:|---:|---:|---:|---:|\n");

		var byType = ledgers.ToDictionary(l => l.Type, StringComparer.Ordinal);
		foreach (var group in results.GroupBy(r => r.Type).OrderBy(g => g.Key, StringComparer.Ordinal))
		{
			var entries = byType.TryGetValue(group.Key, out var ledger) ? ledger.Entries : [];
			Row(text, group.Key, [.. group], entries);
		}

		Row(text, "**Total**", results, [.. ledgers.SelectMany(l => l.Entries)]);
		return text.ToString();
	}

	private static void Row(StringBuilder text, string type, IReadOnlyList<CaseComparison> results, IReadOnlyList<LedgerEntry> entries)
	{
		var refused = results.Count(OracleRefuses);
		text.Append(CultureInfo.InvariantCulture, $"| {type} | {Count(results.Count)} | {Count(refused)} |");
		foreach (var level in Comparer.Levels)
		{
			var passed = results.Count(r => r[level].Outcome == Outcome.Pass);
			var applies = results.Count(r => r[level].Outcome != Outcome.NotApplicable);
			text.Append(' ').Append(Rate(passed, applies)).Append(" |");
		}

		foreach (var category in new[] { LedgerCategory.Bug, LedgerCategory.ByDesign, LedgerCategory.OutOfScope })
		{
			text.Append(' ').Append(Count(entries.Count(e => e.Category == category))).Append(" |");
		}

		text.Append('\n');
	}

	// L1 does not apply when both sides refuse, and L0 misses the oracle's refusal when only the provider accepts.
	private static bool OracleRefuses(CaseComparison result) =>
		result[Level.L1].Outcome == Outcome.NotApplicable || result[Level.L0].Differences.Any(d => d.Missing && d.Line == "accept\tno");

	private static string Rate(int passed, int applies) =>
		applies == 0
			? "n/a"
			: string.Create(CultureInfo.InvariantCulture, $"{100.0 * passed / applies:0.0}% ({Count(passed)} of {Count(applies)})");

	private static string Count(int count) => count.ToString("#,0", CultureInfo.InvariantCulture);
}
