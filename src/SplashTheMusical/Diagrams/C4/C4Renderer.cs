// Upstream: packages/mermaid/src/diagrams/c4/c4Renderer.ts
// Upstream: packages/mermaid/src/diagrams/c4/svgDraw.ts
using System.Globalization;
using SplashTheMusical.Rendering;
using SplashTheMusical.Rendering.Shapes;
using SplashTheMusical.Text;

namespace SplashTheMusical.Diagrams.C4;

/// <summary>
/// Writes a <see cref="C4Layout"/> as <c>c4Renderer.ts</c> builds its DOM: each shape in a group translated to its centre
/// and each boundary in a group of its own, in the order they were laid out, then the markers, a group of the relations
/// and their labels, and the title.
/// </summary>
/// <remarks>
/// mermaid.js also defines a computer, a database and a clock <c>symbol</c>, which nothing uses, so STM leaves them out.
/// The inline styles of a shape are presentation attributes here, and the colour of its label is a rule of
/// <see cref="C4Layout.ElementRules"/>. mermaid.js draws no sprite or image, so neither does STM.
/// </remarks>
internal static class C4Renderer
{
	public static void Render(C4Layout layout, SvgWriter svg, string svgId)
	{
		ArgumentNullException.ThrowIfNull(layout);
		ArgumentNullException.ThrowIfNull(svg);
		foreach (var drawing in layout.Drawings)
		{
			switch (drawing)
			{
				case C4ShapeDrawing shape:
					WriteShape(svg, shape, svgId);
					break;
				case C4BoundaryDrawing boundary:
					WriteBoundary(svg, boundary);
					break;
			}
		}

		WriteMarkers(svg, svgId);
		svg.StartElement("g");
		foreach (var rel in layout.Rels)
		{
			WriteRel(svg, rel, svgId);
		}

		svg.EndElement();
		if (layout.Title is { } title)
		{
			svg.StartElement("text");
			svg.WriteAttribute("x", title.X);
			svg.WriteAttribute("y", title.Y);
			svg.WriteText(title.Text);
			svg.EndElement();
		}
	}

	private static void WriteShape(SvgWriter svg, C4ShapeDrawing shape, string svgId)
	{
		svg.StartElement("g");
		svg.WriteAttribute("transform", Translate(shape.CenterX, shape.CenterY));
		svg.StartElement("g");
		svg.WriteAttribute("class", shape.StyleClass is null ? shape.Classes : shape.Classes + " " + shape.StyleClass);
		svg.WriteAttribute("id", svgId + "-" + shape.Alias);
		foreach (var element in shape.Geometry.Elements)
		{
			WriteElement(svg, element, shape);
		}

		svg.StartElement("g");
		svg.WriteAttribute("class", "label");
		svg.WriteAttribute("transform", Translate(shape.Geometry.LabelTranslate.X, shape.Geometry.LabelTranslate.Y));
		foreach (var section in shape.Sections)
		{
			WriteSection(svg, section, shape.FontColor);
		}

		svg.EndElement();
		svg.EndElement();
		svg.EndElement();
	}

	/// <summary>
	/// A section of <c>c4LabelHelper</c>: <c>createText</c>'s SVG text, one outer <c>tspan</c> per line, 1.1em apart and
	/// centred, holding a <c>tspan</c> per word.
	/// </summary>
	private static void WriteSection(SvgWriter svg, C4LabelSection section, string fontColor)
	{
		svg.StartElement("g");
		svg.WriteAttribute("class", section.Class);
		svg.WriteAttribute("transform", Translate(section.X, section.Y));
		svg.StartElement("g");
		svg.StartElement("text");
		WriteFont(svg, section.Font);
		Paint(svg, "fill", fontColor);
		var size = section.Font.Size;
		for (var i = 0; i < section.Box.Lines.Count; i++)
		{
			var line = section.Box.Lines[i];
			if (line.Count == 0)
			{
				continue;
			}

			svg.StartElement("tspan");
			svg.WriteAttribute("class", "text-outer-tspan");
			svg.WriteAttribute("x", 0);
			svg.WriteAttribute("y", ((i * 1.1) - 0.1) * size);
			svg.WriteAttribute("dy", 1.1 * size);
			svg.WriteAttribute("text-anchor", "middle");
			foreach (var word in line)
			{
				svg.StartElement("tspan");
				svg.WriteAttribute("class", "text-inner-tspan");
				svg.WriteText(word.Text);
				svg.EndElement();
			}

			svg.EndElement();
		}

		svg.EndElement();
		svg.EndElement();
		svg.EndElement();
	}

