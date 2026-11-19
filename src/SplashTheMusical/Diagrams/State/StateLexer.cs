// Upstream grammar: packages/mermaid/src/diagrams/state/parser/stateDiagram.jison
using System.Buffers;
using System.Runtime.CompilerServices;
using SplashTheMusical.Parsing;

namespace SplashTheMusical.Diagrams.State;

/// <summary>The terminals of <c>stateDiagram.jison</c>, named as the grammar names them.</summary>
internal enum StateTokenKind : byte
{
	/// <summary>The end of the input, jison's <c>$end</c>.</summary>
	End,

	/// <summary>Where no lexer rule matches. No grammar rule accepts it.</summary>
	Error,

	/// <summary>
	/// <c>&lt;STATE&gt;\w+\s+\w+.*?\{</c>, where the lexer throws <c>State name must be a single word</c>. No grammar
	/// rule accepts it.
	/// </summary>
	MultiWordState,
	Nl,
	Sd,
	Click,
	Href,
	String,
	Default,

	/// <summary><c>direction_tb</c> and the other three. <see cref="StateToken.Direction"/> says which.</summary>
	Direction,
	Scale,
	Width,
	AccTitle,
	AccTitleValue,
	AccDescr,
	AccDescrValue,
	AccDescrMultilineValue,
	ClassDef,

	/// <summary><c>DEFAULT_CLASSDEF_ID</c>, which the lexer returns for <c>classDef default</c> and no grammar rule accepts.</summary>
	DefaultClassDefId,
	ClassDefId,
	ClassDefStyleOpts,
	Class,
	ClassEntityIds,
	StyleClass,
	Style,
	StyleIds,
	StyleDefStyleOpts,
	Fork,
	Join,
	Choice,
	StateDescr,
	As,
	Id,
	CompositState,
	StructStart,
	StructStop,
	Note,
	LeftOf,
	RightOf,
	NoteText,
	HideEmpty,
	EdgeState,
	Descr,
	Arrow,
	Concurrent,
	StyleSeparator,
	Invalid,
}

/// <summary>A token: its kind and its text in the prepared source, after jison's actions have cut it.</summary>
internal readonly record struct StateToken(StateTokenKind Kind, int Start, int Length, byte Direction = 0)
{
	public int End => Start + Length;

	public ReadOnlySpan<char> Text(ReadOnlySpan<char> text) => text.Slice(Start, Length);
}

