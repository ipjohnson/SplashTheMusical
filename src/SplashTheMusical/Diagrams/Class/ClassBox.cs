// Upstream: packages/mermaid/src/rendering-util/rendering-elements/shapes/classBox.ts
// Upstream: packages/mermaid/src/diagrams/class/shapeUtil.ts
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Rendering.Shapes;

namespace SplashTheMusical.Diagrams.Class;

/// <summary>
/// What <c>classBox</c> draws for a class: its first annotation, its name, and its members and methods, each with the
/// style of its classifier.
/// </summary>
/// <param name="Padding"><c>class.padding</c>, or 12.</param>
/// <param name="HideEmptyMembersBox"><c>class.hideEmptyMembersBox</c>.</param>
internal sealed record ClassBoxContent(
	string? Annotation,
	string Label,
	IReadOnlyList<ClassBoxLine> Members,
	IReadOnlyList<ClassBoxLine> Methods,
	double Padding,
	bool HideEmptyMembersBox,
	bool UseGradient);

/// <summary>A member or method line: its text and whether its classifier makes it italic, as an abstract one is.</summary>
internal readonly record struct ClassBoxLine(string Text, bool Italic);

/// <summary>
/// <c>classBox</c> with <c>textHelper</c>: the annotation, the bold name and the members and methods, each through
/// <c>addText</c>, which does not wrap, stacked in groups whose boxes place the frame and its two dividers.
/// </summary>
internal static class ClassBox
{
	public static void Measure(FlowchartMeasure measure, FlowNode node, ClassBoxContent content)
	{
		var html = measure.NodeHtmlLabels;
		var padding = content.Padding;
		var gap = padding;
		var textPadding = html ? 0 : 3;
		var members = content.Members.Count;
		var methods = content.Methods.Count;
		var empty = members == 0 && methods == 0;

		// textHelper: each group's texts in the group's own space.
		var annotation = new TextGroup();
		if (content.Annotation is { } annotationText)
		{
			annotation.Add(Text(measure, $"«{annotationText}»", false, false), 0);
		}

		var label = new TextGroup();
		label.Add(Text(measure, content.Label, true, false), 0);

		var memberGroup = new TextGroup();
		var yOffset = 0.0;
		foreach (var member in content.Members)
		{
			yOffset += memberGroup.Add(Text(measure, member.Text, false, member.Italic), yOffset) + textPadding;
		}

		var membersHeight = memberGroup.Bounds.IsEmpty || memberGroup.Bounds.Height <= 0 ? gap / 2 : memberGroup.Bounds.Height;

		var methodGroup = new TextGroup();
		var methodsOffset = 0.0;
		foreach (var method in content.Methods)
		{
			methodsOffset += methodGroup.Add(Text(measure, method.Text, false, method.Italic), methodsOffset) + textPadding;
		}

		var annotationHeight = annotation.Height;
		var labelHeight = label.Height;
		var helperOffsets = new (TextGroup Group, double X, double Y)[]
		{
			(annotation, -annotation.Width / 2, 0),
			(label, -label.Width / 2, annotationHeight),
			(memberGroup, 0, annotationHeight + labelHeight + (gap * 2)),
			(methodGroup, 0, annotationHeight + labelHeight + (membersHeight != 0 ? membersHeight + (gap * 4) : gap * 2)),
		};
		var bbox = ShapeBounds.None;
		foreach (var (group, x, y) in helperOffsets)
		{
			if (!group.Bounds.IsEmpty)
			{
				bbox = bbox.Union(group.Bounds.Offset(new ShapePoint(x, y)));
			}
		}

		var bboxWidth = bbox.IsEmpty ? 0 : bbox.Width;
		var bboxHeight = bbox.IsEmpty ? 0 : bbox.Height;

		// classBox.
		var renderExtraBox = empty && !content.HideEmptyMembersBox;
		var w = bboxWidth;
		var h = bboxHeight;
		if (empty)
		{
			h += gap;
		}
		else if (members > 0 && methods == 0)
		{
			h += gap * 2;
		}

		var left = -w / 2;
		var top = -h / 2;
		var extraHeight = renderExtraBox ? padding * 2 : empty ? -padding : 0;
		var shift = renderExtraBox ? padding : empty ? -padding / 2 : 0;
		var rectX = left - padding;
		var rectY = top - padding - shift;
		var rectWidth = w + (2 * padding);
		var rectHeight = h + (2 * padding) + extraHeight;

		// The group heights classBox reads, less half the padding when it draws the extra box. JavaScript's "|| 0" keeps a
		// negative difference.
		var less = renderExtraBox ? padding / 2 : 0;
		var annotationGroupHeight = annotation.Height - less;
		var labelGroupHeight = label.Height - less;
		var membersGroupHeight = memberGroup.Height - less;

		var groups = new List<(TextGroup Group, double X, double Y)>(4);
		foreach (var (group, _, translateY) in helperOffsets)
		{
			// The annotation's translate has no y, which the regular expression does not read.
			var readY = ReferenceEquals(group, annotation) ? 0 : translateY;
			var newY = readY + top + padding - shift;
			if (ReferenceEquals(group, methodGroup))
			{
				newY = annotationGroupHeight + labelGroupHeight + Math.Max(membersGroupHeight, gap / 2) + top + (gap * 4) + padding;
			}

			if (empty && content.HideEmptyMembersBox)
			{
				newY = content.Annotation is not null ? readY - gap : readY;
			}

			if (!html)
			{
				newY -= 4;
			}

			var newX = ReferenceEquals(group, annotation) || ReferenceEquals(group, label) ? -group.Width / 2 : left;
			groups.Add((group, newX, newY));
		}

		var neoLine = measure.Look == "neo" && !content.UseGradient;
		var dividerClass = neoLine ? "divider neo-line" : "divider";
		var elements = new List<ShapeElement>
		{
			new() { Kind = ShapeElementKind.Rect, Class = "basic label-container outer-path", X = rectX, Y = rectY, Width = rectWidth, Height = rectHeight },
		};
		if (!empty || renderExtraBox)
		{
			var firstLineY = annotationGroupHeight + labelGroupHeight + top + padding;
			var secondLineY = annotationGroupHeight + labelGroupHeight + membersGroupHeight + top + (gap * 2) + padding;
			elements.Add(Divider(dividerClass, rectX, rectWidth, firstLineY));
			elements.Add(Divider(dividerClass, rectX, rectWidth, secondLineY));
		}

		var labels = new List<(FlowchartLabelBox Box, double X, double Y)>();
		foreach (var (group, x, y) in groups)
		{
			foreach (var (box, translateY) in group.Texts)
			{
				labels.Add((box, x, y + translateY));
			}
		}

		var bounds = new ShapeBounds(rectX, rectY, rectX + rectWidth, rectY + rectHeight);
		node.Geometry = new ShapeGeometry
		{
			Width = rectWidth,
			Height = rectHeight,
			Bounds = bounds,
			Elements = elements,
			Intersect = ShapeIntersect.Rect,
		};
		node.Labels = labels;
		node.Width = rectWidth;
		node.Height = rectHeight;
	}

