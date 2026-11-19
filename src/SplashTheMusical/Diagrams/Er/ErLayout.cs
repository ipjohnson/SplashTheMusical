// Upstream: packages/mermaid/src/diagrams/er/erRenderer-unified.ts
using SplashTheMusical.Config;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Layout.Dagre;
using SplashTheMusical.Layout.Markers;
using SplashTheMusical.Rendering;
using SplashTheMusical.Rendering.Shapes;
using SplashTheMusical.Text;

namespace SplashTheMusical.Diagrams.Er;

/// <summary>
/// A laid out entity relationship diagram: the size of the root <c>svg</c>, its viewBox, and the drawing mermaid.js's
/// dagre renderer leaves, in which a subgraph is a compound node, or a <c>g.root</c> of its own when no relationship
/// crosses its border.
/// </summary>
public sealed class ErLayout : DiagramLayout
{
	internal ErLayout(
		double width,
		double? height,
		SvgViewBox viewBox,
		FlowchartDrawing root,
		FlowchartTitle? title,
		FlowchartStyle style,
		IReadOnlyList<DagreInput> dagreInputs,
		IReadOnlyList<CssRule> diagramRules,
		IReadOnlyList<StyleClass> classes)
		: base(width, height)
	{
		ViewBox = viewBox;
		Root = root;
		Title = title;
		Style = style;
		DagreInputs = dagreInputs;
		DiagramRules = diagramRules;
		Classes = classes;
		ElementRules = FlowchartStyles.ElementRules(root);
	}

	internal override IReadOnlyList<CssRule> DiagramRules { get; }

	internal override IReadOnlyList<StyleClass> Classes { get; }

	internal override IReadOnlyList<CssRule> ElementRules { get; }

	internal FlowchartDrawing Root { get; }

	internal FlowchartTitle? Title { get; }

	internal FlowchartStyle Style { get; }

	/// <summary>The graph of each call to dagre's <c>layout</c>, in call order.</summary>
	internal IReadOnlyList<DagreInput> DagreInputs { get; }
}

/// <summary>
/// <c>erRenderer-unified.ts</c>'s <c>draw</c> with the dagre layout: <c>ErDB.getData</c>, laid out with
/// <c>er.nodeSpacing</c> and <c>er.rankSpacing</c> and drawn by the renderer flowchart shares, then <c>insertTitle</c>
/// and <c>setupViewPortForSVG</c> with a padding of 8.
/// </summary>
internal static class ErLayoutStep
{
	private const double ViewPadding = 8;

	/// <summary>The size of the ER stylesheet's <c>.edgeLabel .label</c> rule.</summary>
	private const double EdgeLabelFontSize = 14;

	public static ErLayout Run(ErDocument document, DiagramRenderContext context)
	{
		var config = context.Config;
		var er = config.Er;
		var flowchart = config.Flowchart;
		var measure = new FlowchartMeasure(config, context.SvgId, FlowchartDagre.ErDiagramType, context.TextMeasurer)
		{
			ErBox = new ErBoxSettings(
				er.DiagramPadding ?? 10,
				er.EntityPadding ?? 6,
				er.MinEntityWidth ?? 0,
				Theme(config, "rowOdd"),
				Theme(config, "rowEven"),
				Theme(config, "nodeBorder"),
				config.HtmlLabels != true,
				new FontSpec((config.FontFamily ?? "Arial").TrimEnd(';'), config.FontSize ?? 12)),
			EdgeLabelFontSize = EdgeLabelFontSize,
		};
		var db = ErDb.From(document);
		var (nodes, edges) = ErData.Get(db);
		var dagre = new FlowchartDagre(measure, FlowchartDagre.ErDiagramType, flowchart.SubGraphTitleMargin.Top ?? 0, flowchart.SubGraphTitleMargin.Bottom ?? 0);
		FlowchartDrawing root;
		try
		{
			root = dagre.Render(nodes, edges, db.Direction, Spacing(er.NodeSpacing, 140), Spacing(er.RankSpacing, 80));
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
			var y = -(er.TitleTopMargin ?? 25);
			title = new FlowchartTitle(document.Title, bounds.MinX + (bounds.Width / 2), y, "erDiagramTitleText");
			var metrics = context.TextMeasurer.Measure(document.Title, new Text.FontSpec(measure.Font.Family, measure.Font.Size));
			bounds = bounds.Union(new ShapeBounds(title.X - (metrics.Width / 2), y - Math.Floor(metrics.Ascent + 0.5), title.X + (metrics.Width / 2), y + Math.Floor(metrics.Descent + 0.5)));
		}

		var width = bounds.Width + (ViewPadding * 2);
		var height = bounds.Height + (ViewPadding * 2);
		var viewBox = new SvgViewBox(bounds.MinX - ViewPadding, bounds.MinY - ViewPadding, width, height);
		var style = new FlowchartStyle(
			FlowchartDagre.ErDiagramType,
			measure.Look,
			Theme(config, "mainBkg"),
			Theme(config, "nodeBorder"),
			Theme(config, "clusterBkg") ?? Theme(config, "mainBkg"),
			Theme(config, "clusterBorder") ?? Theme(config, "nodeBorder"),
			Theme(config, "lineColor"),
			Theme(config, "nodeTextColor") ?? Theme(config, "textColor"),
			measure.Font.Size)
		{
			Theme = config.Theme,
			Markers = MarkerDiagram.Er,
		};
		return new ErLayout(
			width,
			er.UseMaxWidth == false ? height : null,
			viewBox,
			root,
			title,
			style,
			dagre.Inputs,
			ErStyles.Rules(config),
			ErStyles.Classes(db));
	}

	/// <summary><c>conf?.nodeSpacing || 140</c> and <c>conf?.rankSpacing || 80</c>.</summary>
	private static double Spacing(double? value, double fallback) => value is { } number && number != 0 && !double.IsNaN(number) ? number : fallback;

	private static string? Theme(MermaidConfig config, string name) =>
		config.ThemeVariables is { Kind: MermaidValueKind.Mapping } variables
		&& variables.Entries.TryGetValue(name, out var value) && value.Kind == MermaidValueKind.String
		&& SvgValues.IsAllowedPaint(value.String)
			? value.String
			: null;
}
