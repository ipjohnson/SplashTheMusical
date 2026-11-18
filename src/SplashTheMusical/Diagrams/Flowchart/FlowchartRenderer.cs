using System.Collections.Immutable;
using System.Globalization;
using SplashTheMusical.Layout.Markers;
using SplashTheMusical.Rendering;
using SplashTheMusical.Rendering.Shapes;

namespace SplashTheMusical.Diagrams.Flowchart;

/// <summary>
/// Writes a <see cref="FlowchartLayout"/> as mermaid.js's dagre renderer builds its DOM: the edge markers, then a
/// <c>g.root</c> of <c>g.clusters</c>, <c>g.edgePaths</c>, <c>g.edgeLabels</c> and <c>g.nodes</c>, with an extracted
/// subgraph's <c>g.root</c> inside <c>g.nodes</c>. Labels are SVG text, a <c>tspan</c> per run of each line, where
/// mermaid.js draws HTML labels in a <c>foreignObject</c>, which <see cref="SvgWriter"/> does not write.
/// </summary>
/// <remarks>
/// The elements carry mermaid.js's classes and ids, and the flowchart stylesheet paints them. Each is also painted with
/// presentation attributes from the theme, which the stylesheet overrides. A styled element also carries the class of its
/// <see cref="FlowchartLayout.ElementRules"/>, which stand in for the <c>style</c> attributes mermaid.js writes.
/// </remarks>
internal static class FlowchartRenderer
{
	public static void Render(FlowchartLayout layout, SvgWriter svg, string svgId, FlowchartPictures? pictures = null) => Render(layout.Root, layout.Title, layout.Style, svg, svgId, pictures);

	/// <summary>Writes the drawing of mermaid.js's dagre renderer for any diagram type it draws, such as a state diagram.</summary>
	public static void Render(FlowchartDrawing root, FlowchartTitle? title, FlowchartStyle style, SvgWriter svg, string svgId, FlowchartPictures? pictures = null)
	{
		svg.StartElement("g");
		WriteMarkers(svg, svgId, style, ColoredMarkers(root));
		WriteRoot(svg, root, svgId, style, pictures);
		svg.EndElement();
		WriteLookDefs(svg, svgId, style);
		if (title is not null)
		{
			svg.StartElement("text");
			svg.WriteAttribute("text-anchor", "middle");
			svg.WriteAttribute("x", title.X);
			svg.WriteAttribute("y", title.Y);
			svg.WriteAttribute("class", title.Class);
			Paint(svg, "fill", style.TextColor);
			svg.WriteText(title.Text);
			svg.EndElement();
		}
	}

	/// <summary>
	/// <c>insertLookDefs</c>: the drop shadows the looks' rules filter with, and the gradient they stroke with when the
	/// theme's <c>useGradient</c> is on.
	/// </summary>
	private static void WriteLookDefs(SvgWriter svg, string svgId, FlowchartStyle style)
	{
		var flood = style.Theme?.Contains("dark", StringComparison.Ordinal) == true ? "#FFFFFF" : "#000000";
		foreach (var (suffix, size, offset) in new[] { ("-drop-shadow", "130%", 4), ("-drop-shadow-small", "150%", 2) })
		{
			svg.StartElement("defs");
			svg.StartElement("filter");
			svg.WriteAttribute("id", svgId + suffix);
			svg.WriteAttribute("height", size);
			svg.WriteAttribute("width", size);
			svg.StartElement("feDropShadow");
			svg.WriteAttribute("dx", offset);
			svg.WriteAttribute("dy", offset);
			svg.WriteAttribute("stdDeviation", 0);
			svg.WriteAttribute("flood-opacity", "0.06");
			svg.WriteAttribute("flood-color", flood);
			svg.EndElement();
			svg.EndElement();
			svg.EndElement();
		}

		if (style.Gradient is not { } gradient)
		{
			return;
		}

		svg.StartElement("linearGradient");
		svg.WriteAttribute("id", svgId + "-gradient");
		svg.WriteAttribute("gradientUnits", "objectBoundingBox");
		svg.WriteAttribute("x1", "0%");
		svg.WriteAttribute("y1", "0%");
		svg.WriteAttribute("x2", "100%");
		svg.WriteAttribute("y2", "0%");
		foreach (var (offset, color) in new[] { ("0%", gradient.Start), ("100%", gradient.Stop) })
		{
			svg.StartElement("stop");
			svg.WriteAttribute("offset", offset);
			Paint(svg, "stop-color", color);
			svg.WriteAttribute("stop-opacity", 1);
			svg.EndElement();
		}

		svg.EndElement();
	}

