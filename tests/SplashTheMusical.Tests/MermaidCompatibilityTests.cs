using AwesomeAssertions;
using SplashTheMusical.Upstream;

namespace SplashTheMusical.Tests;

public class MermaidCompatibilityTests
{
	[Test]
	public void TheVersionIsTheMermaidOfTheOracleLockFile() =>
		MermaidCompatibility.MermaidJsVersion.Should().Be(CompatibilityWriter.LockedVersion(Repository()));

	[Test]
	public void TheGeneratorRewritesTheCommittedFileWithNoDiff()
	{
		var repository = Repository();
		var committed = File.ReadAllText(Path.Combine(CompatibilityWriter.Folder(repository), CompatibilityWriter.FileName));
		CompatibilityWriter.Write(repository).Should().Be(committed);
	}

	private static string Repository()
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
}
