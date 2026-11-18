using System.Text;
using SplashTheMusical.Config;
using SplashTheMusical.Layout.Dagre;
using SplashTheMusical.Rendering;
using SplashTheMusical.Rendering.Shapes;

namespace SplashTheMusical.Diagrams.Flowchart;

/// <summary>
/// The flowchart of <c>packages/mermaid/src/diagrams/flowchart</c>, under the detector id <c>flowchart-v2</c> for
/// <c>graph</c> and <c>flowchart</c>, or <c>flowchart-elk</c> for <c>flowchart-elk</c>. The parser is
/// <see cref="FlowchartParser"/> and the writer <see cref="FlowchartWriter"/>. The layout is mermaid.js's dagre renderer,
/// <see cref="FlowchartDagre"/>, whatever the <c>layout</c> config names, and <see cref="FlowchartRenderer"/> draws it.
/// </summary>
public sealed class FlowchartModule : DiagramModule<FlowchartDocument, FlowchartLayout>
{
	public const string FlowchartV2 = "flowchart-v2";
	public const string FlowchartElk = "flowchart-elk";

	/// <param name="diagramType"><see cref="FlowchartV2"/> or <see cref="FlowchartElk"/>.</param>
	public FlowchartModule(string diagramType = FlowchartV2)
	{
		if (diagramType is not FlowchartV2 and not FlowchartElk)
		{
			throw new ArgumentException($"A flowchart module's type is {FlowchartV2} or {FlowchartElk}, not '{diagramType}'.", nameof(diagramType));
		}

		DiagramType = diagramType;
	}

	public override string DiagramType { get; }

	/// <summary>mermaid.js's detectors: <c>/^\s*flowchart-elk/</c>, and <c>/^\s*(flowchart|graph)/</c> for <c>flowchart-v2</c>.</summary>
	public override bool Detect(ReadOnlySpan<char> text) =>
		text.StartsWith(FlowchartElk, StringComparison.Ordinal)
			? DiagramType == FlowchartElk
			: DiagramType == FlowchartV2 && (text.StartsWith("flowchart", StringComparison.Ordinal) || text.StartsWith("graph", StringComparison.Ordinal));

	public override FlowchartDocument? Parse(ReadOnlySpan<char> source, DiagramParseContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return FlowchartParser.Parse(source, context);
	}

	public override void Write(FlowchartDocument document, StringBuilder output) => FlowchartWriter.Write(document, output);

