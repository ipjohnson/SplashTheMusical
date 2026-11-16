using AwesomeAssertions;
using SplashTheMusical.Upstream;

namespace SplashTheMusical.Tests.Upstream;

public class GrammarHeaderTests
{
	[Test]
	public void EveryParserSourceNamesItsUpstreamGrammar()
	{
		var repository = UpstreamFixture.Repository();
		var sources = GrammarHeaders.ParserSources(repository);
		sources.Should().NotBeEmpty();
		foreach (var source in sources)
		{
			var paths = GrammarHeaders.Read(File.ReadAllText(source));
			paths.Should().NotBeEmpty($"{source} starts with a \"{GrammarHeaders.Prefix}<path>\" line per upstream file it implements");
			paths.Should().AllSatisfy(p => p.Should().StartWith("packages/", "the path is relative to the root of mermaid-js/mermaid"));
		}
	}

	[Test]
	public void TheHeaderIsTheLeadingRunOfGrammarLines() =>
		GrammarHeaders
			.Read("// Upstream grammar: a.jison\n// Upstream grammar:  b.langium \nusing System;\n// Upstream grammar: c.jison\n")
			.Should()
			.Equal("a.jison", "b.langium");

	[Test]
	public void TheMapNamesTheParserOfEachGrammar() =>
		GrammarHeaders
			.Map(UpstreamFixture.Repository())
			.Should()
			.ContainKey("packages/parser/src/language/info/info.langium")
			.WhoseValue.Should()
			.Equal("src/SplashTheMusical/Diagrams/Info/InfoParser.cs");
}
