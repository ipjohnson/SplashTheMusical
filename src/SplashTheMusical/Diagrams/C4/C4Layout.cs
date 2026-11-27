// Upstream: packages/mermaid/src/diagrams/c4/c4Renderer.ts
// Upstream: packages/mermaid/src/diagrams/c4/c4ShapeAdapter.ts
// Upstream: packages/mermaid/src/diagrams/c4/svgDraw.ts
using System.Globalization;
using SplashTheMusical.Config;
using SplashTheMusical.Rendering;
using SplashTheMusical.Rendering.Shapes;
using SplashTheMusical.Text;

namespace SplashTheMusical.Diagrams.C4;

/// <summary>
/// A laid out C4 diagram: the shapes and boundaries in the order mermaid.js appends them, the relations, the title and
/// the size of the root <c>svg</c>.
/// </summary>
public sealed class C4Layout : DiagramLayout
{
	internal C4Layout(
		double width,
		double? height,
		SvgViewBox viewBox,
		IReadOnlyList<C4Drawing> drawings,
		IReadOnlyList<C4RelDrawing> rels,
		C4TextLine? title,
		IReadOnlyList<CssRule> diagramRules)
		: base(width, height)
	{
		ViewBox = viewBox;
		Drawings = drawings;
		Rels = rels;
		Title = title;
		DiagramRules = diagramRules;
		var rules = new List<CssRule>();
		foreach (var drawing in drawings)
		{
			if (drawing is C4ShapeDrawing shape && CssDeclaration.FromSource("color:" + shape.FontColor, false) is { } color)
			{
				shape.StyleClass = "stm-style-" + rules.Count.ToString(CultureInfo.InvariantCulture);
				rules.Add(new CssStyleRule(CssSelector.Parse("." + shape.StyleClass), color));
			}
		}

		ElementRules = rules;
	}

	internal override IReadOnlyList<CssRule> DiagramRules { get; }

	/// <summary>The <c>color</c> of each element's inline style, which its label's text is filled with.</summary>
	internal override IReadOnlyList<CssRule> ElementRules { get; }

	/// <summary>The shapes and the boundaries, in the order <c>drawInsideBoundary</c> appends them.</summary>
	internal IReadOnlyList<C4Drawing> Drawings { get; }

	internal IReadOnlyList<C4RelDrawing> Rels { get; }

	internal C4TextLine? Title { get; }
}

/// <summary>A shape or a boundary of a laid out C4 diagram.</summary>
internal abstract class C4Drawing;

/// <summary>
/// A person, system, container or component: the shape <c>buildC4Node</c> picks, drawn in a group translated to the centre
/// of its place in the grid.
/// </summary>
internal sealed class C4ShapeDrawing : C4Drawing
{
	public required string Alias { get; init; }

	/// <summary>The classes of the shape's <c>g</c>: <c>node c4-shape c4-</c> and the type, and <c>c4-external</c>.</summary>
	public required string Classes { get; init; }

	/// <summary>The left of the shape's place, <c>c4Shape.x</c>.</summary>
	public required double X { get; init; }

	/// <summary>The top of the shape's place, <c>c4Shape.y</c>.</summary>
	public required double Y { get; init; }

	/// <summary>The width of the outline, which <c>updateNodeBounds</c> gives the node.</summary>
	public required double Width { get; init; }

	/// <summary>The height of the outline.</summary>
	public required double Height { get; init; }

	public required ShapeGeometry Geometry { get; init; }

	public required IReadOnlyList<C4LabelSection> Sections { get; init; }

	/// <summary>The <c>fill</c> of the inline style, or <c>null</c> for none.</summary>
	public string? Fill { get; init; }

	/// <summary>The <c>stroke</c> of the inline style, or <c>null</c> for none.</summary>
	public string? Stroke { get; init; }

	/// <summary>The <c>color</c> of the label's inline style.</summary>
	public required string FontColor { get; init; }

	/// <summary>The class of the shape's rule in <see cref="C4Layout.ElementRules"/>, or <c>null</c> when it has none.</summary>
	public string? StyleClass { get; set; }

	public double CenterX => X + (Width / 2);

	public double CenterY => Y + (Height / 2);
}

/// <summary>A boundary or a deployment node: <c>svgDraw.drawBoundary</c>'s rect and its label, type and description.</summary>
internal sealed class C4BoundaryDrawing : C4Drawing
{
	public required string Alias { get; init; }

	public required double X { get; init; }

	public required double Y { get; init; }

	public required double Width { get; init; }

	public required double Height { get; init; }

	public required string Fill { get; init; }

	public required string Stroke { get; init; }

	/// <summary>Whether the frame is dashed, which a deployment node's is not.</summary>
	public required bool Dashed { get; init; }

	/// <summary>The lines of the label, the type and the description, each a <c>text</c> of its own.</summary>
	public required IReadOnlyList<C4TextLine> Texts { get; init; }
}

