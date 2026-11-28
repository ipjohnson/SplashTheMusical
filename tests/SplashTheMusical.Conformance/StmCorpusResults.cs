namespace SplashTheMusical.Conformance;

/// <summary>
/// <see cref="StmFactsProvider"/>'s comparison of every corpus case with the oracle, made once per test run and read by
/// <see cref="LedgerTests"/> and <see cref="ParityReportTests"/>.
/// </summary>
internal static class StmCorpusResults
{
	private static readonly Lazy<IReadOnlyList<CaseComparison>> Results = new(() => CorpusComparison.Run(CorpusCheck.CorpusRoot, new StmFactsProvider()));

	public static IReadOnlyList<CaseComparison> All => Results.Value;
}
