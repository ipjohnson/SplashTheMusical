using AwesomeAssertions;
using SplashTheMusical.Benchmarks;

namespace SplashTheMusical.Tests;

/// <summary>
/// The public API of the core against docs/api/SplashTheMusical.txt, so a change to the API fails until the file shows
/// it. <c>STM_WRITE_API=1</c> rewrites the file.
/// </summary>
public class PublicApiTests
{
	private const string WriteVariable = "STM_WRITE_API";

	[Test]
	public void TheCommittedApiFileIsTheCoresPublicApi()
	{
		var path = Path.Combine(Repository.Root(), "docs", "api", "SplashTheMusical.txt");
		var api = PublicApi.Write(typeof(Mermaid).Assembly);
		if (Environment.GetEnvironmentVariable(WriteVariable) is { Length: > 0 })
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			File.WriteAllText(path, api);
		}

		var committed = File.Exists(path) ? File.ReadAllText(path).ReplaceLineEndings("\n") : "";
		committed.Should().Be(api, $"the public API changed. Run with {WriteVariable}=1 to rewrite {path} and commit it");
	}

	[Test]
	public void TheApiTextNamesTheServicesAndTheirMembers()
	{
		var api = PublicApi.Write(typeof(Mermaid).Assembly);

		api.Should().Contain("public interface IMermaid : IMermaidParser, IMermaidRenderer, IMermaidWriter\n  public string RenderSvg(ReadOnlySpan<char> source, RenderOptions? options = null)\n");
		api.Should().Contain("  public bool TryParse(ReadOnlySpan<char> source, out MermaidDocument? document, out IReadOnlyList<MermaidDiagnostic> diagnostics, ParseOptions? options = null)\n");
		api.Should().Contain("public sealed class Mermaid : IMermaid\n");
		api.Should().Contain("  public static Mermaid Default { get; }\n");
		api.Should().NotContain("PooledBufferWriter");
	}
}
