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

	/// <param name="nodeLabels">
	/// The <c>wrappingWidth</c> and <c>minWidth</c> the diagram's <c>getData</c> gives its nodes, or <c>null</c> for
	/// <c>flowchart.wrappingWidth</c> and <c>flowchart.minNodeWidth</c>.
	/// </param>
	public FlowchartMeasure(MermaidConfig config, string svgId, string diagramType, ITextMeasurer measurer, IReadOnlyList<FlowchartClass>? classes = null, (double? WrappingWidth, double? MinNodeWidth)? nodeLabels = null)
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
		_configWrappingWidth = nodeLabels is { } labels ? labels.WrappingWidth : config.Flowchart.WrappingWidth;
		_wrappingWidth = Truthy(_configWrappingWidth) ? _configWrappingWidth!.Value : DefaultWrapWidth;
		_imageSize = ImageSizeFactor * (config.FontSize ?? 16);
		_minNodeWidth = (nodeLabels is { } minimum ? minimum.MinNodeWidth : config.Flowchart.MinNodeWidth) ?? 0;
		_markdownAutoWrap = config.MarkdownAutoWrap != false;
		Padding = Truthy(config.Flowchart.Padding) ? config.Flowchart.Padding!.Value : 8;
		var family = ThemeString(config, "fontFamily") ?? config.FontFamily ?? "\"trebuchet ms\", verdana, arial, sans-serif";
		var size = ParseFontSize(ThemeString(config, "fontSize")) ?? config.FontSize ?? 16;
		Font = new FlowchartFont(measurer, family.TrimEnd(';'), size);
		_themeRadius = ParseFontSize(ThemeString(config, "radius"));
		ColorSlots = FlowchartStyles.IsColorTheme(config) ? FlowchartStyles.PaletteLength(config) : 0;
	}

	public string SvgId { get; }

	/// <summary>How many colour slots <c>stampColorSlot</c> cycles through, or 0 when the theme is not a colour theme.</summary>
	public int ColorSlots { get; }

	public string DiagramType { get; }

	public string Look { get; }

	/// <summary><c>flowchart.padding</c>, or 8 when it is 0, which each node gets.</summary>
	public double Padding { get; }

	public bool NodeHtmlLabels { get; }

	public bool EdgeHtmlLabels { get; }

	public FlowchartFont Font { get; }

	/// <summary><c>markdownAutoWrap</c>, which is on unless the config sets it false.</summary>
	public bool MarkdownAutoWrap => _markdownAutoWrap;

	/// <summary>The <c>er</c> config and theme values an entity's <c>erBox</c> reads, or <c>null</c> outside an ER diagram.</summary>
	public Er.ErBoxSettings? ErBox { get; init; }

	/// <summary><c>createText</c>'s label of <paramref name="text"/> at <paramref name="wrapWidth"/>, as HTML or as SVG text.</summary>
	public FlowchartLabelBox Text(string text, FlowchartLabelType type, double wrapWidth, bool html) =>
		html
			? FlowchartLabelText.Measure(FlowchartLabelText.Lines(text, type, true, _markdownAutoWrap), Font, wrapWidth, 0, true)
			: FlowchartLabelText.MeasureSvg(FlowchartLabels.SvgLines(text, type), Font, wrapWidth, 0, false);

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

		if (node.Shape == "rectWithTitle")
		{
			RectWithTitle(node);
			return;
		}

		if (node is { Shape: "classBox", ClassBox: { } content })
		{
			Class.ClassBox.Measure(this, node, content);
			return;
		}

		if (node.Shape == "erBox" && ErBox is { } erBox)
		{
			Er.ErBox.Draw(this, erBox, node);
			return;
		}

		var lines = NodeHtmlLabels ? FlowchartLabelText.Lines(node.Label, node.LabelType, true, _markdownAutoWrap) : FlowchartLabels.SvgLines(node.Label, node.LabelType);
		var pictured = node.Shape is "icon" or "iconCircle" or "iconRounded" or "iconSquare" or "imageSquare";
		var measurer = new LabelMeasurer(this, lines, node.Label.Length > 0)
		{
			ExtraLines = NodeHtmlLabels && EdgeHtmlLabels && lines.Count > 0 && BoxedSpan(node.Classes) ? 2 : 0,
			Inset = NodeHtmlLabels && pictured && lines.Count > 0 ? PicturedLabelPadding : 0,
			Images = NodeHtmlLabels ? FlowchartLabels.Images(node.Label) : default,
			HasMinWidth = !node.IsSubgraph && !node.IsGroup,
			NoWrap = NodeHtmlLabels && NoWrap(node),
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

	/// <summary>
	/// <c>rectWithTitle</c>, a state with descriptions: the title over the descriptions joined by <c>&lt;br/&gt;</c>, each
	/// through <c>createLabel</c>, which does not wrap, the narrower centred on the wider. A line under the title divides
	/// the frame, which is the labels padded by the node's padding.
	/// </summary>
	private void RectWithTitle(FlowNode node)
	{
		var html = EdgeHtmlLabels;
		var title = UnwrappedLabel(node.Label, html);
		var description = UnwrappedLabel(string.Join("<br/>", node.Description), html);
		var padding = node.Padding;
		var halfPadding = padding / 2;
		var titleX = description.Width < title.Width ? 0 : (description.Width - title.Width) / 2;
		var descriptionX = description.Width > title.Width ? 0 : (title.Width - description.Width) / 2;
		var descriptionY = title.Height + halfPadding + 5;
		var width = Math.Max(title.Width, description.Width);
		var height = descriptionY + description.Height;
		var labelX = -width / 2;
		var labelY = (-height / 2) - halfPadding + 3;
		var x = -width / 2 - halfPadding;
		var y = -height / 2 - halfPadding;
		var dividerY = y + title.Height + halfPadding;
		var rect = new ShapeElement { Kind = ShapeElementKind.Rect, Class = "outer title-state", X = x, Y = y, Width = width + padding, Height = height + padding };
		var line = new ShapeElement { Kind = ShapeElementKind.Line, Class = "divider", X = x, Y = dividerY, Width = width / 2 + halfPadding, Height = dividerY };
		var bounds = new ShapeBounds(x, y, x + width + padding, y + height + padding);
		node.Geometry = new ShapeGeometry
		{
			Width = bounds.Width,
			Height = bounds.Height,
			Bounds = bounds,
			Elements = [new ShapeElement { Kind = ShapeElementKind.Group, Children = [rect, line] }],
			Intersect = ShapeIntersect.Rect,
		};
		node.Labels = [(title, labelX + titleX, labelY), (description, labelX + descriptionX, labelY + descriptionY)];
		node.Width = bounds.Width;
		node.Height = bounds.Height;
	}

	/// <summary><c>createLabel</c>'s text: not Markdown, at an unbounded width.</summary>
	private FlowchartLabelBox UnwrappedLabel(string label, bool html) =>
		html
			? FlowchartLabelText.Measure(FlowchartLabelText.Lines(label, FlowchartLabelType.String, true, _markdownAutoWrap), Font, double.PositiveInfinity, 0, true)
			: FlowchartLabelText.MeasureSvg(FlowchartLabels.SvgLines(label, FlowchartLabelType.String), Font, double.PositiveInfinity, 0, false);

	/// <summary>
	/// The title of a state's <c>roundedWithTitle</c> frame, which <c>createLabel</c> draws without Markdown at an
	/// unbounded width.
	/// </summary>
	public FlowchartLabelBox StateClusterLabel(FlowNode node) => UnwrappedLabel(node.Label, EdgeHtmlLabels);

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

	/// <summary>
	/// Whether the node's styles end with <c>white-space: nowrap</c>, which <c>styles2String</c> gives the label's
	/// <c>span</c>. A class diagram's note has it.
	/// </summary>
	private static bool NoWrap(FlowNode node)
	{
		string? value = null;
		foreach (var style in node.CssCompiledStyles.Concat(node.CssStyles))
		{
			var parts = style.Split(':');
			if (parts[0].Trim() == "white-space")
			{
				value = parts.Length > 1 ? parts[1].Trim().TrimEnd(';').Trim() : null;
			}
		}

		return value == "nowrap";
	}

	/// <summary><c>labelHelper</c>'s measurement of a node's label at the width the shape sets.</summary>
	private sealed class LabelMeasurer(FlowchartMeasure measure, List<List<LabelRun>> lines, bool hasLabel) : IShapeLabelMeasurer
	{
		public FlowchartLabelBox Box { get; private set; } = FlowchartLabelBox.Empty(measure.NodeHtmlLabels);

		/// <summary>
		/// Whether the node has <c>node.minWidth</c>, <c>flowchart.minNodeWidth</c>, which <c>FlowDB</c> gives only a
		/// vertex that is not a group. A subgraph's node, such as a collapsed or an empty one, has none.
		/// </summary>
		public bool HasMinWidth { get; init; } = true;

		/// <summary>The empty lines the label's <c>div</c> holds besides its text.</summary>
		public int ExtraLines { get; init; }

		/// <summary>The padding of each side of the label's <c>p</c>.</summary>
		public double Inset { get; init; }

		/// <summary>
		/// Whether the HTML label's <c>span</c> has <c>white-space: nowrap</c>. Its lines then do not wrap: when the
		/// <c>div</c> reaches the wrapping width, <c>addHtmlSpan</c> makes it a table, which widens to the unbroken lines.
		/// </summary>
		public bool NoWrap { get; init; }

		/// <summary>The label's <c>img</c> tags.</summary>
		public (int WithSource, int WithoutSource) Images { get; init; }

		public ShapeLabel Measure(double? nodeWidth, bool ignoreMinWidth)
		{
			var width = Truthy(nodeWidth) ? nodeWidth!.Value : measure._wrappingWidth;
			var minWidth = hasLabel && !Truthy(nodeWidth) && !ignoreMinWidth && HasMinWidth ? measure._minNodeWidth : 0;
			Box = measure.NodeHtmlLabels
				? FlowchartLabelText.Measure(lines, measure.Font, NoWrap ? double.PositiveInfinity : width, minWidth, true, Inset)
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