	/// <summary>An element of the shape's outline, with the shape's inline fill and stroke.</summary>
	private static void WriteElement(SvgWriter svg, ShapeElement element, C4ShapeDrawing shape)
	{
		switch (element.Kind)
		{
			case ShapeElementKind.Rect:
				svg.StartElement("rect");
				svg.WriteAttribute("x", element.X);
				svg.WriteAttribute("y", element.Y);
				svg.WriteAttribute("width", element.Width);
				svg.WriteAttribute("height", element.Height);
				if (element.Rx != 0)
				{
					svg.WriteAttribute("rx", element.Rx);
					svg.WriteAttribute("ry", element.Ry);
				}

				break;
			case ShapeElementKind.Polygon:
				svg.StartElement("polygon");
				svg.WriteAttribute("points", string.Join(' ', element.Points!.Select(p => N(p.X) + "," + N(p.Y))));
				break;
			case ShapeElementKind.Path:
				svg.StartElement("path");
				svg.WriteAttribute("d", element.PathData!);
				break;
			case ShapeElementKind.Circle:
				svg.StartElement("circle");
				svg.WriteAttribute("cx", element.X);
				svg.WriteAttribute("cy", element.Y);
				svg.WriteAttribute("r", element.Rx);
				break;
			case ShapeElementKind.Ellipse:
				svg.StartElement("ellipse");
				svg.WriteAttribute("cx", element.X);
				svg.WriteAttribute("cy", element.Y);
				svg.WriteAttribute("rx", element.Rx);
				svg.WriteAttribute("ry", element.Ry);
				break;
			case ShapeElementKind.Line:
				svg.StartElement("line");
				svg.WriteAttribute("x1", element.X);
				svg.WriteAttribute("y1", element.Y);
				svg.WriteAttribute("x2", element.Width);
				svg.WriteAttribute("y2", element.Height);
				break;
			case ShapeElementKind.Group:
				svg.StartElement("g");
				break;
			default:
				return;
		}

		if (element.Class is { } cls)
		{
			svg.WriteAttribute("class", cls);
		}

		if (element.Translate is { } translate)
		{
			svg.WriteAttribute("transform", Translate(translate.X, translate.Y));
		}

		if (element.Kind != ShapeElementKind.Group)
		{
			Paint(svg, "fill", shape.Fill);
			Paint(svg, "stroke", shape.Stroke);
		}

		foreach (var (name, value) in element.Attributes ?? [])
		{
			if ((SvgValues.WriterAttributes.Contains(name) || SvgValues.IsDataOrAriaName(name)) && SvgValues.IsAllowedValue(name, value))
			{
				svg.WriteAttribute(name, value);
			}
		}

		foreach (var child in element.Children ?? [])
		{
			WriteElement(svg, child, shape);
		}

		svg.EndElement();
	}

	/// <summary><c>svgDraw.drawBoundary</c>: a rect with corners of 2.5, dashed unless it is a deployment node, and its texts.</summary>
	private static void WriteBoundary(SvgWriter svg, C4BoundaryDrawing boundary)
	{
		svg.StartElement("g");
		svg.StartElement("rect");
		svg.WriteAttribute("x", boundary.X);
		svg.WriteAttribute("y", boundary.Y);
		Paint(svg, "fill", boundary.Fill);
		Paint(svg, "stroke", boundary.Stroke);
		svg.WriteAttribute("width", boundary.Width);
		svg.WriteAttribute("height", boundary.Height);
		svg.WriteAttribute("rx", 2.5);
		svg.WriteAttribute("ry", 2.5);
		svg.WriteAttribute("stroke-width", 1);
		if (boundary.Dashed)
		{
			svg.WriteAttribute("stroke-dasharray", "7.0,7.0");
		}

		svg.EndElement();
		foreach (var text in boundary.Texts)
		{
			WriteLine(svg, text);
		}

		svg.EndElement();
	}

	/// <summary>
	/// <c>svgDraw.drawRels</c>: the first relation as a <c>line</c> and the others as a quadratic <c>path</c>, each followed
	/// by its texts.
	/// </summary>
	private static void WriteRel(SvgWriter svg, C4RelDrawing rel, string svgId)
	{
		if (rel.IsLine)
		{
			svg.StartElement("line");
			svg.WriteAttribute("x1", rel.Start.X);
			svg.WriteAttribute("y1", rel.Start.Y);
			svg.WriteAttribute("x2", rel.End.X);
			svg.WriteAttribute("y2", rel.End.Y);
			svg.WriteAttribute("stroke-width", "1");
			Paint(svg, "stroke", rel.Stroke);
			svg.WriteAttribute("fill", "none");
		}
		else
		{
			svg.StartElement("path");
			svg.WriteAttribute("fill", "none");
			svg.WriteAttribute("stroke-width", "1");
			Paint(svg, "stroke", rel.Stroke);
			svg.WriteAttribute("d", rel.PathData);
		}

		if (rel.ArrowEnd)
		{
			svg.WriteAttribute("marker-end", "url(#" + svgId + "-arrowhead)");
		}

		if (rel.ArrowStart)
		{
			svg.WriteAttribute("marker-start", "url(#" + svgId + "-arrowend)");
		}

		svg.EndElement();
		foreach (var text in rel.Texts)
		{
			WriteLine(svg, text);
		}
	}

