namespace SplashTheMusical.Conformance;

/// <summary>
/// Section 7's oracle round trip, which proves mermaid.js reads what STM writes: <c>mise run oracle-roundtrip</c> writes
/// STM's text of every accepted corpus case of a module of <see cref="Mermaid.CoreModules"/> to
/// <c>&lt;folder&gt;/corpus</c>, with the case's name, and a sample of random documents of each generator of
/// <c>tests/SplashTheMusical.Tests/Generation</c> to <c>&lt;folder&gt;/random</c>. <c>tools/oracle/facts.mjs</c> then
/// writes the oracle's facts beside each. <see cref="CopyDifferingOriginals"/> copies each case whose written text's facts
/// differ from the committed ones to <c>&lt;folder&gt;/original</c>, and the oracle writes their facts there too, on the
/// same machine. <see cref="Problems"/> then compares each written case with those facts where they exist, and with the
/// committed ones otherwise. The committed facts come from macOS, and another platform's fonts can wrap a label
/// differently, which says nothing about what STM wrote.
/// </summary>
public static class OracleRoundTrip
{
	/// <summary>The environment variable that names the folder.</summary>
	public const string FolderVariable = "STM_ROUNDTRIP_DIR";

	public const string CorpusFolder = "corpus";

	public const string RandomFolder = "random";

	public const string OriginalFolder = "original";

	/// <summary>Empties <c>&lt;folder&gt;/corpus</c> and <c>&lt;folder&gt;/original</c>, then writes STM's text of each case to the first.</summary>
	/// <returns>The number of cases written.</returns>
	public static int WriteCases(string corpus, string folder)
	{
		var target = Path.Combine(folder, CorpusFolder);
		foreach (var emptied in new[] { target, Path.Combine(folder, OriginalFolder) })
		{
			if (Directory.Exists(emptied))
			{
				Directory.Delete(emptied, recursive: true);
			}
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
	/// Empties <c>&lt;folder&gt;/original</c>, then copies there the corpus source of each written case the oracle accepted
	/// with facts that differ from the committed ones.
	/// </summary>
	/// <returns>The number of cases copied.</returns>
	public static int CopyDifferingOriginals(string corpus, string folder)
	{
		var target = Path.Combine(folder, OriginalFolder);
		if (Directory.Exists(target))
		{
			Directory.Delete(target, recursive: true);
		}

		Directory.CreateDirectory(target);
		var written = Path.Combine(folder, CorpusFolder);
		var copied = 0;
		foreach (var name in Directory.Exists(written) ? CorpusCheck.Cases(written) : [])
		{
			if (!File.Exists(Path.Combine(written, name + ".facts.txt"))
				|| OracleFiles.Read(written, name) is not { Accepted: true } actual
				|| FirstDifference(OracleFiles.Read(corpus, name).Facts, actual.Facts) is null)
			{
				continue;
			}

			var path = Path.Combine(target, name + ".mmd");
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			File.Copy(Path.Combine(corpus, name + ".mmd"), path);
			copied++;
		}

		return copied;
	}

	/// <summary>
	/// One line per written case whose facts differ from its original's, per random document the oracle refused, and per
	/// written text the oracle has no facts for. The original's facts are the oracle's of <c>&lt;folder&gt;/original</c>
	/// where it has them, and the committed ones otherwise.
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

			var original = Path.Combine(folder, OriginalFolder);
			if (File.Exists(Path.Combine(original, name + ".mmd")) && !File.Exists(Path.Combine(original, name + ".facts.txt")))
			{
				problems.Add($"{OriginalFolder}/{name}: the oracle wrote no facts");
				continue;
			}

			var expected = File.Exists(Path.Combine(original, name + ".facts.txt"))
				? OracleFiles.Read(original, name)
				: OracleFiles.Read(corpus, name);
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
