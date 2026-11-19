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
		Verify(MermaidText.HasBreaks(text));
		Verify(Lines(text).SequenceEqual(["a", "b"]));
	}

	[Test]
	[Arguments("a < b")]
	[Arguments("<b>bold</b>")]
	[Arguments("a<brx>b")]
	[Arguments("a<br")]
	public void IgnoresOtherTags(string text)
	{
		Verify(!MermaidText.HasBreaks(text));
		Verify(Lines(text).SequenceEqual([text]));
	}

	[Test]
	public void KeepsEmptyLines() => Verify((Lines("<br/>a<br><br>")).SequenceEqual(["", "a", "", ""]));

	[Test]
	public void AHostMeasurerReplacesTheDefault()
	{
		Verify(MermaidText.CalculateTextDimensions("abc<br/>de", Font, Host) == new TextDimensions(30, 24, 12));
		Verify(Math.Abs(MermaidText.CalculateTextWidth("abc", Font) - Math.Round(TextMeasurer.Default.Measure("abc", Font).Width)) <= 1e-9);
		Verify(MermaidText.WrapLabel("aa bb cc", 45, Font, Host) == "aa<br/>bb<br/>cc");
		Verify(MermaidText.WrapLabel("aa bb cc", 45, Font) == "aa bb cc");
	}

	[Test]
	public void RoundsEachLineToWholePixels() =>
		Verify(Math.Abs(MermaidText.CalculateTextDimensions("Hello John, how are you?", new FontSpec("Arial", 16)).Width - 181) <= 1e-9);

	[Test]
	public void MeasuresNothingForEmptyText() => Verify(MermaidText.CalculateTextDimensions("", Font, Host) == default(TextDimensions));

	[Test]
	public void WrapsWhenTheLineAndTheNextWordReachTheMaximum()
	{
		// "aa " is 30 and "bb " is 30: 20 + 30 is under 51, and "aa bb" (50) + "cc " (30) reaches it.
		Verify(MermaidText.WrapLabel("aa bb cc", 51, Font, Host) == "aa bb<br/>cc");
		Verify(MermaidText.WrapLabel("aa  bb", 1000, Font, Host) == "aa bb");
		Verify(MermaidText.WrapLabel("aa bb cc", 51, Font, Host, joinWith: "\n") == "aa bb\ncc");
	}

	[Test]
	public void ReturnsALabelWithBreaksAsItIs() => Verify(MermaidText.WrapLabel("aa bb<br>cc dd", 10, Font, Host) == "aa bb<br>cc dd");

	[Test]
	public void HyphenatesAWordWiderThanTheMaximum()
	{
		Verify(MermaidText.WrapLabel("x abcdefg", 30, Font, Host) == "x<br/>abc-<br/>def-<br/>g");
		var (lines, remaining) = MermaidText.BreakString("abcdef", 30, Font, Host);
		Verify(lines.SequenceEqual(["abc-", "def"]));
		Verify(!remaining.Any());
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