	/// <summary>The marker copies the edges' stroke colours need, each once, in the order the edges are drawn.</summary>
	private static List<ColoredMarker> ColoredMarkers(FlowchartDrawing root)
	{
		var markers = new List<ColoredMarker>();
		var seen = new HashSet<string>(StringComparer.Ordinal);
		void Walk(FlowchartDrawing drawing)
		{
			foreach (var edge in drawing.Edges)
			{
				markers.AddRange(edge.ColoredMarkers.Where(m => seen.Add(m.Id)));
			}

			foreach (var node in drawing.Nodes)
			{
				if (node.Subgraph is { } subgraph)
				{
					Walk(subgraph);
				}
			}
		}

		Walk(root);
		return markers;
	}

	/// <summary>
	/// <c>insertMarkers</c>, then each copy <c>addEdgeMarker</c> appends after the markers: the marker with the copy's id,
	/// whose paths, circles and lines take the colour as their stroke, and as their fill for an arrow type that fills.
	/// </summary>
	private static void WriteMarkers(SvgWriter svg, string svgId, FlowchartStyle style, List<ColoredMarker> colored)
	{
		var definitions = new Dictionary<string, MarkerDefinition>(StringComparer.Ordinal);
		foreach (var name in EdgeMarkers.SetsFor(style.Markers, style.Look == "neo"))
		{
			if (EdgeMarkers.FindSet(name) is not { } set)
			{
				continue;
			}

			foreach (var marker in set.Markers)
			{
				var id = EdgeMarkers.Id(svgId, style.DiagramType, marker);
				definitions[id] = marker;
				WriteMarker(svg, marker, id, style, null);
			}
		}

		foreach (var copy in colored)
		{
			if (definitions.TryGetValue(copy.BaseId, out var marker))
			{
				WriteMarker(svg, marker, copy.Id, style, copy);
			}
		}
	}

	private static void WriteMarker(SvgWriter svg, MarkerDefinition marker, string id, FlowchartStyle style, ColoredMarker? color)
	{
		if (marker.InDefs)
		{
			svg.StartElement("defs");
		}

		svg.StartElement("marker");
		svg.WriteAttribute("id", id);
		if (marker.Class is { } cls)
		{
			svg.WriteAttribute("class", cls + " " + style.DiagramType);
		}

		WriteMarkerAttributes(svg, marker.Attributes, style, null);
		foreach (var child in marker.Children)
		{
			WriteMarkerElement(svg, child, style, color);
		}

		svg.EndElement();
		if (marker.InDefs)
		{
			svg.EndElement();
		}
	}

	private static void WriteMarkerElement(SvgWriter svg, MarkerElement element, FlowchartStyle style, ColoredMarker? color)
	{
		if (!SvgValues.WriterElements.Contains(element.Tag))
		{
			return;
		}

		svg.StartElement(element.Tag);
		WriteMarkerAttributes(svg, element.Attributes, style, element.Tag is "path" or "circle" or "line" ? color : null);
		foreach (var child in element.Children)
		{
			WriteMarkerElement(svg, child, style, color);
		}

		svg.EndElement();
	}

	/// <summary>
	/// The marker's inline styles as presentation attributes, then its attributes that no style overrides, then the
	/// colour of a copy, which replaces the stroke and fill attributes but not a style.
	/// </summary>
	private static void WriteMarkerAttributes(SvgWriter svg, ImmutableArray<MarkerAttribute> attributes, FlowchartStyle style, ColoredMarker? color)
	{
		var written = new HashSet<string>(StringComparer.Ordinal);
		foreach (var attribute in attributes.Where(a => a.IsStyle).Concat(attributes.Where(a => !a.IsStyle)))
		{
			if (written.Contains(attribute.Name)
				|| (!attribute.IsStyle && color is not null && (attribute.Name == "stroke" || (color.Fill && attribute.Name == "fill"))))
			{
				continue;
			}

			var value = attribute.Theme switch
			{
				MarkerThemeValue.MainBackground => style.NodeFill ?? attribute.Value,
				MarkerThemeValue.TransitionColor => style.LineColor ?? attribute.Value,
				_ => attribute.Value,
			};
			WriteChecked(svg, attribute.Name, value);
			written.Add(attribute.Name);
		}

		if (color is not null)
		{
			if (!written.Contains("stroke"))
			{
				Paint(svg, "stroke", color.Color);
			}

			if (color.Fill && !written.Contains("fill"))
			{
				Paint(svg, "fill", color.Color);
			}
		}
	}

