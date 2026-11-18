using System.Diagnostics;

namespace SplashTheMusical.Conformance.Fuzzing;

/// <summary>How STM and mermaid.js disagree on a source.</summary>
/// <param name="Level">L0 when one accepts the source and the other refuses it, L1 when their model facts differ, or <c>exception</c> when STM throws.</param>
/// <param name="Kinds">For L1 the first field of each differing fact, such as <c>edge</c>. Otherwise one line that says which side accepts, or the exception's type.</param>
/// <param name="Lines">The differing facts, each after <c>-</c> when only mermaid.js has it and <c>+</c> when only STM has it.</param>
public sealed record Disagreement(string Level, IReadOnlySet<string> Kinds, IReadOnlyList<string> Lines)
{
	/// <summary>The level and the first kind, which a minimized source must still show.</summary>
	public string Signature => Level + " " + Kinds.Order(StringComparer.Ordinal).First();

	public bool Shows(string signature)
	{
		var space = signature.IndexOf(' ', StringComparison.Ordinal);
		return Level == signature[..space] && Kinds.Contains(signature[(space + 1)..]);
	}
}

/// <param name="Type">The corpus folder of the diagram type.</param>
/// <param name="Name">The first 16 hex digits of the SHA-256 of the minimized source.</param>
/// <param name="Index">The index of the source in the run, which with the run's seed makes it again.</param>
/// <param name="Tests">The sources the minimizer ran through both sides.</param>
public sealed record DifferentialFind(string Type, string Name, long Index, string Original, string Minimized, Disagreement Disagreement, int Tests);

/// <param name="Sources">The sources made.</param>
/// <param name="Compared">The sources the oracle answered, which were compared.</param>
public sealed record DifferentialRun(string Type, int Seeds, int Sources, int Compared, IReadOnlyList<DifferentialFind> Finds);

/// <summary>
/// Section 7's differential fuzzing: runs mutated corpus cases and sentences of the upstream grammar through mermaid.js
/// and STM, keeps each source whose acceptance or L1 facts differ, and minimizes it while the difference remains. A
/// difference with the signature of an earlier find of the run is counted and not kept.
/// </summary>
/// <param name="oracle">mermaid.js's facts of a source, or null when it gives none.</param>
/// <param name="stm">STM's facts of a source.</param>
public sealed class DifferentialRunner(Func<string, CaseFacts?> oracle, Func<string, CaseFacts> stm)
{
	/// <summary>The most sources a minimization runs.</summary>
	public int MaxMinimizeTests { get; init; } = 400;

	/// <summary>The most finds per type.</summary>
	public int MaxFinds { get; init; } = 5;

	/// <summary>Ends the run after a find this returns true for.</summary>
	public Func<DifferentialFind, bool>? StopAfter { get; init; }

	/// <summary>
	/// Makes sources until <paramref name="maxSources"/> or <paramref name="budget"/> runs out. Every third source is a
	/// sentence of <paramref name="grammar"/> when there is one, and the others mutate a case of <paramref name="seeds"/>.
	/// </summary>
	public DifferentialRun Run(string type, IReadOnlyList<string> seeds, Grammar? grammar, long seed, int maxSources, TimeSpan budget)
	{
		ArgumentOutOfRangeException.ThrowIfZero(seeds.Count);
		var clock = Stopwatch.StartNew();
		var finds = new List<DifferentialFind>();
		var signatures = new HashSet<string>(StringComparer.Ordinal);
		var compared = 0;
		var index = 0;
		for (; index < maxSources && clock.Elapsed < budget && finds.Count < MaxFinds; index++)
		{
			var source = Source(seeds, grammar, seed, index);
			if (oracle(source) is not { } expected)
			{
				continue;
			}

			compared++;
			if (Compare(expected, source) is not { } disagreement || !signatures.Add(disagreement.Signature))
			{
				continue;
			}

			var signature = disagreement.Signature;
			var minimized = Minimizer.Minimize(source, candidate => Disagree(candidate)?.Shows(signature) == true, MaxMinimizeTests, out var tests);
			var shown = Disagree(minimized) ?? disagreement;
			var find = new DifferentialFind(type, CorpusCheck.SourceHash(minimized), index, source, minimized, shown, tests);
			finds.Add(find);
			if (StopAfter?.Invoke(find) == true)
			{
				index++;
				break;
			}
		}

		return new DifferentialRun(type, seeds.Count, index, compared, finds);
	}

