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

		var boxesPath = Path.Combine(corpus, name + ".boxes.txt");
		var (boxes, canvas) = File.Exists(boxesPath) ? ReadBoxes(boxesPath) : ([], null);
		return new CaseFacts(accepted, error, facts, boxes, canvas);
	}

	/// <summary>The <c>box</c> lines and the <c>canvas</c> line of a boxes file.</summary>
	public static (List<string> Boxes, string? Canvas) ReadBoxes(string path)
	{
		var boxes = new List<string>();
		string? canvas = null;
		foreach (var line in File.ReadLines(path))
		{
			if (line.StartsWith("box\t", StringComparison.Ordinal))
			{
				boxes.Add(line);
			}
			else if (line.StartsWith("canvas\t", StringComparison.Ordinal))
			{
				canvas = line;
			}
		}

		return (boxes, canvas);
	}
}
