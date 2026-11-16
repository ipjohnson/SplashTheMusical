using System.Buffers;
using System.Collections.Frozen;
using System.Collections.ObjectModel;
using System.Text;
using SplashTheMusical.Config;
using SplashTheMusical.Diagrams;
using SplashTheMusical.Diagrams.Info;
using SplashTheMusical.Icons;
using SplashTheMusical.Parsing;
using SplashTheMusical.Rendering;
using SplashTheMusical.Text;
using SplashTheMusical.Themes;
using SplashTheMusical.Writing;

namespace SplashTheMusical;

/// <summary>
/// Parses, writes and draws Mermaid diagrams with a fixed set of diagram modules and extension points. An instance
/// holds no state that a call changes, so one instance serves any number of threads.
/// </summary>
public sealed class Mermaid : IMermaid
{
	private const string DefaultSvgId = "mermaid";

	private readonly MermaidOptions _options;
	private readonly MermaidValue? _siteConfig;
	private readonly FrozenDictionary<string, IDiagramModule> _modules;
	private readonly IDiagramModule[] _addedModules;
	private readonly ITextMeasurer _textMeasurer;
	private readonly IIconProvider _icons;
	private readonly IReadOnlyList<ILayoutEngine> _layoutEngines;
	private readonly IThemeProvider _themes;

	/// <param name="options">The options of every call. Default: <see cref="MermaidOptions.Default"/>.</param>
	/// <param name="modules">
	/// Diagram modules besides <see cref="CoreModules"/>. A module of a type a core module has replaces it.
	/// </param>
	/// <param name="textMeasurer">Measures label text. Default: <see cref="TextMeasurer.Default"/>.</param>
	/// <param name="icons">The icons diagrams can name. Default: none.</param>
	/// <param name="layoutEngines">The graph layouts a <c>layout</c> config can name. Default: none.</param>
	/// <param name="themes">The themes a <c>theme</c> config can name. Default: <see cref="MermaidThemes.Default"/>.</param>
	public Mermaid(
		MermaidOptions? options = null,
		IEnumerable<IDiagramModule>? modules = null,
		ITextMeasurer? textMeasurer = null,
		IIconProvider? icons = null,
		IEnumerable<ILayoutEngine>? layoutEngines = null,
		IThemeProvider? themes = null)
	{
		_options = options ?? MermaidOptions.Default;
		_siteConfig = WithLockedKeys(_options.SiteConfig, _options.LockedKeys);
		_addedModules = modules is null ? [] : [.. modules];
		var byType = new Dictionary<string, IDiagramModule>(StringComparer.Ordinal);
		foreach (var module in CoreModules.Concat(_addedModules))
		{
			ArgumentNullException.ThrowIfNull(module, nameof(modules));
			byType[module.DiagramType] = module;
		}

		_modules = byType.ToFrozenDictionary(StringComparer.Ordinal);
		_textMeasurer = textMeasurer ?? TextMeasurer.Default;
		_icons = icons ?? NoIcons.Instance;
		_layoutEngines = layoutEngines is null ? [] : Array.AsReadOnly([.. layoutEngines]);
		_themes = themes ?? MermaidThemes.Default;
	}

	/// <summary>The diagram modules of the core, one per diagram type STM supports.</summary>
	public static IReadOnlyList<IDiagramModule> CoreModules { get; } = new ReadOnlyCollection<IDiagramModule>([new InfoModule()]);

	/// <summary>An instance with the default options, <see cref="CoreModules"/> and the default extension points.</summary>
	public static Mermaid Default { get; } = new();

	public MermaidDocument Parse(ReadOnlySpan<char> source, ParseOptions? options = null)
	{
		var diagnostics = new List<MermaidDiagnostic>();
		return TryParseCore(source, options, diagnostics) ?? throw new MermaidParseException(diagnostics);
	}

	public bool TryParse(
		ReadOnlySpan<char> source,
		out MermaidDocument? document,
		out IReadOnlyList<MermaidDiagnostic> diagnostics,
		ParseOptions? options = null)
	{
		var list = new List<MermaidDiagnostic>();
		diagnostics = list;
		try
		{
			document = TryParseCore(source, options, list);
		}
		catch (MermaidParseException exception)
		{
			list.AddRange(exception.Diagnostics);
			document = null;
		}

		return document is not null;
	}

