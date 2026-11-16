using System.Text;
using AwesomeAssertions;
using SplashTheMusical.Parsing;
using SplashTheMusical.Writing;

namespace SplashTheMusical.Tests.Parsing;

public class AccessibilityReaderTests
{
	[Test]
	public void ReadsATitleToTheEndOfTheLine()
	{
		var document = new TestDocument();
		var cursor = new SourceCursor("accTitle : Sales by region \nA");
		AccessibilityReader.TryRead(ref cursor, document).Should().BeTrue();
		document.AccessibilityTitle.Should().Be("Sales by region");
		cursor.Remaining.ToString().Should().Be("\nA");
	}

	[Test]
	public void ReadsADescriptionOnOneLineOrInBraces()
	{
		var document = new TestDocument();
		var cursor = new SourceCursor("accDescr: One line\naccDescr {\n  First\n  Second\n}\nA");
		AccessibilityReader.TryRead(ref cursor, document).Should().BeTrue();
		document.AccessibilityDescription.Should().Be("One line");
		cursor.SkipLineBreak();
		AccessibilityReader.TryRead(ref cursor, document).Should().BeTrue();
		document.AccessibilityDescription.Should().Be("First\n  Second");
		cursor.Remaining.ToString().Should().Be("\nA");
	}

	[Test]
	[Arguments("accTitle Sales")]
	[Arguments("accTitle { Sales }")]
	[Arguments("accessible: yes")]
	public void OtherTextIsNotAStatement(string source)
	{
		var cursor = new SourceCursor(source);
		AccessibilityReader.TryRead(ref cursor, new TestDocument()).Should().BeFalse();
		cursor.Position.Should().Be(0);
	}

	[Test]
	[Arguments("One line")]
	[Arguments("First\nSecond")]
	public void AWrittenStatementReadsBackTheSame(string description)
	{
		var document = new TestDocument { AccessibilityTitle = "Title", AccessibilityDescription = description };
		var builder = new StringBuilder();
		PreambleWriter.WriteAccessibility(builder, document, "  ");

		var again = new TestDocument();
		var cursor = new SourceCursor(builder.ToString());
		while (!cursor.IsAtEnd)
		{
			cursor.SkipWhitespaceAndLineBreaks();
			if (!cursor.IsAtEnd)
			{
				AccessibilityReader.TryRead(ref cursor, again).Should().BeTrue();
			}
		}

		again.AccessibilityTitle.Should().Be("Title");
		again.AccessibilityDescription.Should().Be(description);
	}

	private sealed class TestDocument : MermaidDocument
	{
		public override string DiagramType => "";
	}
}
