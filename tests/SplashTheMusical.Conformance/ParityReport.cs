using System.Globalization;
using System.Text;

namespace SplashTheMusical.Conformance;

/// <summary>
/// The parity report of section 7 of docs/mermaid-parity-plan.md: one file per diagram type in docs/parity with the
/// pass rate of each level and the ledger counts. No file holds a total, so branches that change different types do
/// not touch the same file. The documentation site adds the rows up.
/// </summary>
public static class ParityReport
{
	/// <summary>The environment variable that rewrites the committed reports when it is set to any value.</summary>
	public const string WriteVariable = "STM_WRITE_REPORT";

	/// <summary>The table's header and separator lines, which tools/site/parity.mjs reads.</summary>
	public const string Header =
		"| Type | Cases | Refused by mermaid.js | L0 | L1 | L2 | L3 | L4 | bug | by-design | out-of-scope |\n"
		+ "|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|\n";

	public static string Folder => Path.Combine(CorpusCheck.RepositoryRoot, "docs", "parity");

	public static string FileName(string type) => type + ".md";

	/// <param name="l4">The L4 outcome of each case from <see cref="RasterOutcomes"/>. A case without one does not count at L4.</param>
	/// <param name="provider">The name of the provider that produced the results.</param>
	/// <param name="mermaid">The mermaid version tools/oracle pins.</param>
	/// <returns>The report of each type that has results, by type.</returns>
	public static IReadOnlyDictionary<string, string> Render(
		IReadOnlyList<CaseComparison> results,
		IReadOnlyList<Ledger> ledgers,
		IReadOnlyDictionary<string, Outcome> l4,
		string provider,
		string mermaid
	)
	{
		var byType = ledgers.ToDictionary(l => l.Type, StringComparer.Ordinal);
		var reports = new SortedDictionary<string, string>(StringComparer.Ordinal);
		foreach (var group in results.GroupBy(r => r.Type))
		{
			var entries = byType.TryGetValue(group.Key, out var ledger) ? ledger.Entries : [];
			var text = new StringBuilder();
			text.Append(CultureInfo.InvariantCulture, $"# Parity of {group.Key}\n\n");
			text.Append(
				CultureInfo.InvariantCulture,
				$"STM, as `{provider}`, against mermaid.js {mermaid} on the {group.Key} cases of `corpus/`. `mise run parity` writes\n"
			);
			text.Append("this file, and `ParityReportTests` in `tests/SplashTheMusical.Conformance` fails when it is out of date.\n");
			text.Append("[The README](README.md) describes the columns.\n\n");
			text.Append(Header);
			Row(text, group.Key, [.. group], l4, entries);
			reports[group.Key] = text.ToString();
		}

		return reports;
	}

	/// <summary>
	/// Writes each report to <paramref name="folder"/> and deletes the report of a type that has none. The README stays.
	/// </summary>
	public static void Write(string folder, IReadOnlyDictionary<string, string> reports)
	{
		_ = Directory.CreateDirectory(folder);
		foreach (var stale in Committed(folder).Where(t => !reports.ContainsKey(t)))
		{
			File.Delete(Path.Combine(folder, FileName(stale)));
		}

		foreach (var (type, text) in reports)
		{
			File.WriteAllText(Path.Combine(folder, FileName(type)), text);
		}
	}

	/// <summary>The type of each report in <paramref name="folder"/>.</summary>
	public static IReadOnlyList<string> Committed(string folder) =>
		Directory.Exists(folder)
			? [
				.. Directory
					.EnumerateFiles(folder, "*.md")
					.Select(f => Path.GetFileNameWithoutExtension(f))
					.Where(t => t != "README")
					.Order(StringComparer.Ordinal),
			]
			: [];

	private static void Row(
		StringBuilder text,
		string type,
		IReadOnlyList<CaseComparison> results,
		IReadOnlyDictionary<string, Outcome> l4,
		IReadOnlyList<LedgerEntry> entries
	)
	{
		var refused = results.Count(OracleRefuses);
		text.Append(CultureInfo.InvariantCulture, $"| {type} | {Count(results.Count)} | {Count(refused)} |");
		foreach (var level in Comparer.Levels)
		{
			var passed = results.Count(r => r[level].Outcome == Outcome.Pass);
			var applies = results.Count(r => r[level].Outcome != Outcome.NotApplicable);
			text.Append(' ').Append(Rate(passed, applies)).Append(" |");
		}

		var raster = results.Select(r => l4.GetValueOrDefault(RasterOutcomes.Key(r.Name), Outcome.NotApplicable)).ToList();
		var judged = raster.Count(o => o is Outcome.Pass or Outcome.Fail);
		text.Append(' ')
			.Append(
				judged == 0 && raster.Contains(Outcome.ReportOnly) ? "report-only" : Rate(raster.Count(o => o == Outcome.Pass), judged)
			)
			.Append(" |");

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
