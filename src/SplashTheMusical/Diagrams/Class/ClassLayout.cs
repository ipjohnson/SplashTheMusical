// Upstream: packages/mermaid/src/diagrams/class/classRenderer-v3-unified.ts
using SplashTheMusical.Config;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Layout.Dagre;
using SplashTheMusical.Layout.Markers;
using SplashTheMusical.Parsing;
using SplashTheMusical.Rendering;
using SplashTheMusical.Rendering.Shapes;

namespace SplashTheMusical.Diagrams.Class;

/// <summary>
/// A laid out class diagram: the size of the root <c>svg</c>, its viewBox, and the drawing mermaid.js's dagre renderer
/// leaves, in which a namespace is a compound node, or a <c>g.root</c> of its own when no relation crosses its border.
/// </summary>
public sealed class ClassLayout : DiagramLayout
{
	internal ClassLayout(
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
	/// The box of each class, note, interface, namespace, relation and relation label in the viewBox's space, by the ids
	/// of <c>ClassDB.getData</c>: a relation and its label by <c>start --&gt; end</c>.
	/// </summary>
	internal IReadOnlyList<FlowchartBox> Boxes() => FlowchartLayout.BoxesOf(Root);
}

/// <summary>
/// <c>classRenderer-v3-unified.ts</c>'s <c>draw</c> with the dagre layout: <c>ClassDB.getData</c>, laid out and drawn by
/// the renderer flowchart shares, then <c>insertTitle</c> and <c>setupViewPortForSVG</c> with a padding of 8. The
/// renderer reads its spacing, title margin and <c>useMaxWidth</c> from the <c>state</c> config, as mermaid.js does.
/// </summary>
internal static class ClassLayoutStep
{
	/// <summary><c>insertTitle</c>'s size, from the <c>classDiagramTitleText</c> rule.</summary>
	private const double TitleFontSize = 18;

	private const double ViewPadding = 8;

	private const string DomIdPrefix = "classId-";

	public static ClassLayout Run(ClassDocument document, DiagramRenderContext context)
	{
		var config = context.Config;
		var state = config.State;
		var flowchart = config.Flowchart;
		var measure = new FlowchartMeasure(config, context.SvgId, FlowchartDagre.ClassDiagramType, context.TextMeasurer, null, (flowchart.WrappingWidth, null));
		var data = ClassData.From(document);
		var (nodes, edges) = Get(data, config, measure.Look);
		var dagre = new FlowchartDagre(measure, FlowchartDagre.ClassDiagramType, flowchart.SubGraphTitleMargin.Top ?? 0, flowchart.SubGraphTitleMargin.Bottom ?? 0);
		FlowchartDrawing root;
		try
		{
			root = dagre.Render(nodes, edges, data.Direction, Spacing(state.NodeSpacing), Spacing(state.RankSpacing));
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
			title = new FlowchartTitle(document.Title, bounds.MinX + (bounds.Width / 2), y, "classDiagramTitleText");
			var metrics = context.TextMeasurer.Measure(document.Title, new Text.FontSpec(measure.Font.Family, TitleFontSize));
			bounds = bounds.Union(new ShapeBounds(title.X - (metrics.Width / 2), y - metrics.Ascent, title.X + (metrics.Width / 2), y + metrics.Descent));
		}

		var width = bounds.Width + (ViewPadding * 2);
		var height = bounds.Height + (ViewPadding * 2);
		var viewBox = new SvgViewBox(bounds.MinX - ViewPadding, bounds.MinY - ViewPadding, width, height);
		var style = new FlowchartStyle(
			FlowchartDagre.ClassDiagramType,
			measure.Look,
			Theme(config, "mainBkg"),
			Theme(config, "nodeBorder"),
			Theme(config, "clusterBkg"),
			Theme(config, "clusterBorder"),
			Theme(config, "lineColor"),
			Theme(config, "classText") ?? Theme(config, "textColor"),
			measure.Font.Size)
		{
			Theme = config.Theme,
			Markers = MarkerDiagram.Class,
		};
		return new ClassLayout(
			width,
			state.UseMaxWidth == false ? height : null,
			viewBox,
			root,
			title,
			style,
			dagre.Inputs,
			ClassStyles.Rules(config, context.SvgId),
			ClassStyles.Classes(data));
	}