/// <summary>
/// A relation: a <c>line</c> for the first and a quadratic <c>path</c> for each other, and its label and technology.
/// </summary>
internal sealed class C4RelDrawing
{
	public required string From { get; init; }

	public required string To { get; init; }

	public required ShapePoint Start { get; init; }

	public required ShapePoint End { get; init; }

	/// <summary>Whether it is drawn as a <c>line</c>, which only the first relation is.</summary>
	public required bool IsLine { get; init; }

	public required string Stroke { get; init; }

	/// <summary>Whether the end has the arrowhead, which <c>Rel_Back</c>'s does not.</summary>
	public required bool ArrowEnd { get; init; }

	/// <summary>Whether the start has the arrow end, which <c>BiRel</c>'s and <c>Rel_Back</c>'s do.</summary>
	public required bool ArrowStart { get; init; }

	public required IReadOnlyList<C4TextLine> Texts { get; init; }

	/// <summary>
	/// The path's <c>d</c>: <c>Mstartx,starty Qcontrolx,controly stopx,stopy </c> with the numbers as JavaScript writes
	/// them, the control point a quarter of the way along in x and half way in y.
	/// </summary>
	public string PathData
	{
		get
		{
			var dx = End.X - Start.X;
			var controlX = Start.X + (dx / 2) - (dx / 4);
			var controlY = Start.Y + ((End.Y - Start.Y) / 2);
			return "M" + N(Start.X) + "," + N(Start.Y) + " Q" + N(controlX) + "," + N(controlY) + " " + N(End.X) + "," + N(End.Y) + " ";
		}
	}

	private static string N(double value) => JsNumber.ToString(value);
}

/// <summary>
/// A line of text <c>_drawTextCandidateFunc</c>'s <c>byTspan</c> writes: a <c>text</c> centred on <see cref="X"/> with its
/// middle on <see cref="Y"/>, and a <c>tspan</c> moved down by <see cref="Dy"/>. The title is a plain <c>text</c> at its
/// baseline.
/// </summary>
internal sealed record C4TextLine(string Text, double X, double Y, double Dy, FontSpec Font, string Fill, bool Italic = false);

/// <summary>
/// <c>c4Renderer.ts</c>'s <c>draw</c>, which places every part with arithmetic and no layout engine:
/// <c>drawInsideBoundary</c> walks the boundaries from <c>global</c> down, puts each boundary's shapes in rows with
/// <c>Bounds.insert</c> and the boundaries in rows of <c>c4BoundaryInRow</c>, and sizes each boundary to what it holds.
/// <c>drawRels</c> then joins the outlines of each relation's ends.
/// </summary>
/// <remarks>
/// The shapes are mermaid's unified shapes, which <c>buildC4Node</c> picks and gives the stacked label of
/// <c>c4LabelHelper</c>. The top-level width limit is <c>screen.availWidth</c>, 800 in the oracle's headless Chromium.
/// The db's mutations are kept: the type of a boundary is drawn in brackets, and a <c>C4Dynamic</c> relation's label is
/// numbered.
/// </remarks>
internal sealed class C4LayoutStep
{
	/// <summary><c>screen.availWidth</c> of the oracle's headless Chromium.</summary>
	internal const double ScreenWidth = 800;

	private const string DefaultFamily = "\"Open Sans\", sans-serif";
	private const string DefaultLineColor = "#444444";
	private const double SpriteSize = 48;

	private static readonly HashSet<string> QueueShapes = new(
		["system_queue", "external_system_queue", "container_queue", "external_container_queue", "component_queue", "external_component_queue"],
		StringComparer.Ordinal);

	private static readonly HashSet<string> DbShapes = new(
		["system_db", "external_system_db", "container_db", "external_container_db", "component_db", "external_component_db"],
		StringComparer.Ordinal);

	private static readonly HashSet<string> ElementTypes = new(C4Styles.ElementTypes, StringComparer.Ordinal);

	private readonly C4Db _db;
	private readonly MermaidConfig _config;
	private readonly MermaidValue _c4;
	private readonly ITextMeasurer _measurer;
	private readonly double _marginX;
	private readonly double _marginY;
	private readonly double _shapeMargin;
	private readonly double _shapePadding;
	private readonly double _elementWidth;
	private readonly double _nextLinePaddingX;
	private readonly bool _c4Wrap;
	private readonly bool _textWrap;
	private readonly ShapeLook _look;
	private readonly FontSpec _boundaryFont;
	private readonly FontSpec _messageFont;
	private readonly Dictionary<(C4DbItem Item, string Field), Measured> _measured = [];
	private readonly Dictionary<C4DbItem, string> _types = [];
	private readonly Dictionary<C4DbItem, C4ShapeDrawing> _shapes = [];
	private readonly Dictionary<C4DbItem, C4BoundaryDrawing> _boundaries = [];
	private readonly List<C4Drawing> _drawings = [];
	private double _globalMaxX;
	private double _globalMaxY;

