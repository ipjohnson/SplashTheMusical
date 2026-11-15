namespace SplashTheMusical.Conformance;

/// <summary>Turns a source into the facts the oracle writes for it, so the comparer can diff the two.</summary>
public interface IFactsProvider
{
	/// <summary>The name the comparison results carry.</summary>
	string Name { get; }

	/// <param name="source">The case's text as it is on disk.</param>
	CaseFacts Facts(string source);
}
