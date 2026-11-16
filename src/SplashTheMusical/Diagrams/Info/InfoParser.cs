// Upstream grammar: packages/parser/src/language/info/info.langium
// Upstream grammar: packages/parser/src/language/common/common.langium
using System.Globalization;
using System.Text;
using SplashTheMusical.Parsing;

namespace SplashTheMusical.Diagrams.Info;

/// <summary>
/// The parser of <c>packages/parser/src/language/info/info.langium</c> at mermaid 12.3.0:
/// <code>
/// entry Info: NEWLINE* "info" NEWLINE* ("showInfo" NEWLINE*)? TitleAndAccessibilities?;
/// fragment TitleAndAccessibilities: ((accDescr=ACC_DESCR | accTitle=ACC_TITLE | title=TITLE) EOL)+;
/// EOL: NEWLINE+ | EOF;
/// </code>
/// The terminals are those of <c>common/common.langium</c>. <c>InfoTokenBuilder</c> lets a keyword match only before
/// space, <c>%%</c> or the end. As langium does, the whole body is lexed first, a character no token matches is a lexer
/// error that skips to the next place a token matches, and the errors read as <c>MermaidParseError</c> writes them.
/// </summary>
internal static class InfoParser
{
	private enum Kind
	{
		Hidden,
		NewLine,
		Info,
		ShowInfo,
		AccDescr,
		AccTitle,
		Title,
		End,
	}

	private readonly record struct Token(Kind Kind, int Start, int Length);

	public static InfoDocument? Parse(ReadOnlySpan<char> source, DiagramParseContext context)
	{
		var lexerErrors = new List<string>();
		var tokens = Lex(source, context.BodyStart, lexerErrors);
		var document = new InfoDocument();
		var parserErrors = new List<string>();
		Read(source, tokens, document, parserErrors);
		if (lexerErrors.Count == 0 && parserErrors.Count == 0)
		{
			return document;
		}

		var (line, column) = Position(source, context.BodyStart);
		context.AddError($"Parsing failed: {string.Join('\n', lexerErrors)} {string.Join('\n', parserErrors)}".Trim(), line, column);
		return null;
	}

	private static void Read(ReadOnlySpan<char> source, List<Token> tokens, InfoDocument document, List<string> errors)
	{
		var at = SkipNewLines(tokens, 0);
		if (tokens[at].Kind != Kind.Info)
		{
			errors.Add(Error(source, tokens[at], "Expecting token of type 'info' but found `" + Image(source, tokens[at]) + "`."));
			return;
		}

		at = SkipNewLines(tokens, at + 1);
		if (tokens[at].Kind == Kind.ShowInfo)
		{
			document.ShowInfo = true;
			at = SkipNewLines(tokens, at + 1);
		}

		while (tokens[at].Kind is Kind.AccDescr or Kind.AccTitle or Kind.Title)
		{
			var token = tokens[at];
			var text = source.Slice(token.Start, token.Length);
			switch (token.Kind)
			{
				case Kind.AccDescr:
					document.AccessibilityDescription = AccDescrValue(text);
					break;
				case Kind.AccTitle:
					document.AccessibilityTitle = LineValue(text[(text.IndexOf(':') + 1)..]);
					break;
				default:
					document.DiagramTitle = LineValue(text[(text.IndexOf("title", StringComparison.Ordinal) + "title".Length)..]);
					break;
			}

			at++;
			if (tokens[at].Kind == Kind.NewLine)
			{
				at = SkipNewLines(tokens, at);
			}
			else if (tokens[at].Kind != Kind.End)
			{
				errors.Add(Error(source, tokens[at], "Expecting token of type 'NEWLINE' but found `" + Image(source, tokens[at]) + "`."));
				return;
			}
		}

		if (tokens[at].Kind != Kind.End)
		{
			errors.Add(Error(source, tokens[at], "Redundant input, expecting EOF but found: " + Image(source, tokens[at])));
		}
	}

	private static int SkipNewLines(List<Token> tokens, int at)
	{
		while (tokens[at].Kind == Kind.NewLine)
		{
			at++;
		}

		return at;
	}

