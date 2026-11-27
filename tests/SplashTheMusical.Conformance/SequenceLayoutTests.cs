using SplashTheMusical.Config;
using SplashTheMusical.Diagrams.Sequence;

namespace SplashTheMusical.Conformance;

/// <summary>
/// The sequence layout of participants and messages against mermaid.js: L3 on the accepted sequence cases without
/// notes, blocks, activations, central connections, <c>create</c> or <c>destroy</c>, whose boxes it places alone. A
/// case that fails must be in <c>corpus/ledger/sequence.txt</c> at L3 under an issue other than #13, which only says
/// the type is unsupported. When <c>STM_DIFF_FILE</c> is set, the L3 differences of every such case are written to it.
/// </summary>
public class SequenceLayoutTests
{
	/// <summary>The fewest such cases that must pass, so the check cannot pass by comparing nothing.</summary>
	private const int MinimumPassing = 100;

	private static readonly Mermaid Stm = new(new MermaidOptions { SiteConfig = StmFactsProvider.OracleSite, Limits = ResourceLimits.Unlimited });

	[Test]
	public void ParticipantsAndMessagesPassL3()
	{
		var problems = new List<string>();
		var ledger = Ledger.Parse("sequence", "sequence.txt", File.ReadAllLines(Path.Combine(CorpusCheck.CorpusRoot, "ledger", "sequence.txt")), problems);
		var listed = ledger.Entries.Where(e => e.Level == Level.L3).ToDictionary(e => e.Case, e => e.Issue, StringComparer.Ordinal);
		var provider = new StmFactsProvider();
		var all = new List<string>();
		var cases = 0;
		var passing = 0;
		foreach (var name in CorpusCheck.Cases(CorpusCheck.CorpusRoot).Where(n => n.StartsWith("sequence/", StringComparison.Ordinal)))
		{
			if (!PlacedAlone(CorpusComparison.Source(CorpusCheck.CorpusRoot, name)))
			{
				continue;
			}

			cases++;
			var result = CorpusComparison.Run(CorpusCheck.CorpusRoot, name, provider)[Level.L3];
			var caseName = name["sequence/".Length..];
			all.AddRange(result.Differences.Select(d => caseName + ": " + d));
			if (result.Outcome != Outcome.Fail)
			{
				passing++;
			}
			else if (!listed.TryGetValue(caseName, out var issue) || issue is null || issue == "#13")
			{
				problems.Add(caseName + " fails L3 and the ledger names no issue for it: " + string.Join(" | ", result.Differences.Take(3)));
			}
		}

		var diffFile = Environment.GetEnvironmentVariable("STM_DIFF_FILE");
		if (!string.IsNullOrEmpty(diffFile))
		{
			File.WriteAllLines(diffFile, [$"{passing} of {cases} pass", .. all]);
		}

		Verify(!problems.Any(), $"{string.Join('\n', problems)}");
		Verify(() => passing >= MinimumPassing);
	}

	/// <summary>
	/// Whether mermaid.js accepts the source and the layout places all of it: no notes, blocks, activations, central
	/// connections, created or destroyed participants.
	/// </summary>
	internal static bool PlacedAlone(string source)
	{
		if (!Stm.TryParse(source, out var document, out _) || document is not SequenceDocument sequence)
		{
			return false;
		}

		var db = SequenceDb.From(sequence);
		return db.CreatedActors.Count == 0
			&& db.DestroyedActors.Count == 0
			&& db.Messages.All(m => m.CentralConnection == 0 && (SequenceLayoutStep.IsSignal(m.Type) || m.Type == SequenceLineType.Autonumber));
	}
}
