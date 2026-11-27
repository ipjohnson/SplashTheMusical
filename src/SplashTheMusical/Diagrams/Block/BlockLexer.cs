// Upstream grammar: packages/mermaid/src/diagrams/block/parser/block.jison
using SplashTheMusical.Diagrams.State;
using SplashTheMusical.Parsing;

namespace SplashTheMusical.Diagrams.Block;

/// <summary>The terminals of <c>block.jison</c> that a grammar rule reads, named as the grammar names them.</summary>
internal enum BlockTokenKind : byte
{
	/// <summary>jison's <c>$end</c>, which the lexer returns after <see cref="Eof"/>, or at the end of an exclusive condition.</summary>
	End,

	/// <summary>Where no lexer rule matches. No grammar rule accepts it.</summary>
	Error,

	/// <summary>
	/// A terminal no grammar rule reads: <c>DEFAULT</c>, <c>LINKSTYLE</c>, <c>INTERPOLATE</c>, <c>acc_title</c>,
	/// <c>acc_descr</c>, <c>acc_descr_multiline_value</c>, <c>MD_STR</c>, <c>NODE_DESCR</c> and
	/// <c>DEFAULT_CLASSDEF_ID</c>. The lexer stops after it, because the parser fails on it.
	/// </summary>
	Unread,

	/// <summary><c>EOF</c>, which the <c>&lt;&lt;EOF&gt;&gt;</c> rule returns once at the end of the input.</summary>
	Eof,

	/// <summary><c>BLOCK_DIAGRAM_KEY</c>: <c>block</c> or <c>block-beta</c>.</summary>
	BlockDiagramKey,

	/// <summary><c>id-block</c>: <c>block:</c>.</summary>
	IdBlock,

	/// <summary><c>COLUMNS</c> with a number. The token is the digits.</summary>
	Columns,

	/// <summary><c>COLUMNS</c> of <c>columns auto</c>.</summary>
	ColumnsAuto,

	/// <summary><c>SPACE_BLOCK</c>. The token is the digits of <c>space:3</c>, or empty for <c>space</c>.</summary>
	SpaceBlock,

	/// <summary><c>end</c>.</summary>
	EndKeyword,
	NodeId,
	NodeDstart,
	NodeDend,
	BlockArrowStart,
	BlockArrowEnd,

	/// <summary><c>DIR</c>, without the <c>,</c> and the space after it, as the rule's action leaves it.</summary>
	Dir,

	/// <summary>The text between double quotes, without them.</summary>
	Str,
	Link,
	StartLink,
	LinkLabel,

	/// <summary><c>SIZE</c>. The token is the digits after <c>:</c>.</summary>
	Size,
	ClassDef,
	ClassDefId,
	ClassDefStyleOpts,
	Class,
	ClassEntityIds,
	StyleClass,
	Style,
	StyleEntityIds,
	StyleDefinitionData,
}

/// <summary>A token: its kind and its text in the prepared source.</summary>
internal readonly record struct BlockToken(BlockTokenKind Kind, int Start, int Length)
{
	public ReadOnlySpan<char> Text(ReadOnlySpan<char> text) => text.Slice(Start, Length);
}

