using SplashTheMusical.Config;
using SplashTheMusical.Diagrams.Sequence;

namespace SplashTheMusical.Conformance;

/// <summary>
/// The sequence layout against mermaid.js: L3 on every accepted sequence case. A case that fails must be in
/// <c>corpus/ledger/sequence.txt</c> at L3 under an issue other than #13, which only says the type is unsupported.
/// The cases split in two groups: those with only participants, boxes and messages, and those with notes, blocks,
/// activations, central connections, <c>create</c> or <c>destroy</c>. When <c>STM_DIFF_FILE</c> is set, the count of
/// each group, the count that pass, the L3 differences of every case, and STM's boxes of each case that fails are
/// written to it.
/// </summary>
public class SequenceLayoutTests
{
	/// <summary>The fewest cases of each group that must pass, so the check cannot pass by comparing nothing.</summary>
	private const int MinimumPassing = 100;

	private static readonly Mermaid Stm = new(new MermaidOptions { SiteConfig = StmFactsProvider.OracleSite, Limits = ResourceLimits.Unlimited });

	[Test]
	public void SequenceCasesPassL3()
	{
		var problems = new List<string>();
		var ledger = Ledger.Parse("sequence", "sequence.txt", File.ReadAllLines(Path.Combine(CorpusCheck.CorpusRoot, "ledger", "sequence.txt")), problems);
		var listed = ledger.Entries.Where(e => e.Level == Level.L3).ToDictionary(e => e.Case, e => e.Issue, StringComparer.Ordinal);
		var provider = new StmFactsProvider();
		var all = new List<string>();
		var cases = new int[2];
		var passing = new int[2];
		foreach (var name in CorpusCheck.Cases(CorpusCheck.CorpusRoot).Where(n => n.Split('/', '\\')[0] == "sequence"))
		{
			if (Group(CorpusComparison.Source(CorpusCheck.CorpusRoot, name)) is not { } group)
			{
				continue;
			}

			cases[group]++;
			var result = CorpusComparison.Run(CorpusCheck.CorpusRoot, name, provider)[Level.L3];
			var caseName = name["sequence/".Length..];
			all.AddRange(result.Differences.Select(d => caseName + ": " + d));
			if (result.Outcome != Outcome.Fail)
			{
				passing[group]++;
			}
			else
			{
				var facts = provider.Facts(CorpusComparison.Source(CorpusCheck.CorpusRoot, name));
				all.AddRange(facts.Boxes.Select(b => caseName + ": stm " + b));
				if (!listed.TryGetValue(caseName, out var issue) || issue is null || issue == "#13")
				{
					problems.Add(caseName + " fails L3 and the ledger names no issue for it: " + string.Join(" | ", result.Differences.Take(3)));
				}
			}
		}

		var diffFile = Environment.GetEnvironmentVariable("STM_DIFF_FILE");
		if (!string.IsNullOrEmpty(diffFile))
		{
			File.WriteAllLines(diffFile, [
				$"participants and messages: {passing[0]} of {cases[0]} pass",
				$"notes, blocks, activations, create and destroy: {passing[1]} of {cases[1]} pass",
				.. all]);
		}

		Verify(!problems.Any(), $"{string.Join('\n', problems)}");
		Verify(() => passing[0] >= MinimumPassing && passing[1] >= MinimumPassing);
	}

	/// <summary>
	/// The group of an accepted source: 0 when the layout places only participants, boxes and messages, 1 when it also
	/// has notes, blocks, activations, central connections, created or destroyed participants, and <c>null</c> when
	/// mermaid.js refuses it.
	/// </summary>
	internal static int? Group(string source)
	{
		if (!Stm.TryParse(source, out var document, out _) || document is not SequenceDocument sequence)
		{
			return null;
		}

		var db = SequenceDb.From(sequence);
		var alone = db.CreatedActors.Count == 0
			&& db.DestroyedActors.Count == 0
			&& db.Messages.All(m => m.CentralConnection == 0 && (SequenceLayoutStep.IsSignal(m.Type) || m.Type == SequenceLineType.Autonumber));
		return alone ? 0 : 1;
	}
}
