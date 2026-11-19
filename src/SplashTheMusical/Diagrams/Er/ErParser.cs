// Upstream grammar: packages/mermaid/src/diagrams/er/parser/erDiagram.jison
using System.Globalization;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Diagrams.State;
using SplashTheMusical.Parsing;
using Kind = SplashTheMusical.Diagrams.Er.ErTokenKind;

namespace SplashTheMusical.Diagrams.Er;

/// <summary>
/// The parser of <c>erDiagram.jison</c> at mermaid 12.3.0. It reads the tokens of <see cref="ErLexer"/> by recursive
/// descent over the grammar:
/// <code>
/// start: ER_DIAGRAM document EOF
/// document: line*
/// line: statement | NEWLINE | EOF
/// statement: entityName (STYLE_SEPARATOR idList)? relSpec entityName (STYLE_SEPARATOR idList)? COLON role
///     | entityName (SQS entityName SQE)? (STYLE_SEPARATOR idList)? (BLOCK_START attributes? BLOCK_STOP)?
///     | acc_title acc_title_value | acc_descr acc_descr_value | acc_descr_multiline_value | direction
///     | CLASSDEF idList stylesOpt separator | CLASS idList idList | STYLE idList stylesOpt separator
///     | SUBGRAPH entityName (SQS entityName+ SQE)? separator document END
/// idList: (UNICODE_TEXT | STYLE_TEXT) (COMMA (UNICODE_TEXT | STYLE_TEXT))*
/// stylesOpt: style (COMMA style)*
/// style: (STYLE_TEXT | NUM | COLON | BRKT)+
/// separator: SEMI | NEWLINE | EOF
/// entityName: ENTITY_NAME | UNICODE_TEXT | NUM | DECIMAL_NUM | ENTITY_ONE
/// attribute: ATTRIBUTE_WORD '?'? ATTRIBUTE_WORD (ATTRIBUTE_KEY (',' ATTRIBUTE_KEY)*)? COMMENT?
/// relSpec: cardinality relType cardinality
/// role: WORD | ENTITY_NAME | UNICODE_TEXT
/// </code>
/// The grammar's <c>title title_value</c> has no lexer rule, so it never matches. The text is prepared as mermaid.js
/// prepares it for every jison diagram, by <see cref="FlowchartText.Prepare"/>. The lexer does not depend on the
/// parser, so the whole text is lexed first.
/// </summary>
internal ref struct ErParser
{
	private readonly ReadOnlySpan<char> _text;
	private readonly ReadOnlySpan<ErToken> _tokens;
	private readonly ErDocument _document;
	private ResourceGuard _guard;
	private SourceCursor _cursor;
	private int _at;
	private int _depth;
	private string? _expected;
	private int _errorAt;

	private PooledStringTable _ids;
	private PooledList<ErStatement> _statements;
	private PooledList<char> _scratch;

	private ErParser(ReadOnlySpan<char> text, ReadOnlySpan<ErToken> tokens, DiagramParseContext context, ErDocument document)
	{
		_text = text;
		_tokens = tokens;
		_document = document;
		_guard = ResourceGuard.Start(text, context.Limits);
		_cursor = new SourceCursor(text);
	}

	public static ErDocument? Parse(ReadOnlySpan<char> source, DiagramParseContext context)
	{
		var body = source[context.BodyStart..];
		var text = new PooledList<char>(body.Length + 16);
		var tokens = new PooledList<ErToken>(64);
		try
		{
			FlowchartText.Prepare(body, ref text, foldSpaceAfterBraces: false);
			ErLexer.Lex(text.AsSpan(), ref tokens);
			var document = new ErDocument();
			var parser = new ErParser(text.AsSpan(), tokens.AsSpan(), context, document);
			try
			{
				if (!parser.ParseStart())
				{
					parser.Report(source, context);
					return null;
				}
			}
			finally
			{
				parser.Dispose();
			}

			return document;
		}
		finally
		{
			text.Dispose();
			tokens.Dispose();
		}
	}

	private void Dispose()
	{
		_ids.Dispose();
		_statements.Dispose();
		_scratch.Dispose();
	}

	private readonly ErToken Current => _tokens[Math.Min(_at, _tokens.Length - 1)];

	private readonly Kind Peek => Current.Kind;

	private ErToken Take()
	{
		var token = Current;
		_at = Math.Min(_at + 1, _tokens.Length - 1);
		return token;
	}

	private bool ParseStart()
	{
		if (Peek != Kind.ErDiagram)
		{
			return Fail("'ER_DIAGRAM'");
		}

		_ = Take();
		if (!ParseDocument(false, _document.Statements))
		{
			return false;
		}

		_ = Take();
		return Peek == Kind.End || Fail("'EOF'");
	}

	/// <summary>
	/// The lines up to the <c>EOF</c> of the input, or up to the <c>END</c> of a subgraph, added to
	/// <paramref name="statements"/> once they have all parsed. jison takes the last <c>EOF</c> as the end of
	/// <c>start</c>, and inside a subgraph as a line, after which <c>$end</c> comes before <c>END</c>.
	/// </summary>
	private bool ParseDocument(bool inSubgraph, List<ErStatement> statements)
	{
		var frame = _statements.Count;
		while (true)
		{
			switch (Peek)
			{
				case Kind.Newline:
					_ = Take();
					continue;
				case Kind.Eof when !inSubgraph:
				case Kind.EndKeyword when inSubgraph:
					statements.AddRange(_statements.AsSpan()[frame..]);
					_statements.Truncate(frame);
					return true;
				case Kind.Eof:
					_ = Take();
					return Fail("'END'");
			}

			if (!ParseStatement())
			{
				return false;
			}

			_cursor.Advance(Math.Min(Current.Start, _text.Length) - _cursor.Position);
			_guard.Statement(_cursor);
		}
	}

	private bool ParseStatement()
	{
		var token = Current;
		switch (token.Kind)
		{
			case Kind.EntityName or Kind.UnicodeText or Kind.Num or Kind.DecimalNum or Kind.EntityOne:
				return ParseEntity();
			case Kind.AccTitle:
				_ = Take();
				if (Peek != Kind.AccTitleValue)
				{
					return Fail("'acc_title_value'");
				}

				_document.AccessibilityTitle = Plain(StateLexer.Trim(Take().Text(_text)));
				return true;
			case Kind.AccDescr:
				_ = Take();
				if (Peek != Kind.AccDescrValue)
				{
					return Fail("'acc_descr_value'");
				}

				_document.AccessibilityDescription = Plain(StateLexer.Trim(Take().Text(_text)));
				return true;
			case Kind.AccDescrMultilineValue:
				_document.AccessibilityDescription = Plain(StateLexer.Trim(Take().Text(_text)));
				return true;
			case Kind.Direction:
				_ = Take();
				_statements.Add(new ErDirection(token.Value switch
				{
					0 => "TB",
					1 => "BT",
					2 => "RL",
					_ => "LR",
				}));
				return true;
			case Kind.ClassDef or Kind.Style:
				{
					_ = Take();
					var ids = ParseIdList();
					if (ids is null)
					{
						return false;
					}

					var styles = ParseStyles();
					if (styles is null)
					{
						return false;
					}

					if (Peek is not (Kind.Semi or Kind.Newline or Kind.Eof))
					{
						return Fail("'SEMI', 'NEWLINE', 'EOF'");
					}

					_ = Take();
					_statements.Add(token.Kind == Kind.ClassDef ? new ErClassDefinition(ids, styles) : new ErStyle(ids, styles));
					return true;
				}

			case Kind.Class:
				{
					_ = Take();
					var ids = ParseIdList();
					if (ids is null)
					{
						return false;
					}

					var classes = ParseIdList();
					if (classes is null)
					{
						return false;
					}

					_statements.Add(new ErClassAssignment(ids, classes));
					return true;
				}

			case Kind.Subgraph:
				return ParseSubgraph();
			default:
				return Fail(null);
		}
	}

	/// <summary>The statements that start with an <c>entityName</c>: an entity, or a relationship.</summary>
	private bool ParseEntity()
	{
		var name = Name(Take());
		List<string>? classes = null;
		if (Peek == Kind.Sqs)
		{
			_ = Take();
			if (!Current.IsEntityName)
			{
				return Fail("'ENTITY_NAME', 'UNICODE_TEXT', 'NUM', 'DECIMAL_NUM', 'ENTITY_ONE'");
			}

			var alias = Name(Take());
			if (Peek != Kind.Sqe)
			{
				return Fail("'SQE'");
			}

			_ = Take();
			var aliased = new ErEntity(name) { Alias = alias };
			if (Peek == Kind.StyleSeparator)
			{
				_ = Take();
				if ((aliased.Classes = ParseIdList()) is null)
				{
					return false;
				}
			}

			_statements.Add(aliased);
			return Peek != Kind.BlockStart || ParseAttributes(aliased);
		}

		if (Peek == Kind.StyleSeparator)
		{
			_ = Take();
			if ((classes = ParseIdList()) is null)
			{
				return false;
			}
		}

		if (Peek == Kind.Cardinality)
		{
			return ParseRelationship(name, classes);
		}

		var entity = new ErEntity(name) { Classes = classes };
		_statements.Add(entity);
		return Peek != Kind.BlockStart || ParseAttributes(entity);
	}

	/// <summary><c>relSpec entityName (STYLE_SEPARATOR idList)? COLON role</c>, after the first entity.</summary>
	private bool ParseRelationship(string entityA, List<string>? classesA)
	{
		var cardinalityA = (ErCardinality)Take().Value;
		if (Peek != Kind.Identification)
		{
			return Fail("'NON_IDENTIFYING', 'IDENTIFYING'");
		}

		var identifying = Take().Value == 1;
		if (Peek != Kind.Cardinality)
		{
			return Fail("'ZERO_OR_ONE', 'ZERO_OR_MORE', 'ONE_OR_MORE', 'ONLY_ONE', 'MD_PARENT'");
		}

		var cardinalityB = (ErCardinality)Take().Value;
		if (!Current.IsEntityName)
		{
			return Fail("'ENTITY_NAME', 'UNICODE_TEXT', 'NUM', 'DECIMAL_NUM', 'ENTITY_ONE'");
		}

		var entityB = Name(Take());
		List<string>? classesB = null;
		if (Peek == Kind.StyleSeparator)
		{
			_ = Take();
			if ((classesB = ParseIdList()) is null)
			{
				return false;
			}
		}

		if (Peek != Kind.Colon)
		{
			return Fail(classesB is null ? "'STYLE_SEPARATOR', 'COLON'" : "'COMMA', 'COLON'");
		}

		_ = Take();
		var role = Current;
		string text;
		switch (role.Kind)
		{
			case Kind.Word or Kind.EntityName:
				text = Plain(Unquote(role.Text(_text)));
				break;
			case Kind.UnicodeText:
				text = Plain(role.Text(_text));
				break;
			default:
				return Fail("'WORD', 'ENTITY_NAME', 'UNICODE_TEXT'");
		}

		_ = Take();
		_statements.Add(new ErRelationship(entityA, entityB, text)
		{
			CardinalityA = cardinalityA,
			CardinalityB = cardinalityB,
			Identifying = identifying,
			ClassesA = classesA,
			ClassesB = classesB,
		});
		return true;
	}

	/// <summary><c>BLOCK_START attributes? BLOCK_STOP</c>.</summary>
	private bool ParseAttributes(ErEntity entity)
	{
		_ = Take();
		var attributes = new List<ErAttribute>();
		while (Peek != Kind.BlockStop)
		{
			if (Peek != Kind.AttributeWord)
			{
				return Fail(attributes.Count == 0 ? "'ATTRIBUTE_WORD', 'BLOCK_STOP'" : "'ATTRIBUTE_WORD'");
			}

			var type = Take().Text(_text);
			var optional = Peek == Kind.Char && _text[Current.Start] == '?';
			if (optional)
			{
				_ = Take();
			}

			if (Peek != Kind.AttributeWord)
			{
				return Fail(optional ? "'ATTRIBUTE_WORD'" : "'ATTRIBUTE_WORD', '?'");
			}

			var name = Take().Text(_text);
			var attribute = new ErAttribute(optional ? Plain(type, "?") : Plain(type), Plain(name));
			if (Peek == Kind.AttributeKey)
			{
				attribute.Keys.Add(Id(Take().Text(_text)));
				while (Peek == Kind.Char && _text[Current.Start] == ',')
				{
					_ = Take();
					if (Peek != Kind.AttributeKey)
					{
						return Fail("'ATTRIBUTE_KEY'");
					}

					attribute.Keys.Add(Id(Take().Text(_text)));
				}
			}

			if (Peek == Kind.Comment)
			{
				attribute.Comment = Plain(Unquote(Take().Text(_text)));
			}

			attributes.Add(attribute);
			_guard.Statement(_cursor);
		}

		_ = Take();
		entity.Attributes = attributes;
		return true;
	}

	private bool ParseSubgraph()
	{
		_ = Take();
		if (!Current.IsEntityName)
		{
			return Fail("'ENTITY_NAME', 'UNICODE_TEXT', 'NUM', 'DECIMAL_NUM', 'ENTITY_ONE'");
		}

		var subgraph = new ErSubgraph(Name(Take()));
		if (Peek == Kind.Sqs)
		{
			_ = Take();
			if (!Current.IsEntityName)
			{
				return Fail("'ENTITY_NAME', 'UNICODE_TEXT', 'NUM', 'DECIMAL_NUM', 'ENTITY_ONE'");
			}

			_scratch.Truncate(0);
			var first = true;
			while (Current.IsEntityName)
			{
				if (!first)
				{
					_scratch.Add(' ');
				}

				first = false;
				var name = Take();
				var text = name.Text(_text);
				_scratch.AddRange(name.Kind == Kind.EntityName ? text[1..^1] : text);
			}

			if (Peek != Kind.Sqe)
			{
				return Fail("'SQE'");
			}

			_ = Take();
			subgraph.Title = Restored(_scratch.AsSpan().ToString());
		}

		if (Peek is not (Kind.Semi or Kind.Newline or Kind.Eof))
		{
			return Fail("'SEMI', 'NEWLINE', 'EOF'");
		}

		_ = Take();
		_statements.Add(subgraph);
		_depth++;
		_guard.Depth(_depth, _cursor);
		if (!ParseDocument(true, subgraph.Statements))
		{
			return false;
		}

		_depth--;
		_ = Take();
		return true;
	}

	/// <summary><c>idList</c>: names separated by commas.</summary>
	private List<string>? ParseIdList()
	{
		if (Peek is not (Kind.UnicodeText or Kind.StyleText))
		{
			_ = Fail("'UNICODE_TEXT', 'STYLE_TEXT'");
			return null;
		}

		var ids = new List<string> { Id(Take().Text(_text)) };
		while (Peek == Kind.Comma)
		{
			_ = Take();
			if (Peek is not (Kind.UnicodeText or Kind.StyleText))
			{
				_ = Fail("'UNICODE_TEXT', 'STYLE_TEXT'");
				return null;
			}

			ids.Add(Id(Take().Text(_text)));
		}

		return ids;
	}

	/// <summary><c>stylesOpt</c>: each style the text of its components run together.</summary>
	private List<string>? ParseStyles()
	{
		var styles = new List<string>();
		while (true)
		{
			if (!IsStyleComponent(Peek))
			{
				_ = Fail("'STYLE_TEXT', 'NUM', 'COLON', 'BRKT'");
				return null;
			}

			_scratch.Truncate(0);
			while (IsStyleComponent(Peek))
			{
				_scratch.AddRange(Take().Text(_text));
			}

			styles.Add(Restored(_scratch.AsSpan().ToString()));
			if (Peek != Kind.Comma)
			{
				return styles;
			}

			_ = Take();
		}
	}

	private static bool IsStyleComponent(Kind kind) => kind is Kind.StyleText or Kind.Num or Kind.Colon or Kind.Brkt;

	/// <summary>An <c>entityName</c>: the text of the token, without the quotes of an <c>ENTITY_NAME</c>.</summary>
	private string Name(ErToken token)
	{
		var text = token.Text(_text);
		return Id(token.Kind == Kind.EntityName ? text[1..^1] : text);
	}

	/// <summary><c>replace(/"/g, '')</c> on a token that the lexer only lets hold quotes at its ends.</summary>
	private static ReadOnlySpan<char> Unquote(ReadOnlySpan<char> text) => text.Length >= 2 && text[0] == '"' ? text[1..^1] : text;

	/// <summary>An id, interned, with mermaid.js's entity placeholders turned back into the source's text.</summary>
	private string Id(ReadOnlySpan<char> text)
	{
		if (!FlowchartText.HasPlaceholder(text))
		{
			return _ids[_ids.GetOrAdd(text)];
		}

		_scratch.Truncate(0);
		FlowchartText.Restore(text, ref _scratch);
		return _ids[_ids.GetOrAdd(_scratch.AsSpan())];
	}

	/// <summary>Text, with mermaid.js's entity placeholders turned back into the source's text.</summary>
	private string Plain(ReadOnlySpan<char> text)
	{
		if (!FlowchartText.HasPlaceholder(text))
		{
			return text.ToString();
		}

		_scratch.Truncate(0);
		FlowchartText.Restore(text, ref _scratch);
		return _scratch.AsSpan().ToString();
	}

	private string Plain(ReadOnlySpan<char> text, string suffix)
	{
		_scratch.Truncate(0);
		FlowchartText.Restore(text, ref _scratch);
		_scratch.AddRange(suffix);
		return _scratch.AsSpan().ToString();
	}

	private static string Restored(string text)
	{
		if (!FlowchartText.HasPlaceholder(text))
		{
			return text;
		}

		var output = new PooledList<char>(text.Length);
		try
		{
			FlowchartText.Restore(text, ref output);
			return output.AsSpan().ToString();
		}
		finally
		{
			output.Dispose();
		}
	}

	private bool Fail(string? expected)
	{
		if (_expected is null)
		{
			_errorAt = _at;
			_expected = expected ?? "";
		}

		return false;
	}

	private readonly void Report(ReadOnlySpan<char> source, DiagramParseContext context)
	{
		var token = _tokens[Math.Min(_errorAt, _tokens.Length - 1)];
		var offset = Math.Min(token.Start, _text.Length);
		var (line, column) = TextPosition.Of(_text, offset);
		var (bodyLine, _) = TextPosition.Of(source, context.BodyStart);
		var message = token.Kind == Kind.Error
			? string.Create(CultureInfo.InvariantCulture, $"Lexical error on line {line}. Unrecognized text.\n{Excerpt(offset)}")
			: string.Create(
				CultureInfo.InvariantCulture,
				$"Parse error on line {line}:\n{Excerpt(offset)}\nExpecting {(_expected is { Length: > 0 } ? _expected : "a statement")}, got '{token.Kind}'");
		context.AddError(message, line + bodyLine - 1, column);
	}

	/// <summary>jison's <c>showPosition</c>: the line up to the error and a caret under it.</summary>
	private readonly string Excerpt(int offset)
	{
		var lineStart = _text[..offset].LastIndexOf('\n') + 1;
		var lineEnd = _text[offset..].IndexOf('\n');
		var line = _text[lineStart..(lineEnd < 0 ? _text.Length : offset + lineEnd)];
		return string.Concat(line, "\n", new string('-', offset - lineStart), "^");
	}
}
