namespace SplashTheMusical;

/// <summary>
/// The options of a <see cref="Mermaid"/> instance, for every call it serves. The options argument of a call overrides
/// them.
/// </summary>
public sealed record MermaidOptions
{
	/// <summary>
	/// The site config, as mermaid.js's <c>initialize</c> takes it: the config every diagram starts from, under its front
	/// matter and directives. <c>null</c> is mermaid's default config.
	/// </summary>
	public MermaidValue? SiteConfig { get; init; }

	/// <summary>The limits on the work one call does.</summary>
	public ResourceLimits Limits { get; init; } = ResourceLimits.Default;

	/// <summary>
	/// The config keys a diagram cannot change, which mermaid.js calls <c>secure</c>. When set, it replaces the
	/// <c>secure</c> list of <see cref="SiteConfig"/>. <c>null</c> keeps that list, which is mermaid's by default.
	/// </summary>
	public IReadOnlyList<string>? LockedKeys { get; init; }

	/// <summary>
	/// Whether an image node's <c>http</c> or <c>https</c> URL is drawn, as an <c>image</c> the viewer loads. STM never
	/// fetches it. Default: <c>false</c>, so only a <c>data:</c> PNG or SVG image is drawn, and a remote image gives a
	/// warning.
	/// </summary>
	public bool AllowRemoteImages { get; init; }

	public static MermaidOptions Default { get; } = new();
}

/// <summary>The options of one parse. A property left <c>null</c> takes its value from <see cref="MermaidOptions"/>.</summary>
public sealed record ParseOptions
{
	public ResourceLimits? Limits { get; init; }
}

/// <summary>The options of one render. A property left <c>null</c> takes its value from <see cref="MermaidOptions"/>.</summary>
public sealed record RenderOptions
{
	/// <summary>The <c>id</c> of the <c>svg</c> element, which scopes its stylesheet. It is a CSS identifier. Default: <c>mermaid</c>.</summary>
	public string? SvgId { get; init; }

	/// <inheritdoc cref="MermaidOptions.SiteConfig"/>
	public MermaidValue? SiteConfig { get; init; }

	public ResourceLimits? Limits { get; init; }

	/// <inheritdoc cref="MermaidOptions.AllowRemoteImages"/>
	public bool? AllowRemoteImages { get; init; }

	/// <summary>
	/// The collection the render adds its warnings to, such as an icon no pack has or an image it does not draw.
	/// Default: none, and the warnings are dropped.
	/// </summary>
	public ICollection<MermaidDiagnostic>? Diagnostics { get; init; }
}

/// <summary>The options of one write of <c>.mmd</c> text.</summary>
public sealed record WriteOptions
{
	/// <summary>The line break between statements. Default: LF.</summary>
	public string NewLine { get; init; } = "\n";

	public static WriteOptions Default { get; } = new();
}