	/// <summary>
	/// <c>ClassDB.getData</c>: the namespaces as groups, the classes with a colour slot each, the notes with an edge to
	/// the class each is for, the lollipops' interfaces, then the relations. Without <c>class.hierarchicalNamespaces</c>
	/// only the namespaces a statement names are groups, with no parent, and a class or note is in the nearest of them.
	/// </summary>
	internal static (List<FlowNode> Nodes, List<FlowEdge> Edges) Get(ClassData data, MermaidConfig config, string look)
	{
		var hierarchical = config.Class.HierarchicalNamespaces ?? true;
		var padding = config.Class.Padding;
		var nodes = new List<FlowNode>();
		var edges = new List<FlowEdge>();
		string? ParentOf(string? parent) => hierarchical ? parent : ExplicitAncestor(data, parent);

		foreach (var space in data.Namespaces.Values)
		{
			if (!hierarchical && !space.Explicit)
			{
				continue;
			}

			nodes.Add(new FlowNode
			{
				Id = space.Id,
				Label = hierarchical ? space.Label : space.Id,
				LabelType = FlowchartLabelType.String,
				IsGroup = true,
				IsSubgraph = true,
				Padding = padding ?? 16,
				Shape = "rect",
				DomId = space.Id,
				ParentId = hierarchical ? space.Parent : null,
			});
		}

		var useGradient = ThemeValue(config, "useGradient") is { } gradient && PreambleParser.IsTruthy(gradient);
		var colorIndex = 0;
		var counter = 0;
		foreach (var node in data.Classes.Values)
		{
			nodes.Add(new FlowNode
			{
				Id = node.Id,
				Label = node.Text,
				Shape = "classBox",
				DomId = DomIdPrefix + node.Id + "-" + counter++,
				Classes = node.CssClasses,
				CssStyles = node.Styles,
				ParentId = ParentOf(node.Parent),
				ColorIndex = colorIndex++,
				ClassBox = new ClassBoxContent(
					node.Annotations.Count > 0 ? node.Annotations[0] : null,
					node.Text,
					[.. node.Attributes.Select(Line)],
					[.. node.Methods.Select(Line)],
					padding ?? 12,
					config.Class.HideEmptyMembersBox == true,
					useGradient),
			});
		}

		foreach (var note in data.Notes)
		{
			nodes.Add(new FlowNode
			{
				Id = note.Id,
				Label = note.Text,
				LabelType = FlowchartLabelType.Markdown,
				Shape = "note",
				Padding = padding ?? 6,
				DomId = note.Id,
				Classes = "undefined",
				CssStyles = ["text-align: left", "white-space: nowrap", $"fill: {ThemeText(config, "noteBkgColor")}", $"stroke: {ThemeText(config, "noteBorderColor")}"],
				ParentId = ParentOf(note.Parent),
			});
			if (note.Class is { } className && data.Classes.TryGetValue(className, out var target))
			{
				edges.Add(new FlowEdge
				{
					Id = "edgeNote" + note.Index.ToString(System.Globalization.CultureInfo.InvariantCulture),
					Index = edges.Count,
					Start = note.Id,
					End = target.Id,
					ArrowTypeStart = "none",
					ArrowTypeEnd = "none",
					Curve = config.Flowchart.Curve,
					Classes = "relation",
					Styles = ["fill: none"],
					Pattern = "dotted",

					// getData gives a note's edge no labelpos.
					LabelPos = "r",
				});
			}
		}

		foreach (var face in data.Interfaces)
		{
			nodes.Add(new FlowNode
			{
				Id = face.Id,
				Label = face.Label,
				LabelType = FlowchartLabelType.Markdown,
				Shape = "rect",
				DomId = face.Id,
				Classes = "undefined",
				CssStyles = ["opacity: 0;"],
			});
		}

		var count = 0;
		foreach (var relation in data.Relations)
		{
			count++;
			edges.Add(new FlowEdge
			{
				Id = $"id_{relation.Id1}_{relation.Id2}_{count.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
				Index = edges.Count,
				Start = relation.Id1,
				End = relation.Id2,
				Label = relation.Title ?? "",
				LabelType = FlowchartLabelType.Markdown,
				ArrowTypeStart = Marker(relation.Type1),
				ArrowTypeEnd = Marker(relation.Type2),
				Curve = config.Flowchart.Curve,
				Classes = "relation",
				Pattern = relation.Line == ClassLineStyle.Dotted ? "dashed" : "solid",
			});
		}

		return (nodes, edges);
	}

	private static ClassBoxLine Line(ClassMemberText member) => new(member.Text, member.Classifier == "*");

	/// <summary><c>getArrowMarker</c>.</summary>
	private static string Marker(ClassRelationEnd end) => end switch
	{
		ClassRelationEnd.Aggregation => "aggregation",
		ClassRelationEnd.Extension => "extension",
		ClassRelationEnd.Composition => "composition",
		ClassRelationEnd.Dependency => "dependency",
		ClassRelationEnd.Lollipop => "lollipop",
		_ => "none",
	};

	/// <summary><c>resolveExplicitAncestor</c>: the namespace or the nearest one around it that a statement names.</summary>
	private static string? ExplicitAncestor(ClassData data, string? id)
	{
		for (var current = id; current is not null;)
		{
			if (!data.Namespaces.TryGetValue(current, out var space))
			{
				return null;
			}

			if (space.Explicit)
			{
				return current;
			}

			current = space.Parent;
		}

		return null;
	}

	private static double Spacing(double? value) => value is { } number && number != 0 && !double.IsNaN(number) ? number : 50;

	private static MermaidValue? ThemeValue(MermaidConfig config, string name) =>
		config.ThemeVariables is { Kind: MermaidValueKind.Mapping } variables && variables.Entries.TryGetValue(name, out var value) ? value : null;

	/// <summary>A theme variable as a template literal writes it.</summary>
	private static string ThemeText(MermaidConfig config, string name) => ThemeValue(config, name) is { } value ? PreambleParser.ToJsString(value) : "undefined";

	private static string? Theme(MermaidConfig config, string name) =>
		ThemeValue(config, name) is { Kind: MermaidValueKind.String } value && SvgValues.IsAllowedPaint(value.String)
			? value.String
			: null;
}