	/// <summary>A divider line across the frame, its end 0.001 lower so a gradient stroke has a box to paint.</summary>
	private static ShapeElement Divider(string cls, double x, double width, double y) =>
		new() { Kind = ShapeElementKind.Line, Class = cls, X = x, Y = y, Width = x + width, Height = y + 0.001 };

	/// <summary>
	/// <c>addText</c>'s label: the text with the backslash before an SVG label's visibility left out, through
	/// <c>createText</c> at a width it does not reach, in bold for the name and italic for an abstract member.
	/// </summary>
	private static FlowchartLabelBox Text(FlowchartMeasure measure, string text, bool bold, bool italic)
	{
		var html = measure.NodeHtmlLabels;
		if (!html && text.StartsWith('\\'))
		{
			text = text[1..];
		}
		else if (html && text.Length > 1 && text[0] == '\\' && text[1] is '+' or '-' or '#' or '~')
		{
			// The db writes a visibility as \+, \-, \# or \~, and marked reads the backslash as an escape.
			text = text[1..];
		}

		var lines = html
			? FlowchartLabelText.Lines(text, FlowchartLabelType.Markdown, true, measure.MarkdownAutoWrap)
			: FlowchartLabels.SvgLines(text, FlowchartLabelType.Markdown);
		if (bold || italic)
		{
			for (var i = 0; i < lines.Count; i++)
			{
				lines[i] = [.. lines[i].Select(run => run with { Bold = run.Bold || bold, Italic = run.Italic || italic })];
			}
		}

		return html
			? FlowchartLabelText.Measure(lines, measure.Font, double.PositiveInfinity, 0, true)
			: FlowchartLabelText.MeasureSvg(lines, measure.Font, double.PositiveInfinity, 0, false);
	}

	/// <summary>A <c>g</c> of <c>textHelper</c>: its texts, each at <c>translate(0, -height / (2 * lines) + yOffset)</c>.</summary>
	private sealed class TextGroup
	{
		public List<(FlowchartLabelBox Box, double Y)> Texts { get; } = [];

		public ShapeBounds Bounds { get; private set; } = ShapeBounds.None;

		public double Width => Bounds.IsEmpty ? 0 : Bounds.Width;

		public double Height => Bounds.IsEmpty ? 0 : Bounds.Height;

		/// <summary>Adds the text at the offset and returns its height.</summary>
		public double Add(FlowchartLabelBox box, double yOffset)
		{
			var lines = Math.Max(1, box.Lines.Count);
			var y = (-box.Height / (2 * lines)) + yOffset;
			Texts.Add((box, y));
			if (box.Width > 0 || box.Height > 0)
			{
				var x = box.Html ? 0 : box.X;
				var top = y + (box.Html ? 0 : box.Y);
				Bounds = Bounds.Union(new ShapeBounds(x, top, x + box.Width, top + box.Height));
			}

			return box.Height;
		}
	}
}