	private static void WriteRoot(SvgWriter svg, FlowchartDrawing drawing, string svgId, FlowchartStyle style, FlowchartPictures? pictures)
	{
		svg.StartElement("g");
		svg.WriteAttribute("class", "root");

		svg.StartElement("g");
		svg.WriteAttribute("class", "clusters");
		foreach (var cluster in drawing.Clusters)
		{
			WriteCluster(svg, cluster, svgId, style);
		}

		svg.EndElement();

		svg.StartElement("g");
		svg.WriteAttribute("class", "edgePaths");
		foreach (var edge in drawing.Edges)
		{
			WriteEdge(svg, edge, svgId, style);
		}

		svg.EndElement();

		svg.StartElement("g");
		svg.WriteAttribute("class", "edgeLabels");
		foreach (var edge in drawing.Edges)
		{
			if (edge.Label is { HasText: true } label)
			{
				svg.StartElement("g");
				svg.WriteAttribute("class", WithStyleClass("edgeLabel", edge.LabelStyleClass));
				svg.WriteAttribute("transform", Translate(edge.LabelX, edge.LabelY));
				svg.StartElement("g");
				svg.WriteAttribute("class", "label");
				svg.WriteAttribute("data-id", edge.Id);
				svg.WriteAttribute("transform", Translate(-label.Width / 2, -label.Height / 2));
				WriteText(svg, label, style, "edgeLabel");
				svg.EndElement();
				svg.EndElement();
			}
		}

		svg.EndElement();

		svg.StartElement("g");
		svg.WriteAttribute("class", "nodes");
		foreach (var node in drawing.Nodes)
		{
			WriteNode(svg, node, svgId, style, pictures);
		}

		svg.EndElement();
		svg.EndElement();
	}

	private static void WriteCluster(SvgWriter svg, FlowchartClusterDrawing cluster, string svgId, FlowchartStyle style)
	{
		svg.StartElement("g");
		svg.WriteAttribute("class", WithStyleClass(cluster.Classes, cluster.StyleClass));
		svg.WriteAttribute("id", svgId + "-" + cluster.DomId);
		if (cluster.Shape is "rect" or "roundedWithTitle")
		{
			svg.WriteAttribute("data-id", cluster.Id);
		}

		if (cluster.Shape != "noteGroup")
		{
			svg.WriteAttribute("data-look", style.Look);
		}

		if (cluster.ColorId is { } colorId)
		{
			svg.WriteAttribute("data-color-id", colorId);
		}

		if (cluster.Shape != "rect")
		{
			WriteStateCluster(svg, cluster, style);
			svg.EndElement();
			return;
		}

		svg.StartElement("rect");
		svg.WriteAttribute("x", cluster.X);
		svg.WriteAttribute("y", cluster.Y);
		svg.WriteAttribute("width", cluster.Width);
		svg.WriteAttribute("height", cluster.Height);
		Paint(svg, "fill", style.ClusterFill);
		Paint(svg, "stroke", style.ClusterStroke);
		svg.EndElement();
		svg.StartElement("g");
		svg.WriteAttribute("class", "cluster-label");
		svg.WriteAttribute("transform", Translate(cluster.LabelX, cluster.LabelY));
		WriteText(svg, cluster.Label, style, "nodeLabel");
		svg.EndElement();
		svg.EndElement();
	}