	private C4LayoutStep(C4Db db, DiagramRenderContext context)
	{
		_db = db;
		_config = context.Config;
		var c4 = _config.C4;
		_c4 = c4.ToValue();
		_measurer = context.TextMeasurer;
		_marginX = c4.DiagramMarginX ?? 50;
		_marginY = c4.DiagramMarginY ?? 10;
		_shapeMargin = c4.C4ShapeMargin ?? 50;
		_shapePadding = c4.C4ShapePadding ?? 20;
		_elementWidth = c4.Width ?? 216;
		_nextLinePaddingX = c4.NextLinePaddingX ?? 0;
		_c4Wrap = c4.Wrap ?? true;

		// Each shape, boundary and relation takes the db's wrap when it is added, which is the root wrap the parse ran with.
		_textWrap = _config.Wrap == true && _c4Wrap;
		_look = (_config.Look ?? "classic") switch
		{
			"neo" => ShapeLook.Neo,
			"handDrawn" => ShapeLook.HandDrawn,
			_ => ShapeLook.Classic,
		};
		_boundaryFont = ConfigFont("boundary", DefaultFamily, 14);
		_messageFont = ConfigFont("message", DefaultFamily, 12);
	}

	public static C4Layout Run(C4Document document, DiagramRenderContext context)
	{
		ArgumentNullException.ThrowIfNull(document);
		ArgumentNullException.ThrowIfNull(context);
		var db = C4Db.From(document);
		return new C4LayoutStep(db, context).Layout(context.Config);
	}

	private C4Layout Layout(MermaidConfig config)
	{
		var screen = new Bounds();
		screen.SetData(_marginX, _marginX, _marginY, _marginY);
		screen.WidthLimit = ScreenWidth;
		_globalMaxX = _marginX;
		_globalMaxY = _marginY;
		DrawInsideBoundary(screen, Boundaries(C4Value.String("")));
		var rels = DrawRels();

		var boxStartX = screen.StartX;
		var boxStartY = screen.StartY;
		var boxHeight = _globalMaxY - boxStartY;
		var height = boxHeight + (2 * _marginY);
		var boxWidth = _globalMaxX - boxStartX;
		var width = boxWidth + (2 * _marginX);
		C4TextLine? title = null;
		if (_db.Title.Length > 0)
		{
			var font = new FontSpec((config.FontFamily ?? "").TrimEnd(';'), config.FontSize ?? 16);
			title = new C4TextLine(_db.Title, (boxWidth / 2) - (4 * _marginX), boxStartY + _marginY, 0, font, "");
		}

		var extra = title is null ? 0 : 60;
		var viewBox = new SvgViewBox(boxStartX - _marginX, -(_marginY + extra), width, height + extra);
		return new C4Layout(width, config.C4.UseMaxWidth == false ? height : null, viewBox, _drawings, rels, title, C4Styles.Rules(config));
	}

	/// <summary><c>getC4ShapeArray(parentBoundary)</c>.</summary>
	private List<C4DbItem> Shapes(C4Value? parent) => [.. _db.Shapes.Where(s => C4Value.Same(s["parentBoundary"], parent))];

	/// <summary><c>getBoundaries(parentBoundary)</c>.</summary>
	private List<C4DbItem> Boundaries(C4Value? parent) => [.. _db.Boundaries.Where(b => C4Value.Same(b["parentBoundary"], parent))];

