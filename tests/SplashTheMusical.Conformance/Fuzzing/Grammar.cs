namespace SplashTheMusical.Conformance.Fuzzing;

/// <summary>An element of a grammar rule.</summary>
public abstract record GrammarNode;

public sealed record GrammarSequence(IReadOnlyList<GrammarNode> Items) : GrammarNode;

public sealed record GrammarChoice(IReadOnlyList<GrammarNode> Branches) : GrammarNode;

/// <param name="Max">The most repeats, or -1 for no limit.</param>
public sealed record GrammarRepeat(GrammarNode Item, int Min, int Max) : GrammarNode;

/// <summary>A rule of the grammar, or a token that <see cref="Grammar.Tokens"/> samples, by name.</summary>
public sealed record GrammarReference(string Name) : GrammarNode;

public sealed record GrammarText(string Text) : GrammarNode;

/// <summary>
/// A grammar read from an upstream jison or langium file, as rules and token samplers, which emits random sentences of
/// it. A sentence joins its tokens with a space. A reference that is neither a rule nor a token emits nothing.
/// </summary>
/// <param name="Start">The rule a sentence derives from.</param>
/// <param name="Rules">The rules by name.</param>
/// <param name="Tokens">Each token's ways of making its text.</param>
public sealed record Grammar(string Start, IReadOnlyDictionary<string, GrammarNode> Rules, IReadOnlyDictionary<string, IReadOnlyList<Func<FuzzRandom, string>>> Tokens)
{
	/// <summary>Past this depth a sentence takes the shortest way out of each rule.</summary>
	public const int MaxDepth = 24;

	/// <summary>Past this many tokens a repeat takes its minimum.</summary>
	public const int MaxTokens = 400;

	private Dictionary<GrammarNode, int>? _heights;

	public string Sentence(FuzzRandom random)
	{
		_heights ??= Heights();
		var tokens = new List<string>();
		Emit(new GrammarReference(Start), 0, tokens, random);
		return string.Join(' ', tokens);
	}

	private void Emit(GrammarNode node, int depth, List<string> tokens, FuzzRandom random)
	{
		var shortest = depth > MaxDepth || tokens.Count > MaxTokens;
		switch (node)
		{
			case GrammarText text:
				tokens.Add(text.Text);
				break;
			case GrammarSequence sequence:
				foreach (var item in sequence.Items)
				{
					Emit(item, depth, tokens, random);
				}

				break;
			case GrammarChoice choice:
				var branch = shortest ? choice.Branches.MinBy(Height)! : random.Pick(choice.Branches);
				Emit(branch, depth, tokens, random);
				break;
			case GrammarRepeat repeat:
				var count = shortest ? repeat.Min : random.Between(repeat.Min, repeat.Max < 0 ? repeat.Min + 3 : repeat.Max);
				for (var i = 0; i < count; i++)
				{
					Emit(repeat.Item, depth, tokens, random);
				}

				break;
			case GrammarReference reference when Rules.TryGetValue(reference.Name, out var rule):
				Emit(rule, depth + 1, tokens, random);
				break;
			case GrammarReference reference when Tokens.TryGetValue(reference.Name, out var samplers):
				tokens.Add(random.Pick(samplers)(random));
				break;
			case GrammarReference:
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(node), node, null);
		}
	}

	private int Height(GrammarNode node) => _heights!.GetValueOrDefault(node, int.MaxValue / 2);

	/// <summary>The fewest rule expansions each node needs to end, found by iterating to a fixed point.</summary>
	private Dictionary<GrammarNode, int> Heights()
	{
		var heights = new Dictionary<GrammarNode, int>(ReferenceEqualityComparer.Instance);
		const int Unknown = int.MaxValue / 2;
		int Of(GrammarNode node)
		{
			var height = node switch
			{
				GrammarText => 0,
				GrammarSequence sequence => sequence.Items.Count == 0 ? 0 : sequence.Items.Max(Of),
				GrammarChoice choice => choice.Branches.Min(Of),
				GrammarRepeat repeat => repeat.Min == 0 ? 0 : Of(repeat.Item),
				GrammarReference reference when Rules.TryGetValue(reference.Name, out var rule) => Math.Min(Unknown, heights.GetValueOrDefault(rule, Unknown) + 1),
				_ => 0,
			};
			heights[node] = height;
			return height;
		}

		for (var changed = true; changed;)
		{
			changed = false;
			foreach (var rule in Rules.Values)
			{
				var before = heights.GetValueOrDefault(rule, Unknown);
				if (Of(rule) < before)
				{
					changed = true;
				}
			}
		}

		return heights;
	}
}
