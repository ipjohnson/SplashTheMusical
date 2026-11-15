namespace SplashTheMusical.Upstream;

/// <summary>Writes or checks a folder of generated files. The folder holds only generated files.</summary>
public static class GeneratedFiles
{
	/// <summary>Writes each file that changed and deletes the files that are no longer generated.</summary>
	public static int Write(string folder, IReadOnlyDictionary<string, string> files)
	{
		Directory.CreateDirectory(folder);
		foreach (var stale in Stale(folder, files))
		{
			File.Delete(Path.Combine(folder, stale));
			Console.WriteLine($"deleted {stale}");
		}

		foreach (var (name, content) in files)
		{
			var path = Path.Combine(folder, name);
			if (!File.Exists(path) || File.ReadAllText(path) != content)
			{
				File.WriteAllText(path, content);
				Console.WriteLine($"wrote {name}");
			}
		}

		return 0;
	}

	/// <summary>Fails when a generated file differs from the one in the folder, is missing, or is no longer generated.</summary>
	public static int Check(string folder, IReadOnlyDictionary<string, string> files)
	{
		var problems = Stale(folder, files).Select(name => $"{name} is no longer generated").ToList();
		foreach (var (name, content) in files)
		{
			var path = Path.Combine(folder, name);
			if (!File.Exists(path))
			{
				problems.Add($"{name} is missing");
			}
			else if (File.ReadAllText(path) != content)
			{
				problems.Add($"{name} differs");
			}
		}

		foreach (var problem in problems)
		{
			Console.Error.WriteLine(problem);
		}

		return problems.Count == 0 ? 0 : 1;
	}

	private static List<string> Stale(string folder, IReadOnlyDictionary<string, string> files) =>
		Directory.Exists(folder)
			? [.. Directory.EnumerateFiles(folder).Select(Path.GetFileName).OfType<string>().Where(name => !files.ContainsKey(name)).Order(StringComparer.Ordinal)]
			: [];
}