	private void DrawInsideBoundary(Bounds parentBounds, List<C4DbItem> currentBoundaries)
	{
		var currentBounds = new Bounds { WidthLimit = parentBounds.WidthLimit / Math.Min(_db.BoundaryInRow, currentBoundaries.Count) };
		for (var i = 0; i < currentBoundaries.Count; i++)
		{
			var boundary = currentBoundaries[i];
			var y = 0.0;
			if (Truthy(boundary["sprite"]))
			{
				y = SpriteSize;
			}

			var labelFont = _boundaryFont with { Size = _boundaryFont.Size + 2, Weight = 700 };
			var label = Measure(boundary, "label", Text(boundary, "label") ?? "", _textWrap, labelFont, currentBounds.WidthLimit);
			label.Y = y + 8;
			y = label.Y + label.Height;

			if (Text(boundary, "type") is { Length: > 0 } type)
			{
				_types[boundary] = "[" + type + "]";
				var measured = Measure(boundary, "type", _types[boundary], _textWrap, _boundaryFont, currentBounds.WidthLimit);
				measured.Y = y + 5;
				y = measured.Y + measured.Height;
			}

			if (Text(boundary, "descr") is { Length: > 0 } descr)
			{
				var measured = Measure(boundary, "descr", descr, _textWrap, _boundaryFont with { Size = _boundaryFont.Size - 2 }, currentBounds.WidthLimit);
				measured.Y = y + 20;
				y = measured.Y + measured.Height;
			}

			if (i == 0 || i % _db.BoundaryInRow == 0)
			{
				var x = parentBounds.StartX + _marginX;
				var top = parentBounds.StopY + _marginY + y;
				currentBounds.SetData(x, x, top, top);
			}
			else
			{
				var x = currentBounds.StopX != currentBounds.StartX ? currentBounds.StopX + _marginX : currentBounds.StartX;
				var top = currentBounds.StartY;
				currentBounds.SetData(x, x, top, top);
			}

			var alias = boundary["alias"];
			var shapes = Shapes(alias);
			if (shapes.Count > 0)
			{
				DrawShapes(currentBounds, shapes);
			}

			var nested = Boundaries(alias);
			if (nested.Count > 0)
			{
				DrawInsideBoundary(currentBounds, nested);
			}

			if (boundary.StringOf("alias") != "global")
			{
				DrawBoundary(boundary, currentBounds);
			}

			parentBounds.StopY = Math.Max(currentBounds.StopY + _shapeMargin, parentBounds.StopY);
			parentBounds.StopX = Math.Max(currentBounds.StopX + _shapeMargin, parentBounds.StopX);
			_globalMaxX = Math.Max(_globalMaxX, parentBounds.StopX);
			_globalMaxY = Math.Max(_globalMaxY, parentBounds.StopY);
		}
	}

	/// <summary>
	/// <c>drawC4ShapeArray</c>: each shape measured at <c>c4.width</c>, placed with <c>Bounds.insert</c>, and drawn at its
	/// place.
	/// </summary>
	private void DrawShapes(Bounds bounds, List<C4DbItem> shapes)
	{
		var built = new List<(C4DbItem Item, Built Node)>(shapes.Count);
		foreach (var shape in shapes)
		{
			built.Add((shape, Build(shape)));
		}

		var places = new List<(double X, double Y)>(built.Count);
		foreach (var (_, node) in built)
		{
			places.Add(bounds.Insert(node.Width, node.Height, _shapeMargin, _db.ShapeInRow, _nextLinePaddingX));
		}

		for (var i = 0; i < built.Count; i++)
		{
			var (item, node) = built[i];
			var drawing = new C4ShapeDrawing
			{
				Alias = item.StringOf("alias") ?? "",
				Classes = node.Classes,
				X = places[i].X,
				Y = places[i].Y,
				Width = node.Width,
				Height = node.Height,
				Geometry = node.Geometry,
				Sections = node.Label.Sections,
				Fill = node.Fill,
				Stroke = node.Stroke,
				FontColor = node.FontColor,
			};
			_shapes[item] = drawing;
			_drawings.Add(drawing);
		}

		bounds.BumpLastMargin(_shapeMargin);
	}

	/// <summary><c>buildC4Node</c> and the shape it names, measured.</summary>
	private Built Build(C4DbItem shape)
	{
		var type = Text(shape, "typeC4Shape") ?? "";
		var external = type.StartsWith("external_", StringComparison.Ordinal);
		var classes = "node c4-shape c4-" + type + (external ? " c4-external" : "");
		var shapeName = ResolveShape(shape, type);
		var known = ElementTypes.Contains(type);
		var fill = shape["bgColor"] is { } bg ? bg.Text : known ? ConfigString(type + "_bg_color") : null;
		var stroke = shape["borderColor"] is { } border ? border.Text : known ? ConfigString(type + "_border_color") : null;
		var fontColor = shape["fontColor"]?.Text ?? "#FFFFFF";

		var elementFont = known ? ConfigFont(type, (_config.FontFamily ?? "").TrimEnd(';'), _config.FontSize ?? 16) : RootFont();
		var sections = new List<C4SectionText>(3);
		void Add(string cls, string? text, FontSpec sectionFont)
		{
			if (!string.IsNullOrEmpty(text))
			{
				sections.Add(new C4SectionText(cls, text, sectionFont));
			}
		}

		// A named argument in the label's place makes the label's text an object, which c4LabelHelper reads as no name.
		Add("c4-name", shape["label"] is { Argument: not null } ? null : Text(shape, "label"), elementFont with { Weight = 700 });
		Add("c4-type", Stereotype(type, Text(shape, "techn")), elementFont with { Size = elementFont.Size * 0.75 });
		Add("c4-descr", Text(shape, "descr"), elementFont with { Size = elementFont.Size * 0.82 });

		var label = new C4ShapeLabel(_measurer, sections, _shapePadding, _c4Wrap, _config.Flowchart.WrappingWidth ?? 200);
		var node = new ShapeNode { Label = label, Look = _look, Padding = _shapePadding, Width = _elementWidth, HtmlLabels = false };
		var geometry = NodeShapes.Find(shapeName)!.Draw(node);
		if (shapeName == "rounded")
		{
			// The inline rx and ry of 12px win over the rounded rect's own corner radius.
			geometry = new ShapeGeometry
			{
				Width = geometry.Width,
				Height = geometry.Height,
				Bounds = geometry.Bounds,
				Elements = [.. geometry.Elements.Select(e => e.Kind == ShapeElementKind.Rect ? e with { Rx = 12, Ry = 12 } : e)],
				Label = geometry.Label,
				LabelTranslate = geometry.LabelTranslate,
				Intersect = geometry.Intersect,
				Picture = geometry.Picture,
			};
		}

		var outline = ShapeBounds.None;
		foreach (var element in geometry.Elements)
		{
			outline = outline.Union(element.Bounds().Offset(element.Translate));
		}

		var width = outline.IsEmpty ? _elementWidth : outline.Width;
		var height = outline.IsEmpty ? _config.C4.Height ?? 60 : outline.Height;
		return new Built(classes, geometry, label, width, height, Paint(fill), Paint(stroke), fontColor);
	}

