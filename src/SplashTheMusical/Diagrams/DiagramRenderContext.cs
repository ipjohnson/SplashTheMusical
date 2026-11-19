using SplashTheMusical.Config;
using SplashTheMusical.Icons;
using SplashTheMusical.Text;

namespace SplashTheMusical.Diagrams;

/// <summary>What a module's layout step and renderer get besides the document: the resolved config and the host's extension points.</summary>
public sealed class DiagramRenderContext
{
	private readonly IReadOnlyList<ILayoutEngine> _layoutEngines;
	private readonly ICollection<MermaidDiagnostic>? _diagnostics;

	public DiagramRenderContext(
		MermaidConfig config,
		string svgId,
		ITextMeasurer textMeasurer,
		IIconProvider icons,
		IReadOnlyList<ILayoutEngine> layoutEngines,
		ResourceLimits limits,
		bool allowRemoteImages = false,
		ICollection<MermaidDiagnostic>? diagnostics = null)
	{
		ArgumentNullException.ThrowIfNull(config);
		ArgumentNullException.ThrowIfNull(svgId);
		ArgumentNullException.ThrowIfNull(textMeasurer);
		ArgumentNullException.ThrowIfNull(icons);
		ArgumentNullException.ThrowIfNull(layoutEngines);
		ArgumentNullException.ThrowIfNull(limits);
		Config = config;
		SvgId = svgId;
		TextMeasurer = textMeasurer;
		Icons = icons;
		_layoutEngines = layoutEngines;
		Limits = limits;
		AllowRemoteImages = allowRemoteImages;
		_diagnostics = diagnostics;
	}

	/// <inheritdoc cref="MermaidOptions.AllowRemoteImages"/>
	public bool AllowRemoteImages { get; }

	/// <summary>Adds a warning of the render to the caller's <see cref="RenderOptions.Diagnostics"/>, when it gave one.</summary>
	public void Report(MermaidDiagnostic diagnostic) => _diagnostics?.Add(diagnostic);

	/// <summary>The config the diagram is drawn with: the defaults, the site config, the front matter and the directives.</summary>
	public MermaidConfig Config { get; }

	/// <summary>The <c>id</c> of the root <c>svg</c> element, which mermaid.js prefixes element ids with.</summary>
	public string SvgId { get; }

	public ITextMeasurer TextMeasurer { get; }

	public IIconProvider Icons { get; }

	public ResourceLimits Limits { get; }

	/// <summary>The engine that answers to the <c>layout</c> name, or <c>null</c> when none does.</summary>
	public ILayoutEngine? LayoutEngine(string name)
	{
		ArgumentNullException.ThrowIfNull(name);
		foreach (var engine in _layoutEngines)
		{
			if (engine.Name == name)
			{
				return engine;
			}
		}

		return null;
	}
}
