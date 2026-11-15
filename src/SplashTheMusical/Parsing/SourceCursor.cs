using System.Buffers;

namespace SplashTheMusical.Parsing;

/// <summary>
/// A position in Mermaid source that tracks its line and column. The helpers read one construct at the position and
/// return it as a <see cref="Token"/>. Nothing here allocates.
/// </summary>
/// <remarks>
/// <c>\n</c>, <c>\r\n</c> and a lone <c>\r</c> each end a line, as mermaid.js's preprocessing does when it replaces
/// <c>\r\n?</c> with <c>\n</c>. Lines and columns are 1-based.
/// </remarks>
internal ref struct SourceCursor
{
	private static readonly SearchValues<char> HorizontalSpace = SearchValues.Create(" \t");
	private static readonly SearchValues<char> Space = SearchValues.Create(" \t\r\n");
	private static readonly SearchValues<char> LineBreaks = SearchValues.Create("\r\n");
	private static readonly SearchValues<char> IdentifierChars =
		SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_");
	private static readonly SearchValues<char> OperatorChars = SearchValues.Create("-=.<>~&|*+!");

	private readonly ReadOnlySpan<char> _source;
	private int _position;
	private int _line;
	private int _lineStart;
	private int _longestFinishedLine;

	public SourceCursor(ReadOnlySpan<char> source)
	{
		_source = source;
		_line = 1;
	}

	public readonly ReadOnlySpan<char> Source => _source;

	public readonly int Position => _position;

	public readonly int Line => _line;

	public readonly int Column => _position - _lineStart + 1;

	public readonly bool IsAtEnd => _position >= _source.Length;

	/// <summary>The character at the position, or <c>'\0'</c> at the end.</summary>
	public readonly char Current => Peek(0);

	public readonly ReadOnlySpan<char> Remaining => _source[_position..];

	/// <summary>The length of the longest line the cursor has reached, without its line break.</summary>
	public readonly int LongestLine => Math.Max(_longestFinishedLine, _position - _lineStart);

	public readonly char Peek(int offset)
	{
		var index = _position + offset;
		return (uint)index < (uint)_source.Length ? _source[index] : '\0';
	}

	/// <summary>Moves forward <paramref name="count"/> characters and counts the line breaks it passes.</summary>
	public void Advance(int count)
	{
		var end = _position + count;
		var offset = _position;
		var rest = _source[offset..end];
		int found;
		while ((found = rest.IndexOfAny(LineBreaks)) >= 0)
		{
			var at = offset + found;
			if (_source[at] == '\n')
			{
				EndLine(at > 0 && _source[at - 1] == '\r' ? at - 1 : at, at + 1);
			}
			else if (at + 1 >= _source.Length || _source[at + 1] != '\n')
			{
				EndLine(at, at + 1);
			}

			offset = at + 1;
			rest = rest[(found + 1)..];
		}

		_position = end;
	}

	/// <summary>Skips spaces and tabs. Line breaks stop it.</summary>
	public void SkipWhitespace()
	{
		var found = Remaining.IndexOfAnyExcept(HorizontalSpace);
		_position = found < 0 ? _source.Length : _position + found;
	}

	/// <summary>Skips spaces, tabs and line breaks.</summary>
	public void SkipWhitespaceAndLineBreaks()
	{
		var found = Remaining.IndexOfAnyExcept(Space);
		Advance(found < 0 ? _source.Length - _position : found);
	}

	/// <summary>Consumes one line break, if the cursor is at one.</summary>
	public bool SkipLineBreak()
	{
		switch (Current)
		{
			case '\n':
				Advance(1);
				return true;
			case '\r':
				Advance(Peek(1) == '\n' ? 2 : 1);
				return true;
			default:
				return false;
		}
	}

	/// <summary>
	/// Reads ASCII letters, digits, underscores and non-ASCII letters and digits. The token is empty when the cursor is
	/// not at one.
	/// </summary>
	public Token ReadIdentifier()
	{
		var start = _position;
		var rest = Remaining;
		var length = rest.IndexOfAnyExcept(IdentifierChars);
		while (length >= 0 && rest[length] >= '\u0080' && char.IsLetterOrDigit(rest[length]))
		{
			var next = rest[(length + 1)..].IndexOfAnyExcept(IdentifierChars);
			length = next < 0 ? -1 : length + 1 + next;
		}

		if (length < 0)
		{
			length = rest.Length;
		}

		_position += length;
		return new Token(TokenKind.Identifier, start, length);
	}

	/// <summary>Reads to the end of the line. The line break, and a <c>\r</c> before it, are not part of the token.</summary>
	public Token ReadToEndOfLine(TokenKind kind)
	{
		var start = _position;
		var length = Remaining.IndexOfAny(LineBreaks);
		if (length < 0)
		{
			length = _source.Length - start;
		}

		_position += length;
		return new Token(kind, start, length, EntityFlag(_source.Slice(start, length)));
	}

	/// <summary>
	/// Reads a string from the <c>"</c> at the cursor to the next <c>"</c>. The token is the text between them. A string
	/// with no closing quote is a <see cref="TokenKind.UnterminatedString"/> that runs to the end of the source.
	/// </summary>
	public Token ReadQuotedString()
	{
		var start = _position + 1;
		var length = _source[start..].IndexOf('"');
		if (length < 0)
		{
			return ReadUnterminated(TokenKind.UnterminatedString, start);
		}

		var text = _source.Slice(start, length);
		Advance(length + 2);
		return new Token(TokenKind.String, start, length, EntityFlag(text));
	}

	/// <summary>
	/// Reads a Markdown string from the <c>"`</c> at the cursor to the next <c>`"</c>. The token is the text between
	/// them. A string with no closing <c>`"</c> is a <see cref="TokenKind.UnterminatedMarkdownString"/> that runs to the
	/// end of the source.
	/// </summary>
	public Token ReadMarkdownString()
	{
		var start = _position + 2;
		var length = _source[start..].IndexOf("`\"");
		if (length < 0)
		{
			return ReadUnterminated(TokenKind.UnterminatedMarkdownString, start);
		}

		var text = _source.Slice(start, length);
		Advance(length + 4);
		return new Token(TokenKind.MarkdownString, start, length, EntityFlag(text));
	}

	/// <summary>
	/// Reads a <c>%%</c> comment at the cursor. The token is the text after <c>%%</c> to the end of the line. The caller
	/// checks <see cref="IsAtComment"/> first, because <c>%%{</c> starts a directive.
	/// </summary>
	public Token ReadComment()
	{
		_position += 2;
		return ReadToEndOfLine(TokenKind.Comment);
	}

	/// <summary>Whether the cursor is at <c>%%</c> that is not the start of a <c>%%{</c> directive.</summary>
	public readonly bool IsAtComment => Current == '%' && Peek(1) == '%' && Peek(2) != '{';

	/// <summary>
	/// Skips spaces and tabs and reads the next token with the helpers above. This is the general tokenizer. A diagram
	/// parser calls the helpers directly where its grammar needs a different split.
	/// </summary>
	public Token NextToken()
	{
		SkipWhitespace();
		var start = _position;
		if (IsAtEnd)
		{
			return new Token(TokenKind.End, start, 0);
		}

		var c = Current;
		switch (c)
		{
			case '\r':
			case '\n':
				SkipLineBreak();
				return new Token(TokenKind.LineBreak, start, _position - start);
			case '"':
				return Peek(1) == '`' ? ReadMarkdownString() : ReadQuotedString();
			case '%' when IsAtComment:
				return ReadComment();
		}

		if (IdentifierChars.Contains(c) || (c >= '\u0080' && char.IsLetterOrDigit(c)))
		{
			return ReadIdentifier();
		}

		if (OperatorChars.Contains(c))
		{
			var length = Remaining.IndexOfAnyExcept(OperatorChars);
			_position += length < 0 ? _source.Length - start : length;
			return new Token(TokenKind.Operator, start, _position - start);
		}

		_position++;
		return new Token(TokenKind.Punctuation, start, 1);
	}

	private Token ReadUnterminated(TokenKind kind, int start)
	{
		var length = _source.Length - start;
		Advance(_source.Length - _position);
		return new Token(kind, start, length);
	}

	private void EndLine(int lineEnd, int nextLineStart)
	{
		_longestFinishedLine = Math.Max(_longestFinishedLine, lineEnd - _lineStart);
		_line++;
		_lineStart = nextLineStart;
	}

	private static TokenFlags EntityFlag(ReadOnlySpan<char> text) => text.Contains('#') ? TokenFlags.MayHaveEntity : TokenFlags.None;
}