	/// <summary>
	/// The state cluster shapes of <c>clusters.js</c>: <c>roundedWithTitle</c>'s outer frame, title and inner frame,
	/// <c>divider</c>'s frame, and <c>noteGroup</c>'s unfilled frame.
	/// </summary>
	private static void WriteStateCluster(SvgWriter svg, FlowchartClusterDrawing cluster, FlowchartStyle style)
	{
		if (cluster.Shape == "noteGroup")
		{
			svg.StartElement("rect");
			svg.WriteAttribute("x", cluster.X);
			svg.WriteAttribute("y", cluster.Y);
			svg.WriteAttribute("width", cluster.Width);
			svg.WriteAttribute("height", cluster.Height);
			svg.WriteAttribute("fill", "none");
			svg.EndElement();
			return;
		}

		svg.StartElement("g");
		svg.StartElement("rect");
		svg.WriteAttribute("class", cluster.Shape == "divider" ? "divider" : "outer");
		svg.WriteAttribute("x", cluster.X);
		svg.WriteAttribute("y", cluster.Y);
		svg.WriteAttribute("width", cluster.Width);
		svg.WriteAttribute("height", cluster.Height);
		svg.WriteAttribute("data-look", style.Look);
		Paint(svg, "fill", style.ClusterFill);
		Paint(svg, "stroke", style.NodeStroke);
		svg.EndElement();
		svg.EndElement();
		if (cluster.Shape == "divider")
		{
			return;
		}

		svg.StartElement("g");
		svg.WriteAttribute("class", "cluster-label");
		svg.WriteAttribute("transform", Translate(cluster.LabelX, cluster.LabelY));
		WriteText(svg, cluster.Label, style, "nodeLabel");
		svg.EndElement();
		if (cluster.Inner is { } inner)
		{
			svg.StartElement("rect");
			svg.WriteAttribute("class", "inner");
			svg.WriteAttribute("x", cluster.X);
			svg.WriteAttribute("y", inner.Y);
			svg.WriteAttribute("width", cluster.Width);
			svg.WriteAttribute("height", inner.Height);
			Paint(svg, "fill", style.NodeFill);
			Paint(svg, "stroke", style.NodeStroke);
			svg.EndElement();
		}
	}

	private static void WriteEdge(SvgWriter svg, FlowchartEdgeDrawing edge, string svgId, FlowchartStyle style)
	{
		if (edge.PathData.Length == 0)
		{
			return;
		}

		svg.StartElement("path");
		svg.WriteAttribute("d", edge.PathData);
		svg.WriteAttribute("id", svgId + "-" + edge.Id);
		svg.WriteAttribute("class", WithStyleClass(edge.Classes, edge.StyleClass));
		svg.WriteAttribute("fill", "none");
		if (edge.Invisible)
		{
			svg.WriteAttribute("stroke-width", 0);
		}
		else
		{
			Paint(svg, "stroke", style.LineColor);
			svg.WriteAttribute("stroke-width", edge.Thick ? 3.5 : 1);
			if (edge.Classes.Contains("edge-pattern-dotted", StringComparison.Ordinal))
			{
				svg.WriteAttribute("stroke-dasharray", "3");
			}
		}

		svg.WriteAttribute("data-edge", "true");
		svg.WriteAttribute("data-et", "edge");
		svg.WriteAttribute("data-id", edge.Id);
		svg.WriteAttribute("data-look", style.Look);
		if (edge.MarkerStart is { } start)
		{
			WriteChecked(svg, "marker-start", "url(#" + start + ")");
		}

		if (edge.MarkerEnd is { } end)
		{
			WriteChecked(svg, "marker-end", "url(#" + end + ")");
		}

		svg.EndElement();
	}

	private static void WriteNode(SvgWriter svg, FlowchartNodeDrawing node, string svgId, FlowchartStyle style, FlowchartPictures? pictures)
	{
		if (node.Subgraph is { } subgraph)
		{
			svg.StartElement("g");
			svg.WriteAttribute("class", "root");
			svg.WriteAttribute("id", svgId + "-" + node.DomId);
			svg.WriteAttribute("transform", Translate(node.X, node.Y));
			WriteRoot(svg, subgraph, svgId, style, pictures);
			svg.EndElement();
			return;
		}

		if (node.IsDummy || node.Geometry is not { } geometry)
		{
			return;
		}

		svg.StartElement("g");
		svg.WriteAttribute("class", WithStyleClass(node.Classes, node.StyleClass));
		svg.WriteAttribute("id", svgId + "-" + node.DomId);
		svg.WriteAttribute("data-id", node.Id);
		svg.WriteAttribute("data-look", style.Look);
		if (node.ColorId is { } colorId)
		{
			svg.WriteAttribute("data-color-id", colorId);
		}

		svg.WriteAttribute("transform", Translate(node.X, node.Y));
		foreach (var element in geometry.Elements)
		{
			WriteShapeElement(svg, element, style);
		}

		if (node.Labels is { } labels)
		{
			svg.StartElement("g");
			svg.WriteAttribute("class", "label");
			foreach (var (box, x, y) in labels)
			{
				svg.StartElement("g");
				svg.WriteAttribute("transform", Translate(x, y));
				WriteText(svg, box, style, "nodeLabel");
				svg.EndElement();
			}

			svg.EndElement();
		}
		else if (node.Label is { HasText: true } label)
		{
			svg.StartElement("g");
			svg.WriteAttribute("class", "label");
			svg.WriteAttribute("transform", Translate(geometry.LabelTranslate.X, geometry.LabelTranslate.Y));
			WriteText(svg, label, style, "nodeLabel");
			svg.EndElement();
		}

		if (geometry.Picture is { } picture)
		{
			pictures?.Write(svg, node, picture, style);
		}

		svg.EndElement();
	}

