namespace SplashTheMusical;

/// <summary>
/// Limits on the work one call does for one diagram. Every limit is on by default, with values well above any realistic
/// diagram. A call that exceeds one throws <see cref="MermaidResourceLimitException"/>. The defaults are Mermaider's.
/// </summary>
public sealed record ResourceLimits
{
	/// <summary>The most characters of source accepted. Default: 512 K.</summary>
	public int MaxInputLength { get; init; } = 512 * 1024;

	/// <summary>
	/// The most statements in a diagram. With one statement per line, this is the number of non-blank, non-comment lines.
	/// Default: 10,000.
	/// </summary>
	public int MaxLines { get; init; } = 10_000;

	/// <summary>The most characters on one line, without its line break. Default: 8,000.</summary>
	public int MaxLineLength { get; init; } = 8_000;

	/// <summary>The most parsed elements, such as nodes and edges, in a diagram. Default: 5,000.</summary>
	public int MaxElements { get; init; } = 5_000;

	/// <summary>The most nodes after layout adds the virtual nodes of long edges. Default: 20,000.</summary>
	public int MaxNodesAfterLayout { get; init; } = 20_000;

	/// <summary>The deepest nesting a parser or a tree renderer follows. Default: 64.</summary>
	public int MaxRecursionDepth { get; init; } = 64;

	/// <summary>The most characters of SVG output. Default: 8 M.</summary>
	public int MaxOutputLength { get; init; } = 8 * 1024 * 1024;

	/// <summary>
	/// The time budget for parsing, layout and rendering. It is cooperative: the parser checks it once per statement.
	/// <c>null</c> turns it off. Default: 5 seconds.
	/// </summary>
	public TimeSpan? RenderDeadline { get; init; } = TimeSpan.FromSeconds(5);

	/// <summary>The clock <see cref="RenderDeadline"/> is measured with.</summary>
	public TimeProvider TimeProvider { get; init; } = TimeProvider.System;

	public static ResourceLimits Default { get; } = new();

	/// <summary>No limits. Use it only for trusted input.</summary>
	public static ResourceLimits Unlimited { get; } = new()
	{
		MaxInputLength = int.MaxValue,
		MaxLines = int.MaxValue,
		MaxLineLength = int.MaxValue,
		MaxElements = int.MaxValue,
		MaxNodesAfterLayout = int.MaxValue,
		MaxRecursionDepth = int.MaxValue,
		MaxOutputLength = int.MaxValue,
		RenderDeadline = null,
	};
}
