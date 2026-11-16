using AwesomeAssertions;
using SplashTheMusical.Text;

namespace SplashTheMusical.Tests.Text;

public class MermaidTextTests
{
	private static readonly FontSpec Font = new("Arial", 10);

	/// <summary>A host measurer that gives every character 10 pixels and every line 12.</summary>
	private sealed class MonospaceMeasurer : ITextMeasurer
	{
		public TextMetrics Measure(ReadOnlySpan<char> text, in FontSpec font) => new(10 * text.Length, 9, 3);
	}

	private static readonly MonospaceMeasurer Host = new();

	[Test]
	[Arguments("a<br>b")]
	[Arguments("a<br/>b")]
	[Arguments("a<BR />b")]
	[Arguments("a</br>b")]
	[Arguments("a<br \t>b")]
	public void FindsEachFormOfTheBreakTag(string text)
	{
		MermaidText.HasBreaks(text).Should().BeTrue();
		Lines(text).Should().Equal("a", "b");
	}

	[Test]
	[Arguments("a < b")]
	[Arguments("<b>bold</b>")]
	[Arguments("a<brx>b")]
	[Arguments("a<br")]
	public void IgnoresOtherTags(string text)
	{
		MermaidText.HasBreaks(text).Should().BeFalse();
		Lines(text).Should().Equal(text);
	}

	[Test]
	public void KeepsEmptyLines() => Lines("<br/>a<br><br>").Should().Equal("", "a", "", "");

	[Test]
	public void AHostMeasurerReplacesTheDefault()
	{
		MermaidText.CalculateTextDimensions("abc<br/>de", Font, Host).Should().Be(new TextDimensions(30, 24, 12));
		MermaidText.CalculateTextWidth("abc", Font).Should().Be(Math.Round(TextMeasurer.Default.Measure("abc", Font).Width));
		MermaidText.WrapLabel("aa bb cc", 45, Font, Host).Should().Be("aa<br/>bb<br/>cc");
		MermaidText.WrapLabel("aa bb cc", 45, Font).Should().Be("aa bb cc");
	}

	[Test]
	public void RoundsEachLineToWholePixels() =>
		MermaidText.CalculateTextDimensions("Hello John, how are you?", new FontSpec("Arial", 16)).Width.Should().Be(181);

	[Test]
	public void MeasuresNothingForEmptyText() => MermaidText.CalculateTextDimensions("", Font, Host).Should().Be(default(TextDimensions));

	[Test]
	public void WrapsWhenTheLineAndTheNextWordReachTheMaximum()
	{
		// "aa " is 30 and "bb " is 30: 20 + 30 is under 51, and "aa bb" (50) + "cc " (30) reaches it.
		MermaidText.WrapLabel("aa bb cc", 51, Font, Host).Should().Be("aa bb<br/>cc");
		MermaidText.WrapLabel("aa  bb", 1000, Font, Host).Should().Be("aa bb");
		MermaidText.WrapLabel("aa bb cc", 51, Font, Host, joinWith: "\n").Should().Be("aa bb\ncc");
	}

	[Test]
	public void ReturnsALabelWithBreaksAsItIs() => MermaidText.WrapLabel("aa bb<br>cc dd", 10, Font, Host).Should().Be("aa bb<br>cc dd");

	[Test]
	public void HyphenatesAWordWiderThanTheMaximum()
	{
		MermaidText.WrapLabel("x abcdefg", 30, Font, Host).Should().Be("x<br/>abc-<br/>def-<br/>g");
		var (lines, remaining) = MermaidText.BreakString("abcdef", 30, Font, Host);
		lines.Should().Equal("abc-", "def");
		remaining.Should().BeEmpty();
	}

	private static List<string> Lines(string text)
	{
		var lines = new List<string>();
		foreach (var line in MermaidText.SplitBreaks(text))
		{
			lines.Add(line.ToString());
		}

		return lines;
	}
}
