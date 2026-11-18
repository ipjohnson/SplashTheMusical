using SplashTheMusical.Conformance.Fuzzing;

namespace SplashTheMusical.Conformance;

public class MutatorTests
{
	[Test]
	[Arguments("flowchart LR\n  A-->B\r\n  B[\"x y\"]~~~C;")]
	[Arguments("")]
	[Arguments("graph TD\n  A((😀)) -.-> B{{b}}\r")]
	public async Task SplitsASourceIntoTokensThatJoinBackToIt(string source)
	{
		await Assert.That(string.Concat(SourceTokens.Split(source).Select(t => t.Text))).IsEqualTo(source);
		await Assert.That(string.Concat(SourceTokens.Lines(source))).IsEqualTo(source);
	}

	[Test]
	public async Task SplitsArrowsBracketsWordsAndLineBreaks()
	{
		var tokens = SourceTokens.Split("A-->B[x]\r\n");

		await Assert.That(string.Join(",", tokens.Select(t => $"{t.Kind}:{Mutator.Escape(t.Text)}")))
			.IsEqualTo("Word:A,Arrow:-->,Word:B,Bracket:[,Word:x,Bracket:],Newline:\\r\\n");
	}

	[Test]
	public async Task MakesTheSameSourceFromTheSameSeedAndIndex()
	{
		const string Source = "flowchart LR\n  A-->B\n  B-->C[c]\n";

		var first = Enumerable.Range(0, 50).Select(i => Mutator.Mutate(Source, FuzzRandom.For(7, i))).ToList();
		var second = Enumerable.Range(0, 50).Select(i => Mutator.Mutate(Source, FuzzRandom.For(7, i))).ToList();

		await Assert.That(string.Join("|", first)).IsEqualTo(string.Join("|", second));
		await Assert.That(first.Distinct(StringComparer.Ordinal).Count()).IsGreaterThan(40);
		await Assert.That(first.Count(m => m != Source)).IsGreaterThan(45);
	}

	[Test]
	public async Task ChangesAnArrowToAnotherArrow()
	{
		for (var i = 0; i < 20; i++)
		{
			var mutated = Mutator.Apply("A-->B", MutatorEdit.ChangeArrow, FuzzRandom.For(1, i));

			await Assert.That(mutated[0]).IsEqualTo('A');
			await Assert.That(mutated[^1]).IsEqualTo('B');
			await Assert.That(Mutator.Arrows.Contains(mutated[1..^1])).IsTrue();
		}
	}

	[Test]
	public async Task ChangesABracketToAnotherBracket()
	{
		for (var i = 0; i < 20; i++)
		{
			var mutated = Mutator.Apply("A[x]", MutatorEdit.ChangeBracket, FuzzRandom.For(1, i));

			var changed = mutated.StartsWith("A[x", StringComparison.Ordinal)
				? mutated[3..]
				: mutated[1..mutated.IndexOf('x', StringComparison.Ordinal)];
			await Assert.That(Mutator.Brackets.Contains(changed)).IsTrue();
		}
	}

	[Test]
	public async Task InsertsAnArrowWhenTheSourceHasNone()
	{
		var mutated = Mutator.Apply("A B", MutatorEdit.ChangeArrow, FuzzRandom.For(3, 0));

		await Assert.That(Mutator.Arrows.Any(a => mutated.Contains(a, StringComparison.Ordinal))).IsTrue();
	}

	[Test]
	[Arguments(MutatorEdit.InsertWhitespace)]
	[Arguments(MutatorEdit.InsertComment)]
	[Arguments(MutatorEdit.InsertLineBreak)]
	public async Task InsertsTextWithoutLosingAnyOfTheSource(MutatorEdit edit)
	{
		var mutated = Mutator.Apply("A-->B", edit, FuzzRandom.For(5, (int)edit));
		var inserted = edit switch
		{
			MutatorEdit.InsertWhitespace => Mutator.Whitespace,
			MutatorEdit.InsertComment => Mutator.Comments,
			_ => Mutator.LineBreaks,
		};

		await Assert.That(inserted.Any(text => RemoveOnce(mutated, text) == "A-->B")).IsTrue();
	}

	[Test]
	public async Task DeletesDuplicatesAndSwapsTokens()
	{
		var deleted = Mutator.Apply("A --> B", MutatorEdit.Delete, FuzzRandom.For(2, 0));
		var duplicated = Mutator.Apply("A --> B", MutatorEdit.Duplicate, FuzzRandom.For(2, 0));
		var swapped = Mutator.Apply("A --> B", MutatorEdit.Swap, FuzzRandom.For(2, 1));

		await Assert.That(SourceTokens.Split(deleted).Count).IsEqualTo(4);
		await Assert.That(SourceTokens.Split(duplicated).Count).IsGreaterThanOrEqualTo(5);
		await Assert.That(string.Concat(swapped.Order())).IsEqualTo(string.Concat("A --> B".Order()));
	}

	private static string RemoveOnce(string text, string part)
	{
		var at = text.IndexOf(part, StringComparison.Ordinal);
		return at < 0 ? text : text.Remove(at, part.Length);
	}
}