	/// <summary>The tokens that are not hidden, ending with <see cref="Kind.End"/>.</summary>
	private static List<Token> Lex(ReadOnlySpan<char> source, int start, List<string> errors)
	{
		var tokens = new List<Token>();
		var at = start;
		while (at < source.Length)
		{
			if (Match(source, at, out var kind, out var length))
			{
				if (kind != Kind.Hidden)
				{
					tokens.Add(new Token(kind, at, length));
				}

				at += length;
				continue;
			}

			var skipped = at + 1;
			while (skipped < source.Length && !Match(source, skipped, out _, out _))
			{
				skipped++;
			}

			var (line, column) = Position(source, at);
			errors.Add(string.Create(
				CultureInfo.InvariantCulture,
				$"Lexer error on line {line}, column {column}: unexpected character: ->{source[at]}<- at offset: {at}, skipped {skipped - at} characters."));
			at = skipped;
		}

		tokens.Add(new Token(Kind.End, source.Length, 0));
		return tokens;
	}

	/// <summary>The first token of mermaid's order that matches at <paramref name="at"/>: the keywords, longest first, then the terminals.</summary>
	private static bool Match(ReadOnlySpan<char> source, int at, out Kind kind, out int length)
	{
		var text = source[at..];
		kind = Kind.Hidden;
		length = Keyword(text, "showInfo");
		if (length > 0)
		{
			kind = Kind.ShowInfo;
			return true;
		}

		length = Keyword(text, "info");
		if (length > 0)
		{
			kind = Kind.Info;
			return true;
		}

		var indent = 0;
		while (indent < text.Length && text[indent] is ' ' or '\t')
		{
			indent++;
		}

		var statement = text[indent..];
		if ((length = AccDescr(statement)) > 0)
		{
			kind = Kind.AccDescr;
			length += indent;
			return true;
		}

		if ((length = AccTitle(statement)) > 0)
		{
			kind = Kind.AccTitle;
			length += indent;
			return true;
		}

		if (statement.StartsWith("title", StringComparison.Ordinal))
		{
			kind = Kind.Title;
			length = indent + "title".Length + (statement.Length > 5 && statement[5] is ' ' or '\t' ? LineLength(statement[5..]) : 0);
			return true;
		}

		if (text[0] == '\n' || text[0] == '\r')
		{
			kind = Kind.NewLine;
			length = text.StartsWith("\r\n", StringComparison.Ordinal) ? 2 : 1;
			return true;
		}

		if (indent > 0)
		{
			length = indent;
			return true;
		}

		if (text.StartsWith("---", StringComparison.Ordinal) && (length = Yaml(text)) > 0)
		{
			return true;
		}

		if (text.StartsWith("%%", StringComparison.Ordinal))
		{
			length = DirectiveReader.MatchLength(text);
			if (length == 0)
			{
				var lineEnd = text.IndexOfAny('\r', '\n');
				length = lineEnd < 0 ? text.Length : lineEnd;
			}

			return true;
		}

		return false;
	}

	/// <summary>A keyword followed by <c>(?:(?=%%)|(?!\S))</c>.</summary>
	private static int Keyword(ReadOnlySpan<char> text, string keyword)
	{
		if (!text.StartsWith(keyword, StringComparison.Ordinal))
		{
			return 0;
		}

		var after = text[keyword.Length..];
		return after.IsEmpty || PreambleParser.IsSpace(after[0]) || after.StartsWith("%%", StringComparison.Ordinal) ? keyword.Length : 0;
	}

	/// <summary><c>accDescr(?:[\t ]*:([^\n\r]*?(?=%%)|[^\n\r]*)|\s*{([^}]*)})</c>.</summary>
	private static int AccDescr(ReadOnlySpan<char> text)
	{
		const string Name = "accDescr";
		if (!text.StartsWith(Name, StringComparison.Ordinal))
		{
			return 0;
		}

		var at = Name.Length;
		while (at < text.Length && text[at] is ' ' or '\t')
		{
			at++;
		}

		if (at < text.Length && text[at] == ':')
		{
			return at + 1 + LineLength(text[(at + 1)..]);
		}

		at = Name.Length;
		while (at < text.Length && PreambleParser.IsSpace(text[at]))
		{
			at++;
		}

		if (at == text.Length || text[at] != '{')
		{
			return 0;
		}

		var close = text[at..].IndexOf('}');
		return close < 0 ? 0 : at + close + 1;
	}