	/// <summary><c>resolveNodeShape</c>: <c>$shape</c>, then <c>$sprite</c>, then the first <c>$tags</c> keyword, then the type.</summary>
	private static string ResolveShape(C4DbItem shape, string type)
	{
		if ((Keyword(shape.StringOf("shape")) ?? Keyword(shape.StringOf("sprite"))) is { } explicitShape)
		{
			return explicitShape;
		}

		if (shape.StringOf("tags") is { Length: > 0 } tags)
		{
			foreach (var tag in tags.Split(','))
			{
				if (Keyword(tag.Trim()) is { } tagged)
				{
					return tagged;
				}
			}
		}

		return type is "person" or "external_person" ? "person"
			: DbShapes.Contains(type) ? "cylinder"
			: QueueShapes.Contains(type) ? "h-cyl"
			: "rounded";
	}

	/// <summary>The Structurizr shape keywords of <c>SHAPE_KEYWORDS</c>, in any case.</summary>
	private static string? Keyword(string? value) => value?.ToLowerInvariant() switch
	{
		"person" => "person",
		"box" or "rounded" => "rounded",
		"cylinder" or "database" or "db" => "cylinder",
		"queue" or "pipe" => "h-cyl",
		"component" => "fr-rect",
		_ => null,
	};

	/// <summary><c>stereotypeText</c>: <c>[Software System]</c>, or <c>[Container: technology]</c> with a technology.</summary>
	private static string Stereotype(string type, string? techn)
	{
		var name = type.StartsWith("external_", StringComparison.Ordinal) ? type["external_".Length..] : type;
		if (name.EndsWith("_db", StringComparison.Ordinal))
		{
			name = name[..^3];
		}
		else if (name.EndsWith("_queue", StringComparison.Ordinal))
		{
			name = name[..^6];
		}

		var stereotype = name switch
		{
			"person" => "Person",
			"system" => "Software System",
			"container" => "Container",
			"component" => "Component",
			_ => name.Replace('_', ' '),
		};
		return string.IsNullOrEmpty(techn) ? "[" + stereotype + "]" : "[" + stereotype + ": " + techn + "]";
	}

	/// <summary>
	/// <c>drawBoundary</c>: the boundary takes the box its bounds reached, and <c>svgDraw.drawBoundary</c> draws its rect,
	/// then its label in bold 2px larger than the boundary font, its type and its description 2px smaller.
	/// </summary>
	private void DrawBoundary(C4DbItem boundary, Bounds bounds)
	{
		var x = bounds.StartX;
		var y = bounds.StartY;
		var width = bounds.StopX - x;
		var height = bounds.StopY - y;
		var labelFont = _boundaryFont with { Size = _boundaryFont.Size + 2, Weight = 700 };
		var labelText = Text(boundary, "label") ?? "";
		var label = Measure(boundary, "label", labelText, _textWrap, labelFont, MermaidText.CalculateTextWidth(labelText, labelFont, _measurer));
		var texts = new List<C4TextLine>();
		AddLines(texts, label.Text, x, y + label.Y, width, labelFont, DefaultLineColor, false);
		if (_types.TryGetValue(boundary, out _) && _measured.TryGetValue((boundary, "type"), out var type))
		{
			AddLines(texts, type.Text, x, y + type.Y, width, _boundaryFont, DefaultLineColor, false);
		}

		if (Text(boundary, "descr") is { Length: > 0 } && _measured.TryGetValue((boundary, "descr"), out var descr))
		{
			AddLines(texts, descr.Text, x, y + descr.Y, width, _boundaryFont with { Size = _boundaryFont.Size - 2 }, DefaultLineColor, false);
		}

		var drawing = new C4BoundaryDrawing
		{
			Alias = boundary.StringOf("alias") ?? "",
			X = x,
			Y = y,
			Width = width,
			Height = height,
			Fill = Paint(TruthyString(boundary, "bgColor")) ?? "none",
			Stroke = Paint(TruthyString(boundary, "borderColor")) ?? DefaultLineColor,
			Dashed = !Truthy(boundary["nodeType"]),
			Texts = texts,
		};
		_boundaries[boundary] = drawing;
		_drawings.Add(drawing);
	}

