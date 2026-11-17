using AwesomeAssertions;
using SplashTheMusical.Config;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Rendering.Shapes;
using SplashTheMusical.Text;

namespace SplashTheMusical.Tests.Diagrams;

/// <summary>
/// HTML labels as Chromium lays out <c>addHtmlSpan</c>'s <c>div</c>. The expected sizes are the oracle's, from the
/// <c>mermaider-rfc-*</c> and icon cases, whose redux-color theme sets 14px Recursive text and 120px wrapping.
/// </summary>
public class FlowchartHtmlLabelTests
{
	private static readonly FlowchartFont Recursive = new(TextMeasurer.Default, "\"Recursive Variable\", arial, sans-serif", 14);

	private static FlowchartLabelBox Measure(string label, FlowchartLabelType type = FlowchartLabelType.String, bool autoWrap = true) =>
		FlowchartLabelText.Measure(FlowchartLabelText.Lines(label, type, true, autoWrap), Recursive, 120, 120, true);

	private static List<string> Texts(FlowchartLabelBox box) => [.. box.Lines.Select(line => string.Concat(line.Select(run => run.Text)))];

	[Test]
	public void ALineBreaksAfterAHyphen()
	{
		var box = Measure("V2 build\\npath_prefix: elasticsearch-fundamentals");

		Texts(box).Should().Equal("V2 build", "path_prefix:", "elasticsearch-", "fundamentals");
		box.Width.Should().Be(120);
		box.Height.Should().Be(84);
	}

	[Test]
	public void AWordWiderThanTheWrappingWidthWidensTheLabelAndTheOtherLinesWrapAtThatWidth()
	{
		var box = Measure("V1 TableOfContentNodes");

		Texts(box).Should().Equal("V1", "TableOfContentNodes");
		box.Width.Should().Be(137.75);
	}

	[Test]
	public void AMarkdownBlankLineTakesNoLine()
	{
		var lines = FlowchartLabelText.Lines("one\n\n  two", FlowchartLabelType.Markdown, true);

		lines.Select(line => string.Concat(line.Select(run => run.Text))).Should().Equal("one", "two");
	}

	[Test]
	public void WithoutMarkdownAutoWrapAMarkdownLabelKeepsItsSpacesAndDoesNotWrap()
	{
		const string Label = "This is a long markdown label that would wrap";

		Measure(Label, FlowchartLabelType.Markdown).Lines.Should().HaveCountGreaterThan(1);
		Measure(Label, FlowchartLabelType.Markdown, autoWrap: false).Lines.Should().ContainSingle();
	}

	[Test]
	public void TheSpaceAfterALeadingIconTakesALineOfItsOwnWhenTheLabelWraps()
	{
		var box = Measure("fa:fa-circle [DBServer\\SharedDbInstance].[SupportDb]");

		box.Lines.Should().HaveCount(3);
		Texts(box)[1].Should().Be("[DBServer\\SharedDbInstance].");
		box.Width.Should().BeApproximately(192.219, 0.001);
	}

	[Test]
	public void ALabelOfAnIconAloneHasTheMinimumWidthAndNoHeight()
	{
		var box = Measure("fa:fa-spinner");

		box.Width.Should().Be(120);
		box.Height.Should().Be(0);
	}

	[Test]
	public void AnImageAloneIsFiveFontSizesSquareAndAnImageWithTextIsAsWideAsTheLabel()
	{
		var measure = new FlowchartMeasure(new MermaidConfig(), "m", "flowchart-v2", TextMeasurer.Default);
		var alone = new FlowNode { Id = "B", Label = "<img src='logo.svg'>", Padding = 15 };
		var withText = new FlowNode { Id = "D", Label = "<img src='logo.svg'> some text", Padding = 15 };
		var broken = new FlowNode { Id = "C", Label = "<img>", Padding = 15 };

		measure.Node(alone, "TB");
		measure.Node(withText, "TB");
		measure.Node(broken, "TB");

		alone.LabelBox!.Width.Should().Be(120);
		alone.LabelBox.Height.Should().Be(80);
		withText.LabelBox!.Height.Should().Be(withText.LabelBox.Width + withText.LabelBox.LineHeight);
		broken.LabelBox!.Height.Should().Be(0);
	}

	[Test]
	public void AnIconNodeStacksItsLabelAndIcon()
	{
		var label = new FixedLabel(44, 25);

		var icon = ShapeDrawings.Icon(new ShapeNode { Label = label, HasLabel = true, WrappingWidth = 120 });
		var square = ShapeDrawings.IconSquare(new ShapeNode { Label = label, HasLabel = true, AssetHeight = 60, Padding = 15, LabelPosition = "t", WrappingWidth = 120 });
		var bare = ShapeDrawings.Icon(new ShapeNode { Label = new FixedLabel(0, 0), WrappingWidth = 120 });

		(icon.Width, icon.Height).Should().Be((48, 81));
		square.Width.Should().BeApproximately(75, 1e-9);
		square.Height.Should().BeApproximately(108, 1e-9);
		(bare.Width, bare.Height).Should().Be((48, 48));
		label.Widths.Should().AllBeEquivalentTo(120);
	}

	private sealed class FixedLabel(double width, double height) : IShapeLabelMeasurer
	{
		public List<double?> Widths { get; } = [];

		public ShapeLabel Measure(double? nodeWidth, bool ignoreMinWidth)
		{
			Widths.Add(nodeWidth);
			return new ShapeLabel(width, height, true);
		}
	}
}
