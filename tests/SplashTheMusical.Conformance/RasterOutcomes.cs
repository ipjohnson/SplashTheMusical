namespace SplashTheMusical.Conformance;

/// <summary>
/// Reads corpus/l4.txt, the L4 outcome of every corpus case, which tools/oracle/raster.mjs writes. tools/oracle/README.md
/// describes the file.
/// </summary>
public static class RasterOutcomes
{
	public static string FilePath => Path.Combine(CorpusCheck.CorpusRoot, "l4.txt");

	/// <summary>The outcome of each case, by its name with / between folders.</summary>
	public static IReadOnlyDictionary<string, Outcome> Parse(IEnumerable<string> lines, List<string> problems)
	{
		var outcomes = new Dictionary<string, Outcome>(StringComparer.Ordinal);
		var number = 0;
		foreach (var line in lines)
		{
			number++;
			if (line.Length == 0 || line[0] == '#')
			{
				continue;
			}

			var tab = line.IndexOf('\t', StringComparison.Ordinal);
			Outcome? outcome = tab < 0
				? null
				: line[(tab + 1)..] switch
				{
					"pass" => Outcome.Pass,
					"fail" => Outcome.Fail,
					"n/a" => Outcome.NotApplicable,
					"report" => Outcome.ReportOnly,
					_ => null,
				};
			if (outcome is null)
			{
				problems.Add($"line {number}: {line} is not a case, a tab and pass, fail, n/a or report");
			}
			else if (!outcomes.TryAdd(line[..tab], outcome.Value))
			{
				problems.Add($"line {number}: {line[..tab]} is listed twice");
			}
		}

		return outcomes;
	}

	public static IReadOnlyDictionary<string, Outcome> Read(string path, List<string> problems)
	{
		if (!File.Exists(path))
		{
			problems.Add($"{path} does not exist. mise run raster writes it");
			return new Dictionary<string, Outcome>();
		}

		return Parse(File.ReadLines(path), problems);
	}

	/// <summary>One line per case without an outcome and per outcome without a case.</summary>
	public static IReadOnlyList<string> Problems(IReadOnlyDictionary<string, Outcome> outcomes, IEnumerable<string> cases)
	{
		var names = cases.Select(Key).ToHashSet(StringComparer.Ordinal);
		return
		[
			.. names.Where(n => !outcomes.ContainsKey(n)).Order(StringComparer.Ordinal).Select(n => $"{n}: no L4 outcome"),
			.. outcomes.Keys.Where(n => !names.Contains(n)).Order(StringComparer.Ordinal).Select(n => $"{n}: has an L4 outcome and no case"),
		];
	}

	/// <summary>A case's name as the file writes it, with / between folders on every platform.</summary>
	public static string Key(string name) => name.Replace('\\', '/');
}
