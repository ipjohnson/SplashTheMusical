using System.Text;
using SplashTheMusical.Rendering;
using SplashTheMusical.Writing;

namespace SplashTheMusical.Diagrams.Info;

/// <summary>
/// The <c>info</c> diagram of <c>packages/mermaid/src/diagrams/info</c>: the detector <c>/^\s*info/</c>, the parser of
/// <see cref="InfoParser"/>, and <c>infoRenderer.ts</c>, which draws <c>v</c> and the version of mermaid.js.
/// </summary>
public sealed class InfoModule : DiagramModule<InfoDocument, InfoLayout>
{
	public override string DiagramType => "info";

	public override bool Detect(ReadOnlySpan<char> text) => text.StartsWith("info", StringComparison.Ordinal);

	public override InfoDocument? Parse(ReadOnlySpan<char> source, DiagramParseContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return InfoParser.Parse(source, context);
	}

	public override void Write(InfoDocument document, StringBuilder output)
	{
		ArgumentNullException.ThrowIfNull(document);
		ArgumentNullException.ThrowIfNull(output);
		output.Append("info\n");
		if (document.ShowInfo)
		{
			output.Append("showInfo\n");
		}

		if (document.DiagramTitle is { } title)
		{
			output.Append(title.Length > 0 ? "title " : "title").Append(title).Append('\n');
		}

		PreambleWriter.WriteAccessibility(output, document, "");
	}

	/// <summary>
	/// <c>configureSvgSize(svg, 100, 400, true)</c> gives the root <c>width="100%"</c> and <c>max-width: 400px</c>.
	/// <see cref="SvgWriter"/> writes no <c>style</c> attribute, so the width is 400, and the height is left to the
	/// browser, as mermaid.js leaves it.
	/// </summary>
	public override InfoLayout Layout(InfoDocument document, DiagramRenderContext context) =>
		new("v" + MermaidCompatibility.MermaidJsVersion);

	public override void Render(InfoDocument document, InfoLayout layout, SvgWriter svg, DiagramRenderContext context)
	{
		ArgumentNullException.ThrowIfNull(layout);
		ArgumentNullException.ThrowIfNull(svg);
		svg.StartElement("g");
		svg.StartElement("text");
		svg.WriteAttribute("x", layout.X);
		svg.WriteAttribute("y", layout.Y);
		svg.WriteAttribute("class", "version");
		svg.WriteAttribute("font-size", layout.FontSize);
		svg.WriteAttribute("text-anchor", "middle");
		svg.WriteText(layout.Text);
		svg.EndElement();
		svg.EndElement();
	}
}

/// <summary>The place of the version text of <c>infoRenderer.ts</c>.</summary>
public sealed class InfoLayout(string text) : DiagramLayout(400)
{
	public string Text { get; } = text;

	/// <summary>The x of the text's middle.</summary>
	public double X => 100;

	/// <summary>The y of the text's baseline.</summary>
	public double Y => 40;

	public double FontSize => 32;
}