	public string RenderSvg(ReadOnlySpan<char> source, RenderOptions? options = null) =>
		RenderSvg(Parse(source, options?.Limits is { } limits ? new ParseOptions { Limits = limits } : null), options);

	public string RenderSvg(MermaidDocument document, RenderOptions? options = null)
	{
		using var buffer = new PooledBufferWriter();
		var limits = Render(document, buffer, options);
		var svg = Encoding.UTF8.GetString(buffer.WrittenSpan);
		ResourceGuard.OutputLength(svg.Length, limits);
		return svg;
	}

	public void RenderSvg(MermaidDocument document, IBufferWriter<byte> utf8Destination, RenderOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(utf8Destination);
		var counter = new CountingBufferWriter(utf8Destination);
		var limits = Render(document, counter, options);
		ResourceGuard.OutputLength(counter.Written, limits);
	}

	public string Write(MermaidDocument document, WriteOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(document);
		var module = ModuleFor(document);
		var builder = new StringBuilder();
		PreambleWriter.Write(builder, document);
		module.Write(document, builder);
		var newLine = (options ?? WriteOptions.Default).NewLine;
		return newLine == "\n" ? builder.ToString() : builder.Replace("\n", newLine).ToString();
	}

	/// <summary>
	/// The type mermaid.js's detectors give the source, then the first added module's whose detector matches, or
	/// <c>null</c>. <paramref name="keyword"/> is the offset of the text the detectors see.
	/// </summary>
	internal string? DetectType(ReadOnlySpan<char> source, int bodyStart, out int keyword)
	{
		var type = DiagramDetector.Detect(source, bodyStart, out keyword);
		if (type is not null)
		{
			return type;
		}

		foreach (var module in _addedModules)
		{
			if (module.Detect(source[keyword..]))
			{
				return module.DiagramType;
			}
		}

		return null;
	}

	private MermaidDocument? TryParseCore(ReadOnlySpan<char> source, ParseOptions? options, List<MermaidDiagnostic> diagnostics)
	{
		var limits = options?.Limits ?? _options.Limits;
		ResourceGuard.Start(source, limits);
		var preamble = new PreambleDocument();
		if (!PreambleParser.TryParse(source, preamble, diagnostics, limits, out var bodyStart))
		{
			return null;
		}

		var type = DetectType(source, bodyStart, out var keyword);
		var (line, column) = TextPosition.Of(source, keyword);
		if (type is null)
		{
			diagnostics.Add(Error(
				"No diagram type detected matching given configuration for text: " + DiagramDetector.DetectedText(source[keyword..]),
				line,
				column));
			return null;
		}

		if (type == "---")
		{
			diagnostics.Add(Error(
				"Diagrams beginning with --- are not valid. If you were trying to use a YAML front-matter, please ensure that you've correctly opened and closed the YAML front-matter with un-indented `---` blocks",
				line,
				column));
			return null;
		}

		if (!_modules.TryGetValue(type, out var module))
		{
			diagnostics.Add(Error($"STM does not support the {type} diagram yet.", line, column));
			return null;
		}

		var document = module.Parse(source, new DiagramParseContext(type, keyword, limits, diagnostics));
		if (document is null)
		{
			if (!diagnostics.Exists(d => d.Severity == DiagnosticSeverity.Error))
			{
				diagnostics.Add(Error($"The {type} diagram did not parse.", line, column));
			}

			return null;
		}

		document.Title = preamble.Title;
		document.DisplayMode = preamble.DisplayMode;
		document.FrontMatterConfig = preamble.FrontMatterConfig;
		document.Directives.InsertRange(0, preamble.Directives);
		return document;
	}

