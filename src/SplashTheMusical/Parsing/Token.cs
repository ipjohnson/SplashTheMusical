namespace SplashTheMusical.Parsing;

/// <summary>The kinds of token the shared lexing helpers produce.</summary>
internal enum TokenKind : byte
{
	/// <summary>The end of the source.</summary>
	End,

	/// <summary>A line break: <c>\n</c>, <c>\r\n</c> or <c>\r</c>.</summary>
	LineBreak,

	/// <summary>Letters, digits and underscores.</summary>
	Identifier,

	/// <summary>The text between two double quotes, without the quotes.</summary>
	String,

	/// <summary>The text between <c>"`</c> and <c>`"</c>, without the delimiters.</summary>
	MarkdownString,

	/// <summary>The text after <c>%%</c> to the end of the line.</summary>
	Comment,

	/// <summary>A run of operator characters such as <c>--&gt;</c> or <c>-.-&gt;</c>.</summary>
	Operator,

	/// <summary>One bracket, separator or other character that no other kind covers.</summary>
	Punctuation,

	/// <summary>A string whose closing quote is missing. It runs to the end of the source.</summary>
	UnterminatedString,

	/// <summary>A Markdown string whose closing <c>`"</c> is missing. It runs to the end of the source.</summary>
	UnterminatedMarkdownString,
}

/// <summary>Facts about a token's text that decide how it becomes a string.</summary>
[Flags]
internal enum TokenFlags : byte
{
	None = 0,

	/// <summary>The text contains a <c>#</c>, so it may hold an entity code such as <c>#35;</c> or <c>#quot;</c>.</summary>
	MayHaveEntity = 1,
}

/// <summary>A token is a range of the source. It holds no text.</summary>
internal readonly record struct Token(TokenKind Kind, int Start, int Length, TokenFlags Flags = TokenFlags.None)
{
	public int End => Start + Length;

	public ReadOnlySpan<char> Text(ReadOnlySpan<char> source) => source.Slice(Start, Length);

	/// <summary>
	/// The token's text as a string. Entity codes are decoded only when <see cref="TokenFlags.MayHaveEntity"/> is set, so
	/// a token without the flag is a plain substring of the source.
	/// </summary>
	public string ToText(ReadOnlySpan<char> source) =>
		(Flags & TokenFlags.MayHaveEntity) != 0 ? EntityCodes.Decode(Text(source)) : Text(source).ToString();
}
