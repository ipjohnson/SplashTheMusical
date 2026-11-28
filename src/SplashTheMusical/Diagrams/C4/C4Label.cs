// Upstream: packages/mermaid/src/rendering-util/rendering-elements/shapes/c4LabelHelper.ts
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Rendering.Shapes;
using SplashTheMusical.Text;

namespace SplashTheMusical.Diagrams.C4;

/// <summary>A section of an element's label before it is measured: its class, its text and the font CSS gives it.</summary>
internal sealed record C4SectionText(string Class, string Text, FontSpec Font);

/// <summary>
/// A measured section of an element's label: the SVG text <c>createText</c> makes of it, and the <c>translate</c> of its
/// <c>g</c> in the label's space.
/// </summary>
internal sealed record C4LabelSection(string Class, FlowchartLabelBox Box, FontSpec Font, double X, double Y);

/// <summary>
/// <c>c4LabelHelper</c>: the name, the stereotype and the description of an element, each an SVG text of its own,
/// stacked 3 apart and each centred on the widest. The sections wrap at the node's width less twice its padding, at
/// least 32, when <c>c4.wrap</c> is on.
/// </summary>
/// <remarks>
/// The label's <c>getBBox</c> starts at its origin, so its box has no offset. The helper places the label at minus half
/// its size, as <c>labelHelper</c> places an HTML label, so the box is given as an HTML one to the shapes, which then
/// centre it the same way.
/// </remarks>
internal sealed class C4ShapeLabel(ITextMeasurer measurer, IReadOnlyList<C4SectionText> sections, double padding, bool wrap, double wrappingWidth)
	: IShapeLabelMeasurer
{
	private const double SectionGap = 3;
	private const double MinWrapWidth = 32;

	/// <summary>The sections of the last measure, in the order they are drawn.</summary>
	public IReadOnlyList<C4LabelSection> Sections { get; private set; } = [];

	public ShapeLabel Measure(double? nodeWidth, bool ignoreMinWidth)
	{
		var wrapWidth = nodeWidth is { } width && width != 0 && !double.IsNaN(width)
			? Math.Max(width - (2 * padding), MinWrapWidth)
			: wrappingWidth;
		var limit = wrap ? wrapWidth : double.PositiveInfinity;
		var boxes = new List<(C4SectionText Section, FlowchartLabelBox Box)>(sections.Count);
		foreach (var section in sections)
		{
			var lines = FlowchartLabels.SvgLines(section.Text, FlowchartLabelType.Text);
			var font = new FlowchartFont(measurer, section.Font.Family, section.Font.Size);
			var box = FlowchartLabelText.MeasureSvg(lines, font, limit, 0, false);
			if (section.Font.Weight >= 600)
			{
				box = Bold(box, font);
			}

			boxes.Add((section, box));
		}

		var totalWidth = 0.0;
		foreach (var (_, box) in boxes)
		{
			totalWidth = Math.Max(totalWidth, box.Width);
		}

		var measured = new List<C4LabelSection>(boxes.Count);
		var y = 0.0;
		foreach (var (section, box) in boxes)
		{
			measured.Add(new C4LabelSection(section.Class, box, section.Font, (totalWidth / 2) - box.X - (box.Width / 2), y - box.Y));
			y += box.Height + SectionGap;
		}

		Sections = measured;
		var totalHeight = boxes.Count > 0 ? y - SectionGap : 0;
		return new ShapeLabel(totalWidth, totalHeight, Html: true);
	}

	/// <summary>
	/// The section's box once its words are bold. <c>createText</c> wraps the lines while each word's <c>tspan</c> has
	/// <c>font-weight="normal"</c>, and the helper drops the attribute before it measures the box, so the lines break as
	/// normal text and are as wide as bold text.
	/// </summary>
	private static FlowchartLabelBox Bold(FlowchartLabelBox box, FlowchartFont font)
	{
		var lines = new List<IReadOnlyList<LabelRun>>(box.Lines.Count);
		var width = 0.0;
		foreach (var line in box.Lines)
		{
			var bold = line.Select(run => run with { Bold = true }).ToList();
			var lineWidth = 0.0;
			foreach (var word in bold)
			{
				lineWidth += Math.Ceiling(font.Width(word) * 64) / 64;
			}

			width = Math.Max(width, lineWidth);
			lines.Add(bold);
		}

		return new FlowchartLabelBox(lines, width, box.Height, box.LineHeight, false) { X = -width / 2, Y = box.Y };
	}
}