	private static void WriteShapeElement(SvgWriter svg, ShapeElement element, FlowchartStyle style)
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
			case ShapeElementKind.Text:
				svg.StartElement("text");
				svg.WriteAttribute("x", element.X);
				svg.WriteAttribute("y", element.Y);
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

		if (element.Kind is not ShapeElementKind.Group and not ShapeElementKind.Text)
		{
			Paint(svg, "fill", style.NodeFill);
			Paint(svg, "stroke", style.NodeStroke);
		}

		if (element.Attributes is { } attributes)
		{
			foreach (var (name, value) in attributes)
			{
				WriteChecked(svg, name, value);
			}
		}

		if (element.Kind == ShapeElementKind.Text && element.Text is { } text)
		{
			svg.WriteText(text);
		}

		foreach (var child in element.Children ?? [])
		{
			WriteShapeElement(svg, child, style);
		}

		svg.EndElement();
	}

	/// <summary>
	/// The label's lines as <c>tspan</c> elements, centred in its box, each line's baseline in the middle of its line box
	/// as a browser sets an HTML label's lines.
	/// </summary>
	private static void WriteText(SvgWriter svg, FlowchartLabelBox label, FlowchartStyle style, string cls)
	{
		if (!label.HasText)
		{
			return;
		}

		svg.StartElement("text");
		svg.WriteAttribute("class", cls);
		svg.WriteAttribute("text-anchor", "middle");
		svg.WriteAttribute("font-size", style.FontSize);
		Paint(svg, "fill", style.TextColor);

		// A classDef's ".class>*" rule strokes the label's group, which mermaid.js's HTML label ignores and SVG text would inherit.
		svg.WriteAttribute("stroke", "none");
		var baseline = (label.LineHeight / 2) + (style.FontSize * 0.35);
		for (var i = 0; i < label.Lines.Count; i++)
		{
			var line = label.Lines[i];
			for (var j = 0; j < line.Count; j++)
			{
				var run = line[j];
				svg.StartElement("tspan");
				if (j == 0)
				{
					svg.WriteAttribute("x", label.Width / 2);
					svg.WriteAttribute("y", (i * label.LineHeight) + baseline);
				}

				if (run.Bold)
				{
					svg.WriteAttribute("font-weight", "bold");
				}

				if (run.Italic)
				{
					svg.WriteAttribute("font-style", "italic");
				}

				svg.WriteText(run.Text);
				svg.EndElement();
			}
		}

		svg.EndElement();
	}

	private static string WithStyleClass(string classes, string? styleClass) => styleClass is null ? classes : classes + " " + styleClass;

	private static void Paint(SvgWriter svg, string name, string? value)
	{
		if (value is not null && SvgValues.IsAllowedPaint(value))
		{
			svg.WriteAttribute(name, value);
		}
	}

	/// <summary>Writes an attribute the writer allows with a value its grammar allows, and leaves out any other.</summary>
	private static void WriteChecked(SvgWriter svg, string name, string value)
	{
		if ((SvgValues.WriterAttributes.Contains(name) || SvgValues.IsDataOrAriaName(name)) && SvgValues.IsAllowedValue(name, value))
		{
			svg.WriteAttribute(name, value);
		}
	}

	private static string Translate(double x, double y) => "translate(" + N(x) + ", " + N(y) + ")";

	private static string N(double value) => JsNumber.ToString(value);

	private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
}
