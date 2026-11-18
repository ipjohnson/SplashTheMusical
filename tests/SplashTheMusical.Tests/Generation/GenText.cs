using CsCheck;
using SplashTheMusical.Parsing;

namespace SplashTheMusical.Tests.Generation;

/// <summary>
/// Random text for labels, titles and accessibility statements: words, non-ASCII letters and emoji, quotes, brackets,
/// entity codes, HTML line breaks and the punctuation diagram grammars give a meaning to. Every value is trimmed as
/// JavaScript's <c>trim</c> does and is not empty, because the parsers trim what they read.
/// </summary>
public static class GenText
{
	private static readonly string[] Words = ["a", "Node", "hello world", "x1", "Ünïcödé", "日本語", "😀", "ß", "Ωμέγα", "مرحبا"];

	private static readonly string[] Marks =
	[
		"\"", "'", "[", "]", "(", ")", "{", "}", "|", "<", ">", "#quot;", "#35;", "#9829;", "&amp;", "&lt;", "<br>", "<br/>",
		"*", "_", "`", ":", ";", ",", ".", "-", "=", "~", "/", "\\", "@", "#", "%", "!", "?", "+", "&", "^", "$",
	];

	private static readonly string[] Spaces = [" ", "  ", "\t", " "];

	/// <summary>Text with any of the fragments, and line breaks.</summary>
	public static Gen<string> Label { get; } = Join([.. Words, .. Marks, .. Spaces, "\n", " \n "]);

	/// <summary>
	/// One line of text for statements that read to the end of the line, such as <c>accTitle:</c> and <c>title</c>:
	/// no <c>%</c>, which would start a comment, and no braces.
	/// </summary>
	public static Gen<string> Line { get; } = Join([.. Words, .. Marks.Where(m => m.AsSpan().IndexOfAny("%{}") < 0), .. Spaces]).Select(WithoutTags);

	/// <summary><see cref="Line"/> fragments with line breaks, for <c>accDescr { }</c>.</summary>
	public static Gen<string> Lines { get; } = Join([.. Words, .. Marks.Where(m => m.AsSpan().IndexOfAny("%{}") < 0), " ", "\n"]).Select(WithoutTags);

	/// <summary><paramref name="text"/> or <c>null</c>, one time in three.</summary>
	public static Gen<string?> OrNull(this Gen<string> text) =>
		Gen.Frequency((1, Gen.Const((string?)null)), (2, text.Select(t => (string?)t)));

	/// <summary>
	/// The text as langium's <c>CommonValueConverter</c> leaves it: each line trimmed, runs of spaces and tabs folded to
	/// one space, and blank lines dropped.
	/// </summary>
	public static string Folded(string text)
	{
		var lines = new List<string>();
		foreach (var line in text.Split('\n'))
		{
			var folded = new System.Text.StringBuilder();
			foreach (var c in line.Trim(" \t\r\n\f\v".ToCharArray()))
			{
				if (c is ' ' or '\t')
				{
					if (folded.Length == 0 || folded[^1] != ' ')
					{
						folded.Append(' ');
					}
				}
				else
				{
					folded.Append(c);
				}
			}

			if (folded.Length > 0)
			{
				lines.Add(folded.ToString());
			}
		}

		return string.Join('\n', lines);
	}

	/// <summary>
	/// The text without a <c>&lt;</c> that starts a word, other than <c>&lt;br&gt;</c> and <c>&lt;br/&gt;</c>. mermaid.js
	/// quotes the attributes of <c>/&lt;(\w+)([^&gt;]*)&gt;/</c> in the whole source before it parses, so such a tag that
	/// a label opens can reach into the statements after it.
	/// </summary>
	public static string WithoutTags(string text)
	{
		while (true)
		{
			var builder = new System.Text.StringBuilder(text.Length);
			for (var i = 0; i < text.Length; i++)
			{
				var rest = text.AsSpan(i);
				if (text[i] == '<' && rest.Length > 1 && (char.IsLetterOrDigit(rest[1]) || rest[1] == '_')
					&& !rest.StartsWith("<br>", StringComparison.Ordinal) && !rest.StartsWith("<br/>", StringComparison.Ordinal))
				{
					continue;
				}

				builder.Append(text[i]);
			}

			var next = Trim(builder.ToString());
			if (next == text)
			{
				return text;
			}

			text = next;
		}
	}

	/// <summary><paramref name="text"/> without the characters of <paramref name="removed"/>, or <c>x</c> when nothing is left.</summary>
	public static string Without(string text, string removed) => Trim(string.Concat(text.Where(c => !removed.Contains(c))));

	private static Gen<string> Join(string[] fragments) =>
		Gen.OneOf(fragments).Array[1, 6].Select(parts => Trim(string.Concat(parts)));

	private static string Trim(string text)
	{
		var start = 0;
		var end = text.Length;
		while (start < end && PreambleParser.IsSpace(text[start]))
		{
			start++;
		}

		while (end > start && PreambleParser.IsSpace(text[end - 1]))
		{
			end--;
		}

		return start == end ? "x" : text[start..end];
	}
}
