using System.Globalization;
using SplashTheMusical.Config;
using SplashTheMusical.Rendering.Shapes;
using SplashTheMusical.Text;

namespace SplashTheMusical.Diagrams.Flowchart;

/// <summary>
/// Sizes what the dagre renderer measures in the browser: a node through its shape and <c>labelHelper</c>, as
/// <c>insertMeasuredNode</c> does, an edge's label as <c>insertEdgeLabel</c> does, and a subgraph's title as the
/// cluster's <c>rect</c> does.
/// </summary>
internal sealed class FlowchartMeasure
{
	/// <summary>The width <c>createText</c> wraps at when it is given none, as for edge labels.</summary>
	private const double DefaultWrapWidth = 200;

	/// <summary>The size of the box of the two nodes a self loop is laid out through: a <c>rect</c> 0.1 wide, as a float.</summary>
	private static readonly double DummySize = (double)0.1f;

	/// <summary>The padding the icon and image shapes' styles give each side of the label's <c>p</c>.</summary>
	private const double PicturedLabelPadding = 2;

	/// <summary><c>configureLabelImages</c>' <c>enlargingFactor</c>: a label of images alone shows each this many font sizes wide.</summary>
	private const double ImageSizeFactor = 5;

	/// <summary>
	/// The side of the square STM takes an image node's image to be when the node gives no <c>h</c> or <c>w</c>. mermaid.js
	/// reads the image's natural size once it loads, which STM does not do.
	/// </summary>
	private const double DefaultImageSide = 48;

	private readonly double? _configWrappingWidth;
	private readonly double _wrappingWidth;
	private readonly double _imageSize;
	private readonly double _minNodeWidth;
	private readonly double? _themeRadius;
	private readonly bool _flowchartHtmlLabels;
	private readonly bool _markdownAutoWrap;

	private readonly HashSet<string> _boxedClasses = new(StringComparer.Ordinal);

	public FlowchartMeasure(MermaidConfig config, string svgId, string diagramType, ITextMeasurer measurer, IReadOnlyList<FlowchartClass>? classes = null)
	{
		foreach (var definition in classes ?? [])
		{
			if (definition.Styles.Exists(BoxesInline))
			{
				_boxedClasses.Add(definition.Id);
			}
		}

		SvgId = svgId;
		DiagramType = diagramType;
		Look = config.Look ?? "classic";
		NodeHtmlLabels = config.HtmlLabels ?? true;
		EdgeHtmlLabels = config.HtmlLabels ?? config.Flowchart.HtmlLabels ?? true;
		_flowchartHtmlLabels = config.Flowchart.HtmlLabels ?? false;
		_configWrappingWidth = config.Flowchart.WrappingWidth;
		_wrappingWidth = Truthy(config.Flowchart.WrappingWidth) ? config.Flowchart.WrappingWidth!.Value : DefaultWrapWidth;
		_imageSize = ImageSizeFactor * (config.FontSize ?? 16);
		_minNodeWidth = config.Flowchart.MinNodeWidth ?? 0;
		_markdownAutoWrap = config.MarkdownAutoWrap != false;
		Padding = Truthy(config.Flowchart.Padding) ? config.Flowchart.Padding!.Value : 8;
		var family = ThemeString(config, "fontFamily") ?? config.FontFamily ?? "\"trebuchet ms\", verdana, arial, sans-serif";
		var size = ParseFontSize(ThemeString(config, "fontSize")) ?? config.FontSize ?? 16;
		Font = new FlowchartFont(measurer, family.TrimEnd(';'), size);
		_themeRadius = ParseFontSize(ThemeString(config, "radius"));
	}

	public string SvgId { get; }

	public string DiagramType { get; }

	public string Look { get; }

	/// <summary><c>flowchart.padding</c>, or 8 when it is 0, which each node gets.</summary>
	public double Padding { get; }

	public bool NodeHtmlLabels { get; }

	public bool EdgeHtmlLabels { get; }

	public FlowchartFont Font { get; }

