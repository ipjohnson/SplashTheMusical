using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Text;

namespace SplashTheMusical.Tests.Diagrams;

/// <summary>
/// SVG labels (<c>htmlLabels: false</c>) as <c>createText</c> lays them out and mermaid.js measures them. The expected
/// sizes and text are the oracle's for <c>e2e-flowchart-visual-check-5a-…</c> and
/// <c>e2e-flowchart-visual-check-stadium-tall-labels</c>, whose redux-color theme sets 14px text.
/// </summary>
public class FlowchartSvgLabelTests
{
	private static readonly FlowchartFont Trebuchet = new(TextMeasurer.Default, "\"trebuchet ms\", verdana, arial, sans-serif", 14);

	[Test]
	[Arguments("first", 26.5625)]
	[Arguments("second", 49.15625)]
	public void AnEdgeLabelHasABackgroundTwoWiderOnEachSide(string label, double width)
	{
		var box = FlowchartLabelText.MeasureSvg(FlowchartLabels.SvgLines(label, FlowchartLabelType.Text), Trebuchet, 200, 0, background: true);

		Verify(Math.Abs(box.Width - width) <= 0.001);
		Verify(Math.Abs(box.Height - 20) <= 1e-9);
	}

	[Test]
	public void AShortNodeLabelIsWidenedToTheMinimumAndStaysCentred()
	{
		var box = FlowchartLabelText.MeasureSvg(FlowchartLabels.SvgLines("Start", FlowchartLabelType.Text), Trebuchet, 120, 120, background: false);

		Verify(Math.Abs(box.Width - 120) <= 1e-9);
		Verify(Math.Abs(box.Height - 16) <= 1e-9);
		Verify(Math.Abs(box.X - -60) <= 1e-9);
		Verify(Math.Abs(box.Y - 1) <= 1e-9);
	}

	[Test]
	public void LinesAre11EmApartAndEachLineBoxIsTheRoundedAscentAndDescent()
	{
		var box = FlowchartLabelText.MeasureSvg(FlowchartLabels.SvgLines("one<br>two", FlowchartLabelType.Text), Trebuchet, 200, 0, background: true);

		Verify(box.Lines.Count == 2);
		Verify(Math.Abs(box.Height - 35.4) <= 1e-9);
	}

	[Test]
	public void ALineWiderThanTheWrappingWidthIsSplitBetweenWords()
	{
		const string Label = "A long stadium label that wraps into many lines without losing its circular end caps or letting text cross the border";
		var courier = new FlowchartFont(TextMeasurer.Default, "courier", 14);

		var box = FlowchartLabelText.MeasureSvg(FlowchartLabels.SvgLines(Label, FlowchartLabelType.Text), courier, 120, 0, background: false);

		var text = string.Concat(box.Lines.SelectMany(line => line.Select(run => run.Text)));

		Verify(text == "A long stadiumlabel that wrapsinto many lineswithout losing itscircular end capsor letting text crossthe border");
	}

	[Test]
	public void AWordWiderThanTheWrappingWidthIsSplitBetweenCharacters()
	{
		var box = FlowchartLabelText.MeasureSvg(FlowchartLabels.SvgLines("abcdefghijklmnop", FlowchartLabelType.Text), Trebuchet, 40, 0, background: false);

		Verify(box.Lines.Count > 1);
		Verify(string.Concat(box.Lines.SelectMany(line => line.Select(run => run.Text))) == "abcdefghijklmnop");
		Verify(box.Lines.All(line => FlowchartLabelText.LineWidth(line, Trebuchet) <= 40));
	}

	[Test]
	public void AMarkdownLabelsStrongTextIsABoldWordOfItsOwn()
	{
		var lines = FlowchartLabels.SvgLines("The **cat** in", FlowchartLabelType.Markdown);

		Verify(lines.Count == 1);
		SequenceAssert.Equal(lines[0],
			new LabelRun("The", false, false),
			new LabelRun("cat", true, false),
			new LabelRun("in", false, false));
	}
}