	/// <summary><c>byTspan</c>'s text of one line.</summary>
	private static void WriteLine(SvgWriter svg, C4TextLine line)
	{
		svg.StartElement("text");
		svg.WriteAttribute("x", line.X);
		svg.WriteAttribute("y", line.Y);
		svg.WriteAttribute("text-anchor", "middle");
		svg.WriteAttribute("dominant-baseline", "middle");
		WriteFont(svg, line.Font);
		Paint(svg, "fill", line.Fill);
		if (line.Italic)
		{
			svg.WriteAttribute("font-style", "italic");
		}

		svg.StartElement("tspan");
		svg.WriteAttribute("dy", line.Dy);
		svg.WriteAttribute("alignment-baseline", "mathematical");
		svg.WriteText(line.Text);
		svg.EndElement();
		svg.EndElement();
	}

	private static void WriteFont(SvgWriter svg, FontSpec font)
	{
		svg.WriteAttribute("font-size", font.Size);
		svg.WriteAttribute("font-weight", font.Weight switch
		{
			700 => "bold",
			400 => "normal",
			_ => font.Weight.ToString(CultureInfo.InvariantCulture),
		});
		if (font.Family.Length > 0)
		{
			svg.WriteAttribute("font-family", font.Family);
		}
	}

	/// <summary>
	/// <c>insertArrowHead</c>, <c>insertArrowEnd</c>, <c>insertArrowCrossHead</c> and <c>insertArrowFilledHead</c>, each in
	/// a <c>defs</c> of its own.
	/// </summary>
	private static void WriteMarkers(SvgWriter svg, string svgId)
	{
		void Marker(string suffix, (string Name, string Value)[] attributes, params (string Name, string Value)[][] paths)
		{
			svg.StartElement("defs");
			svg.StartElement("marker");
			svg.WriteAttribute("id", svgId + suffix);
			foreach (var (name, value) in attributes)
			{
				svg.WriteAttribute(name, value);
			}

			foreach (var path in paths)
			{
				svg.StartElement("path");
				foreach (var (name, value) in path)
				{
					svg.WriteAttribute(name, value);
				}

				svg.EndElement();
			}

			svg.EndElement();
			svg.EndElement();
		}

		(string, string)[] userSpace = [("markerUnits", "userSpaceOnUse"), ("markerWidth", "12"), ("markerHeight", "12"), ("orient", "auto")];
		Marker("-arrowhead", [("refX", "9"), ("refY", "5"), .. userSpace], [("d", "M 0 0 L 10 5 L 0 10 z")]);
		Marker("-arrowend", [("refX", "1"), ("refY", "5"), .. userSpace], [("d", "M 10 0 L 0 5 L 10 10 z")]);
		Marker(
			"-crosshead",
			[("markerWidth", "15"), ("markerHeight", "8"), ("orient", "auto"), ("refX", "16"), ("refY", "4")],
			[("fill", "black"), ("stroke", "#000000"), ("stroke-dasharray", "0, 0"), ("stroke-width", "1px"), ("d", "M 9,2 V 6 L16,4 Z")],
			[("fill", "none"), ("stroke", "#000000"), ("stroke-dasharray", "0, 0"), ("stroke-width", "1px"), ("d", "M 0,1 L 6,7 M 6,1 L 0,7")]);
		Marker("-filled-head", [("refX", "18"), ("refY", "7"), ("markerWidth", "20"), ("markerHeight", "28"), ("orient", "auto")], [("d", "M 18,7 L9,13 L14,7 L9,1 Z")]);
	}

	private static void Paint(SvgWriter svg, string name, string? value)
	{
		if (value is not null && SvgValues.IsAllowedPaint(value))
		{
			svg.WriteAttribute(name, value);
		}
	}

	private static string Translate(double x, double y) => "translate(" + N(x) + ", " + N(y) + ")";

	private static string N(double value) => JsNumber.ToString(value);
}