	/// <summary>
	/// Draws the root <c>svg</c> as mermaid.js's <c>render</c> builds it: the size the module's layout gives, the
	/// accessibility title and description, the stylesheet, an empty group, then the module's drawing.
	/// </summary>
	private ResourceLimits Render(MermaidDocument document, IBufferWriter<byte> output, RenderOptions? options)
	{
		ArgumentNullException.ThrowIfNull(document);
		var module = ModuleFor(document);
		var limits = options?.Limits ?? _options.Limits;
		var id = options?.SvgId ?? DefaultSvgId;
		if (!CssText.IsIdentifier(id))
		{
			throw new ArgumentException($"The SVG id '{id}' is not a CSS identifier.", nameof(options));
		}

		var site = options?.SiteConfig is { } siteConfig ? WithLockedKeys(siteConfig, _options.LockedKeys) : _siteConfig;
		var config = MermaidConfig.Resolve(site, document, document.DiagramType, _themes);
		var context = new DiagramRenderContext(config, id, _textMeasurer, _icons, _layoutEngines, limits);
		var layout = module.Layout(document, context);

		var svg = new SvgWriter(output);
		svg.StartElement("svg");
		svg.WriteAttribute("id", id);
		svg.WriteAttribute("width", layout.Width);
		if (layout.Height is { } height)
		{
			svg.WriteAttribute("height", height);
		}

		if (layout.ViewBox is { } box)
		{
			svg.WriteAttribute("viewBox", FormattableString.Invariant($"{JsNumber.ToString(box.X)} {JsNumber.ToString(box.Y)} {JsNumber.ToString(box.Width)} {JsNumber.ToString(box.Height)}"));
		}

		svg.WriteAttribute("role", "graphics-document document");
		svg.WriteAttribute("aria-roledescription", document.DiagramType);
		if (document.AccessibilityTitle is not null)
		{
			svg.WriteAttribute("aria-labelledby", "chart-title-" + id);
		}

		if (document.AccessibilityDescription is not null)
		{
			svg.WriteAttribute("aria-describedby", "chart-desc-" + id);
		}

		WriteTextElement(svg, "title", "chart-title-" + id, document.AccessibilityTitle);
		WriteTextElement(svg, "desc", "chart-desc-" + id, document.AccessibilityDescription);
		svg.WriteStyle(MermaidStylesheet.Build(config, [], []));
		svg.StartElement("g");
		svg.EndElement();
		module.Render(document, layout, svg, context);
		svg.EndElement();
		if (!svg.IsComplete)
		{
			throw new InvalidOperationException($"The {document.DiagramType} module left an element open.");
		}

		return limits;
	}

	private static void WriteTextElement(SvgWriter svg, string name, string id, string? text)
	{
		if (text is null)
		{
			return;
		}

		svg.StartElement(name);
		svg.WriteAttribute("id", id);
		svg.WriteText(text);
		svg.EndElement();
	}

	private IDiagramModule ModuleFor(MermaidDocument document) =>
		_modules.TryGetValue(document.DiagramType, out var module)
			? module
			: throw new NotSupportedException($"No diagram module handles the {document.DiagramType} diagram.");

	/// <summary>The site config with <c>secure</c> set to <paramref name="lockedKeys"/>, when that is not <c>null</c>.</summary>
	private static MermaidValue? WithLockedKeys(MermaidValue? siteConfig, IReadOnlyList<string>? lockedKeys)
	{
		if (lockedKeys is null)
		{
			return siteConfig;
		}

		var entries = siteConfig is { Kind: MermaidValueKind.Mapping }
			? new OrderedDictionary<string, MermaidValue>(siteConfig.Entries, StringComparer.Ordinal)
			: new OrderedDictionary<string, MermaidValue>(StringComparer.Ordinal);
		entries["secure"] = MermaidValue.FromItems([.. lockedKeys.Select(MermaidValue.FromString)]);
		return MermaidValue.FromEntries(entries);
	}

	private static MermaidDiagnostic Error(string message, int line, int column) => new(DiagnosticSeverity.Error, message, line, column);

	/// <summary>The front matter and directives, before the module's parser makes the document.</summary>
	private sealed class PreambleDocument : MermaidDocument
	{
		public override string DiagramType => "";
	}

	private sealed class CountingBufferWriter(IBufferWriter<byte> inner) : IBufferWriter<byte>
	{
		public long Written { get; private set; }

		public void Advance(int count)
		{
			inner.Advance(count);
			Written += count;
		}

		public Memory<byte> GetMemory(int sizeHint = 0) => inner.GetMemory(sizeHint);

		public Span<byte> GetSpan(int sizeHint = 0) => inner.GetSpan(sizeHint);
	}
}
