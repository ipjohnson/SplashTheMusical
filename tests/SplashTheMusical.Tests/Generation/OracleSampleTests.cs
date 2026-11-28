using AwesomeAssertions;
using CsCheck;

namespace SplashTheMusical.Tests.Generation;

/// <summary>
/// The random half of section 7's oracle round trip: <c>mise run oracle-roundtrip</c> runs this with
/// <see cref="FolderVariable"/> set, and it writes <see cref="Count"/> random documents of each generator of
/// <see cref="DocumentGenerators"/> to <c>&lt;folder&gt;/random/&lt;generator&gt;</c>. The oracle must accept every
/// one, which <c>OracleRoundTripTests</c> in the conformance project checks. Otherwise it writes nothing.
/// </summary>
public class OracleSampleTests
{
	public const string FolderVariable = "STM_ROUNDTRIP_DIR";

	public const int Count = 100;

	/// <summary>The documents are the same on every run, so a pull request fails only on what it changes.</summary>
	public const uint Seed = 1;

	[Test]
	public void WritesRandomDocumentsWhenAsked()
	{
		if (Environment.GetEnvironmentVariable(FolderVariable) is not { Length: > 0 } folder)
		{
			return;
		}

		var random = Path.Combine(folder, "random");
		if (Directory.Exists(random))
		{
			Directory.Delete(random, recursive: true);
		}

		foreach (var generator in DocumentGenerators.All)
		{
			var target = Directory.CreateDirectory(Path.Combine(random, generator.Name)).FullName;
			var pcg = new PCG(1, Seed);
			var texts = new List<string>();
			for (var i = 0; i < Count; i++)
			{
				texts.Add(Mermaid.Default.Write(generator.Documents.Generate(pcg, null, out _)));
			}
			for (var i = 0; i < texts.Count; i++)
			{
				File.WriteAllText(Path.Combine(target, $"{generator.Name}-{i:D4}.mmd"), texts[i]);
			}

			Directory.GetFiles(target, "*.mmd").Should().HaveCount(Count);
		}
	}
}