	/// <summary>Draws the node's shape at its label's size and gives the node the size of the drawing.</summary>
	public void Node(FlowNode node, string direction)
	{
		if (node.Shape == "labelRect")
		{
			var dummy = new ShapeElement { Kind = ShapeElementKind.Rect, Width = DummySize, Height = DummySize };
			node.Geometry = new ShapeGeometry
			{
				Width = DummySize,
				Height = DummySize,
				Bounds = new ShapeBounds(0, 0, DummySize, DummySize),
				Elements = [dummy],
				Intersect = ShapeIntersect.Rect,
			};
			node.Width = DummySize;
			node.Height = DummySize;
			return;
		}

		var lines = NodeHtmlLabels ? FlowchartLabelText.Lines(node.Label, node.LabelType, true, _markdownAutoWrap) : FlowchartLabels.SvgLines(node.Label, node.LabelType);
		var pictured = node.Shape is "icon" or "iconCircle" or "iconRounded" or "iconSquare" or "imageSquare";
		var measurer = new LabelMeasurer(this, lines, node.Label.Length > 0)
		{
			ExtraLines = NodeHtmlLabels && EdgeHtmlLabels && lines.Count > 0 && BoxedSpan(node.Classes) ? 2 : 0,
			Inset = NodeHtmlLabels && pictured && lines.Count > 0 ? PicturedLabelPadding : 0,
			Images = NodeHtmlLabels ? FlowchartLabels.Images(node.Label) : default,
		};
		var vertex = node.Vertex;
		var imageSide = vertex?.AssetHeight ?? vertex?.AssetWidth ?? DefaultImageSide;
		var shape = NodeShapes.Find(node.Shape) ?? NodeShapes.Find("squareRect")!;
		var geometry = shape.Draw(new ShapeNode
		{
			HasLabel = node.Label.Length > 0,
			AssetWidth = vertex?.AssetWidth,
			AssetHeight = vertex?.AssetHeight,
			LabelPosition = vertex?.LabelPosition,
			Constraint = vertex?.Constraint,
			ImageNaturalWidth = imageSide,
			ImageNaturalHeight = imageSide,
			WrappingWidth = _configWrappingWidth,
			Label = measurer,
			Look = Look switch
			{
				"neo" => ShapeLook.Neo,
				"handDrawn" => ShapeLook.HandDrawn,
				_ => ShapeLook.Classic,
			},
			Padding = node.Padding,
			Direction = direction,
			ThemeRadius = _themeRadius,
			HtmlLabels = NodeHtmlLabels,
			FlowchartHtmlLabels = _flowchartHtmlLabels,
		});
		node.Geometry = geometry;
		node.LabelBox = geometry.Label is null ? null : measurer.Box;
		node.Width = geometry.Width;
		node.Height = geometry.Height;
	}

	/// <summary>Sizes the edge's label, which dagre makes room for. An SVG label has a background 2 wider on each side.</summary>
	public void Edge(FlowEdge edge)
	{
		var box = EdgeHtmlLabels
			? FlowchartLabelText.Measure(FlowchartLabelText.Lines(edge.Label, edge.LabelType, true, _markdownAutoWrap), Font, DefaultWrapWidth, 0, true)
			: FlowchartLabelText.MeasureSvg(FlowchartLabels.SvgLines(edge.Label, edge.LabelType), Font, DefaultWrapWidth, 0, edge.Label.Length > 0);
		edge.LabelBox = box;
		edge.Width = box.Width;
		edge.Height = box.Height;
	}

	/// <summary>
	/// A subgraph's title: Markdown goes through <c>createText</c> and wraps at the frame's width, and other text goes
	/// through <c>createLabel</c> and does not wrap.
	/// </summary>
	public FlowchartLabelBox ClusterLabel(FlowNode node)
	{
		var markdown = node.LabelType == FlowchartLabelType.Markdown;
		var width = markdown ? (Truthy(node.Width) ? node.Width : DefaultWrapWidth) : double.PositiveInfinity;
		if (markdown && !EdgeHtmlLabels)
		{
			return FlowchartLabelText.MeasureSvg(FlowchartLabels.SvgLines(node.Label, node.LabelType), Font, width, 0, false);
		}

		var lines = FlowchartLabelText.Lines(node.Label, node.LabelType, EdgeHtmlLabels, _markdownAutoWrap);
		return FlowchartLabelText.Measure(lines, Font, width, 0, EdgeHtmlLabels);
	}