/// <summary>
/// The lexer of <c>block.jison</c> at mermaid 12.3.0. jison tries the rules of the current start condition in the
/// order the grammar lists them, takes the first that matches, and adds <c>\b</c> after a rule whose pattern ends in a
/// letter, such as <c>"block"</c> and <c>"columns"\s+"auto"</c>. Each start condition is a method here that checks
/// its rules in that order. Every condition is exclusive, so only the initial one has the <c>&lt;&lt;EOF&gt;&gt;</c>
/// rule, and the end of the input in another gives <c>$end</c>. As jison does, a token read when the input was
/// already used up, such as the empty <c>CLASSDEF_STYLEOPTS</c> of a <c>classDef a</c> at the end, is followed by
/// <c>$end</c>.
/// </summary>
/// <remarks>
/// <c>[\s]+</c> comes before <c>&lt;INITIAL&gt;({CRLF}|{LF})</c>, so the lexer never returns <c>NL</c> and a newline
/// is space like any other. The link rules come after <c>NODE_ID</c>, whose characters include <c>x</c>, <c>o</c>,
/// <c>.</c> and <c>~</c>, so in the initial condition a link never starts with <c>x</c> or <c>o</c> and <c>~~~</c> is
/// a block's id.
/// </remarks>
internal ref struct BlockLexer
{
	private const byte Initial = 0;
	private const byte StringCondition = 1;
	private const byte MarkdownString = 2;
	private const byte Node = 3;
	private const byte BlockArrow = 4;
	private const byte ArrowDirection = 5;
	private const byte LinkLabelCondition = 6;
	private const byte ClassDefCondition = 7;
	private const byte ClassDefIdCondition = 8;
	private const byte ClassCondition = 9;
	private const byte ClassStyleCondition = 10;
	private const byte StyleStatement = 11;
	private const byte StyleDefinition = 12;
	private const byte AccDescrMultiline = 13;

	/// <summary>The starts of a node's brackets, in the grammar's order.</summary>
	private static readonly string[] NodeStarts = ["-)", "(-", "))", ")", "(((", "((", "{{", "{", ">", "([", "(", "[[", "[|", "[(", ")))", "[\\", "[/", "["];

	/// <summary>The ends of a node's brackets, in the grammar's order.</summary>
	private static readonly string[] NodeEnds = ["(((", ")))", "))", "}}", "}", "(-", "-)", "((", "]]", "(", "])", "\\]", "/]", ")]", ")", "]>", "]"];

	/// <summary>The words of the <c>ARROW_DIR</c> rules, in the grammar's order.</summary>
	private static readonly string[] Directions = ["right", "left", "x", "y", "up", "down"];

	private readonly ReadOnlySpan<char> _text;
	private PooledList<byte> _conditions;
	private int _at;
	private bool _done;

	private BlockLexer(ReadOnlySpan<char> text)
	{
		_text = text;
		_conditions = new PooledList<byte>(8);
		_conditions.Add(Initial);
	}

	/// <summary>
	/// Lexes <paramref name="text"/> into <paramref name="tokens"/>, ending with <see cref="BlockTokenKind.End"/>, or
	/// ends them with an <see cref="BlockTokenKind.Error"/> or <see cref="BlockTokenKind.Unread"/> token and
	/// <see cref="BlockTokenKind.End"/> where lexing stops.
	/// </summary>
	public static void Lex(ReadOnlySpan<char> text, ref PooledList<BlockToken> tokens)
	{
		var lexer = new BlockLexer(text);
		try
		{
			while (true)
			{
				var token = lexer.Next();
				tokens.Add(token);
				if (token.Kind is BlockTokenKind.End or BlockTokenKind.Error or BlockTokenKind.Unread)
				{
					if (token.Kind != BlockTokenKind.End)
					{
						tokens.Add(new BlockToken(BlockTokenKind.End, text.Length, 0));
					}

					return;
				}
			}
		}
		finally
		{
			lexer._conditions.Dispose();
		}
	}

	/// <summary>Whether <paramref name="c"/> is one of <c>NODE_ID</c>'s <c>[^\(\[\n\-\)\{\}\s\&lt;\&gt;:=]</c>.</summary>
	public static bool IsNodeIdChar(char c) =>
		c is not ('(' or '[' or '\n' or '-' or ')' or '{' or '}' or '<' or '>' or ':' or '=') && !StateLexer.IsSpace(c);

	/// <summary>The length of the run of <c>\w</c> at <paramref name="at"/>.</summary>
	public static int WordLength(ReadOnlySpan<char> text, int at)
	{
		var end = at;
		while (end < text.Length && StateLexer.IsWordChar(text[end]))
		{
			end++;
		}

		return end - at;
	}

	/// <summary>
	/// The length of <c>(\w+)+((","\s*\w+)*)</c> at the start of <paramref name="text"/>, the ids of a <c>class</c> or
	/// <c>style</c> statement, or 0 where it does not match.
	/// </summary>
	public static int EntityIdsLength(ReadOnlySpan<char> text)
	{
		var end = WordLength(text, 0);
		if (end == 0)
		{
			return 0;
		}

		while (end < text.Length && text[end] == ',')
		{
			var word = SpaceEnd(text, end + 1);
			var length = WordLength(text, word);
			if (length == 0)
			{
				break;
			}

			end = word + length;
		}

		return end;
	}

	private readonly byte Condition => _conditions[^1];

	private void Push(byte condition) => _conditions.Add(condition);

	private void Pop() => _conditions.Truncate(_conditions.Count - 1);

	private BlockToken Next()
	{
		while (true)
		{
			if (_done)
			{
				return new BlockToken(BlockTokenKind.End, _text.Length, 0);
			}

			if (_at >= _text.Length)
			{
				_done = true;
			}

			var start = _at;
			var kind = Condition switch
			{
				Initial => InitialRule(),
				StringCondition => StringRule(),
				MarkdownString => MarkdownRule(),
				Node => NodeRule(),
				BlockArrow => BlockArrowRule(),
				ArrowDirection => DirectionRule(ref start),
				LinkLabelCondition => LinkLabelRule(),
				ClassDefCondition => ClassDefRule(),
				ClassDefIdCondition or ClassStyleCondition or StyleDefinition => RestOfLine(),
				ClassCondition => EntityIds(BlockTokenKind.ClassEntityIds, ClassStyleCondition),
				StyleStatement => EntityIds(BlockTokenKind.StyleEntityIds, StyleDefinition),
				_ => AccDescrRule(),
			};

			// null is a rule that returns no token, such as the space rule or a quote that starts a string.
			if (kind is not { } found)
			{
				continue;
			}

			if (found == BlockTokenKind.Error && _at >= _text.Length)
			{
				return new BlockToken(BlockTokenKind.End, _text.Length, 0);
			}

			return found switch
			{
				BlockTokenKind.Columns => new BlockToken(found, _at - DigitsBefore(_at), DigitsBefore(_at)),
				BlockTokenKind.SpaceBlock => new BlockToken(found, _at - DigitsBefore(_at), DigitsBefore(_at)),
				BlockTokenKind.Size => new BlockToken(found, start + 1, _at - start - 1),
				_ => new BlockToken(found, start, _at - start),
			};
		}
	}

	private readonly int DigitsBefore(int end)
	{
		var at = end;
		while (at > 0 && char.IsAsciiDigit(_text[at - 1]))
		{
			at--;
		}

		return end - at;
	}

	private BlockTokenKind? InitialRule()
	{
		var rest = _text[_at..];
		if (Keyword(rest, "block-beta"))
		{
			_at += 10;
			return BlockTokenKind.BlockDiagramKey;
		}

		if (rest.StartsWith("block:", StringComparison.Ordinal))
		{
			_at += 6;
			return BlockTokenKind.IdBlock;
		}

		if (Keyword(rest, "block"))
		{
			_at += 5;
			return BlockTokenKind.BlockDiagramKey;
		}

		if (!rest.IsEmpty && StateLexer.IsSpace(rest[0]))
		{
			_at = SpaceEnd(_text, _at);
			return null;
		}

		if (rest.StartsWith("columns", StringComparison.Ordinal))
		{
			var space = SpaceEnd(_text, _at + 7);
			if (space > _at + 7)
			{
				var after = _text[space..];
				if (Keyword(after, "auto"))
				{
					_at = space + 4;
					return BlockTokenKind.ColumnsAuto;
				}

				var digits = DigitLength(after);
				if (digits > 0)
				{
					_at = space + digits;
					return BlockTokenKind.Columns;
				}
			}
		}

		if (rest.StartsWith("\"`", StringComparison.Ordinal))
		{
			_at += 2;
			Push(MarkdownString);
			return null;
		}

		if (rest.StartsWith('"'))
		{
			_at++;
			Push(StringCondition);
			return null;
		}

		if (rest.StartsWith("space:", StringComparison.Ordinal) && DigitLength(rest[6..]) is > 0 and var spaceDigits)
		{
			_at += 6 + spaceDigits;
			return BlockTokenKind.SpaceBlock;
		}

		if (Keyword(rest, "space"))
		{
			_at += 5;
			return BlockTokenKind.SpaceBlock;
		}

		if (Keyword(rest, "default") || Keyword(rest, "linkStyle") || Keyword(rest, "interpolate"))
		{
			return BlockTokenKind.Unread;
		}

		if (WordThenSpace(rest, "classDef", out var classDefLength))
		{
			_at += classDefLength;
			Push(ClassDefCondition);
			return BlockTokenKind.ClassDef;
		}

		if (WordThenSpace(rest, "class", out var classLength))
		{
			_at += classLength;
			Push(ClassCondition);
			return BlockTokenKind.Class;
		}

		if (WordThenSpace(rest, "style", out var styleLength))
		{
			_at += styleLength;
			Push(StyleStatement);
			return BlockTokenKind.Style;
		}

		if (AccessibilityRule(rest, out var accessibility))
		{
			return accessibility;
		}

		if (Keyword(rest, "end"))
		{
			_at = SpaceEnd(_text, _at + 3);
			return BlockTokenKind.EndKeyword;
		}

		if (Match(rest, NodeStarts) is > 0 and var nodeStart)
		{
			_at += nodeStart;
			Push(Node);
			return BlockTokenKind.NodeDstart;
		}

		if (rest.StartsWith("<[", StringComparison.Ordinal))
		{
			_at += 2;
			Push(BlockArrow);
			return BlockTokenKind.BlockArrowStart;
		}

		var id = 0;
		while (id < rest.Length && IsNodeIdChar(rest[id]))
		{
			id++;
		}

		if (id > 0)
		{
			_at += id;
			return BlockTokenKind.NodeId;
		}

		if (rest.IsEmpty)
		{
			return BlockTokenKind.Eof;
		}

		if (LinkLength(_text, _at, initial: true, out var startsLabel) is > 0 and var link)
		{
			_at += link;
			if (startsLabel)
			{
				Push(LinkLabelCondition);
				return BlockTokenKind.StartLink;
			}

			return BlockTokenKind.Link;
		}

		if (rest[0] == ':' && DigitLength(rest[1..]) is > 0 and var sizeDigits)
		{
			_at += 1 + sizeDigits;
			return BlockTokenKind.Size;
		}

		return BlockTokenKind.Error;
	}

	/// <summary>
	/// <c>accTitle\s*":"\s*</c> and <c>accDescr\s*":"\s*</c>, which give terminals no grammar rule reads, and
	/// <c>accDescr\s*"{"\s*</c>, which gives none and starts <c>acc_descr_multiline</c>, so <c>accDescr {}</c> is read
	/// as nothing.
	/// </summary>
	private bool AccessibilityRule(ReadOnlySpan<char> rest, out BlockTokenKind? kind)
	{
		kind = null;
		var isTitle = rest.StartsWith("accTitle", StringComparison.Ordinal);
		if (!isTitle && !rest.StartsWith("accDescr", StringComparison.Ordinal))
		{
			return false;
		}

		var after = SpaceEnd(_text, _at + 8);
		if (after < _text.Length && _text[after] == ':')
		{
			kind = BlockTokenKind.Unread;
			return true;
		}

		if (!isTitle && after < _text.Length && _text[after] == '{')
		{
			_at = SpaceEnd(_text, after + 1);
			Push(AccDescrMultiline);
			return true;
		}

		return false;
	}

	/// <summary><c>&lt;acc_descr_multiline&gt;[\}]</c>, which pops, and <c>[^\}]*</c>.</summary>
	private BlockTokenKind? AccDescrRule()
	{
		if (_at < _text.Length && _text[_at] == '}')
		{
			_at++;
			Pop();
			return null;
		}

		return BlockTokenKind.Unread;
	}

	/// <summary><c>&lt;string&gt;["]</c>, which pops, and <c>[^"]*</c>, which can match nothing at the end.</summary>
	private BlockTokenKind? StringRule()
	{
		if (_at < _text.Length && _text[_at] == '"')
		{
			_at++;
			Pop();
			return null;
		}

		var end = _text[_at..].IndexOf('"');
		_at = end < 0 ? _text.Length : _at + end;
		return BlockTokenKind.Str;
	}

	/// <summary><c>&lt;md_string&gt;[^`"]+</c>, whose <c>MD_STR</c> no rule reads, and <c>[`]["]</c>, which pops.</summary>
	private BlockTokenKind? MarkdownRule()
	{
		var rest = _text[_at..];
		if (!rest.IsEmpty && rest[0] is not ('`' or '"'))
		{
			return BlockTokenKind.Unread;
		}

		if (rest.StartsWith("`\"", StringComparison.Ordinal))
		{
			_at += 2;
			Pop();
			return null;
		}

		return BlockTokenKind.Error;
	}

	/// <summary>The ends of a node's brackets, then <c>["][`]</c> and <c>["]</c>.</summary>
	private BlockTokenKind? NodeRule()
	{
		var rest = _text[_at..];
		if (Match(rest, NodeEnds) is > 0 and var end)
		{
			_at += end;
			Pop();
			return BlockTokenKind.NodeDend;
		}

		return QuoteStarts(rest) ? null : BlockTokenKind.Error;
	}

	/// <summary><c>["][`]</c> and <c>["]</c>, which start a string and return no token.</summary>
	private bool QuoteStarts(ReadOnlySpan<char> rest)
	{
		if (rest.StartsWith("\"`", StringComparison.Ordinal))
		{
			_at += 2;
			Push(MarkdownString);
			return true;
		}

		if (rest.StartsWith('"'))
		{
			_at++;
			Push(StringCondition);
			return true;
		}

		return false;
	}

	/// <summary><c>&lt;BLOCK_ARROW&gt;</c>: the quotes, and <c>"]&gt;"\s*"("</c>, which starts <c>ARROW_DIR</c>.</summary>
	private BlockTokenKind? BlockArrowRule()
	{
		var rest = _text[_at..];
		if (QuoteStarts(rest))
		{
			return null;
		}

		if (rest.StartsWith("]>", StringComparison.Ordinal))
		{
			var open = SpaceEnd(_text, _at + 2);
			if (open < _text.Length && _text[open] == '(')
			{
				_at = open + 1;
				Push(ArrowDirection);
				return null;
			}
		}

		return BlockTokenKind.Error;
	}

	/// <summary>
	/// <c>","?\s*right\s*</c> and the other five, whose action takes off <c>^,\s*</c>, and <c>")"\s*</c>, which pops
	/// <c>ARROW_DIR</c> and <c>BLOCK_ARROW</c>. <paramref name="start"/> moves past what the action takes off.
	/// </summary>
	private BlockTokenKind? DirectionRule(ref int start)
	{
		var at = _at;
		if (at < _text.Length && _text[at] == ',')
		{
			at++;
		}

		var word = SpaceEnd(_text, at);
		foreach (var direction in Directions)
		{
			if (_text[word..].StartsWith(direction, StringComparison.Ordinal))
			{
				if (_text[_at] == ',')
				{
					start = word;
				}

				_at = SpaceEnd(_text, word + direction.Length);
				return BlockTokenKind.Dir;
			}
		}

		if (_at < _text.Length && _text[_at] == ')')
		{
			_at = SpaceEnd(_text, _at + 1);
			Pop();
			Pop();
			return BlockTokenKind.BlockArrowEnd;
		}

		return BlockTokenKind.Error;
	}

	/// <summary><c>&lt;LLABEL&gt;</c>: the quotes, the second returning <c>LINK_LABEL</c>, then the first three link rules.</summary>
	private BlockTokenKind? LinkLabelRule()
	{
		var rest = _text[_at..];
		if (rest.StartsWith("\"`", StringComparison.Ordinal))
		{
			_at += 2;
			Push(MarkdownString);
			return null;
		}

		if (rest.StartsWith('"'))
		{
			_at++;
			Push(StringCondition);
			return BlockTokenKind.LinkLabel;
		}

		if (LinkLength(_text, _at, initial: false, out _) is > 0 and var link)
		{
			_at += link;
			Pop();
			return BlockTokenKind.Link;
		}

		return BlockTokenKind.Error;
	}

	/// <summary><c>&lt;CLASSDEF&gt;DEFAULT\s+</c>, whose terminal no rule reads, then <c>\w+\s+</c>.</summary>
	private BlockTokenKind? ClassDefRule()
	{
		var rest = _text[_at..];
		if (WordThenSpace(rest, "DEFAULT", out _))
		{
			return BlockTokenKind.Unread;
		}

		var word = WordLength(_text, _at);
		var space = SpaceEnd(_text, _at + word);
		if (word == 0 || space == _at + word)
		{
			return BlockTokenKind.Error;
		}

		_at = space;
		Pop();
		Push(ClassDefIdCondition);
		return BlockTokenKind.ClassDefId;
	}

	/// <summary><c>[^\n]*</c>, which pops, as the last rule of <c>classDef</c>, <c>class</c> and <c>style</c> does.</summary>
	private BlockTokenKind? RestOfLine()
	{
		var condition = Condition;
		var end = _text[_at..].IndexOf('\n');
		_at = end < 0 ? _text.Length : _at + end;
		Pop();
		return condition switch
		{
			ClassDefIdCondition => BlockTokenKind.ClassDefStyleOpts,
			ClassStyleCondition => BlockTokenKind.StyleClass,
			_ => BlockTokenKind.StyleDefinitionData,
		};
	}

	/// <summary><c>(\w+)+((","\s*\w+)*)</c>, which pops and starts <paramref name="next"/>.</summary>
	private BlockTokenKind? EntityIds(BlockTokenKind kind, byte next)
	{
		var length = EntityIdsLength(_text[_at..]);
		if (length == 0)
		{
			return BlockTokenKind.Error;
		}

		_at += length;
		Pop();
		Push(next);
		return kind;
	}

	/// <summary>
	/// The length of the first of the link rules that matches at <paramref name="at"/>, or 0. In the initial condition
	/// they are <c>LINK</c>'s four and then <c>START_LINK</c>'s three, and in <c>LLABEL</c> the first three.
	/// </summary>
	/// <remarks>
	/// <code>
	/// \s*[xo&lt;]?\-\-+[-xo&gt;]\s*    \s*[xo&lt;]?\=\=+[=xo&gt;]\s*    \s*[xo&lt;]?\-?\.+\-[xo&gt;]?\s*    \s*\~\~[\~]+\s*
	/// \s*[xo&lt;]?\-\-\s*    \s*[xo&lt;]?\=\=\s*    \s*[xo&lt;]?\-\.\s*
	/// </code>
	/// </remarks>
	public static int LinkLength(ReadOnlySpan<char> text, int at, bool initial, out bool startsLabel)
	{
		startsLabel = false;
		var head = SpaceEnd(text, at);
		var body = head < text.Length && text[head] is 'x' or 'o' or '<' ? head + 1 : head;
		int end;
		if ((end = Repeated(text, body, '-')) > 0 || (end = Repeated(text, body, '=')) > 0 || (end = Dotted(text, body)) > 0)
		{
			return SpaceEnd(text, end) - at;
		}

		if (!initial)
		{
			return 0;
		}

		if (text[head..].StartsWith("~~~", StringComparison.Ordinal))
		{
			end = head + 3;
			while (end < text.Length && text[end] == '~')
			{
				end++;
			}

			return SpaceEnd(text, end) - at;
		}

		var rest = text[body..];
		if (rest.StartsWith("--", StringComparison.Ordinal) || rest.StartsWith("==", StringComparison.Ordinal) || rest.StartsWith("-.", StringComparison.Ordinal))
		{
			startsLabel = true;
			return SpaceEnd(text, body + 2) - at;
		}

		return 0;
	}

	/// <summary>The end of <c>\-\-+[-xo&gt;]</c> or <c>\=\=+[=xo&gt;]</c> at <paramref name="at"/>, or 0.</summary>
	private static int Repeated(ReadOnlySpan<char> text, int at, char line)
	{
		var end = at;
		while (end < text.Length && text[end] == line)
		{
			end++;
		}

		var run = end - at;
		if (run >= 2 && end < text.Length && text[end] is 'x' or 'o' or '>')
		{
			return end + 1;
		}

		// The last character of the run is the [-xo>] or [=xo>].
		return run >= 3 ? end : 0;
	}

	/// <summary>The end of <c>\-?\.+\-[xo&gt;]?</c> at <paramref name="at"/>, or 0.</summary>
	private static int Dotted(ReadOnlySpan<char> text, int at)
	{
		var dots = at < text.Length && text[at] == '-' ? at + 1 : at;
		var end = dots;
		while (end < text.Length && text[end] == '.')
		{
			end++;
		}

		if (end == dots || end >= text.Length || text[end] != '-')
		{
			return 0;
		}

		end++;
		return end < text.Length && text[end] is 'x' or 'o' or '>' ? end + 1 : end;
	}

	/// <summary>Whether <paramref name="text"/> starts with <paramref name="word"/> and the <c>\b</c> jison adds after it.</summary>
	private static bool Keyword(ReadOnlySpan<char> text, string word) =>
		text.StartsWith(word, StringComparison.Ordinal) && (text.Length == word.Length || !StateLexer.IsWordChar(text[word.Length]));

	/// <summary>Whether <paramref name="text"/> starts with <paramref name="word"/> and <c>\s+</c>, which has no <c>\b</c>.</summary>
	private static bool WordThenSpace(ReadOnlySpan<char> text, string word, out int length)
	{
		length = 0;
		if (!text.StartsWith(word, StringComparison.Ordinal))
		{
			return false;
		}

		var end = SpaceEnd(text, word.Length);
		if (end == word.Length)
		{
			return false;
		}

		length = end;
		return true;
	}

	/// <summary>The length of the first of <paramref name="options"/> that <paramref name="text"/> starts with, or 0.</summary>
	private static int Match(ReadOnlySpan<char> text, string[] options)
	{
		foreach (var option in options)
		{
			if (text.StartsWith(option, StringComparison.Ordinal))
			{
				return option.Length;
			}
		}

		return 0;
	}

	private static int DigitLength(ReadOnlySpan<char> text)
	{
		var end = 0;
		while (end < text.Length && char.IsAsciiDigit(text[end]))
		{
			end++;
		}

		return end;
	}

	/// <summary>The end of the run of <c>\s</c> at <paramref name="at"/>.</summary>
	public static int SpaceEnd(ReadOnlySpan<char> text, int at)
	{
		while (at < text.Length && StateLexer.IsSpace(text[at]))
		{
			at++;
		}

		return at;
	}
}
