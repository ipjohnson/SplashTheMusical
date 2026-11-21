// Upstream: packages/mermaid/src/diagrams/requirement/requirementRenderer.ts
using SplashTheMusical.Config;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Layout.Dagre;
using SplashTheMusical.Layout.Markers;
using SplashTheMusical.Rendering;
using SplashTheMusical.Rendering.Shapes;
using SplashTheMusical.Text;

namespace SplashTheMusical.Diagrams.Requirement;

/// <summary>
/// A laid out requirement diagram: the size of the root <c>svg</c>, its viewBox, and the drawing mermaid.js's dagre
/// renderer leaves.
/// </summary>
public sealed class RequirementLayout : DiagramLayout
{
	internal RequirementLayout(
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
/// <c>requirementRenderer.ts</c>'s <c>draw</c> with the dagre layout: <c>RequirementDB.getData</c>, laid out with the
/// <c>state</c> config's <c>nodeSpacing</c> and <c>rankSpacing</c>, 50 when unset, and drawn by the renderer flowchart
/// shares, then <c>insertTitle</c> with <c>state.titleTopMargin</c> and <c>setupViewPortForSVG</c> with a padding of 8
/// and <c>state.useMaxWidth</c>. The renderer reads the <c>state</c> config, not the <c>requirement</c> one.
/// </summary>
internal static class RequirementLayoutStep
{
	/// <summary>The detector id, which the renderer's ids and marker ids carry.</summary>
	public const string DiagramType = "requirement";

	private const double ViewPadding = 8;

	public static RequirementLayout Run(RequirementDocument document, DiagramRenderContext context)
	{
		var config = context.Config;
		var state = config.State;
		var flowchart = config.Flowchart;
		var measure = new FlowchartMeasure(config, context.SvgId, DiagramType, context.TextMeasurer);
		var db = RequirementDb.From(document);
		var configFont = new FontSpec((config.FontFamily ?? "Arial").TrimEnd(';'), config.FontSize ?? 12);
		var (nodes, edges) = RequirementData.Get(db, flowchart.Curve, configFont);
		var dagre = new FlowchartDagre(measure, DiagramType, flowchart.SubGraphTitleMargin.Top ?? 0, flowchart.SubGraphTitleMargin.Bottom ?? 0);
		FlowchartDrawing root;
		try
		{
			root = dagre.Render(nodes, edges, db.Direction, state.NodeSpacing ?? 50, state.RankSpacing ?? 50);
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
			var y = -(state.TitleTopMargin ?? 25);
			title = new FlowchartTitle(document.Title, bounds.MinX + (bounds.Width / 2), y, "requirementDiagramTitleText");
			var metrics = context.TextMeasurer.Measure(document.Title, new FontSpec(measure.Font.Family, measure.Font.Size));
			bounds = bounds.Union(new ShapeBounds(title.X - (metrics.Width / 2), y - Math.Floor(metrics.Ascent + 0.5), title.X + (metrics.Width / 2), y + Math.Floor(metrics.Descent + 0.5)));
		}

		var width = bounds.Width + (ViewPadding * 2);
		var height = bounds.Height + (ViewPadding * 2);
		var viewBox = new SvgViewBox(bounds.MinX - ViewPadding, bounds.MinY - ViewPadding, width, height);
		var style = new FlowchartStyle(
			DiagramType,
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
			Markers = MarkerDiagram.Requirement,

			// An HTML label's div has the .labelBkg background, and an SVG label's rect the .edgeLabel .label rect fill.
			EdgeLabelBackground = measure.EdgeHtmlLabels
				? Theme(config, "requirementEdgeLabelBackground") ?? Theme(config, "edgeLabelBackground")
				: Theme(config, "edgeLabelBackground"),
		};
		return new RequirementLayout(
			width,
			state.UseMaxWidth == false ? height : null,
			viewBox,
			root,
			title,
			style,
			dagre.Inputs,
			RequirementStyles.Rules(config, measure.EdgeHtmlLabels),
			RequirementStyles.Classes(db));
	}

	private static string? Theme(MermaidConfig config, string name) =>
		config.ThemeVariables is { Kind: MermaidValueKind.Mapping } variables
		&& variables.Entries.TryGetValue(name, out var value) && value.Kind == MermaidValueKind.String
		&& SvgValues.IsAllowedPaint(value.String)
			? value.String
			: null;
}
