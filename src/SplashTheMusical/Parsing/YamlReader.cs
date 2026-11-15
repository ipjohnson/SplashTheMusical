using System.Buffers;
using System.Globalization;
using System.Text;

namespace SplashTheMusical.Parsing;

/// <summary>
/// Reads the YAML that mermaid.js accepts in front matter and in <c>@{ ... }</c> node metadata into a
/// <see cref="MermaidValue"/>. mermaid.js loads both with js-yaml and its <c>JSON_SCHEMA</c>, so plain scalars resolve to
/// null, booleans, numbers and strings by js-yaml's rules.
/// </summary>
/// <remarks>
/// <para>
/// Supported: block mappings, block sequences, a sequence at the same indentation as its key, compact entries such as
/// <c>- key: value</c>, flow mappings and flow sequences that may span lines, plain, single-quoted and double-quoted
/// scalars on one line, literal and folded block scalars with their chomping indicators, and comments.
/// </para>
/// <para>
/// Not supported, each with an error: anchors, aliases, tags, complex keys, YAML directives, document markers, plain and
/// quoted scalars that continue on the next line, single-pair mappings inside a flow sequence, and indentation
/// indicators on block scalars.
/// </para>
/// <para>
/// js-yaml stops at the first error, and so does the reader. Mapping keys are the key's text, where JavaScript would
/// convert a resolved key such as <c>~</c> or <c>0x10</c> back to a string.
/// </para>
/// </remarks>
internal ref struct YamlReader
{
	private static readonly SearchValues<char> BlockPlainStops = SearchValues.Create(":#\r\n");
	private static readonly SearchValues<char> FlowPlainStops = SearchValues.Create(":#\r\n,[]{}");
	private static readonly SearchValues<char> DoubleQuotedStops = SearchValues.Create("\"\\\r\n");
	private static readonly SearchValues<char> SingleQuotedStops = SearchValues.Create("'\r\n");
	private static readonly SearchValues<char> LineBreaks = SearchValues.Create("\r\n");

	private readonly List<MermaidDiagnostic> _diagnostics;
	private readonly int _maxDepth;
	private readonly int _firstLine;
	private readonly int _firstColumn;
	private SourceCursor _cursor;
	private bool _failed;

	private YamlReader(ReadOnlySpan<char> source, List<MermaidDiagnostic> diagnostics, int maxDepth, int firstLine, int firstColumn)
	{
		_cursor = new SourceCursor(source);
		_diagnostics = diagnostics;
		_maxDepth = maxDepth;
		_firstLine = firstLine;
		_firstColumn = firstColumn;
	}

	/// <summary>
	/// Reads one YAML document. An empty document is <see cref="MermaidValue.Null"/>. On an error the result is
	/// <c>null</c> and <paramref name="diagnostics"/> holds the error. <paramref name="firstLine"/> and
	/// <paramref name="firstColumn"/> are the position of <paramref name="source"/> in the diagram, for the diagnostics.
	/// </summary>
	/// <exception cref="MermaidResourceLimitException">The collections nest deeper than <see cref="ResourceLimits.MaxRecursionDepth"/>.</exception>
	public static MermaidValue? Read(
		ReadOnlySpan<char> source,
		List<MermaidDiagnostic> diagnostics,
		ResourceLimits? limits = null,
		int firstLine = 1,
		int firstColumn = 1)
	{
		var reader = new YamlReader(source, diagnostics, (limits ?? ResourceLimits.Default).MaxRecursionDepth, firstLine, firstColumn);
		return reader.ReadDocument();
	}

	private readonly char Current => _cursor.Current;

	private readonly int Indent => _cursor.Column - 1;

	private MermaidValue? ReadDocument()
	{
		if (!SkipBlankLines())
		{
			return _failed ? null : MermaidValue.Null;
		}

		if (Current == '%' && _cursor.Column == 1)
		{
			return Fail("YAML directives are not supported.");
		}

		var value = ParseBlockNode(-1, 0);
		if (!_failed && !_cursor.IsAtEnd)
		{
			FailUnexpected("Unexpected content after the document.");
		}

		return _failed ? null : value;
	}

	/// <summary>A node whose first character is at the cursor, at the start of its line's content.</summary>
	private MermaidValue? ParseBlockNode(int parentIndent, int depth)
	{
		if (IsDocumentMarker())
		{
			return Fail("Document markers are not supported.");
		}

		if (IsSequenceEntry())
		{
			return ParseBlockSequence(depth);
		}

		if (IsMappingEntry())
		{
			return ParseBlockMapping(depth);
		}

		return ParseScalarLine(parentIndent, depth);
	}

	private MermaidValue? ParseBlockSequence(int depth)
	{
		Enter(depth + 1);
		var indent = Indent;
		var items = new List<MermaidValue>();
		while (true)
		{
			_cursor.Advance(1);
			var item = ParseEntryValue(indent, sameIndentSequence: false, compact: true, depth + 1);
			if (item is null)
			{
				return null;
			}

			items.Add(item);
			if (_cursor.IsAtEnd || Indent < indent)
			{
				break;
			}

			if (Indent > indent)
			{
				return Fail("Bad indentation of a sequence entry.");
			}

			if (!IsSequenceEntry())
			{
				break;
			}
		}

		return MermaidValue.FromItems(items);
	}

	private MermaidValue? ParseBlockMapping(int depth)
	{
		Enter(depth + 1);
		var indent = Indent;
		var entries = new OrderedDictionary<string, MermaidValue>();
		while (true)
		{
			var (line, column) = Position();
			var key = ReadMappingKey();
			if (key is null)
			{
				return null;
			}

			var value = ParseEntryValue(indent, sameIndentSequence: true, compact: false, depth + 1);
			if (value is null)
			{
				return null;
			}

			if (!entries.TryAdd(key, value))
			{
				return FailAt(line, column, $"Duplicate mapping key '{key}'.");
			}

			if (_cursor.IsAtEnd || Indent < indent)
			{
				break;
			}

			if (Indent > indent)
			{
				return Fail("Bad indentation of a mapping entry.");
			}

			if (!IsMappingEntry())
			{
				return FailUnexpected("Expected a mapping entry.");
			}
		}

		return MermaidValue.FromEntries(entries);
	}

	/// <summary>
	/// The value after a mapping key's <c>:</c> or a sequence entry's <c>-</c>. It is on the same line, on the lines
	/// below indented more than the parent, or missing, which is null.
	/// </summary>
	private MermaidValue? ParseEntryValue(int parentIndent, bool sameIndentSequence, bool compact, int depth)
	{
		_cursor.SkipWhitespace();
		if (_cursor.IsAtEnd || Current is '\r' or '\n' or '#')
		{
			if (!FinishLine())
			{
				return null;
			}

			if (!SkipBlankLines())
			{
				return _failed ? null : MermaidValue.Null;
			}

			if (Indent > parentIndent || (sameIndentSequence && Indent == parentIndent && IsSequenceEntry()))
			{
				return ParseBlockNode(parentIndent, depth);
			}

			return MermaidValue.Null;
		}

		if (IsSequenceEntry())
		{
			return compact ? ParseBlockSequence(depth) : Fail("A block sequence cannot start on the line of its key.");
		}

		if (compact && IsMappingEntry())
		{
			return ParseBlockMapping(depth);
		}

		return ParseScalarLine(parentIndent, depth);
	}

	/// <summary>A block scalar, or a scalar or flow collection followed by the end of its line.</summary>
	private MermaidValue? ParseScalarLine(int parentIndent, int depth)
	{
		if (Current is '|' or '>')
		{
			return ParseBlockScalar(parentIndent);
		}

		var plain = IsPlainStart(_cursor.Remaining, flow: false);
		var value = ParseInlineValue(depth, flow: false);
		if (value is null || !FinishLine())
		{
			return null;
		}

		if (!SkipBlankLines())
		{
			return _failed ? null : value;
		}

		if (plain && Indent > parentIndent && !IsMappingEntry())
		{
			return Fail("Plain scalars that continue on the next line are not supported.");
		}

		return value;
	}

	private MermaidValue? ParseInlineValue(int depth, bool flow)
	{
		var c = Current;
		switch (c)
		{
			case '[':
			case '{':
				return ParseFlowCollection(depth);
			case '"':
				return ReadDoubleQuoted();
			case '\'':
				return ReadSingleQuoted();
			case '&':
				return Fail("Anchors are not supported.");
			case '*':
				return Fail("Aliases are not supported.");
			case '!':
				return Fail("Tags are not supported.");
			case '?' when IsSpaceOrEnd(_cursor.Peek(1)):
				return Fail("Complex mapping keys are not supported.");
		}

		if (!IsPlainStart(_cursor.Remaining, flow))
		{
			return Fail($"A plain scalar cannot start with '{c}'.");
		}

		var length = PlainLength(_cursor.Remaining, flow);
		var text = _cursor.Remaining[..length].TrimEnd(" \t");
		var value = ResolvePlain(text);
		_cursor.Advance(text.Length);
		return value;
	}

	private MermaidValue? ParseFlowCollection(int depth)
	{
		Enter(depth + 1);
		var (line, column) = Position();
		var mapping = Current == '{';
		var close = mapping ? '}' : ']';
		var items = mapping ? null : new List<MermaidValue>();
		var entries = mapping ? new OrderedDictionary<string, MermaidValue>() : null;
		_cursor.Advance(1);
		while (true)
		{
			SkipFlowSpace();
			if (_cursor.IsAtEnd)
			{
				return FailAt(line, column, $"The flow collection has no closing '{close}'.");
			}

			if (Current == close)
			{
				_cursor.Advance(1);
				break;
			}

			var plain = IsPlainStart(_cursor.Remaining, flow: true);
			if (entries is not null)
			{
				var (keyLine, keyColumn) = Position();
				var key = ReadFlowKey();
				if (key is null)
				{
					return null;
				}

				SkipFlowSpace();
				var value = MermaidValue.Null;
				plain = false;
				if (Current == ':')
				{
					_cursor.Advance(1);
					SkipFlowSpace();
					if (Current != ',' && Current != close && !_cursor.IsAtEnd)
					{
						plain = IsPlainStart(_cursor.Remaining, flow: true);
						var parsed = ParseInlineValue(depth + 1, flow: true);
						if (parsed is null)
						{
							return null;
						}

						value = parsed;
					}
				}

				if (!entries.TryAdd(key, value))
				{
					return FailAt(keyLine, keyColumn, $"Duplicate mapping key '{key}'.");
				}
			}
			else
			{
				var value = ParseInlineValue(depth + 1, flow: true);
				if (value is null)
				{
					return null;
				}

				items!.Add(value);
			}

			var lineBefore = _cursor.Line;
			SkipFlowSpace();
			if (Current == ',')
			{
				_cursor.Advance(1);
				continue;
			}

			if (Current == close || _cursor.IsAtEnd)
			{
				continue;
			}

			if (!mapping && Current == ':')
			{
				return Fail("Single-pair mappings inside a flow sequence are not supported.");
			}

			if (plain && _cursor.Line > lineBefore)
			{
				return Fail("Plain scalars that continue on the next line are not supported.");
			}

			return Fail($"Expected ',' or '{close}'.");
		}

		return entries is not null ? MermaidValue.FromEntries(entries) : MermaidValue.FromItems(items!);
	}

	private string? ReadFlowKey()
	{
		switch (Current)
		{
			case '"':
				return ReadDoubleQuoted()?.String;
			case '\'':
				return ReadSingleQuoted()?.String;
			case '[':
			case '{':
			case '?' when IsSpaceOrEnd(_cursor.Peek(1)):
				Fail("Complex mapping keys are not supported.");
				return null;
		}

		if (!IsPlainStart(_cursor.Remaining, flow: true))
		{
			ParseInlineValue(0, flow: true);
			return null;
		}

		var text = _cursor.Remaining[..PlainLength(_cursor.Remaining, flow: true)].TrimEnd(" \t");
		_cursor.Advance(text.Length);
		return text.ToString();
	}

	/// <summary>Reads a block mapping key and its <c>:</c>. The caller has checked <see cref="IsMappingEntry"/>.</summary>
	private string? ReadMappingKey()
	{
		string key;
		switch (Current)
		{
			case '"':
				var doubleQuoted = ReadDoubleQuoted();
				if (doubleQuoted is null)
				{
					return null;
				}

				key = doubleQuoted.String;
				break;
			case '\'':
				var singleQuoted = ReadSingleQuoted();
				if (singleQuoted is null)
				{
					return null;
				}

				key = singleQuoted.String;
				break;
			default:
				var length = PlainLength(_cursor.Remaining, flow: false);
				key = _cursor.Remaining[..length].TrimEnd(" \t").ToString();
				_cursor.Advance(length);
				break;
		}

		_cursor.SkipWhitespace();
		_cursor.Advance(1);
		return key;
	}

	private MermaidValue? ReadDoubleQuoted()
	{
		var rest = _cursor.Remaining[1..];
		var length = DoubleQuotedLength(rest, out var escaped);
		if (length < 0)
		{
			return length == -1 ? Fail("The quoted scalar has no closing quote.") : Fail("Quoted scalars that continue on the next line are not supported.");
		}

		var text = rest[..length];
		string value;
		if (escaped)
		{
			var unescaped = Unescape(text);
			if (unescaped is null)
			{
				return null;
			}

			value = unescaped;
		}
		else
		{
			value = text.ToString();
		}

		_cursor.Advance(length + 2);
		return MermaidValue.FromString(value);
	}

	private MermaidValue? ReadSingleQuoted()
	{
		var rest = _cursor.Remaining[1..];
		var length = SingleQuotedLength(rest, out var escaped);
		if (length < 0)
		{
			return length == -1 ? Fail("The quoted scalar has no closing quote.") : Fail("Quoted scalars that continue on the next line are not supported.");
		}

		var text = rest[..length].ToString();
		_cursor.Advance(length + 2);
		return MermaidValue.FromString(escaped ? text.Replace("''", "'", StringComparison.Ordinal) : text);
	}

	private string? Unescape(ReadOnlySpan<char> text)
	{
		var builder = new StringBuilder(text.Length);
		for (var i = 0; i < text.Length; i++)
		{
			var c = text[i];
			if (c != '\\')
			{
				builder.Append(c);
				continue;
			}

			var escape = text[++i];
			var simple = escape switch
			{
				'0' => '\0',
				'a' => '\a',
				'b' => '\b',
				't' or '\t' => '\t',
				'n' => '\n',
				'v' => '\v',
				'f' => '\f',
				'r' => '\r',
				'e' => (char)0x1B,
				' ' or '"' or '/' or '\\' => escape,
				'N' => (char)0x85,
				'_' => (char)0xA0,
				'L' => (char)0x2028,
				'P' => (char)0x2029,
				_ => (char)0xFFFF,
			};
			if (simple != (char)0xFFFF)
			{
				builder.Append(simple);
				continue;
			}

			switch (escape)
			{
				case 'x' or 'u' or 'U':
					var digits = escape == 'x' ? 2 : escape == 'u' ? 4 : 8;
					if (i + digits >= text.Length
						|| !int.TryParse(text.Slice(i + 1, digits), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var code)
						|| !Rune.IsValid(code))
					{
						Fail($"The escape sequence '\\{escape}' needs {digits} hexadecimal digits of a valid character.");
						return null;
					}

					builder.Append(new Rune(code).ToString());
					i += digits;
					break;
				default:
					Fail($"Unknown escape sequence '\\{escape}'.");
					return null;
			}
		}

		return builder.ToString();
	}

	/// <summary>A <c>|</c> literal or <c>&gt;</c> folded block scalar, read by YAML's indentation and chomping rules.</summary>
	private MermaidValue? ParseBlockScalar(int parentIndent)
	{
		var literal = Current == '|';
		_cursor.Advance(1);
		var chomping = 0;
		for (var i = 0; i < 2; i++)
		{
			var c = Current;
			if (c is '-' or '+' && chomping == 0)
			{
				chomping = c == '-' ? -1 : 1;
				_cursor.Advance(1);
			}
			else if (char.IsAsciiDigit(c))
			{
				return Fail("Indentation indicators on block scalars are not supported.");
			}
		}

		if (Current is not (' ' or '\t' or '\r' or '\n' or '#') && !_cursor.IsAtEnd)
		{
			return Fail($"Unexpected '{Current}' in a block scalar header.");
		}

		if (!FinishLine())
		{
			return null;
		}

		var text = new StringBuilder();
		var contentIndent = -1;
		var emptyLines = 0;
		var hasContent = false;
		var previousMoreIndented = false;
		while (!_cursor.IsAtEnd)
		{
			var rest = _cursor.Remaining;
			var lineEnd = rest.IndexOfAny(LineBreaks);
			if (lineEnd < 0)
			{
				lineEnd = rest.Length;
			}

			var spaces = rest[..lineEnd].IndexOfAnyExcept(' ');
			if (spaces < 0)
			{
				emptyLines++;
				_cursor.Advance(lineEnd);
				_cursor.SkipLineBreak();
				continue;
			}

			if (contentIndent < 0)
			{
				if (spaces <= parentIndent)
				{
					break;
				}

				contentIndent = spaces;
			}
			else if (spaces < contentIndent)
			{
				break;
			}

			var line = rest[contentIndent..lineEnd];
			var moreIndented = line[0] is ' ' or '\t';
			if (!hasContent)
			{
				text.Append('\n', emptyLines);
			}
			else if (literal || previousMoreIndented || moreIndented)
			{
				text.Append('\n', emptyLines + 1);
			}
			else if (emptyLines == 0)
			{
				text.Append(' ');
			}
			else
			{
				text.Append('\n', emptyLines);
			}

			text.Append(line);
			hasContent = true;
			previousMoreIndented = moreIndented;
			emptyLines = 0;
			_cursor.Advance(lineEnd);
			_cursor.SkipLineBreak();
		}

		if (hasContent && chomping >= 0)
		{
			text.Append('\n');
		}

		if (chomping > 0)
		{
			text.Append('\n', emptyLines);
		}

		var value = MermaidValue.FromString(text.ToString());
		SkipBlankLines();
		return _failed ? null : value;
	}

	/// <summary>Moves past the rest of the line, which may hold a comment and nothing else.</summary>
	private bool FinishLine()
	{
		_cursor.SkipWhitespace();
		if (Current == '#' && IsAfterSpace())
		{
			_cursor.ReadToEndOfLine(TokenKind.Comment);
		}

		if (_cursor.IsAtEnd || _cursor.SkipLineBreak())
		{
			return true;
		}

		if (Current == ':')
		{
			Fail("A mapping value is not allowed here.");
		}
		else
		{
			FailUnexpected("Unexpected content after the value.");
		}

		return false;
	}

	/// <summary>
	/// Skips blank lines and comment lines from the start of a line. It stops at the first character of the next line
	/// with content and returns <c>true</c>, or at the end and returns <c>false</c>.
	/// </summary>
	private bool SkipBlankLines()
	{
		while (!_cursor.IsAtEnd)
		{
			var lineStart = _cursor.Position;
			_cursor.SkipWhitespace();
			if (Current == '#')
			{
				_cursor.ReadToEndOfLine(TokenKind.Comment);
			}

			if (_cursor.SkipLineBreak())
			{
				continue;
			}

			if (_cursor.IsAtEnd)
			{
				return false;
			}

			if (_cursor.Source[lineStart.._cursor.Position].Contains('\t'))
			{
				Fail("Tabs cannot be used for indentation.");
				return false;
			}

			return true;
		}

		return false;
	}

	/// <summary>Skips spaces, tabs, line breaks and comments inside a flow collection.</summary>
	private void SkipFlowSpace()
	{
		while (true)
		{
			_cursor.SkipWhitespaceAndLineBreaks();
			if (Current != '#' || !IsAfterSpace())
			{
				return;
			}

			_cursor.ReadToEndOfLine(TokenKind.Comment);
		}
	}

	private readonly bool IsAfterSpace()
	{
		var position = _cursor.Position;
		return position == 0 || _cursor.Source[position - 1] is ' ' or '\t' or '\r' or '\n';
	}

	private readonly bool IsSequenceEntry() => Current == '-' && IsSpaceOrEnd(_cursor.Peek(1));

	private readonly bool IsDocumentMarker()
	{
		var rest = _cursor.Remaining;
		return _cursor.Column == 1
			&& (rest.StartsWith("---", StringComparison.Ordinal) || rest.StartsWith("...", StringComparison.Ordinal))
			&& IsSpaceOrEnd(_cursor.Peek(3));
	}

	/// <summary>Whether the line at the cursor is a <c>key: value</c> entry with a plain or quoted key.</summary>
	private readonly bool IsMappingEntry()
	{
		var rest = _cursor.Remaining;
		if (rest.IsEmpty)
		{
			return false;
		}

		int end;
		switch (rest[0])
		{
			case '"':
				end = DoubleQuotedLength(rest[1..], out _);
				break;
			case '\'':
				end = SingleQuotedLength(rest[1..], out _);
				break;
			default:
				if (!IsPlainStart(rest, flow: false))
				{
					return false;
				}

				end = PlainLength(rest, flow: false);
				return end < rest.Length && rest[end] == ':';
		}

		if (end < 0)
		{
			return false;
		}

		var after = rest[(end + 2)..];
		var colon = after.IndexOfAnyExcept(" \t");
		return colon >= 0 && after[colon] == ':' && (colon + 1 == after.Length || IsSpaceOrEnd(after[colon + 1]));
	}

	private readonly (int Line, int Column) Position() =>
		(_cursor.Line + _firstLine - 1, _cursor.Line == 1 ? _cursor.Column + _firstColumn - 1 : _cursor.Column);

	private readonly void Enter(int depth)
	{
		if (depth > _maxDepth)
		{
			var (line, column) = Position();
			throw new MermaidResourceLimitException(nameof(ResourceLimits.MaxRecursionDepth), depth, _maxDepth, line, column);
		}
	}

	private MermaidValue? FailUnexpected(string message) =>
		IsDocumentMarker() ? Fail("Document markers are not supported.") : Fail(message);

	private MermaidValue? Fail(string message)
	{
		var (line, column) = Position();
		return FailAt(line, column, message);
	}

	/// <summary>Records the first error and moves to the end, so every caller unwinds.</summary>
	private MermaidValue? FailAt(int line, int column, string message)
	{
		if (!_failed)
		{
			_failed = true;
			_diagnostics.Add(new MermaidDiagnostic(DiagnosticSeverity.Error, message, line, column));
		}

		_cursor.Advance(_cursor.Source.Length - _cursor.Position);
		return null;
	}

	private static bool IsSpaceOrEnd(char c) => c is ' ' or '\t' or '\r' or '\n' or '\0';

	private static bool IsFlowIndicator(char c) => c is ',' or '[' or ']' or '{' or '}';

	/// <summary>Whether a plain scalar can start at <paramref name="rest"/>, by js-yaml's rules.</summary>
	private static bool IsPlainStart(ReadOnlySpan<char> rest, bool flow)
	{
		if (rest.IsEmpty)
		{
			return false;
		}

		switch (rest[0])
		{
			case ' ' or '\t' or '\r' or '\n':
			case ',' or '[' or ']' or '{' or '}':
			case '#' or '&' or '*' or '!' or '|' or '>' or '\'' or '"' or '%' or '@' or '`':
				return false;
			case '-' or '?' or ':':
				var next = rest.Length > 1 ? rest[1] : '\0';
				return !IsSpaceOrEnd(next) && !(flow && IsFlowIndicator(next));
			default:
				return true;
		}
	}

	/// <summary>
	/// The length of the plain scalar at the start of <paramref name="rest"/>, with any trailing spaces. It stops at a line
	/// break, at <c>: </c>, at <c> #</c> and, in a flow collection, at a flow indicator.
	/// </summary>
	private static int PlainLength(ReadOnlySpan<char> rest, bool flow)
	{
		var stops = flow ? FlowPlainStops : BlockPlainStops;
		var offset = 0;
		while (true)
		{
			var found = rest[offset..].IndexOfAny(stops);
			if (found < 0)
			{
				return rest.Length;
			}

			var at = offset + found;
			var c = rest[at];
			var next = at + 1 < rest.Length ? rest[at + 1] : '\0';
			var stop = c switch
			{
				':' => IsSpaceOrEnd(next) || (flow && IsFlowIndicator(next)),
				'#' => at > 0 && rest[at - 1] is ' ' or '\t',
				_ => true,
			};
			if (stop)
			{
				return at;
			}

			offset = at + 1;
		}
	}

	/// <summary>
	/// The length of a double-quoted scalar's text, after its opening quote. -1 when it has no closing quote, -2 when it
	/// reaches a line break first.
	/// </summary>
	private static int DoubleQuotedLength(ReadOnlySpan<char> rest, out bool escaped)
	{
		escaped = false;
		var offset = 0;
		while (true)
		{
			var found = rest[offset..].IndexOfAny(DoubleQuotedStops);
			if (found < 0)
			{
				return -1;
			}

			var at = offset + found;
			switch (rest[at])
			{
				case '"':
					return at;
				case '\\':
					escaped = true;
					if (at + 1 >= rest.Length)
					{
						return -1;
					}

					if (rest[at + 1] is '\r' or '\n')
					{
						return -2;
					}

					offset = at + 2;
					break;
				default:
					return -2;
			}
		}
	}

	/// <summary>
	/// The length of a single-quoted scalar's text, after its opening quote. -1 when it has no closing quote, -2 when it
	/// reaches a line break first.
	/// </summary>
	private static int SingleQuotedLength(ReadOnlySpan<char> rest, out bool escaped)
	{
		escaped = false;
		var offset = 0;
		while (true)
		{
			var found = rest[offset..].IndexOfAny(SingleQuotedStops);
			if (found < 0)
			{
				return -1;
			}

			var at = offset + found;
			if (rest[at] != '\'')
			{
				return -2;
			}

			if (at + 1 < rest.Length && rest[at + 1] == '\'')
			{
				escaped = true;
				offset = at + 2;
				continue;
			}

			return at;
		}
	}

	/// <summary>Resolves a plain scalar by js-yaml's <c>JSON_SCHEMA</c>: null, boolean, integer, float, then string.</summary>
	internal static MermaidValue ResolvePlain(ReadOnlySpan<char> text)
	{
		switch (text)
		{
			case "" or "~" or "null" or "Null" or "NULL":
				return MermaidValue.Null;
			case "true" or "True" or "TRUE":
				return MermaidValue.True;
			case "false" or "False" or "FALSE":
				return MermaidValue.False;
			case ".inf" or ".Inf" or ".INF" or "+.inf" or "+.Inf" or "+.INF":
				return MermaidValue.FromNumber(double.PositiveInfinity);
			case "-.inf" or "-.Inf" or "-.INF":
				return MermaidValue.FromNumber(double.NegativeInfinity);
			case ".nan" or ".NaN" or ".NAN":
				return MermaidValue.FromNumber(double.NaN);
		}

		return TryParseInteger(text, out var number) || TryParseFloat(text, out number)
			? MermaidValue.FromNumber(number)
			: MermaidValue.FromString(text.ToString());
	}

	/// <summary>js-yaml's integer: decimal, <c>0b</c>, <c>0o</c> or <c>0x</c>, with an optional sign and <c>_</c> separators.</summary>
	private static bool TryParseInteger(ReadOnlySpan<char> text, out double value)
	{
		value = 0;
		var negative = false;
		var digits = text;
		if (!digits.IsEmpty && digits[0] is '-' or '+')
		{
			negative = digits[0] == '-';
			digits = digits[1..];
		}

		if (digits.IsEmpty)
		{
			return false;
		}

		var radix = 10;
		if (digits.Length > 1 && digits[0] == '0' && digits[1] is 'b' or 'o' or 'x')
		{
			radix = digits[1] switch
			{
				'b' => 2,
				'o' => 8,
				_ => 16,
			};
			digits = digits[2..];
		}
		else if (digits[0] == '_')
		{
			return false;
		}

		if (digits.IsEmpty || digits[^1] == '_')
		{
			return false;
		}

		var hasDigits = false;
		foreach (var c in digits)
		{
			if (c == '_')
			{
				continue;
			}

			var digit = char.IsAsciiDigit(c) ? c - '0' : char.IsAsciiHexDigit(c) ? (c | 0x20) - 'a' + 10 : 99;
			if (digit >= radix)
			{
				return false;
			}

			value = (value * radix) + digit;
			hasDigits = true;
		}

		if (!hasDigits)
		{
			return false;
		}

		if (radix == 10)
		{
			value = ParseDouble(digits);
		}

		value = negative ? -value : value;
		return true;
	}

	/// <summary>
	/// js-yaml's float: <c>[-+]?[0-9][0-9_]*(\.[0-9_]*)?([eE][-+]?[0-9]+)?</c> or <c>\.[0-9_]+([eE][-+]?[0-9]+)?</c>,
	/// not ending in <c>_</c>. The infinities and NaN are matched before this.
	/// </summary>
	private static bool TryParseFloat(ReadOnlySpan<char> text, out double value)
	{
		value = 0;
		if (text.IsEmpty || text[^1] == '_')
		{
			return false;
		}

		var i = 0;
		if (text[0] == '.')
		{
			i = SkipDigits(text, 1, underscores: true);
			if (i == 1)
			{
				return false;
			}
		}
		else
		{
			if (text[0] is '-' or '+')
			{
				i++;
			}

			if (i >= text.Length || !char.IsAsciiDigit(text[i]))
			{
				return false;
			}

			i = SkipDigits(text, i + 1, underscores: true);
			if (i < text.Length && text[i] == '.')
			{
				i = SkipDigits(text, i + 1, underscores: true);
			}
		}

		if (i < text.Length && text[i] is 'e' or 'E')
		{
			i++;
			if (i < text.Length && text[i] is '-' or '+')
			{
				i++;
			}

			var exponent = i;
			i = SkipDigits(text, i, underscores: false);
			if (i == exponent)
			{
				return false;
			}
		}

		if (i != text.Length)
		{
			return false;
		}

		value = ParseDouble(text);
		return true;
	}

	private static int SkipDigits(ReadOnlySpan<char> text, int start, bool underscores)
	{
		var i = start;
		while (i < text.Length && (char.IsAsciiDigit(text[i]) || (underscores && text[i] == '_')))
		{
			i++;
		}

		return i;
	}

	/// <summary>Parses decimal text without its <c>_</c> separators, as JavaScript's <c>parseFloat</c> would. NaN when it cannot.</summary>
	private static double ParseDouble(ReadOnlySpan<char> text)
	{
		if (!text.Contains('_'))
		{
			return Parse(text);
		}

		var buffer = text.Length <= 128 ? stackalloc char[text.Length] : new char[text.Length];
		var length = 0;
		foreach (var c in text)
		{
			if (c != '_')
			{
				buffer[length++] = c;
			}
		}

		return Parse(buffer[..length]);

		static double Parse(ReadOnlySpan<char> digits) =>
			double.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : double.NaN;
	}
}
