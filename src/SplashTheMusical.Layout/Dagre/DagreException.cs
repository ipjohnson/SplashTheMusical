namespace SplashTheMusical.Layout.Dagre;

/// <summary>
/// An error dagre-d3-es or its graphlib throws on a graph it cannot lay out, with the message it gives in V8. Their own
/// errors and the <c>TypeError</c> of reading an undefined label all end mermaid.js's render, so the caller refuses the
/// diagram.
/// </summary>
internal sealed class DagreException(string message) : Exception(message)
{
	/// <summary>What V8 says when dagre's ranking sets the rank of a cluster node, whose label the non-compound graph lacks.</summary>
	public const string RankOfCluster = "Cannot set properties of undefined (setting 'rank')";
}
