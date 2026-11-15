namespace SplashTheMusical.Conformance;

/// <summary>Reads the facts and boxes files the oracle committed beside a corpus case.</summary>
public static class OracleFiles
{
	private const string ErrorPrefix = "# error ";

	/// <param name="corpus">The corpus folder.</param>
	/// <param name="name">The case's path relative to the corpus folder, without the .mmd extension.</param>
	public static CaseFacts Read(string corpus, string name)
	{
		var accepted = false;
		string? error = null;
		var facts = new List<string>();
		foreach (var line in File.ReadLines(Path.Combine(corpus, name + ".facts.txt")))
		{
			if (line.StartsWith(ErrorPrefix, StringComparison.Ordinal))
			{
				error = line[ErrorPrefix.Length..];
			}
			else if (line == "accept\tyes")
			{
				accepted = true;
			}
			else if (line.Length > 0 && line[0] != '#' && line != "accept\tno")
			{
				facts.Add(line);
			}
		}

		var boxes = new List<string>();
		var boxesPath = Path.Combine(corpus, name + ".boxes.txt");
		if (File.Exists(boxesPath))
		{
			boxes.AddRange(File.ReadLines(boxesPath).Where(l => l.StartsWith("box\t", StringComparison.Ordinal)));
		}

		return new CaseFacts(accepted, error, facts, boxes);
	}
}
