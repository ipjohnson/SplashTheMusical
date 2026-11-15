namespace SplashTheMusical.Benchmarks;

/// <summary>Finds the repository root, which holds the benchmark set and the committed numbers.</summary>
public static class Repository
{
	/// <summary>The environment variable that overrides the search, which the benchmark processes inherit.</summary>
	public const string RootVariable = "STM_REPO";

	/// <summary>The root, from <see cref="RootVariable"/> or the first folder above the binary or the current folder that holds the solution.</summary>
	public static string Root()
	{
		if (Environment.GetEnvironmentVariable(RootVariable) is { Length: > 0 } root)
		{
			return root;
		}

		foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
		{
			for (var folder = new DirectoryInfo(start); folder is not null; folder = folder.Parent)
			{
				if (File.Exists(Path.Combine(folder.FullName, "SplashTheMusical.slnx")))
				{
					return folder.FullName;
				}
			}
		}

		throw new InvalidOperationException($"No SplashTheMusical.slnx above {AppContext.BaseDirectory}. Set {RootVariable}.");
	}

	/// <summary>A path under the root, from parts separated by slashes.</summary>
	public static string Resolve(string relative) => Path.Combine(Root(), relative.Replace('/', Path.DirectorySeparatorChar));
}