	/// <summary>
	/// <c>drawRels</c>: each relation's label, numbered in a <c>C4Dynamic</c> diagram, its technology and description are
	/// measured, and its ends are where the line between the centres of its two ends leaves each outline.
	/// <c>svgDraw.drawRels</c> then puts the label at the middle of the line, moved by the relation's offsets, and the
	/// technology 5 below the message font size under it.
	/// </summary>
	private List<C4RelDrawing> DrawRels()
	{
		var drawings = new List<C4RelDrawing>(_db.Rels.Count);
		var i = 0;
		foreach (var rel in _db.Rels)
		{
			i++;
			var labelText = Text(rel, "label") ?? "";
			if (_db.Kind == C4DiagramKind.Dynamic)
			{
				labelText = i.ToString(CultureInfo.InvariantCulture) + ": " + labelText;
			}

			var label = Measure(rel, "label", labelText, _textWrap, _messageFont, MermaidText.CalculateTextWidth(labelText, _messageFont, _measurer));
			Measured? techn = null;
			if (Text(rel, "techn") is { Length: > 0 } technText)
			{
				techn = Measure(rel, "techn", technText, _textWrap, _messageFont, MermaidText.CalculateTextWidth(technText, _messageFont, _measurer));
			}

			var from = _db.Shape(rel["from"] ?? default);
			var to = _db.Shape(rel["to"] ?? default);
			if (from is null || to is null)
			{
				throw Error($"C4 rel \"{rel.StringOf("from")}\" -> \"{rel.StringOf("to")}\" references an unknown shape or boundary");
			}

			var fromCenter = Center(from);
			var toCenter = Center(to);
			var start = Intersect(from, toCenter);
			var end = Intersect(to, fromCenter);

			var textColor = TruthyString(rel, "textColor") ?? DefaultLineColor;
			var offsetX = Offset(rel["offsetX"]);
			var offsetY = Offset(rel["offsetY"]);
			var x = Math.Min(start.X, end.X) + (Math.Abs(end.X - start.X) / 2) + offsetX;
			var y = Math.Min(start.Y, end.Y) + (Math.Abs(end.Y - start.Y) / 2) + offsetY;
			var texts = new List<C4TextLine>();
			AddLines(texts, label.Text, x, y, label.Width, _messageFont, textColor, false);
			if (techn is not null)
			{
				AddLines(texts, "[" + techn.Text + "]", x, y + _messageFont.Size + 5, Math.Max(label.Width, techn.Width), _messageFont, textColor, true);
			}

			var type = rel.StringOf("type");
			drawings.Add(new C4RelDrawing
			{
				From = rel.StringOf("from") ?? "",
				To = rel.StringOf("to") ?? "",
				Start = start,
				End = end,
				IsLine = i == 1,
				Stroke = Paint(TruthyString(rel, "lineColor")) ?? DefaultLineColor,
				ArrowEnd = type != "rel_b",
				ArrowStart = type is "birel" or "rel_b",
				Texts = texts,
			});
		}

		return drawings;
	}

	/// <summary>The centre of a shape's or a boundary's box, which <c>getIntersectPoints</c> aims at.</summary>
	private ShapePoint Center(C4DbItem item) =>
		_shapes.TryGetValue(item, out var shape) ? new ShapePoint(shape.CenterX, shape.CenterY)
		: _boundaries.TryGetValue(item, out var boundary) ? new ShapePoint(boundary.X + (boundary.Width / 2), boundary.Y + (boundary.Height / 2))
		: throw NoIntersect(item);

	private ShapePoint Intersect(C4DbItem item, ShapePoint point)
	{
		if (_shapes.TryGetValue(item, out var shape))
		{
			return shape.Geometry.Intersect.Find(new ShapePoint(shape.CenterX, shape.CenterY), shape.Width, shape.Height, point);
		}

		if (_boundaries.TryGetValue(item, out var boundary))
		{
			var center = new ShapePoint(boundary.X + (boundary.Width / 2), boundary.Y + (boundary.Height / 2));
			return ShapeIntersect.Rect.Find(center, boundary.Width, boundary.Height, point);
		}

		throw NoIntersect(item);
	}

	private static MermaidParseException NoIntersect(C4DbItem item) =>
		Error($"C4 element \"{item.StringOf("alias")}\" has no intersect function. Please report this to https://github.com/mermaid-js/mermaid/issues");

