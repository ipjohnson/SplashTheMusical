namespace SplashTheMusical.Conformance;

/// <summary>The STM of phase 0 of the plan's roadmap, which refuses every source.</summary>
public sealed class StubFactsProvider : IFactsProvider
{
	public string Name => "stm-stub";

	public CaseFacts Facts(string source) => CaseFacts.Refused("STM does not parse any diagram type yet");
}