	/// <summary>
	/// Whether a class of the node gives its label's <c>span</c> a border, padding or margin. mermaid.js writes a
	/// class's styles to <c>.class span</c> when <c>getEffectiveHtmlLabels</c> is true, and an inline box with one is
	/// not empty, so the empty pieces of the span before and after the label's <c>p</c> each take a line.
	/// </summary>
	private bool BoxedSpan(string classes)
	{
		foreach (var range in classes.AsSpan().Split(' '))
		{
			if (_boxedClasses.Contains(classes[range]))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>Whether the declaration sets a border, padding or margin that is not zero.</summary>
	private static bool BoxesInline(string declaration)
	{
		var colon = declaration.IndexOf(':', StringComparison.Ordinal);
		if (colon < 0)
		{
			return false;
		}

		var property = declaration.AsSpan(0, colon).Trim();
		var value = declaration.AsSpan(colon + 1).Trim();
		var boxing = property is "border" or "border-left" or "border-right" or "border-width" or "padding" or "padding-left" or "padding-right" or "margin" or "margin-left" or "margin-right";
		return boxing && value.Length > 0 && value[0] != '0' && !value.StartsWith("none", StringComparison.OrdinalIgnoreCase);
	}

	private static bool Truthy(double? value) => value is { } number && number != 0 && !double.IsNaN(number);

	private static string? ThemeString(MermaidConfig config, string name) =>
		config.ThemeVariables is { Kind: MermaidValueKind.Mapping } variables && variables.Entries.TryGetValue(name, out var value)
			? value.Kind switch
			{
				MermaidValueKind.String => value.String,
				MermaidValueKind.Number => JsNumber.ToString(value.Number),
				_ => null,
			}
			: null;

	/// <summary>The number at the start of a size such as <c>16px</c>.</summary>
	private static double? ParseFontSize(string? text)
	{
		if (text is null)
		{
			return null;
		}

		var span = text.AsSpan().Trim();
		var end = 0;
		while (end < span.Length && (char.IsAsciiDigit(span[end]) || span[end] == '.'))
		{
			end++;
		}

		return end > 0 && double.TryParse(span[..end], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
	}

	/// <summary><c>labelHelper</c>'s measurement of a node's label at the width the shape sets.</summary>
	private sealed class LabelMeasurer(FlowchartMeasure measure, List<List<LabelRun>> lines, bool hasLabel) : IShapeLabelMeasurer
	{
		public FlowchartLabelBox Box { get; private set; } = FlowchartLabelBox.Empty(measure.NodeHtmlLabels);

		/// <summary>The empty lines the label's <c>div</c> holds besides its text.</summary>
		public int ExtraLines { get; init; }

		/// <summary>The padding of each side of the label's <c>p</c>.</summary>
		public double Inset { get; init; }

		/// <summary>The label's <c>img</c> tags.</summary>
		public (int WithSource, int WithoutSource) Images { get; init; }

		public ShapeLabel Measure(double? nodeWidth, bool ignoreMinWidth)
		{
			var width = Truthy(nodeWidth) ? nodeWidth!.Value : measure._wrappingWidth;
			var minWidth = hasLabel && !Truthy(nodeWidth) && !ignoreMinWidth ? measure._minNodeWidth : 0;
			Box = measure.NodeHtmlLabels
				? FlowchartLabelText.Measure(lines, measure.Font, width, minWidth, true, Inset)
				: FlowchartLabelText.MeasureSvg(lines, measure.Font, width, minWidth, false);
			if (ExtraLines > 0)
			{
				Box = new FlowchartLabelBox(Box.Lines, Box.Width, Box.Height + (ExtraLines * Box.LineHeight), Box.LineHeight, Box.Html);
			}

			if (Images.WithSource + Images.WithoutSource > 0)
			{
				Box = WithImages(width, minWidth);
			}

			return Box.ToShapeLabel();
		}

		/// <summary>
		/// The box with the label's images, which <c>configureLabelImages</c> makes blocks of their own. Without text, each
		/// is <see cref="ImageSizeFactor"/> font sizes wide. With text, each is as wide as the label, which the image's own
		/// width makes wrap. An image without a <c>src</c> shows nothing. STM takes each image to be square.
		/// </summary>
		private FlowchartLabelBox WithImages(double wrapWidth, double minWidth)
		{
			var shown = Images.WithSource;
			if (Box.Lines.Count == 0)
			{
				return new FlowchartLabelBox([], Math.Max(measure._imageSize, minWidth), shown * measure._imageSize, Box.LineHeight, true);
			}

			var width = Math.Max(Box.Width, wrapWidth);
			return new FlowchartLabelBox(Box.Lines, width, Box.Height + (shown * width), Box.LineHeight, true);
		}
	}
}
