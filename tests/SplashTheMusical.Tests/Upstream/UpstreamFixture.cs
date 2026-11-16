using SplashTheMusical.Upstream;

namespace SplashTheMusical.Tests.Upstream;

/// <summary>A folder of files for the upstream tool's tests, deleted on dispose.</summary>
internal sealed class UpstreamFixture : IDisposable
{
	public string Root { get; } = Directory.CreateTempSubdirectory("stm-upstream-").FullName;

	public static string Repository()
	{
		for (var folder = AppContext.BaseDirectory; folder is not null; folder = Path.GetDirectoryName(folder))
		{
			if (File.Exists(Path.Combine(folder, "SplashTheMusical.slnx")))
			{
				return folder;
			}
		}

		throw new InvalidOperationException("SplashTheMusical.slnx not found above " + AppContext.BaseDirectory);
	}

	public void Write(string path, string text)
	{
		var full = Path.Combine(Root, path);
		_ = Directory.CreateDirectory(Path.GetDirectoryName(full)!);
		File.WriteAllText(full, text);
	}

	public DeltaSide Side(string name, string version) =>
		new(
			version,
			Path.Combine(Root, name, "checkout"),
			Path.Combine(Root, name, "corpus"),
			Path.Combine(Root, name, "results.txt"),
			Path.Combine(Root, name, "results.trx")
		);

	public void Dispose() => Directory.Delete(Root, recursive: true);
}