	private static MermaidParseException Error(string message) =>
		new(new MermaidDiagnostic(DiagnosticSeverity.Error, message, 0, 0));

	/// <summary><c>byTspan</c>: a text per line of <paramref name="content"/>, the lines a font size apart around the middle.</summary>
	private static void AddLines(List<C4TextLine> texts, string content, double x, double y, double width, FontSpec font, string fill, bool italic)
	{
		var lines = new List<string>();
		foreach (var line in MermaidText.SplitBreaks(content))
		{
			lines.Add(line.ToString());
		}

		for (var i = 0; i < lines.Count; i++)
		{
			var dy = (i * font.Size) - (font.Size * (lines.Count - 1) / 2);
			texts.Add(new C4TextLine(lines[i], x + (width / 2), y, dy, font, fill, italic));
		}
	}

	/// <summary>
	/// <c>calcC4ShapeTextWH</c>: a text keeps its first measure unless its width was 0. With wrapping it is wrapped at the
	/// limit with <c>wrapLabel</c> and is as wide as the limit, and otherwise it is as wide as its widest line and as tall
	/// as its lines together.
	/// </summary>
	private Measured Measure(C4DbItem item, string field, string text, bool wrap, FontSpec font, double limit)
	{
		var known = _measured.TryGetValue((item, field), out var old);
		if (known && old!.Width != 0 && !double.IsNaN(old.Width))
		{
			return old;
		}

		Measured measured;
		if (wrap)
		{
			var wrapped = MermaidText.WrapLabel(text, limit, font, _measurer);
			measured = new Measured(wrapped, limit, TextHeight(wrapped, font));
		}
		else
		{
			var width = 0.0;
			var height = 0.0;
			foreach (var line in MermaidText.SplitBreaks(text))
			{
				width = Math.Max(MermaidText.CalculateTextWidth(line, font, _measurer), width);
				height += TextHeight(line.ToString(), font);
			}

			measured = new Measured(text, width, height);
		}

		// The renderer measures into the text's own object, so a second measure keeps the place the first gave it.
		measured.Y = known ? old!.Y : 0;
		_measured[(item, field)] = measured;
		return measured;
	}

	/// <summary>
	/// <c>calculateTextHeight</c>: the heights of the lines of <c>getBBox</c> together, in the generic <c>sans-serif</c>
	/// family when it is larger in width, height and line height than the font's family. Chromium rounds a text's ascent
	/// and its descent to whole pixels each, which <see cref="MermaidText.CalculateTextDimensions"/> rounds as one sum
	/// until #242.
	/// </summary>
	private double TextHeight(string text, FontSpec font)
	{
		if (text.Length == 0)
		{
			return 0;
		}

		(double Width, double Height, double LineHeight) Dimensions(FontSpec spec)
		{
			double width = 0, height = 0, lineHeight = 0;
			foreach (var line in MermaidText.SplitBreaks(text))
			{
				var metrics = _measurer.Measure(line, spec);
				width = Math.Round(Math.Max(width, metrics.Width), MidpointRounding.AwayFromZero);
				var box = Math.Floor(metrics.Ascent + 0.5) + Math.Floor(metrics.Descent + 0.5);
				height += box;
				lineHeight = Math.Max(lineHeight, box);
			}

			return (width, height, lineHeight);
		}

		var sansSerif = Dimensions(font with { Family = "sans-serif" });
		var family = Dimensions(font);
		return sansSerif.Height > family.Height && sansSerif.Width > family.Width && sansSerif.LineHeight > family.LineHeight
			? sansSerif.Height
			: family.Height;
	}

	/// <summary>The <c>text</c> of a <c>{ text }</c> field, or <c>null</c> when the field is missing or not one.</summary>
	private static string? Text(C4DbItem item, string field) => item.TextOf(field);

	/// <summary>A plain field, or <c>null</c> when it is missing or empty, as <c>value ? value : fallback</c> reads it.</summary>
	private static string? TruthyString(C4DbItem item, string field) => Truthy(item[field]) ? item[field]!.Value.Text : null;

	/// <summary>Whether JavaScript reads the value as true: anything but <c>undefined</c>, <c>""</c>, 0 and <c>NaN</c>.</summary>
	private static bool Truthy(C4Value? value) => value is { } v && (v.Wrapped || v.Argument is not null || (v.IsNumber ? v.Num != 0 && !double.IsNaN(v.Num) : !string.IsNullOrEmpty(v.Str)));

	/// <summary><c>offset ? parseInt(String(offset)) : 0</c>. An offset that reads as no number is 0.</summary>
	private static double Offset(C4Value? value)
	{
		if (!Truthy(value))
		{
			return 0;
		}

		var number = C4Db.JsParseInt(value!.Value.Text);
		return double.IsNaN(number) ? 0 : number;
	}

