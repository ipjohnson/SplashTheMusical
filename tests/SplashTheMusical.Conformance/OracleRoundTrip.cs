namespace SplashTheMusical.Conformance;

/// <summary>
/// Section 7's oracle round trip, which proves mermaid.js reads what STM writes: <c>mise run oracle-roundtrip</c> writes
/// STM's text of every accepted corpus case of a module of <see cref="Mermaid.CoreModules"/> to
/// <c>&lt;folder&gt;/corpus</c>, with the case's name, and a sample of random documents of each generator of
/// <c>tests/SplashTheMusical.Tests/Generation</c> to <c>&lt;folder&gt;/random</c>. <c>tools/oracle/facts.mjs</c> then
/// writes the oracle's facts beside each, and <see cref="Problems"/> compares them.
/// </summary>
public static class OracleRoundTrip
{
	/// <summary>The environment variable that names the folder.</summary>
	public const string FolderVariable = "STM_ROUNDTRIP_DIR";

	public const string CorpusFolder = "corpus";

	public const string RandomFolder = "random";

	/// <summary>Empties <c>&lt;folder&gt;/corpus</c>, then writes STM's text of each case there.</summary>
	/// <returns>The number of cases written.</returns>
	public static int WriteCases(string corpus, string folder)
	{
		var target = Path.Combine(folder, CorpusFolder);
		if (Directory.Exists(target))
		{
			Directory.Delete(target, recursive: true);
		}

		Directory.CreateDirectory(target);
		var types = new HashSet<string>(Mermaid.CoreModules.Select(m => m.DiagramType), StringComparer.Ordinal);
		var stm = new Mermaid(new MermaidOptions { Limits = ResourceLimits.Unlimited });
		var written = 0;
		foreach (var name in CorpusCheck.Cases(corpus))
		{
			if (!OracleFiles.Read(corpus, name).Accepted
				|| !stm.TryParse(CorpusComparison.Source(corpus, name), out var document, out _)
				|| !types.Contains(document!.DiagramType))
			{
				continue;
			}

			var path = Path.Combine(target, name + ".mmd");
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			File.WriteAllText(path, stm.Write(document));
			written++;
		}

		return written;
	}

	/// <summary>
	/// One line per written case whose facts differ from its corpus case's, per random document the oracle refused, and
	/// per written text the oracle has no facts for.
	/// </summary>
	public static IReadOnlyList<string> Problems(string corpus, string folder)
	{
		var problems = new List<string>();
		var written = Path.Combine(folder, CorpusFolder);
		var cases = Directory.Exists(written) ? CorpusCheck.Cases(written) : [];
		if (cases.Count == 0)
		{
			problems.Add($"{written} holds no written cases. Run mise run oracle-roundtrip.");
		}

		foreach (var name in cases)
		{
			if (!File.Exists(Path.Combine(written, name + ".facts.txt")))
			{
				problems.Add($"{CorpusFolder}/{name}: the oracle wrote no facts");
				continue;
			}

			var expected = OracleFiles.Read(corpus, name);
			var actual = OracleFiles.Read(written, name);
			if (!actual.Accepted)
			{
				problems.Add($"{CorpusFolder}/{name}: mermaid.js refused the written text: {actual.Error}");
			}
			else if (FirstDifference(expected.Facts, actual.Facts) is { } difference)
			{
				problems.Add($"{CorpusFolder}/{name}: mermaid.js reads the written text differently\n{difference}");
			}
		}

		var random = Path.Combine(folder, RandomFolder);
		foreach (var name in Directory.Exists(random) ? CorpusCheck.Cases(random) : [])
		{
			if (!File.Exists(Path.Combine(random, name + ".facts.txt")))
			{
				problems.Add($"{RandomFolder}/{name}: the oracle wrote no facts");
			}
			else if (OracleFiles.Read(random, name) is { Accepted: false } refused)
			{
				problems.Add($"{RandomFolder}/{name}: mermaid.js refused the written text: {refused.Error}");
			}
		}

		return problems;
	}

	private static string? FirstDifference(IReadOnlyList<string> expected, IReadOnlyList<string> actual)
	{
		for (var i = 0; i < Math.Max(expected.Count, actual.Count); i++)
		{
			var e = i < expected.Count ? expected[i] : "(none)";
			var a = i < actual.Count ? actual[i] : "(none)";
			if (e != a)
			{
				return $"  expected {e}\n    actual {a}";
			}
		}

		return null;
	}
}