	/// <summary>
	/// <c>flowRenderer-v3-unified.ts</c>'s <c>draw</c> with the dagre layout: the layout data of the document, laid out
	/// and drawn, then <c>insertTitle</c> and <c>setupViewPortForSVG</c>, which pads the drawing's box by
	/// <c>flowchart.diagramPadding</c> for the viewBox.
	/// </summary>
	public override FlowchartLayout Layout(FlowchartDocument document, DiagramRenderContext context)
	{
		ArgumentNullException.ThrowIfNull(document);
		ArgumentNullException.ThrowIfNull(context);
		var config = context.Config;
		var flowchart = config.Flowchart;
		var measure = new FlowchartMeasure(config, context.SvgId, DiagramType, context.TextMeasurer, document.Classes);
		var (nodes, edges) = FlowchartData.Get(document, measure.Padding, flowchart.Curve);
		var direction = Direction(document.Direction);
		if (flowchart.InheritDir == true)
		{
			nodes = [.. nodes.Select(n => n.IsGroup && n.Dir is null ? WithDir(n, direction) : n)];
		}

		var dagre = new FlowchartDagre(measure, DiagramType, flowchart.SubGraphTitleMargin.Top ?? 0, flowchart.SubGraphTitleMargin.Bottom ?? 0);
		FlowchartDrawing root;
		try
		{
			root = dagre.Render(nodes, edges, direction, Spacing(flowchart.NodeSpacing), Spacing(flowchart.RankSpacing));
		}
		catch (DagreException e)
		{
			throw new MermaidParseException(new MermaidDiagnostic(DiagnosticSeverity.Error, e.Message, 0, 0));
		}

		var bounds = root.Bounds();
		if (bounds.IsEmpty)
		{
			bounds = new ShapeBounds(0, 0, 0, 0);
		}

		FlowchartTitle? title = null;
		if (!string.IsNullOrEmpty(document.Title))
		{
			var y = -(Truthy(flowchart.TitleTopMargin) ? flowchart.TitleTopMargin!.Value : 0);
			title = new FlowchartTitle(document.Title, bounds.MinX + (bounds.Width / 2), y);
			var metrics = context.TextMeasurer.Measure(document.Title, new Text.FontSpec(measure.Font.Family, TitleFontSize));
			bounds = bounds.Union(new ShapeBounds(title.X - (metrics.Width / 2), y - metrics.Ascent, title.X + (metrics.Width / 2), y + metrics.Descent));
		}

		var padding = flowchart.DiagramPadding ?? 8;
		var width = bounds.Width + (padding * 2);
		var height = bounds.Height + (padding * 2);
		var viewBox = new SvgViewBox(bounds.MinX - padding, bounds.MinY - padding, width, height);
		var style = new FlowchartStyle(
			DiagramType,
			measure.Look,
			Theme(config, "mainBkg"),
			Theme(config, "nodeBorder"),
			Theme(config, "clusterBkg"),
			Theme(config, "clusterBorder"),
			Theme(config, "lineColor"),
			Theme(config, "nodeTextColor") ?? Theme(config, "textColor"),
			measure.Font.Size)
		{
			Theme = config.Theme,
			Gradient = config.ThemeVariables is { Kind: MermaidValueKind.Mapping } variables && variables["useGradient"] is { } useGradient
				&& Parsing.PreambleParser.IsTruthy(useGradient)
				? (Theme(config, "gradientStart"), Theme(config, "gradientStop"))
				: null,
		};
		return new FlowchartLayout(
			width,
			flowchart.UseMaxWidth == true ? null : height,
			viewBox,
			root,
			title,
			style,
			dagre.Inputs,
			FlowchartStyles.Rules(config),
			FlowchartStyles.Classes(document));
	}

	public override void Render(FlowchartDocument document, FlowchartLayout layout, SvgWriter svg, DiagramRenderContext context)
	{
		ArgumentNullException.ThrowIfNull(layout);
		ArgumentNullException.ThrowIfNull(svg);
		ArgumentNullException.ThrowIfNull(context);
		FlowchartRenderer.Render(layout, svg, context.SvgId);
	}

	/// <summary>The size of the title's text, from the <c>flowchartTitleText</c> rule of the flowchart stylesheet.</summary>
	private const double TitleFontSize = 18;

	/// <summary><c>FlowDB.setDirection</c> has made <c>TD</c> into <c>TB</c>, which dagre reads as it reads <c>TB</c>.</summary>
	private static string Direction(string direction) => direction == "TD" ? "TB" : direction;

	private static double Spacing(double? value) => Truthy(value) ? value!.Value : 50;

	private static bool Truthy(double? value) => value is { } number && number != 0 && !double.IsNaN(number);

	private static FlowNode WithDir(FlowNode node, string dir) => new()
	{
		Id = node.Id,
		IsGroup = node.IsGroup,
		Label = node.Label,
		LabelType = node.LabelType,
		ParentId = node.ParentId,
		Shape = node.Shape,
		Dir = dir,
		Padding = node.Padding,
		DomId = node.DomId,
		Classes = node.Classes,
		CssStyles = node.CssStyles,
		CssCompiledStyles = node.CssCompiledStyles,
		ColorIndex = node.ColorIndex,
		IsSubgraph = node.IsSubgraph,
	};

	private static string? Theme(MermaidConfig config, string name) =>
		config.ThemeVariables is { Kind: MermaidValueKind.Mapping } variables
		&& variables.Entries.TryGetValue(name, out var value) && value.Kind == MermaidValueKind.String
		&& SvgValues.IsAllowedPaint(value.String)
			? value.String
			: null;
}