	/// <summary><c>accTitle[\t ]*:(?:[^\n\r]*?(?=%%)|[^\n\r]*)</c>.</summary>
	private static int AccTitle(ReadOnlySpan<char> text)
	{
		const string Name = "accTitle";
		if (!text.StartsWith(Name, StringComparison.Ordinal))
		{
			return 0;
		}

		var at = Name.Length;
		while (at < text.Length && text[at] is ' ' or '\t')
		{
			at++;
		}

		return at < text.Length && text[at] == ':' ? at + 1 + LineLength(text[(at + 1)..]) : 0;
	}

	/// <summary><c>[^\n\r]*?(?=%%)|[^\n\r]*</c>: the rest of the line, up to a <c>%%</c> on it.</summary>
	private static int LineLength(ReadOnlySpan<char> text)
	{
		var lineEnd = text.IndexOfAny('\r', '\n');
		var line = lineEnd < 0 ? text : text[..lineEnd];
		var comment = line.IndexOf("%%", StringComparison.Ordinal);
		return comment < 0 ? line.Length : comment;
	}

	/// <summary><c>---[\t ]*\r?\n(?:[\S\s]*?\r?\n)?---(?:\r?\n|(?!\S))</c>.</summary>
	private static int Yaml(ReadOnlySpan<char> text)
	{
		var at = 3;
		while (at < text.Length && text[at] is ' ' or '\t')
		{
			at++;
		}

		if (text[at..].StartsWith("\r\n", StringComparison.Ordinal))
		{
			at += 2;
		}
		else if (at < text.Length && text[at] == '\n')
		{
			at++;
		}
		else
		{
			return 0;
		}

		for (var close = at; close + 3 <= text.Length; close++)
		{
			if ((close == at || text[close - 1] == '\n') && text[close..].StartsWith("---", StringComparison.Ordinal))
			{
				var end = close + 3;
				if (text[end..].StartsWith("\r\n", StringComparison.Ordinal))
				{
					return end + 2;
				}

				if (end == text.Length || PreambleParser.IsSpace(text[end]))
				{
					return end < text.Length && text[end] == '\n' ? end + 1 : end;
				}
			}
		}

		return 0;
	}

	/// <summary><c>CommonValueConverter</c> of a one-line value: trimmed, with runs of spaces and tabs folded to one space.</summary>
	private static string LineValue(ReadOnlySpan<char> text) => FoldSpaces(text.Trim(" \t\r\n\f\v"));

	/// <summary>
	/// <c>CommonValueConverter</c> of <c>accDescr</c>: a one-line value as <see cref="LineValue"/>, and a <c>{ }</c> block
	/// with each line trimmed, runs of spaces and tabs folded and blank lines dropped.
	/// </summary>
	private static string AccDescrValue(ReadOnlySpan<char> text)
	{
		var open = text.IndexOf('{');
		var colon = text.IndexOf(':');
		if (colon >= 0 && (open < 0 || colon < open))
		{
			return LineValue(text[(colon + 1)..]);
		}

		var block = text[(open + 1)..^1];
		var lines = new StringBuilder(block.Length);
		while (true)
		{
			var lineEnd = block.IndexOfAny('\r', '\n');
			var line = (lineEnd < 0 ? block : block[..lineEnd]).Trim(" \t\f\v");
			if (!line.IsEmpty)
			{
				if (lines.Length > 0)
				{
					lines.Append('\n');
				}

				lines.Append(FoldSpaces(line));
			}

			if (lineEnd < 0)
			{
				return lines.ToString();
			}

			block = block[(lineEnd + 1)..];
		}
	}

	private static string FoldSpaces(ReadOnlySpan<char> text)
	{
		var builder = new StringBuilder(text.Length);
		for (var i = 0; i < text.Length; i++)
		{
			if (text[i] is ' ' or '\t' && i + 1 < text.Length && text[i + 1] is ' ' or '\t')
			{
				while (i + 1 < text.Length && text[i + 1] is ' ' or '\t')
				{
					i++;
				}

				builder.Append(' ');
				continue;
			}

			builder.Append(text[i]);
		}

		return builder.ToString();
	}

	private static string Image(ReadOnlySpan<char> source, Token token) =>
		token.Kind == Kind.End ? "EOF" : source.Slice(token.Start, token.Length).ToString();

	private static string Error(ReadOnlySpan<char> source, Token token, string message)
	{
		var (line, column) = Position(source, token.Start);
		return string.Create(CultureInfo.InvariantCulture, $"Parse error on line {line}, column {column}: {message}");
	}

	private static (int Line, int Column) Position(ReadOnlySpan<char> source, int offset) => TextPosition.Of(source, offset);
}
