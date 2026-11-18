using SplashTheMusical.Icons;
using SplashTheMusical.Rendering;
using SplashTheMusical.Rendering.Shapes;

namespace SplashTheMusical.Diagrams.Flowchart;

// Upstream: packages/mermaid/src/rendering-util/rendering-elements/shapes/{icon,iconCircle,iconRounded,iconSquare,
// imageSquare}.ts and packages/mermaid/src/rendering-util/icons.ts

/// <summary>
/// Draws the icon of an icon shape and the image of an image shape, for one render. An icon is its sanitized content
/// in a nested <c>svg</c> of the icon's view box, coloured through <c>currentColor</c> by the node's stroke or the
/// theme's <c>nodeBorder</c>, as <c>getIconSVG</c> and the shapes draw it. An icon no pack has draws
/// <see cref="MermaidIcons.Unknown"/> and gives a warning. An image is drawn when it is a <c>data:</c> URI the
/// sanitizer keeps, or a remote URL the host allows. STM never fetches it, and any other image gives a warning.
/// </summary>
internal sealed class FlowchartPictures(DiagramRenderContext context)
{
	private int _icons;

	public void Write(SvgWriter svg, FlowchartNodeDrawing node, ShapeBounds picture, FlowchartStyle style)
	{
		if (node.Icon is { } name)
		{
			WriteIcon(svg, name, node, picture, style);
		}
		else if (node.Image is { } image)
		{
			WriteImage(svg, image, node, picture);
		}
	}

	private void WriteIcon(SvgWriter svg, string name, FlowchartNodeDrawing node, ShapeBounds picture, FlowchartStyle style)
	{
		var icon = context.Icons.GetIcon(name);
		if (icon is null)
		{
			context.Report(new MermaidDiagnostic(
				DiagnosticSeverity.Warning,
				$"The icon '{name}' of node '{node.Id}' is in no registered icon pack, so it is drawn as mermaid's unknown icon.",
				0,
				0));
			icon = MermaidIcons.Unknown;
		}

		var idPrefix = context.SvgId + "-icon" + _icons++.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-";
		var (left, top, width, height, transform) = icon.Drawing();
		svg.StartElement("g");
		svg.WriteAttribute("class", "icon");
		svg.WriteAttribute("transform", "translate(" + N(picture.MinX) + ", " + N(picture.MinY) + ")");
		var color = node.IconColor is { } stroke && SvgValues.IsAllowedPaint(stroke) ? stroke : style.NodeStroke;
		if (color is not null && SvgValues.IsAllowedPaint(color))
		{
			svg.WriteAttribute("color", color);
		}

		svg.StartElement("svg");
		svg.WriteAttribute("width", picture.Width);
		svg.WriteAttribute("height", picture.Height);
		svg.WriteAttribute("viewBox", N(left) + " " + N(top) + " " + N(width) + " " + N(height));
		if (transform is not null)
		{
			svg.StartElement("g");
			svg.WriteAttribute("transform", transform);
		}

		foreach (var element in icon.Content)
		{
			WriteContent(svg, element, idPrefix);
		}

		if (transform is not null)
		{
			svg.EndElement();
		}

		svg.EndElement();
		svg.EndElement();
	}

	/// <summary>Writes a sanitized element, with its ids and references prefixed so that two icons cannot share an id.</summary>
	private static void WriteContent(SvgWriter svg, IconElement element, string idPrefix)
	{
		if (element.Name is null)
		{
			svg.WriteText(element.Text);
			return;
		}

		if (!SvgValues.WriterElements.Contains(element.Name) || element.Name == "svg")
		{
			return;
		}

		svg.StartElement(element.Name);
		foreach (var attribute in element.Attributes)
		{
			var value = !attribute.IsIdReference ? attribute.Value
				: attribute.Name == "id" ? idPrefix + attribute.Value
				: "url(#" + idPrefix + attribute.Value + ")";
			if ((SvgValues.WriterAttributes.Contains(attribute.Name) || SvgValues.IsDataOrAriaName(attribute.Name)) && SvgValues.IsAllowedValue(attribute.Name, value))
			{
				svg.WriteAttribute(attribute.Name, value);
			}
		}

		foreach (var child in element.Children)
		{
			WriteContent(svg, child, idPrefix);
		}

		svg.EndElement();
	}

	private void WriteImage(SvgWriter svg, string image, FlowchartNodeDrawing node, ShapeBounds picture)
	{
		if (SvgWriter.IsDrawableImage(image, context.AllowRemoteImages))
		{
			svg.WriteImage(image, context.AllowRemoteImages, picture.Width, picture.Height, "translate(" + N(picture.MinX) + ", " + N(picture.MinY) + ")");
			return;
		}

		var reason = SvgWriter.IsRemoteImage(image)
			? "remote images are off. Set AllowRemoteImages to draw it; STM does not fetch it"
			: "it is neither a base64 data: PNG or SVG image nor an http or https URL";
		context.Report(new MermaidDiagnostic(DiagnosticSeverity.Warning, $"The image '{image}' of node '{node.Id}' is not drawn, because {reason}.", 0, 0));
	}

	private static string N(double value) => JsNumber.ToString(value);
}
