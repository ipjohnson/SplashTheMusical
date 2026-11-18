namespace SplashTheMusical.Conformance.Fuzzing;

public enum SourceTokenKind
{
	/// <summary>Letters, digits and underscores.</summary>
	Word,

	/// <summary>Spaces and tabs.</summary>
	Space,

	/// <summary>One line break: LF, CRLF or CR.</summary>
	Newline,

	/// <summary>A run of the characters arrows are made of: <c>- = . &lt; &gt; ~</c>.</summary>
	Arrow,

	/// <summary>One of <c>( ) [ ] { }</c>.</summary>
	Bracket,

	/// <summary>Any other character, with a surrogate pair kept whole.</summary>
	Other,
}

public readonly record struct SourceToken(SourceTokenKind Kind, string Text);

/// <summary>Splits a source into the tokens <see cref="Mutator"/> and <see cref="Minimizer"/> edit. The tokens join back to the source.</summary>
public static class SourceTokens
{
	private const string ArrowChars = "-=.<>~";
	private const string Brackets = "()[]{}";

	public static List<SourceToken> Split(string source)
	{
		var tokens = new List<SourceToken>();
		var i = 0;
		while (i < source.Length)
		{
			var c = source[i];
			var start = i;
			SourceTokenKind kind;
			if (c == '\r' || c == '\n')
			{
				kind = SourceTokenKind.Newline;
				i += c == '\r' && i + 1 < source.Length && source[i + 1] == '\n' ? 2 : 1;
			}
			else if (c is ' ' or '\t')
			{
				kind = SourceTokenKind.Space;
				while (i < source.Length && source[i] is ' ' or '\t')
				{
					i++;
				}
			}
			else if (IsWord(c))
			{
				kind = SourceTokenKind.Word;
				while (i < source.Length && IsWord(source[i]))
				{
					i++;
				}
			}
			else if (ArrowChars.Contains(c, StringComparison.Ordinal))
			{
				kind = SourceTokenKind.Arrow;
				while (i < source.Length && ArrowChars.Contains(source[i], StringComparison.Ordinal))
				{
					i++;
				}
			}
			else
			{
				kind = Brackets.Contains(c, StringComparison.Ordinal) ? SourceTokenKind.Bracket : SourceTokenKind.Other;
				i += char.IsHighSurrogate(c) && i + 1 < source.Length && char.IsLowSurrogate(source[i + 1]) ? 2 : 1;
			}

			tokens.Add(new SourceToken(kind, source[start..i]));
		}

		return tokens;
	}

	/// <summary>The source split into lines, each with its line break.</summary>
	public static List<string> Lines(string source)
	{
		var lines = new List<string>();
		var start = 0;
		for (var i = 0; i < source.Length; i++)
		{
			if (source[i] == '\n')
			{
				lines.Add(source[start..(i + 1)]);
				start = i + 1;
			}
		}

		if (start < source.Length)
		{
			lines.Add(source[start..]);
		}

		return lines;
	}

	private static bool IsWord(char c) => char.IsLetterOrDigit(c) || c == '_';
}
