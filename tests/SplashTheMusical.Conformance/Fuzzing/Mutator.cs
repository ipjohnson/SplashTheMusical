using System.Text;

namespace SplashTheMusical.Conformance.Fuzzing;

public enum MutatorEdit
{
	Delete,
	Duplicate,
	Swap,
	ChangeArrow,
	ChangeBracket,
	InsertWhitespace,
	InsertComment,
	InsertLineBreak,
}

/// <summary>
/// The token-level edits of section 7's differential fuzzing on the tokens of <see cref="SourceTokens"/>. An edit that
/// needs a token the source lacks, such as an arrow, inserts one at a random place instead.
/// </summary>
public static class Mutator
{
	public static readonly IReadOnlyList<MutatorEdit> Edits = Enum.GetValues<MutatorEdit>();

	public static readonly IReadOnlyList<string> Arrows =
	[
		"-->", "---", "-.->", "-.-", "==>", "===", "--o", "--x", "<-->", "o--o", "x--x", "~~~", "--", "->", "<--", "-->>",
		"--|", "==", "-.", ":::", "&", "-- text -->", "-->|text|",
	];

	public static readonly IReadOnlyList<string> Brackets =
	[
		"(", ")", "[", "]", "{", "}", "((", "))", "(((", ")))", "[[", "]]", "{{", "}}", "[(", ")]", "([", "])", "[/", "/]",
		"[\\", "\\]", ">", "|", "@{", "\"", "`", "<<", ">>",
	];

	public static readonly IReadOnlyList<string> Whitespace = [" ", "\t", "   "];

	public static readonly IReadOnlyList<string> Comments = ["%% c\n", "%%", "%%{init: {}}%%\n", "%%{", "}%%"];

	public static readonly IReadOnlyList<string> LineBreaks = ["\n", "\r\n", "\n\n", ";"];

	/// <summary>
	/// Random edits of <paramref name="source"/>: one in half the calls, two in a quarter and three in a quarter. More edits
	/// reach further, and fewer leave more sources that both sides accept.
	/// </summary>
	public static string Mutate(string source, FuzzRandom random)
	{
		var split = SourceTokens.Split(source);
		var tokens = split.ConvertAll(t => t.Text);
		var kinds = split.ConvertAll(t => t.Kind);
		var edits = Math.Max(1, random.Next(4));
		for (var i = 0; i < edits; i++)
		{
			Apply(tokens, kinds, random.Pick(Edits), random);
		}

		return string.Concat(tokens);
	}

	/// <summary>One edit of <paramref name="source"/>.</summary>
	public static string Apply(string source, MutatorEdit edit, FuzzRandom random)
	{
		var split = SourceTokens.Split(source);
		var tokens = split.ConvertAll(t => t.Text);
		var kinds = split.ConvertAll(t => t.Kind);
		Apply(tokens, kinds, edit, random);
		return string.Concat(tokens);
	}

	private static void Apply(List<string> tokens, List<SourceTokenKind> kinds, MutatorEdit edit, FuzzRandom random)
	{
		if (tokens.Count == 0 && edit is MutatorEdit.Delete or MutatorEdit.Duplicate or MutatorEdit.Swap)
		{
			edit = MutatorEdit.InsertLineBreak;
		}

		switch (edit)
		{
			case MutatorEdit.Delete:
				{
					var at = random.Next(tokens.Count);
					tokens.RemoveAt(at);
					kinds.RemoveAt(at);
					break;
				}

			case MutatorEdit.Duplicate:
				{
					var at = random.Next(tokens.Count);
					tokens.Insert(at, tokens[at]);
					kinds.Insert(at, kinds[at]);
					break;
				}

			case MutatorEdit.Swap:
				{
					var a = random.Next(tokens.Count);
					var b = random.Next(tokens.Count);
					(tokens[a], tokens[b]) = (tokens[b], tokens[a]);
					(kinds[a], kinds[b]) = (kinds[b], kinds[a]);
					break;
				}

			case MutatorEdit.ChangeArrow:
				Replace(tokens, kinds, SourceTokenKind.Arrow, Arrows, random);
				break;
			case MutatorEdit.ChangeBracket:
				Replace(tokens, kinds, SourceTokenKind.Bracket, Brackets, random);
				break;
			case MutatorEdit.InsertWhitespace:
				Insert(tokens, kinds, random.Next(tokens.Count + 1), random.Pick(Whitespace), SourceTokenKind.Space);
				break;
			case MutatorEdit.InsertComment:
				Insert(tokens, kinds, random.Next(tokens.Count + 1), random.Pick(Comments), SourceTokenKind.Other);
				break;
			case MutatorEdit.InsertLineBreak:
				Insert(tokens, kinds, random.Next(tokens.Count + 1), random.Pick(LineBreaks), SourceTokenKind.Newline);
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(edit), edit, null);
		}
	}

	private static void Replace(List<string> tokens, List<SourceTokenKind> kinds, SourceTokenKind kind, IReadOnlyList<string> choices, FuzzRandom random)
	{
		var places = new List<int>();
		for (var i = 0; i < kinds.Count; i++)
		{
			if (kinds[i] == kind)
			{
				places.Add(i);
			}
		}

		if (places.Count == 0)
		{
			Insert(tokens, kinds, random.Next(tokens.Count + 1), random.Pick(choices), kind);
			return;
		}

		tokens[random.Pick(places)] = random.Pick(choices);
	}

	private static void Insert(List<string> tokens, List<SourceTokenKind> kinds, int at, string text, SourceTokenKind kind)
	{
		tokens.Insert(at, text);
		kinds.Insert(at, kind);
	}

	/// <summary>The source with its control characters written as escapes, for a report line.</summary>
	public static string Escape(string source)
	{
		var builder = new StringBuilder(source.Length);
		foreach (var c in source)
		{
			_ = c switch
			{
				'\n' => builder.Append("\\n"),
				'\r' => builder.Append("\\r"),
				'\t' => builder.Append("\\t"),
				'\\' => builder.Append("\\\\"),
				_ => builder.Append(c),
			};
		}

		return builder.ToString();
	}
}