	/// <summary>Source <paramref name="index"/> of a run with <paramref name="seed"/>.</summary>
	public static string Source(IReadOnlyList<string> seeds, Grammar? grammar, long seed, long index)
	{
		var random = FuzzRandom.For(seed, index);
		return grammar is not null && index % 3 == 2 ? grammar.Sentence(random) : Mutator.Mutate(random.Pick(seeds), random);
	}

	/// <summary>How STM and mermaid.js disagree on the source, or null when they agree or the oracle gives no facts.</summary>
	public Disagreement? Disagree(string source) => oracle(source) is { } expected ? Compare(expected, source) : null;

	/// <summary>The sources of the corpus folder's cases whose committed oracle facts STM agrees with.</summary>
	public static IReadOnlyList<string> AgreeingSeeds(string corpus, string folder, Func<string, CaseFacts> stm) =>
		Seeds(corpus, folder, AgreeingCases(corpus, folder, stm));

	/// <summary>The names of the corpus folder's cases whose committed oracle facts STM agrees with.</summary>
	public static IReadOnlyList<string> AgreeingCases(string corpus, string folder, Func<string, CaseFacts> stm)
	{
		var names = new List<string>();
		var path = Path.Combine(corpus, folder);
		foreach (var name in CorpusCheck.Cases(path))
		{
			if (Compare(OracleFiles.Read(path, name), CorpusComparison.Source(path, name), stm) is null)
			{
				names.Add(name);
			}
		}

		return names;
	}

	/// <summary>The sources of the named cases of the corpus folder.</summary>
	public static IReadOnlyList<string> Seeds(string corpus, string folder, IEnumerable<string> names)
	{
		var path = Path.Combine(corpus, folder);
		return [.. names.Select(name => CorpusComparison.Source(path, name))];
	}

	/// <summary>
	/// The oracle's facts of a source, or null when it gives none or reads the source as a type STM has no module for,
	/// such as a mutated header, which says nothing about STM's parsers.
	/// </summary>
	public static Func<string, CaseFacts?> SupportedTypes(OracleProcess oracle)
	{
		var supported = new HashSet<string>(Mermaid.CoreModules.Select(m => ConfigLines.Line("type", m.DiagramType)), StringComparer.Ordinal);
		return source => oracle.Facts("fuzz", source) is { } facts && (!facts.Accepted || facts.Facts.Any(supported.Contains)) ? facts : null;
	}

	private Disagreement? Compare(CaseFacts expected, string source) => Compare(expected, source, stm);

	private static Disagreement? Compare(CaseFacts expected, string source, Func<string, CaseFacts> stm)
	{
		CaseFacts actual;
		try
		{
			actual = stm(source);
		}
		catch (Exception e) when (e is not OutOfMemoryException)
		{
			return new Disagreement("exception", new HashSet<string>([e.GetType().Name], StringComparer.Ordinal), [e.Message]);
		}

		if (expected.Accepted != actual.Accepted)
		{
			var side = expected.Accepted ? "mermaid.js-accepts" : "STM-accepts";
			return new Disagreement("L0", new HashSet<string>([side], StringComparer.Ordinal), [$"mermaid.js: {expected.Error ?? "accepts"}", $"STM: {actual.Error ?? "accepts"}"]);
		}

		if (!expected.Accepted)
		{
			return null;
		}

		var counts = new Dictionary<string, int>(StringComparer.Ordinal);
		foreach (var line in Comparer.Lines(Level.L1, expected))
		{
			counts[line] = counts.GetValueOrDefault(line) + 1;
		}

		foreach (var line in Comparer.Lines(Level.L1, actual))
		{
			counts[line] = counts.GetValueOrDefault(line) - 1;
		}

		var lines = new List<string>();
		var kinds = new HashSet<string>(StringComparer.Ordinal);
		foreach (var (line, count) in counts.OrderBy(c => c.Key, StringComparer.Ordinal))
		{
			for (var i = 0; i < Math.Abs(count); i++)
			{
				lines.Add((count > 0 ? "- " : "+ ") + line);
				var tab = line.IndexOf('\t', StringComparison.Ordinal);
				_ = kinds.Add(tab < 0 ? line : line[..tab]);
			}
		}

		return lines.Count == 0 ? null : new Disagreement("L1", kinds, lines);
	}
}
