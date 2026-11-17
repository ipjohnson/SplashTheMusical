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
/// The elements carry mermaid.js's classes and ids. Until the flowchart stylesheet styles them, each is painted with
/// presentation attributes from the theme, which a stylesheet rule overrides.
/// </remarks>
internal static class FlowchartRenderer
{
	public static void Render(FlowchartLayout layout, SvgWriter svg, string svgId)
	{
		var style = layout.Style;
		svg.StartElement("g");
		WriteMarkers(svg, svgId, style);
		WriteRoot(svg, layout.Root, svgId, style);
		svg.EndElement();
		if (layout.Title is { } title)
		{
			svg.StartElement("text");
			svg.WriteAttribute("text-anchor", "middle");
			svg.WriteAttribute("x", title.X);
			svg.WriteAttribute("y", title.Y);
			svg.WriteAttribute("class", "flowchartTitleText");
			Paint(svg, "fill", style.TextColor);
			svg.WriteText(title.Text);
			svg.EndElement();
		}
	}

	private static void WriteMarkers(SvgWriter svg, string svgId, FlowchartStyle style)
	{
		foreach (var name in EdgeMarkers.SetsFor(MarkerDiagram.Flowchart, style.Look == "neo"))
		{
			if (EdgeMarkers.FindSet(name) is not { } set)
			{
				continue;
			}

			foreach (var marker in set.Markers)
			{
				if (marker.InDefs)
				{
					svg.StartElement("defs");
				}

				svg.StartElement("marker");
				svg.WriteAttribute("id", EdgeMarkers.Id(svgId, style.DiagramType, marker));
				if (marker.Class is { } cls)
				{
					svg.WriteAttribute("class", cls + " " + style.DiagramType);
				}

				WriteMarkerAttributes(svg, marker.Attributes, style);
				foreach (var child in marker.Children)
				{
					WriteMarkerElement(svg, child, style);
				}

				svg.EndElement();
				if (marker.InDefs)
				{
					svg.EndElement();
				}
			}
		}
	}

	private static void WriteMarkerElement(SvgWriter svg, MarkerElement element, FlowchartStyle style)
	{
		if (!SvgValues.WriterElements.Contains(element.Tag))
		{
			return;
		}

		svg.StartElement(element.Tag);
		WriteMarkerAttributes(svg, element.Attributes, style);
		foreach (var child in element.Children)
		{
			WriteMarkerElement(svg, child, style);
		}

		svg.EndElement();
	}

	private static void WriteMarkerAttributes(SvgWriter svg, IEnumerable<MarkerAttribute> attributes, FlowchartStyle style)
	{
		foreach (var attribute in attributes)
		{
			if (attribute.IsStyle)
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
		}
	}

	private static void WriteRoot(SvgWriter svg, FlowchartDrawing drawing, string svgId, FlowchartStyle style)
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
				svg.WriteAttribute("class", "edgeLabel");
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
			WriteNode(svg, node, svgId, style);
		}

		svg.EndElement();
		svg.EndElement();
	}

	private static void WriteCluster(SvgWriter svg, FlowchartClusterDrawing cluster, string svgId, FlowchartStyle style)
	{
		svg.StartElement("g");
		svg.WriteAttribute("class", cluster.Classes);
		svg.WriteAttribute("id", svgId + "-" + cluster.Id);
		svg.WriteAttribute("data-id", cluster.Id);
		svg.WriteAttribute("data-look", style.Look);
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

	private static void WriteEdge(SvgWriter svg, FlowchartEdgeDrawing edge, string svgId, FlowchartStyle style)
	{
		if (edge.PathData.Length == 0)
		{
			return;
		}

		svg.StartElement("path");
		svg.WriteAttribute("d", edge.PathData);
		svg.WriteAttribute("id", svgId + "-" + edge.Id);
		svg.WriteAttribute("class", edge.Classes);
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

	private static void WriteNode(SvgWriter svg, FlowchartNodeDrawing node, string svgId, FlowchartStyle style)
	{
		if (node.Subgraph is { } subgraph)
		{
			svg.StartElement("g");
			svg.WriteAttribute("class", "root");
			svg.WriteAttribute("id", svgId + "-" + node.DomId);
			svg.WriteAttribute("transform", Translate(node.X, node.Y));
			WriteRoot(svg, subgraph, svgId, style);
			svg.EndElement();
			return;
		}

		if (node.IsDummy || node.Geometry is not { } geometry)
		{
			return;
		}

		svg.StartElement("g");
		svg.WriteAttribute("class", node.Classes);
		svg.WriteAttribute("id", svgId + "-" + node.DomId);
		svg.WriteAttribute("data-id", node.Id);
		svg.WriteAttribute("data-look", style.Look);
		svg.WriteAttribute("transform", Translate(node.X, node.Y));
		foreach (var element in geometry.Elements)
		{
			WriteShapeElement(svg, element, style);
		}

		if (node.Label is { HasText: true } label)
		{
			svg.StartElement("g");
			svg.WriteAttribute("class", "label");
			svg.WriteAttribute("transform", Translate(geometry.LabelTranslate.X, geometry.LabelTranslate.Y));
			WriteText(svg, label, style, "nodeLabel");
			svg.EndElement();
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
