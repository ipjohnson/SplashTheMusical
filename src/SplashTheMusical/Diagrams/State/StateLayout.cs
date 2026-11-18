using SplashTheMusical.Config;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Layout.Dagre;
using SplashTheMusical.Layout.Markers;
using SplashTheMusical.Rendering;
using SplashTheMusical.Rendering.Shapes;

namespace SplashTheMusical.Diagrams.State;

/// <summary>
/// A laid out state diagram: the size of the root <c>svg</c>, its viewBox, and the drawing mermaid.js's dagre renderer
/// leaves, in which a composite state is a compound node, or a <c>g.root</c> of its own when no transition crosses its
/// border.
/// </summary>
public sealed class StateLayout : DiagramLayout
{
	internal StateLayout(
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

	/// <summary>
	/// The box of each state, composite state, region, note group, transition and transition label in the viewBox's
	/// space, by the ids of <see cref="StateData"/>: a transition and its label by <c>start --&gt; end</c>.
	/// </summary>
	internal IReadOnlyList<FlowchartBox> Boxes() => FlowchartLayout.BoxesOf(Root);
}

/// <summary>
/// <c>stateRenderer-v3-unified.ts</c>'s <c>draw</c> with the dagre layout: <c>StateDB.getData</c>, laid out and drawn
/// by the renderer flowchart shares, then <c>insertTitle</c> and <c>setupViewPortForSVG</c> with a padding of 8.
/// </summary>
internal static class StateLayoutStep
{
	/// <summary><c>insertTitle</c>'s size, from the <c>statediagramTitleText</c> rule.</summary>
	private const double TitleFontSize = 18;

	private const double ViewPadding = 8;

	public static StateLayout Run(StateDocument document, DiagramRenderContext context)
	{
		var config = context.Config;
		var state = config.State;
		var flowchart = config.Flowchart;
		var measure = new FlowchartMeasure(config, context.SvgId, FlowchartDagre.StateDiagramType, context.TextMeasurer, null, (state.WrappingWidth, state.MinNodeWidth));
		var data = StateData.From(document, measure.Look);
		var (nodes, edges) = Get(data, flowchart.Padding, flowchart.Curve);
		var dagre = new FlowchartDagre(measure, FlowchartDagre.StateDiagramType, flowchart.SubGraphTitleMargin.Top ?? 0, flowchart.SubGraphTitleMargin.Bottom ?? 0);
		var root = dagre.Render(nodes, edges, data.RootDirection, Spacing(state.NodeSpacing), Spacing(state.RankSpacing));

		var bounds = root.Bounds();
		if (bounds.IsEmpty)
		{
			bounds = new ShapeBounds(0, 0, 0, 0);
		}

		FlowchartTitle? title = null;
		if (!string.IsNullOrEmpty(document.Title))
		{
			var y = -(state.TitleTopMargin ?? 25);
			title = new FlowchartTitle(document.Title, bounds.MinX + (bounds.Width / 2), y, "statediagramTitleText");
			var metrics = context.TextMeasurer.Measure(document.Title, new Text.FontSpec(measure.Font.Family, TitleFontSize));
			bounds = bounds.Union(new ShapeBounds(title.X - (metrics.Width / 2), y - metrics.Ascent, title.X + (metrics.Width / 2), y + metrics.Descent));
		}

		var width = bounds.Width + (ViewPadding * 2);
		var height = bounds.Height + (ViewPadding * 2);
		var viewBox = new SvgViewBox(bounds.MinX - ViewPadding, bounds.MinY - ViewPadding, width, height);
		var style = new FlowchartStyle(
			FlowchartDagre.StateDiagramType,
			measure.Look,
			Theme(config, "mainBkg"),
			Theme(config, "nodeBorder"),
			Theme(config, "compositeTitleBackground"),
			Theme(config, "nodeBorder"),
			Theme(config, "transitionColor") ?? Theme(config, "lineColor"),
			Theme(config, "stateLabelColor") ?? Theme(config, "textColor"),
			measure.Font.Size)
		{
			Theme = config.Theme,
			Markers = MarkerDiagram.State,
		};
		return new StateLayout(
			width,
			state.UseMaxWidth == false ? height : null,
			viewBox,
			root,
			title,
			style,
			dagre.Inputs,
			StateStyles.Rules(config, context.SvgId),
			StateStyles.Classes(data));
	}

	/// <summary>
	/// The nodes and edges of <see cref="StateData"/> as the renderer's layout data. <c>insertNode</c> draws a state's
	/// <c>rect</c>, which has <c>rx</c> and <c>ry</c>, as <c>roundedRect</c>. A note takes <c>flowchart.padding</c>.
	/// </summary>
	private static (List<FlowNode> Nodes, List<FlowEdge> Edges) Get(StateData data, double? flowchartPadding, string? curve)
	{
		var nodes = new List<FlowNode>(data.Nodes.Count);
		foreach (var node in data.Nodes)
		{
			nodes.Add(new FlowNode
			{
				Id = node.Id,
				IsGroup = node.IsGroup,
				Label = node.Label[0],
				Description = [.. node.Label.Skip(1)],
				LabelType = FlowchartLabelType.Markdown,
				ParentId = node.ParentId,
				Shape = node.Shape switch
				{
					null or "rect" => "roundedRect",
					var shape => shape,
				},
				Dir = node.IsGroup ? node.Direction ?? "TB" : null,
				Padding = node.Padding ?? flowchartPadding ?? 0,
				DomId = node.DomId,
				Classes = node.CssClasses,
				CssStyles = node.CssStyles,
				CssCompiledStyles = node.CssCompiledStyles,
				ColorIndex = node.ColorIndex,
				IsSubgraph = node.IsGroup,
			});
		}

		var edges = new List<FlowEdge>(data.Edges.Count);
		for (var i = 0; i < data.Edges.Count; i++)
		{
			var edge = data.Edges[i];
			edges.Add(new FlowEdge
			{
				Id = edge.Id,
				Index = i,
				Start = edge.Start,
				End = edge.End,
				Label = edge.Label,
				LabelType = FlowchartLabelType.Markdown,
				ArrowTypeStart = "none",
				ArrowTypeEnd = edge.ArrowTypeEnd,
				Curve = curve,
				Classes = edge.IsNoteEdge ? "transition note-edge" : "transition",
				Styles = ["fill:none"],
				Pattern = edge.IsNoteEdge ? "dashed" : null,
			});
		}

		return (nodes, edges);
	}

	private static double Spacing(double? value) => value is { } number && number != 0 && !double.IsNaN(number) ? number : 50;

	private static string? Theme(MermaidConfig config, string name) =>
		config.ThemeVariables is { Kind: MermaidValueKind.Mapping } variables
		&& variables.Entries.TryGetValue(name, out var value) && value.Kind == MermaidValueKind.String
		&& SvgValues.IsAllowedPaint(value.String)
			? value.String
			: null;
}
