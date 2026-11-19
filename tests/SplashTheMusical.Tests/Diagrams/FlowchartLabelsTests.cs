using SplashTheMusical.Diagrams.Flowchart;

namespace SplashTheMusical.Tests.Diagrams;

/// <summary>
/// The label text <see cref="FlowchartLabels"/> gives for a flowchart label. <c>&lt;11</c> and <c>&lt;**1**1</c> are
/// the text the oracle records for them with <c>htmlLabels: false</c>.
/// </summary>
public class FlowchartLabelsTests
{
	[Test]
	[Arguments("Start", "Start")]
	[Arguments("<b>bold</b> text", "bold text")]
	[Arguments("a<script>x</script>b", "ab")]
	[Arguments("a <!-- note --> b", "a  b")]
	[Arguments("#quot;quoted#quot;", "\"quoted\"")]
	[Arguments("#35; and #9829;", "# and ♥")]
	[Arguments("a &amp; b &lt; c", "a & b < c")]
	public void HtmlLabelIsTheTextTheBrowserShows(string label, string expected) =>
		Verify(FlowchartLabels.Text(label, FlowchartLabelType.Text) == expected);

	[Test]
	[Arguments("one<br>two", "one two")]
	[Arguments("one<br/>two", "one two")]
	[Arguments("one\ntwo", "one two")]
	[Arguments("one\\ntwo", "one two")]
	public void HtmlLabelReadsALineBreakAsASpace(string label, string expected) =>
		Verify(FlowchartLabels.Text(label, FlowchartLabelType.Text) == expected);

	[Test]
	public void HtmlLabelTakesOutIcons()
	{
		var icons = new List<string>();

		var text = FlowchartLabels.Text("fa:fa-car Car fab:fa-github", FlowchartLabelType.Text, icons: icons);

		Verify(text == " Car ");
		SequenceAssert.Equal(icons, "fa:fa-car", "fab:fa-github");
	}

	[Test]
	[Arguments("**bold** and *italic*", "bold and italic")]
	[Arguments("__bold__ and _italic_", "bold and italic")]
	[Arguments("one\ntwo", "one two")]
	[Arguments("one\n   two", "one two")]
	[Arguments("a * b", "a * b")]
	public void MarkdownLabelTakesOffTheMarkers(string label, string expected) =>
		Verify(FlowchartLabels.Text(label, FlowchartLabelType.Markdown) == expected);

	[Test]
	[Arguments("one<br>two", "onetwo")]
	[Arguments("one<br />two", "onetwo")]
	[Arguments("one\ntwo", "onetwo")]
	[Arguments("one\\ntwo", "onetwo")]
	public void SvgLabelRunsItsLinesTogether(string label, string expected) =>
		Verify(FlowchartLabels.Text(label, FlowchartLabelType.Text, htmlLabels: false) == expected);

	[Test]
	[Arguments("one   two", "one two")]
	[Arguments("<b>x</b> y", "<b> x </b> y")]
	[Arguments("<11", "<11")]
	[Arguments("a &amp; b", "a & b")]
	public void SvgLabelDrawsItsWordsAndTagsAsText(string label, string expected) =>
		Verify(FlowchartLabels.Text(label, FlowchartLabelType.Text, htmlLabels: false) == expected);

	[Test]
	[Arguments("<**1**1", "< 1 1")]
	[Arguments("**bold** words", "bold words")]
	[Arguments("one\ntwo", "onetwo")]
	public void SvgMarkdownLabelMakesStrongAndEmphasisTheirOwnWords(string label, string expected) =>
		Verify(FlowchartLabels.Text(label, FlowchartLabelType.Markdown, htmlLabels: false) == expected);
}
