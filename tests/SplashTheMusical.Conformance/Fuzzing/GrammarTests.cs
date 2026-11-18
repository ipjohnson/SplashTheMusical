using SplashTheMusical.Conformance.Fuzzing;

namespace SplashTheMusical.Conformance;

public class GrammarTests
{
	[Test]
	public async Task SamplesTextAPatternMatches()
	{
		static string Sample(string pattern, bool jison, int index) => PatternSampler.Parse(pattern, jison).Sample(FuzzRandom.For(11, index));

		for (var i = 0; i < 20; i++)
		{
			var call = Sample("\"call\"[\\s]+", true, i);
			await Assert.That(call.StartsWith("call", StringComparison.Ordinal) && call.Length > 4 && call[4..].All(char.IsWhiteSpace)).IsTrue();
			await Assert.That(Sample("[0-9]+", false, i).All(char.IsAsciiDigit)).IsTrue();
			await Assert.That(Sample("[^\"\\s]+", false, i).Any(c => c == '"' || char.IsWhiteSpace(c))).IsFalse();
			await Assert.That(Sample("(?!x)a{2}\\u0041$", false, i)).IsEqualTo("aaA");
			await Assert.That(Sample("(?:ab|cd)", false, i) is "ab" or "cd").IsTrue();
			await Assert.That(Sample("\\s*\"{\"[^}]*\\}", true, i).Trim()[0]).IsEqualTo('{');
		}
	}

	[Test]
	public async Task EmitsSentencesOfAJisonGrammarWithTheTextOfTheStatesAroundATokenThatNeedsThem()
	{
		const string Text = """
			%lex
			%x string
			%%
			<*>["]                 this.pushState("string");
			<string>[^"]+          return 'STR';
			<string>["]            this.popState();
			"graph"                { if (true) { this.begin("x"); } return 'GRAPH'; }
			\s+                    /* skip */
			"-->"                  return 'ARROW';
			[a-z]+                 return 'ID';
			<<EOF>>                return 'EOF';
			/lex
			%start start
			%%
			start : GRAPH stmts EOF ;
			stmts
				: /* empty */
				| stmts stmt { $$ = "}"; }
				;
			stmt : ID ARROW ID | ID STR | ID '&' ID ;
			%%
			""";
		var grammar = JisonGrammar.Read(Text);

		var sentences = Enumerable.Range(0, 100).Select(i => grammar.Sentence(FuzzRandom.For(3, i))).ToList();

		await Assert.That(sentences.All(s => s.StartsWith("graph", StringComparison.Ordinal))).IsTrue();
		await Assert.That(sentences.All(s => s.Count(c => c == '"') % 2 == 0)).IsTrue();
		await Assert.That(sentences.Any(s => s.Contains(" --> ", StringComparison.Ordinal))).IsTrue();
		await Assert.That(sentences.Any(s => s.Contains(" & ", StringComparison.Ordinal))).IsTrue();
		await Assert.That(sentences.Any(s => s.Contains('"', StringComparison.Ordinal))).IsTrue();
	}

	[Test]
	public async Task EmitsSentencesOfALangiumGrammarAndTheGrammarsItImports()
	{
		var folder = Directory.CreateTempSubdirectory("stm-langium-").FullName;
		try
		{
			Directory.CreateDirectory(Path.Combine(folder, "common"));
			File.WriteAllText(Path.Combine(folder, "main.langium"), """
				grammar Main
				import "./common/common";

				// A comment
				entry Main:
					NEWLINE* "main" NEWLINE* (items+=Item)* {infer Main};
				Item: name=ID ':' value=Number NEWLINE;
				""");
			File.WriteAllText(Path.Combine(folder, "common", "common.langium"), """
				terminal ID: /[a-z]+/;
				terminal Number returns number: INT;
				terminal INT returns number: /[1-9][0-9]*/;
				terminal NEWLINE: /\r?\n/;
				hidden terminal WS: /[\t ]+/;
				""");
			var grammar = LangiumGrammar.Read(Path.Combine(folder, "main.langium"));

			var sentences = Enumerable.Range(0, 50).Select(i => grammar.Sentence(FuzzRandom.For(5, i))).ToList();

			await Assert.That(sentences.All(s => s.Trim().StartsWith("main", StringComparison.Ordinal))).IsTrue();
			await Assert.That(sentences.Any(s => s.Contains(" : ", StringComparison.Ordinal))).IsTrue();
			var item = sentences.SelectMany(s => s.Split('\n')).First(l => l.Contains(':', StringComparison.Ordinal)).Trim().Split(' ');
			await Assert.That(item[0].All(char.IsAsciiLetterLower)).IsTrue();
			await Assert.That(item[2].All(char.IsAsciiDigit)).IsTrue();
		}
		finally
		{
			Directory.Delete(folder, recursive: true);
		}
	}

	/// <summary>With <see cref="FuzzRunTests.MermaidVariable"/> set, STM must accept some sentences of each fuzzed type's grammar.</summary>
	[Test]
	public async Task EmitsSentencesStmAcceptsFromEachUpstreamGrammarWhenAsked()
	{
		if (Environment.GetEnvironmentVariable(FuzzRunTests.MermaidVariable) is not { Length: > 0 } checkout)
		{
			return;
		}

		var stm = new Mermaid(new MermaidOptions { Limits = ResourceLimits.Default });
		foreach (var type in FuzzTypes.Folders(CorpusCheck.CorpusRoot))
		{
			var grammar = FuzzTypes.ReadGrammar(checkout, FuzzTypes.GrammarPath(CorpusCheck.RepositoryRoot, type)!);
			var accepted = Enumerable.Range(0, 300).Count(i => stm.TryParse(grammar.Sentence(FuzzRandom.For(1, i)), out _, out _));
			Console.WriteLine($"{type}: STM accepts {accepted} of 300 sentences");

			await Assert.That(accepted).IsGreaterThan(0);
		}
	}

	[Test]
	public async Task FindsTheGrammarOfEachFuzzedTypeInItsParserSources()
	{
		var root = CorpusCheck.RepositoryRoot;

		await Assert.That(FuzzTypes.GrammarPath(root, "flowchart")).IsEqualTo("packages/mermaid/src/diagrams/flowchart/parser/flow.jison");
		await Assert.That(FuzzTypes.GrammarPath(root, "info")).IsEqualTo("packages/parser/src/language/info/info.langium");
		await Assert.That(FuzzTypes.GrammarPath(root, "pie")).IsNull();
	}
}
