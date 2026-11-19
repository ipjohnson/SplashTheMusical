using SplashTheMusical.Tests.Generation;
using SplashTheMusical.Diagrams.Info;

namespace SplashTheMusical.Tests.Diagrams;

public class InfoModuleTests
{
	[Test]
	[Arguments("info")]
	[Arguments("info showInfo")]
	[Arguments("\n\ninfo\n\nshowInfo\n\n")]
	[Arguments("info %% a comment\n")]
	[Arguments("info%% a comment")]
	[Arguments("info\r\n  showInfo\r\n")]
	[Arguments("info\n%%{init: {\"theme\": \"dark\"}}%%\nshowInfo")]
	[Arguments("info\ntitle\n")]
	public void AcceptsWhatTheGrammarAccepts(string source) =>
		Verify(Mermaid.Default.TryParse(source, out _, out _));

	[Test]
	[Arguments("infos")]
	[Arguments("info showInfo info")]
	[Arguments("info\naccTitle a title")]
	[Arguments("info title a\naccTitle b")]
	[Arguments("info\naccDescr {\n not closed\n")]
	public void RefusesWhatTheGrammarRefuses(string source) =>
		Verify(!Mermaid.Default.TryParse(source, out _, out _));

	[Test]
	public void ALexerErrorReadsAsMermaidsError()
	{
		Mermaid.Default.TryParse("info unsupported", out _, out var diagnostics);

		Verify(diagnostics.Count == 1);
		Verify(diagnostics[0].Message == "Parsing failed: Lexer error on line 1, column 6: unexpected character: ->u<- at offset: 5, skipped 11 characters.");
	}

	[Test]
	public void ReadsTheTitleAndAccessibilityStatements()
	{
		var document = (InfoDocument)Mermaid.Default.Parse("info\ntitle   The   title\naccTitle : Short\naccDescr {\n  line one\n\n  line two\n}\n");

		Verify(document.DiagramTitle == "The title");
		Verify(document.AccessibilityTitle == "Short");
		Verify(document.AccessibilityDescription == "line one\nline two");
	}

	[Test]
	public void AWrittenDocumentParsesBackTheSame()
	{
		const string Source = "---\ntitle: Front\nconfig:\n  theme: dark\n---\n%%{init: {\"fontFamily\": \"courier\"}}%%\ninfo showInfo\ntitle Body\naccTitle: A\naccDescr {\nB\nC\n}\n";
		var document = (InfoDocument)Mermaid.Default.Parse(Source);

		var written = Mermaid.Default.Write(document);
		var again = (InfoDocument)Mermaid.Default.Parse(written);

		SequenceAssert.Equal(DocumentDump.Lines(again), DocumentDump.Lines(document).ToArray());
		Verify(written.EndsWith("info\nshowInfo\ntitle Body\naccTitle: A\naccDescr {\nB\nC\n}\n", StringComparison.Ordinal));
	}

	[Test]
	public void TheFrontMatterAndDirectivesAreTheDocuments()
	{
		var document = Mermaid.Default.Parse("---\ntitle: T\n---\n%%{wrap}%%\ninfo");

		Verify(document.Title == "T");
		Verify(document.Directives.Count == 1);
		Verify(document.Directives[0].Type == "wrap");
	}
}