/// <summary>
/// The lexer of <c>stateDiagram.jison</c> at mermaid 12.3.0. jison tries the rules of the current start condition in the
/// order the grammar lists them, takes the first that matches, and adds <c>\b</c> after a rule that ends in a letter.
/// The grammar is <c>case-insensitive</c>. Each start condition is a method here that checks its rules in that order.
/// The conditions <c>ID</c>, <c>LINE</c>, <c>FORK_STATE</c>, <c>ALIAS</c>, <c>STYLE_IDS</c> and
/// <c>STYLEDEF_STYLEOPTS</c> are never entered, so they have no method.
/// </summary>
internal ref struct StateLexer
{
	private const byte Initial = 0;
	private const byte StateCondition = 1;
	private const byte StateString = 2;
	private const byte StateId = 3;
	private const byte ScaleCondition = 4;
	private const byte AccTitleCondition = 5;
	private const byte AccDescrCondition = 6;
	private const byte AccDescrMultiline = 7;
	private const byte ClassDefCondition = 8;
	private const byte ClassDefIdCondition = 9;
	private const byte ClassCondition = 10;
	private const byte ClassStyle = 11;
	private const byte StyleCondition = 12;
	private const byte StyleDefStyles = 13;
	private const byte NoteCondition = 14;
	private const byte NoteId = 15;
	private const byte NoteTextCondition = 16;
	private const byte FloatingNote = 17;
	private const byte FloatingNoteId = 18;
	private const byte Struct = 19;

	private readonly ReadOnlySpan<char> _text;
	private PooledList<byte> _conditions;
	private int _at;
	private bool _done;

	// The end of a line whose rest, from an earlier position, holds no "direction", so no later position on it does.
	private int _noDirectionBefore;

	private StateLexer(ReadOnlySpan<char> text)
	{
		_text = text;
		_conditions = new PooledList<byte>(8);
		_conditions.Add(Initial);
	}

	/// <summary>
	/// Lexes <paramref name="text"/> into <paramref name="tokens"/>, ending with <see cref="StateTokenKind.End"/>, or
	/// ends them with an <see cref="StateTokenKind.Error"/> or <see cref="StateTokenKind.MultiWordState"/> token where
	/// lexing stops.
	/// </summary>
	public static void Lex(ReadOnlySpan<char> text, ref PooledList<StateToken> tokens)
	{
		var lexer = new StateLexer(text);
		try
		{
			while (true)
			{
				var token = lexer.Next();
				tokens.Add(token);
				if (token.Kind is StateTokenKind.End or StateTokenKind.Error or StateTokenKind.MultiWordState)
				{
					if (token.Kind != StateTokenKind.End)
					{
						tokens.Add(new StateToken(StateTokenKind.End, text.Length, 0));
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

	private readonly byte Condition => _conditions[_conditions.Count - 1];

	private void Push(byte condition) => _conditions.Add(condition);

	// jison's popState leaves the first condition in place.
	private void Pop()
	{
		if (_conditions.Count > 1)
		{
			_conditions.Truncate(_conditions.Count - 1);
		}
	}

	/// <summary>
	/// jison's <c>next</c> and <c>lex</c>: rules whose action returns no token are skipped. At the end of the input the
	/// rules are tried once more, so a rule that matches the empty string, such as <c>&lt;&lt;EOF&gt;&gt;</c>, fires
	/// once, and then the input is over.
	/// </summary>
	private StateToken Next()
	{
		while (true)
		{
			if (_done)
			{
				return new StateToken(StateTokenKind.End, _text.Length, 0);
			}

			if (_at >= _text.Length)
			{
				_done = true;
			}

			var before = _at;
			var condition = Condition;
			var depth = _conditions.Count;
			var token = Rule(out var matched);
			if (!matched)
			{
				return _at >= _text.Length ? new StateToken(StateTokenKind.End, _text.Length, 0) : new StateToken(StateTokenKind.Error, _at, 0);
			}

			if (token.Kind is not StateTokenKind.End)
			{
				return token;
			}

			// A skipped empty match that leaves the condition alone would repeat for ever, as it does in jison.
			if (_at == before && Condition == condition && _conditions.Count == depth && !_done)
			{
				return new StateToken(StateTokenKind.Error, _at, 0);
			}
		}
	}

	/// <summary>
	/// The first rule of the current condition that matches at the cursor. A rule that returns no token gives
	/// <see cref="StateTokenKind.End"/> with <paramref name="matched"/> set.
	/// </summary>
	private StateToken Rule(out bool matched)
	{
		matched = true;
		var rest = _text[_at..];
		switch (Condition)
		{
			case Initial:
				return InitialRule(rest, out matched);
			case Struct:
				return StructRule(rest, out matched);
			case StateCondition:
				return StateRule(rest, out matched);
			case StateString:
				if (rest.Length > 0 && rest[0] == '"')
				{
					Pop();
					return Skip(1);
				}

				return Token(StateTokenKind.StateDescr, NotAny(rest, '"'));
			case StateId:
				{
					var length = NotAny(rest, '\n', '{');
					if (!ProcessId(rest, ref length))
					{
						return Skip(length);
					}

					Pop();
					return Token(StateTokenKind.Id, length);
				}

			case ScaleCondition:
				{
					var digits = Digits(rest);
					if (digits > 0)
					{
						return Token(StateTokenKind.Width, digits);
					}

					var space = Spaces(rest);
					if (space > 0 && Keyword(rest[space..], "width"))
					{
						Pop();
						return Skip(space + 5);
					}

					break;
				}

			case AccTitleCondition:
				Pop();
				return Token(StateTokenKind.AccTitleValue, NotAny(rest, '\n'));
			case AccDescrCondition:
				Pop();
				return Token(StateTokenKind.AccDescrValue, NotAny(rest, '\n'));
			case AccDescrMultiline:
				if (rest.Length > 0 && rest[0] == '}')
				{
					Pop();
					return Skip(1);
				}

				return Token(StateTokenKind.AccDescrMultilineValue, NotAny(rest, '}'));
			case ClassDefCondition:
				{
					if (StartsWith(rest, "default") && Spaces(rest[7..]) is > 0 and var space)
					{
						Pop();
						Push(ClassDefIdCondition);
						return Token(StateTokenKind.DefaultClassDefId, 7 + space);
					}

					var word = Word(rest);
					if (word > 0 && Spaces(rest[word..]) is > 0 and var after)
					{
						Pop();
						Push(ClassDefIdCondition);
						return Token(StateTokenKind.ClassDefId, word + after);
					}

					break;
				}

			case ClassDefIdCondition:
				Pop();
				return Token(StateTokenKind.ClassDefStyleOpts, NotAny(rest, '\n'));
			case ClassCondition:
				{
					var length = ClassIds(rest);
					if (length > 0)
					{
						Pop();
						Push(ClassStyle);
						return Token(StateTokenKind.ClassEntityIds, length);
					}

					break;
				}

			case ClassStyle:
				Pop();
				return Token(StateTokenKind.StyleClass, NotAny(rest, '\n'));
			case StyleCondition:
				{
					var ids = 0;
					while (ids < rest.Length && (IsWordChar(rest[ids]) || rest[ids] == ','))
					{
						ids++;
					}

					if (ids > 0 && Spaces(rest[ids..]) is > 0 and var space)
					{
						Pop();
						Push(StyleDefStyles);
						return Token(StateTokenKind.StyleIds, ids + space);
					}

					break;
				}

			case StyleDefStyles:
				Pop();
				return Token(StateTokenKind.StyleDefStyleOpts, NotAny(rest, '\n'));
			case NoteCondition:
				if (Keyword(rest, "left of"))
				{
					Pop();
					Push(NoteId);
					return Token(StateTokenKind.LeftOf, 7);
				}

				if (Keyword(rest, "right of"))
				{
					Pop();
					Push(NoteId);
					return Token(StateTokenKind.RightOf, 8);
				}

				if (rest.Length > 0 && rest[0] == '"')
				{
					Pop();
					Push(FloatingNote);
					return Skip(1);
				}

				break;
			case NoteId:
				{
					var space = Spaces(rest);
					var id = space;
					while (id < rest.Length && rest[id] is not (':' or '\n' or '-') && !IsSpace(rest[id]))
					{
						id++;
					}

					if (id > space)
					{
						if (!ProcessId(rest, ref id))
						{
							return Skip(id);
						}

						Pop();
						Push(NoteTextCondition);
						return Token(StateTokenKind.Id, id);
					}

					break;
				}

			case NoteTextCondition:
				return NoteTextRule(rest, out matched);
			case FloatingNote:
				{
					var space = Spaces(rest);
					if (StartsWith(rest[space..], "as"))
					{
						Pop();
						Push(FloatingNoteId);
						return Token(StateTokenKind.As, space + 2 + Spaces(rest[(space + 2)..]));
					}

					if (rest.Length > 0 && rest[0] == '"')
					{
						return Skip(1);
					}

					return Token(StateTokenKind.NoteText, NotAny(rest, '"'));
				}

			case FloatingNoteId:
				{
					var length = NotAny(rest, '\n');
					if (!ProcessId(rest, ref length))
					{
						return Skip(length);
					}

					Pop();
					return Token(StateTokenKind.Id, length);
				}
		}

		matched = false;
		return default;
	}

	private StateToken InitialRule(ReadOnlySpan<char> rest, out bool matched)
	{
		matched = true;
		if (Keyword(rest, "click"))
		{
			return Token(StateTokenKind.Click, 5);
		}

		if (Keyword(rest, "href"))
		{
			return Token(StateTokenKind.Href, 4);
		}

		if (rest.Length > 0 && rest[0] == '"' && rest[1..].IndexOf('"') is >= 0 and var close)
		{
			return Token(StateTokenKind.String, close + 2);
		}

		if (Keyword(rest, "default"))
		{
			return Token(StateTokenKind.Default, 7);
		}

		if (DirectionRule(rest, out var direction) is > 0 and var directionLength)
		{
			return Token(StateTokenKind.Direction, directionLength, direction);
		}

		if (rest.Length > 0 && rest[0] == '\n')
		{
			var lines = rest.IndexOfAnyExcept('\n');
			return Token(StateTokenKind.Nl, lines < 0 ? rest.Length : lines);
		}

		if (Spaces(rest) is > 0 and var space)
		{
			return Skip(space);
		}

		if (Comment(rest) is > 0 and var comment)
		{
			return Skip(comment);
		}

		if (StartsWith(rest, "scale") && Spaces(rest[5..]) is > 0 and var scaleSpace)
		{
			Push(ScaleCondition);
			return Token(StateTokenKind.Scale, 5 + scaleSpace);
		}

		if (AccessibilityRule(rest, out var accessibility) is > 0 and var accessibilityLength)
		{
			return accessibility == StateTokenKind.End ? Skip(accessibilityLength) : Token(accessibility, accessibilityLength);
		}

		var keyword = StatementKeyword(rest, out var isKeyword);
		if (isKeyword)
		{
			return keyword;
		}

		if (rest.Length > 0 && rest[0] == '{')
		{
			Pop();
			Push(Struct);
			return Token(StateTokenKind.StructStart, 1);
		}

		if (StartsWith(rest, "note") && Spaces(rest[4..]) is > 0 and var noteSpace)
		{
			Push(NoteCondition);
			return Token(StateTokenKind.Note, 4 + noteSpace);
		}

		if (StartsWith(rest, "stateDiagram") && Spaces(rest[12..]) is > 0 and var sdSpace)
		{
			return Token(StateTokenKind.Sd, 12 + sdSpace);
		}

		if (StartsWith(rest, "stateDiagram-v2") && Spaces(rest[15..]) is > 0 and var sd2Space)
		{
			return Token(StateTokenKind.Sd, 15 + sd2Space);
		}

		if (Keyword(rest, "hide empty description"))
		{
			return Token(StateTokenKind.HideEmpty, 22);
		}

		if (StateBodyRule(rest, out var body) is >= 0 and var bodyLength)
		{
			return body == StateTokenKind.End ? Skip(bodyLength) : TokenAt(body, bodyLength);
		}

		if (rest.Length == 0)
		{
			// <<EOF>> returns NL.
			return Token(StateTokenKind.Nl, 0);
		}

		if (rest[0] is not ('\n' or '\r' or '\u2028' or '\u2029'))
		{
			return Token(StateTokenKind.Invalid, 1);
		}

		matched = false;
		return default;
	}

	private StateToken StructRule(ReadOnlySpan<char> rest, out bool matched)
	{
		matched = true;
		if (SameLineSpaces(rest) is > 0 and var space)
		{
			return Skip(space);
		}

		if (Comment(rest) is > 0 and var comment)
		{
			return Skip(comment);
		}

		var keyword = StatementKeyword(rest, out var isKeyword);
		if (isKeyword)
		{
			return keyword;
		}

		if (DirectionRule(rest, out var direction) is > 0 and var directionLength)
		{
			return Token(StateTokenKind.Direction, directionLength, direction);
		}

		if (rest.Length > 0 && rest[0] == '}')
		{
			Pop();
			return Token(StateTokenKind.StructStop, 1);
		}

		if (rest.Length > 0 && rest[0] == '\n')
		{
			return Skip(1);
		}

		if (StartsWith(rest, "note") && Spaces(rest[4..]) is > 0 and var noteSpace)
		{
			Push(NoteCondition);
			return Token(StateTokenKind.Note, 4 + noteSpace);
		}

		if (StateBodyRule(rest, out var body) is >= 0 and var bodyLength)
		{
			return body == StateTokenKind.End ? Skip(bodyLength) : TokenAt(body, bodyLength);
		}

		if (rest.StartsWith("--"))
		{
			return Token(StateTokenKind.Concurrent, 2);
		}

		matched = false;
		return default;
	}

	/// <summary>
	/// The rules <c>classDef\s+</c>, <c>class\s+</c>, <c>style\s+</c> and <c>state\s+</c>, which the initial condition
	/// and <c>struct</c> share, in that order. <c>state</c> pushes <c>STATE</c> and returns no token, which gives
	/// <see cref="StateTokenKind.End"/>.
	/// </summary>
	private StateToken StatementKeyword(ReadOnlySpan<char> rest, out bool matched)
	{
		matched = true;
		if (StartsWith(rest, "classDef") && Spaces(rest[8..]) is > 0 and var classDefSpace)
		{
			Push(ClassDefCondition);
			return Token(StateTokenKind.ClassDef, 8 + classDefSpace);
		}

		if (StartsWith(rest, "class") && Spaces(rest[5..]) is > 0 and var classSpace)
		{
			Push(ClassCondition);
			return Token(StateTokenKind.Class, 5 + classSpace);
		}

		if (StartsWith(rest, "style") && Spaces(rest[5..]) is > 0 and var styleSpace)
		{
			Push(StyleCondition);
			return Token(StateTokenKind.Style, 5 + styleSpace);
		}

		if (StartsWith(rest, "state") && Spaces(rest[5..]) is > 0 and var stateSpace)
		{
			Push(StateCondition);
			return Skip(5 + stateSpace);
		}

		matched = false;
		return default;
	}

	/// <summary>
	/// The rules the initial condition and <c>struct</c> share after <c>note</c>: <c>"[*]"</c>, the id
	/// <c>[^:\n\s\-\{]+</c>, the description <c>\s*":"(?:[^:\n;]|":"[^:\n;])+</c>, <c>--&gt;</c> and, after
	/// <c>--</c> in <c>struct</c>, <c>:::</c>. Returns the length, or -1. <see cref="StateTokenKind.End"/> is an id
	/// that <c>processId</c> drops.
	/// </summary>
	private readonly int StateBodyRule(ReadOnlySpan<char> rest, out StateTokenKind kind)
	{
		kind = StateTokenKind.EdgeState;
		if (rest.StartsWith("[*]"))
		{
			return 3;
		}

		var id = 0;
		while (id < rest.Length && rest[id] is not (':' or '\n' or '-' or '{') && !IsSpace(rest[id]))
		{
			id++;
		}

		if (id > 0)
		{
			kind = ProcessId(rest, ref id) ? StateTokenKind.Id : StateTokenKind.End;
			return id;
		}

		if (Description(rest) is > 0 and var description)
		{
			kind = StateTokenKind.Descr;
			return description;
		}

		if (rest.StartsWith("-->"))
		{
			kind = StateTokenKind.Arrow;
			return 3;
		}

		if (rest.StartsWith(":::"))
		{
			kind = StateTokenKind.StyleSeparator;
			return 3;
		}

		return -1;
	}

	/// <summary><c>\s*":"(?:[^:\n;]|":"[^:\n;])+</c>. The two choices start with different characters, so the loop does not backtrack.</summary>
	private static int Description(ReadOnlySpan<char> rest)
	{
		var at = Spaces(rest);
		if (at >= rest.Length || rest[at] != ':')
		{
			return 0;
		}

		at++;
		var start = at;
		while (at < rest.Length)
		{
			if (rest[at] is not (':' or '\n' or ';'))
			{
				at++;
			}
			else if (rest[at] == ':' && at + 1 < rest.Length && rest[at + 1] is not (':' or '\n' or ';'))
			{
				at += 2;
			}
			else
			{
				break;
			}
		}

		return at > start ? at : 0;
	}

	private StateToken StateRule(ReadOnlySpan<char> rest, out bool matched)
	{
		matched = true;
		if (SameLineSpaces(rest) is > 0 and var space)
		{
			return Skip(space);
		}

		if (Comment(rest) is > 0 and var comment)
		{
			return Skip(comment);
		}

		var line = rest[..LineLength(rest)];
		var marker = line.IndexOf("<<", StringComparison.Ordinal) < 0 && line.IndexOf("[[", StringComparison.Ordinal) < 0
			? null
			: Marker(rest, "<<fork>>", StateTokenKind.Fork)
			?? Marker(rest, "<<join>>", StateTokenKind.Join)
			?? Marker(rest, "<<choice>>", StateTokenKind.Choice)
			?? Marker(rest, "[[fork]]", StateTokenKind.Fork)
			?? Marker(rest, "[[join]]", StateTokenKind.Join)
			?? Marker(rest, "[[choice]]", StateTokenKind.Choice);
		if (marker is { } found)
		{
			return found;
		}

		if (rest.Length > 0 && rest[0] == '"')
		{
			Push(StateString);
			return Skip(1);
		}

		var asSpace = Spaces(rest);
		if (StartsWith(rest[asSpace..], "as") && Spaces(rest[(asSpace + 2)..]) is > 0 and var afterAs)
		{
			Push(StateId);
			return Token(StateTokenKind.As, asSpace + 2 + afterAs);
		}

		if (MultiWord(rest))
		{
			return Token(StateTokenKind.MultiWordState, NotAny(rest, '\n'));
		}

		var composite = 0;
		while (composite < rest.Length && rest[composite] is not ('\n' or '{') && !IsSpace(rest[composite]))
		{
			composite++;
		}

		if (composite > 0)
		{
			return Token(StateTokenKind.CompositState, composite);
		}

		if (rest.Length > 0 && rest[0] == '\n')
		{
			Pop();
			return Skip(1);
		}

		if (rest.Length > 0 && rest[0] == '{')
		{
			Pop();
			Push(Struct);
			return Token(StateTokenKind.StructStart, 1);
		}

		matched = false;
		return default;
	}

	/// <summary>
	/// <c>.*"&lt;&lt;fork&gt;&gt;"</c> and the like: the line up to the last copy of the marker, popping <c>STATE</c>, with
	/// the text before the marker, trimmed, as the token.
	/// </summary>
	private StateToken? Marker(ReadOnlySpan<char> rest, string marker, StateTokenKind kind)
	{
		var line = LineLength(rest);
		var at = rest[..line].LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
		if (at < 0)
		{
			return null;
		}

		Pop();
		var text = rest[..at];
		var start = text.Length - TrimStart(text).Length;
		var token = new StateToken(kind, _at + start, TrimEnd(text).Length - start);
		_at += at + marker.Length;
		return token;
	}

	/// <summary><c>\w+\s+\w+.*?\{</c>: two words, then a <c>{</c> on the second word's line.</summary>
	private static bool MultiWord(ReadOnlySpan<char> rest)
	{
		var at = Word(rest);
		if (at == 0)
		{
			return false;
		}

		var space = Spaces(rest[at..]);
		if (space == 0)
		{
			return false;
		}

		at += space;
		var second = Word(rest[at..]);
		if (second == 0)
		{
			return false;
		}

		at += second;
		var tail = rest[at..];
		return tail[..LineLength(tail)].Contains('{');
	}

	/// <summary>
	/// <c>\s*":"[^:\n;]+</c>, whose action drops the first two characters, and then
	/// <c>[\s\S]*?\n\s*"end note"\b</c>, whose action drops <c>end note</c>. Both trim the text.
	/// </summary>
	private StateToken NoteTextRule(ReadOnlySpan<char> rest, out bool matched)
	{
		matched = true;
		var colon = Spaces(rest);
		if (colon < rest.Length && rest[colon] == ':')
		{
			var end = colon + 1;
			while (end < rest.Length && rest[end] is not (':' or '\n' or ';'))
			{
				end++;
			}

			if (end > colon + 1)
			{
				Pop();
				var text = rest[Math.Min(2, end)..end];
				var start = Math.Min(2, end) + (text.Length - TrimStart(text).Length);
				var token = new StateToken(StateTokenKind.NoteText, _at + start, Math.Max(0, Math.Min(2, end) + TrimEnd(text).Length - start));
				_at += end;
				return token;
			}
		}

		for (var at = rest.IndexOf('\n'); at >= 0; at = rest[(at + 1)..].IndexOf('\n') is >= 0 and var next ? at + 1 + next : -1)
		{
			var keyword = at + 1 + Spaces(rest[(at + 1)..]);
			if (Keyword(rest[keyword..], "end note"))
			{
				Pop();
				var text = rest[..keyword];
				var start = text.Length - TrimStart(text).Length;
				var token = new StateToken(StateTokenKind.NoteText, _at + start, Math.Max(0, TrimEnd(text).Length - start));
				_at += keyword + 8;
				return token;
			}
		}

		matched = false;
		return default;
	}

	/// <summary>
	/// <c>.*direction\s+TB[^\n]*</c>, then <c>BT</c>, <c>RL</c> and <c>LR</c>: a <c>direction</c> on the line, then space,
	/// which may cross lines, then the direction and the rest of its line. <c>.*</c> takes the last such
	/// <c>direction</c>. Returns the length and which direction, 0 to 3 in that order.
	/// </summary>
	private int DirectionRule(ReadOnlySpan<char> rest, out byte direction)
	{
		direction = 0;
		if (_at < _noDirectionBefore)
		{
			return 0;
		}

		var line = rest[..LineLength(rest)];
		if (line.IndexOf("direction", StringComparison.OrdinalIgnoreCase) < 0)
		{
			_noDirectionBefore = _at + line.Length;
			return 0;
		}

		ReadOnlySpan<string> names = ["TB", "BT", "RL", "LR"];
		for (direction = 0; direction < names.Length; direction++)
		{
			for (var at = line.LastIndexOf("direction", StringComparison.OrdinalIgnoreCase); at >= 0; at = line[..at].LastIndexOf("direction", StringComparison.OrdinalIgnoreCase))
			{
				var after = at + 9;
				var space = Spaces(rest[after..]);
				if (space > 0 && StartsWith(rest[(after + space)..], names[direction]))
				{
					var end = after + space + 2;
					return end + NotAny(rest[end..], '\n');
				}
			}
		}

		return 0;
	}

	/// <summary>The rules <c>accTitle\s*":"\s*</c>, <c>accDescr\s*":"\s*</c> and <c>accDescr\s*"{"\s*</c>, which returns no token.</summary>
	private int AccessibilityRule(ReadOnlySpan<char> rest, out StateTokenKind kind)
	{
		kind = StateTokenKind.End;
		var isTitle = StartsWith(rest, "accTitle");
		if (!isTitle && !StartsWith(rest, "accDescr"))
		{
			return 0;
		}

		var at = 8 + Spaces(rest[8..]);
		if (at >= rest.Length)
		{
			return 0;
		}

		if (rest[at] == ':')
		{
			at++;
			at += Spaces(rest[at..]);
			kind = isTitle ? StateTokenKind.AccTitle : StateTokenKind.AccDescr;
			Push(isTitle ? AccTitleCondition : AccDescrCondition);
			return at;
		}

		if (!isTitle && rest[at] == '{')
		{
			at++;
			at += Spaces(rest[at..]);
			Push(AccDescrMultiline);
			return at;
		}

		return 0;
	}

	/// <summary><c>(\w+)+((","\s*\w+)*)</c>.</summary>
	private static int ClassIds(ReadOnlySpan<char> rest)
	{
		var at = Word(rest);
		if (at == 0)
		{
			return 0;
		}

		while (at < rest.Length && rest[at] == ',')
		{
			var next = at + 1 + Spaces(rest[(at + 1)..]);
			var word = Word(rest[next..]);
			if (word == 0)
			{
				break;
			}

			at = next + word;
		}

		return at;
	}

	/// <summary>
	/// <c>processId</c>: an id with <c>%%</c> in it ends before the <c>%%</c>, and the rest is lexed again. An id that
	/// starts with <c>%%</c> is dropped. <paramref name="length"/> counts from the start of <paramref name="rest"/>.
	/// </summary>
	private static bool ProcessId(ReadOnlySpan<char> rest, ref int length)
	{
		var comment = rest[..length].IndexOf("%%", StringComparison.Ordinal);
		if (comment == 0)
		{
			return false;
		}

		if (comment > 0)
		{
			length = comment;
		}

		return true;
	}

	/// <summary><c>\#[^\n]*</c> and <c>\%\%(?!\{)[^\n]*</c>.</summary>
	private static int Comment(ReadOnlySpan<char> rest)
	{
		if (rest.Length > 0 && rest[0] == '#')
		{
			return NotAny(rest, '\n');
		}

		if (rest.StartsWith("%%") && !(rest.Length > 2 && rest[2] == '{'))
		{
			return NotAny(rest, '\n');
		}

		return 0;
	}

	private StateToken Token(StateTokenKind kind, int length, byte direction = 0)
	{
		var token = new StateToken(kind, _at, length, direction);
		_at += length;
		return token;
	}

	private StateToken TokenAt(StateTokenKind kind, int length)
	{
		if (kind == StateTokenKind.Descr)
		{
			// The action trims the description.
			var text = _text.Slice(_at, length);
			var start = text.Length - TrimStart(text).Length;
			var token = new StateToken(kind, _at + start, TrimEnd(text).Length - start);
			_at += length;
			return token;
		}

		return Token(kind, length);
	}

	private StateToken Skip(int length)
	{
		_at += length;
		return default;
	}

	/// <summary>A literal and, as jison adds after a rule that ends in a letter, a word boundary.</summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool Keyword(ReadOnlySpan<char> rest, string keyword) =>
		StartsWith(rest, keyword) && (rest.Length == keyword.Length || !IsWordChar(rest[keyword.Length]));

	// The first character is compared first. Two ASCII characters equal ignoring case are equal after | 0x20, and a
	// non-ASCII one, which may fold to ASCII, goes to the full comparison.
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool StartsWith(ReadOnlySpan<char> rest, string text) =>
		rest.Length >= text.Length
		&& (rest[0] >= '\u0080' || (rest[0] | 0x20) == (text[0] | 0x20))
		&& rest.StartsWith(text, StringComparison.OrdinalIgnoreCase);

	private static int NotAny(ReadOnlySpan<char> rest, char stop)
	{
		var at = rest.IndexOf(stop);
		return at < 0 ? rest.Length : at;
	}

	private static int NotAny(ReadOnlySpan<char> rest, char stop, char other)
	{
		var at = rest.IndexOfAny(stop, other);
		return at < 0 ? rest.Length : at;
	}

	/// <summary>The length of the line: JavaScript's <c>.</c> stops at line feeds and at U+2028 and U+2029.</summary>
	private static int LineLength(ReadOnlySpan<char> rest)
	{
		var at = rest.IndexOfAny(LineEnds);
		return at < 0 ? rest.Length : at;
	}

	private static readonly SearchValues<char> LineEnds = SearchValues.Create(['\n', '\r', (char)0x2028, (char)0x2029]);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int Spaces(ReadOnlySpan<char> rest)
	{
		var at = 0;
		while (at < rest.Length && IsSpace(rest[at]))
		{
			at++;
		}

		return at;
	}

	/// <summary><c>((?!\n)\s)+</c>.</summary>
	private static int SameLineSpaces(ReadOnlySpan<char> rest)
	{
		var at = 0;
		while (at < rest.Length && rest[at] != '\n' && IsSpace(rest[at]))
		{
			at++;
		}

		return at;
	}

	private static int Word(ReadOnlySpan<char> rest)
	{
		var at = 0;
		while (at < rest.Length && IsWordChar(rest[at]))
		{
			at++;
		}

		return at;
	}

	private static int Digits(ReadOnlySpan<char> rest)
	{
		var at = 0;
		while (at < rest.Length && char.IsAsciiDigit(rest[at]))
		{
			at++;
		}

		return at;
	}

	/// <summary>JavaScript's <c>\s</c>.</summary>
	internal static bool IsSpace(char c) => PreambleParser.IsSpace(c);

	/// <summary>JavaScript's <c>\w</c>.</summary>
	internal static bool IsWordChar(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';

	/// <summary>JavaScript's <c>trim</c>, which removes the characters of <c>\s</c>.</summary>
	internal static ReadOnlySpan<char> Trim(ReadOnlySpan<char> text) => TrimEnd(TrimStart(text));

	internal static ReadOnlySpan<char> TrimStart(ReadOnlySpan<char> text)
	{
		var at = 0;
		while (at < text.Length && IsSpace(text[at]))
		{
			at++;
		}

		return text[at..];
	}

	internal static ReadOnlySpan<char> TrimEnd(ReadOnlySpan<char> text)
	{
		var end = text.Length;
		while (end > 0 && IsSpace(text[end - 1]))
		{
			end--;
		}

		return text[..end];
	}
}
