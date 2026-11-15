using AwesomeAssertions;
using SplashTheMusical.Parsing;

namespace SplashTheMusical.Tests.Parsing;

public class SourceCursorTests
{
	[Test]
	public void SkipWhitespaceStopsAtALineBreak()
	{
		var cursor = new SourceCursor(" \t \nA");
		cursor.SkipWhitespace();
		cursor.Position.Should().Be(3);
		cursor.Line.Should().Be(1);
		cursor.Column.Should().Be(4);
	}

	[Test]
	public void SkipWhitespaceStopsAtTheEnd()
	{
		var cursor = new SourceCursor("  ");
		cursor.SkipWhitespace();
		cursor.IsAtEnd.Should().BeTrue();
	}

	[Test]
	public void SkipWhitespaceAndLineBreaksCountsCrlfAsOneLine()
	{
		var cursor = new SourceCursor("  \r\n\r\n\tA");
		cursor.SkipWhitespaceAndLineBreaks();
		cursor.Current.Should().Be('A');
		cursor.Line.Should().Be(3);
		cursor.Column.Should().Be(2);
	}

	[Test]
	public void ALoneCarriageReturnEndsALine()
	{
		var cursor = new SourceCursor("A\rB");
		cursor.Advance(2);
		cursor.Line.Should().Be(2);
		cursor.Column.Should().Be(1);
	}

	[Test]
	public void AdvanceBetweenCarriageReturnAndLineFeedStaysOnTheLine()
	{
		var cursor = new SourceCursor("A\r\nB");
		cursor.Advance(2);
		cursor.Line.Should().Be(1);
		cursor.Advance(1);
		cursor.Line.Should().Be(2);
		cursor.Column.Should().Be(1);
	}

	[Test]
	public void SkipLineBreakConsumesCrlfLfAndCr()
	{
		var cursor = new SourceCursor("\r\n\n\rA");
		cursor.SkipLineBreak().Should().BeTrue();
		cursor.Position.Should().Be(2);
		cursor.SkipLineBreak().Should().BeTrue();
		cursor.SkipLineBreak().Should().BeTrue();
		cursor.SkipLineBreak().Should().BeFalse();
		cursor.Line.Should().Be(4);
	}

	[Test]
	public void LongestLineExcludesTheLineBreak()
	{
		var cursor = new SourceCursor("abc\r\nabcdef\r\nab");
		cursor.Advance(5);
		cursor.LongestLine.Should().Be(3);
		cursor.SkipWhitespaceAndLineBreaks();
		cursor.ReadIdentifier();
		cursor.LongestLine.Should().Be(6);
	}

	[Test]
	public void ReadIdentifierReadsLettersDigitsAndUnderscores()
	{
		var source = "node_1-->B";
		var cursor = new SourceCursor(source);
		var token = cursor.ReadIdentifier();
		token.Should().Be(new Token(TokenKind.Identifier, 0, 6));
		token.Text(source).ToString().Should().Be("node_1");
		cursor.Current.Should().Be('-');
	}

	[Test]
	public void ReadIdentifierReadsNonAsciiLetters()
	{
		var source = "café_ü x";
		var cursor = new SourceCursor(source);
		cursor.ReadIdentifier().Text(source).ToString().Should().Be("café_ü");
	}

	[Test]
	public void ReadIdentifierIsEmptyAtAnOperator()
	{
		var cursor = new SourceCursor("-->");
		cursor.ReadIdentifier().Length.Should().Be(0);
		cursor.Position.Should().Be(0);
	}

	[Test]
	public void ReadIdentifierRunsToTheEnd()
	{
		var cursor = new SourceCursor("abc");
		cursor.ReadIdentifier().Length.Should().Be(3);
		cursor.IsAtEnd.Should().BeTrue();
	}

	[Test]
	public void ReadToEndOfLineStopsBeforeCrlf()
	{
		var source = "title Hello world\r\nnext";
		var cursor = new SourceCursor(source);
		cursor.Advance(6);
		var token = cursor.ReadToEndOfLine(TokenKind.String);
		token.Text(source).ToString().Should().Be("Hello world");
		cursor.Current.Should().Be('\r');
		cursor.Line.Should().Be(1);
	}

	[Test]
	public void ReadToEndOfLineRunsToTheEndOfTheSource()
	{
		var source = "last line";
		var cursor = new SourceCursor(source);
		cursor.ReadToEndOfLine(TokenKind.String).Text(source).ToString().Should().Be("last line");
		cursor.IsAtEnd.Should().BeTrue();
	}

	[Test]
	public void ReadQuotedStringReturnsTheTextBetweenTheQuotes()
	{
		var source = "\"a b\" rest";
		var cursor = new SourceCursor(source);
		var token = cursor.ReadQuotedString();
		token.Should().Be(new Token(TokenKind.String, 1, 3));
		cursor.Position.Should().Be(5);
	}

	[Test]
	public void ReadQuotedStringCountsTheLinesItSpans()
	{
		var source = "\"one\r\ntwo\" x";
		var cursor = new SourceCursor(source);
		cursor.ReadQuotedString().Text(source).ToString().Should().Be("one\r\ntwo");
		cursor.Line.Should().Be(2);
		cursor.Column.Should().Be(5);
	}

