using SplashTheMusical.Conformance.Fuzzing;

namespace SplashTheMusical.Conformance;

public class MutatorTests
{
	[Test]
	[Arguments("flowchart LR\n  A-->B\r\n  B[\"x y\"]~~~C;")]
	[Arguments("")]
	[Arguments("graph TD\n  A((😀)) -.-> B{{b}}\r")]
	public void SplitsASourceIntoTokensThatJoinBackToIt(string source)
	{
		Verify(string.Concat(SourceTokens.Split(source).Select(t => t.Text)) == source, $"was {string.Concat(SourceTokens.Split(source).Select(t => t.Text))}");
		Verify(string.Concat(SourceTokens.Lines(source)) == source, $"was {string.Concat(SourceTokens.Lines(source))}");
	}

	[Test]
	public void SplitsArrowsBracketsWordsAndLineBreaks()
	{
		var tokens = SourceTokens.Split("A-->B[x]\r\n");

		Verify(string.Join(",", tokens.Select(t => $"{t.Kind}:{Mutator.Escape(t.Text)}")) == "Word:A,Arrow:-->,Word:B,Bracket:[,Word:x,Bracket:],Newline:\\r\\n", $"was {(string.Join(",", tokens.Select(t => $"{t.Kind}:{Mutator.Escape(t.Text)}")))}");
	}

	[Test]
	public void MakesTheSameSourceFromTheSameSeedAndIndex()
	{
		const string Source = "flowchart LR\n  A-->B\n  B-->C[c]\n";

		var first = Enumerable.Range(0, 50).Select(i => Mutator.Mutate(Source, FuzzRandom.For(7, i))).ToList();
		var second = Enumerable.Range(0, 50).Select(i => Mutator.Mutate(Source, FuzzRandom.For(7, i))).ToList();

		Verify(string.Join("|", first) == string.Join("|", second), $"was {string.Join("|", first)}");
		Verify(first.Distinct(StringComparer.Ordinal).Count() > 40, $"was {first.Distinct(StringComparer.Ordinal).Count()}");
		Verify(first.Count(m => m != Source) > 45, $"was {first.Count(m => m != Source)}");
	}

	[Test]
	public void ChangesAnArrowToAnotherArrow()
	{
		for (var i = 0; i < 20; i++)
		{
			var mutated = Mutator.Apply("A-->B", MutatorEdit.ChangeArrow, FuzzRandom.For(1, i));

			Verify(mutated[0] == 'A', $"was {mutated[0]}");
			Verify(mutated[^1] == 'B', $"was {mutated[^1]}");
			Verify(Mutator.Arrows.Contains(mutated[1..^1]));
		}
	}

	[Test]
	public void ChangesABracketToAnotherBracket()
	{
		for (var i = 0; i < 20; i++)
		{
			var mutated = Mutator.Apply("A[x]", MutatorEdit.ChangeBracket, FuzzRandom.For(1, i));

			var changed = mutated.StartsWith("A[x", StringComparison.Ordinal)
				? mutated[3..]
				: mutated[1..mutated.IndexOf('x', StringComparison.Ordinal)];
			Verify(Mutator.Brackets.Contains(changed));
		}
	}

	[Test]
	public void InsertsAnArrowWhenTheSourceHasNone()
	{
		var mutated = Mutator.Apply("A B", MutatorEdit.ChangeArrow, FuzzRandom.For(3, 0));

		Verify(Mutator.Arrows.Any(a => mutated.Contains(a, StringComparison.Ordinal)));
	}

	[Test]
	[Arguments(MutatorEdit.InsertWhitespace)]
	[Arguments(MutatorEdit.InsertComment)]
	[Arguments(MutatorEdit.InsertLineBreak)]
	public void InsertsTextWithoutLosingAnyOfTheSource(MutatorEdit edit)
	{
		var mutated = Mutator.Apply("A-->B", edit, FuzzRandom.For(5, (int)edit));
		var inserted = edit switch
		{
			MutatorEdit.InsertWhitespace => Mutator.Whitespace,
			MutatorEdit.InsertComment => Mutator.Comments,
			_ => Mutator.LineBreaks,
		};

		Verify(inserted.Any(text => RemoveOnce(mutated, text) == "A-->B"));
	}

	[Test]
	public void DeletesDuplicatesAndSwapsTokens()
	{
		var deleted = Mutator.Apply("A --> B", MutatorEdit.Delete, FuzzRandom.For(2, 0));
		var duplicated = Mutator.Apply("A --> B", MutatorEdit.Duplicate, FuzzRandom.For(2, 0));
		var swapped = Mutator.Apply("A --> B", MutatorEdit.Swap, FuzzRandom.For(2, 1));

		Verify(SourceTokens.Split(deleted).Count == 4, $"was {SourceTokens.Split(deleted).Count}");
		Verify(SourceTokens.Split(duplicated).Count >= 5, $"was {SourceTokens.Split(duplicated).Count}");
		Verify(string.Concat(swapped.Order()) == string.Concat("A --> B".Order()), $"was {string.Concat(swapped.Order())}");
	}

	private static string RemoveOnce(string text, string part)
	{
		var at = text.IndexOf(part, StringComparison.Ordinal);
		return at < 0 ? text : text.Remove(at, part.Length);
	}
}