	private static string? Paint(string? value) => value is not null && SvgValues.IsAllowedPaint(value) ? value : null;

	private string? ConfigString(string key) => _c4[key] is { Kind: MermaidValueKind.String } value ? value.String : null;

	private FontSpec RootFont() => new((_config.FontFamily ?? "").TrimEnd(';'), _config.FontSize ?? 16);

	/// <summary>The family, size and weight of the <c>c4</c> config keys that start with <paramref name="prefix"/>.</summary>
	private FontSpec ConfigFont(string prefix, string family, double size)
	{
		var familyValue = _c4[prefix + "FontFamily"];
		var sizeValue = _c4[prefix + "FontSize"];
		var weightValue = _c4[prefix + "FontWeight"];
		return new FontSpec(
			familyValue is { Kind: MermaidValueKind.String, String.Length: > 0 } f ? f.String : family,
			FontSize(sizeValue) ?? size,
			FontWeight(weightValue));
	}

	private static double? FontSize(MermaidValue? value)
	{
		if (value is { Kind: MermaidValueKind.Number })
		{
			return value.Number;
		}

		if (value is { Kind: MermaidValueKind.String })
		{
			var text = value.String.AsSpan().Trim();
			if (text.EndsWith("px", StringComparison.Ordinal))
			{
				text = text[..^2];
			}

			if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && number > 0)
			{
				return number;
			}
		}

		return null;
	}

	private static int FontWeight(MermaidValue? value)
	{
		if (value is { Kind: MermaidValueKind.Number })
		{
			return (int)value.Number;
		}

		if (value is { Kind: MermaidValueKind.String })
		{
			var text = value.String.Trim();
			if (text is "bold" or "bolder")
			{
				return 700;
			}

			if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
			{
				return number;
			}
		}

		return 400;
	}

	/// <summary>A measured text: the text as drawn, which wrapping changes, its size and where it sits in its boundary.</summary>
	private sealed class Measured(string text, double width, double height)
	{
		public string Text { get; } = text;

		public double Width { get; } = width;

		public double Height { get; } = height;

		/// <summary>The <c>Y</c> the renderer gives a boundary's text, below the boundary's top.</summary>
		public double Y { get; set; }
	}

	private sealed record Built(string Classes, ShapeGeometry Geometry, C4ShapeLabel Label, double Width, double Height, string? Fill, string? Stroke, string FontColor);

	/// <summary>
	/// <c>Bounds</c>: <c>data</c>, the box of what a boundary holds so far, and <c>nextData</c>, the row being filled.
	/// <c>setData</c> sets both but keeps the row's count, so a boundary's first row counts the shapes of the boundary
	/// before it in the same call.
	/// </summary>
	private sealed class Bounds
	{
		public double StartX { get; private set; }

		public double StopX { get; set; }

		public double StartY { get; private set; }

		public double StopY { get; set; }

		public double WidthLimit { get; set; }

		private double _nextStartX;
		private double _nextStopX;
		private double _nextStartY;
		private double _nextStopY;
		private int _count;

		public void SetData(double startX, double stopX, double startY, double stopY)
		{
			_nextStartX = StartX = startX;
			_nextStopX = StopX = stopX;
			_nextStartY = StartY = startY;
			_nextStopY = StopY = stopY;
		}

		/// <summary>
		/// <c>insert</c>: the shape goes right of the row's last, and starts a new row below the row when it would reach
		/// the width limit or the row has <paramref name="shapeInRow"/> shapes.
		/// </summary>
		public (double X, double Y) Insert(double width, double height, double margin, double shapeInRow, double nextLinePaddingX)
		{
			_count++;
			var startX = _nextStartX == _nextStopX ? _nextStopX + margin : _nextStopX + (margin * 2);
			var stopX = startX + width;
			var startY = _nextStartY + (margin * 2);
			var stopY = startY + height;
			if (startX >= WidthLimit || stopX >= WidthLimit || _count > shapeInRow)
			{
				startX = _nextStartX + margin + nextLinePaddingX;
				startY = _nextStopY + (margin * 2);
				_nextStopX = stopX = startX + width;
				_nextStartY = _nextStopY;
				_nextStopY = stopY = startY + height;
				_count = 1;
			}

			StartX = Math.Min(startX, StartX);
			StartY = Math.Min(startY, StartY);
			StopX = Math.Max(stopX, StopX);
			StopY = Math.Max(stopY, StopY);
			_nextStartX = Math.Min(startX, _nextStartX);
			_nextStartY = Math.Min(startY, _nextStartY);
			_nextStopX = Math.Max(stopX, _nextStopX);
			_nextStopY = Math.Max(stopY, _nextStopY);
			return (startX, startY);
		}

		public void BumpLastMargin(double margin)
		{
			StopX += margin;
			StopY += margin;
		}
	}
}