	[Test]
	public void ReadQuotedStringFlagsAHash()
	{
		var cursor = new SourceCursor("\"a #35; b\"");
		cursor.ReadQuotedString().Flags.Should().Be(TokenFlags.MayHaveEntity);
	}

	[Test]
	public void AnUnterminatedStringRunsToTheEnd()
	{
		var source = "A[\"abc\r\ndef";
		var cursor = new SourceCursor(source);
		cursor.Advance(2);
		var token = cursor.ReadQuotedString();
		token.Kind.Should().Be(TokenKind.UnterminatedString);
		token.Text(source).ToString().Should().Be("abc\r\ndef");
		cursor.IsAtEnd.Should().BeTrue();
		cursor.Line.Should().Be(2);
	}

	[Test]
	public void ReadMarkdownStringReturnsTheTextBetweenTheDelimiters()
	{
		var source = "\"`**bold** \"quoted\"`\" x";
		var cursor = new SourceCursor(source);
		var token = cursor.ReadMarkdownString();
		token.Kind.Should().Be(TokenKind.MarkdownString);
		token.Text(source).ToString().Should().Be("**bold** \"quoted\"");
		cursor.Current.Should().Be(' ');
	}

	[Test]
	public void AnUnterminatedMarkdownStringRunsToTheEnd()
	{
		var source = "\"`abc\" def";
		var cursor = new SourceCursor(source);
		var token = cursor.ReadMarkdownString();
		token.Kind.Should().Be(TokenKind.UnterminatedMarkdownString);
		token.Text(source).ToString().Should().Be("abc\" def");
		cursor.IsAtEnd.Should().BeTrue();
	}

	[Test]
	public void ReadCommentReturnsTheTextAfterThePercentSigns()
	{
		var source = "%% a note\r\nA";
		var cursor = new SourceCursor(source);
		cursor.IsAtComment.Should().BeTrue();
		var token = cursor.ReadComment();
		token.Kind.Should().Be(TokenKind.Comment);
		token.Text(source).ToString().Should().Be(" a note");
		cursor.Current.Should().Be('\r');
	}

	[Test]
	public void ADirectiveIsNotAComment()
	{
		var cursor = new SourceCursor("%%{init: {}}%%");
		cursor.IsAtComment.Should().BeFalse();
	}

	[Test]
	public void NextTokenSplitsAFlowchartLine()
	{
		var source = "  A-->B[\"x\"] %% c\r\n\"`md`\"";
		Kinds(source).Should().Equal(
			(TokenKind.Identifier, "A"),
			(TokenKind.Operator, "-->"),
			(TokenKind.Identifier, "B"),
			(TokenKind.Punctuation, "["),
			(TokenKind.String, "x"),
			(TokenKind.Punctuation, "]"),
			(TokenKind.Comment, " c"),
			(TokenKind.LineBreak, "\r\n"),
			(TokenKind.MarkdownString, "md"),
			(TokenKind.End, ""));
	}

	[Test]
	public void ToTextDecodesOnlyAFlaggedToken()
	{
		var source = "\"a #35; b\" \"#35;\"";
		var cursor = new SourceCursor(source);
		var flagged = cursor.ReadQuotedString();
		flagged.ToText(source).Should().Be("a # b");
		(flagged with { Flags = TokenFlags.None }).ToText(source).Should().Be("a #35; b");
	}

	[Test]
	public void LexingAHundredLineFlowchartAllocatesNothing()
	{
		var source = Flowchart(100);
		var tokens = 0;
		var unterminated = 0;
		Allocations.ShouldAllocateExactly(0, () =>
		{
			tokens = 0;
			unterminated = 0;
			var cursor = new SourceCursor(source);
			Token token;
			while ((token = cursor.NextToken()).Kind != TokenKind.End)
			{
				tokens++;
				if (token.Kind is TokenKind.UnterminatedString or TokenKind.UnterminatedMarkdownString)
				{
					unterminated++;
				}
			}
		});
		tokens.Should().BeGreaterThan(500);
		unterminated.Should().Be(0);
	}

	private static string Flowchart(int lines)
	{
		var builder = new System.Text.StringBuilder("flowchart LR\n");
		for (var i = 1; i < lines; i++)
		{
			builder.Append((i % 4) switch
			{
				0 => $"  %% step {i}\n",
				1 => $"  n{i}[\"Label #quot;{i}#quot;\"] --> n{i + 1}{{Decide}}\n",
				2 => $"  n{i} -.->|yes| n{i + 1}(\"`**md** {i}`\")\r\n",
				_ => $"  n{i} ==> n{i + 1}\n",
			});
		}

		return builder.ToString();
	}

	private static List<(TokenKind, string)> Kinds(string source)
	{
		var result = new List<(TokenKind, string)>();
		var cursor = new SourceCursor(source);
		Token token;
		do
		{
			token = cursor.NextToken();
			result.Add((token.Kind, token.Text(source).ToString()));
		}
		while (token.Kind != TokenKind.End);

		return result;
	}
}
