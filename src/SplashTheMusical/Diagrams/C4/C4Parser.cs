// Upstream grammar: packages/mermaid/src/diagrams/c4/parser/c4Diagram.jison
using System.Globalization;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Diagrams.State;
using SplashTheMusical.Parsing;
using Kind = SplashTheMusical.Diagrams.C4.C4TokenKind;

namespace SplashTheMusical.Diagrams.C4;

/// <summary>
/// The parser of <c>c4Diagram.jison</c> at mermaid 12.3.0. It reads the tokens of <see cref="C4Lexer"/> by recursive
/// descent over the grammar:
/// <code>
/// start: mermaidDoc | direction
/// graphConfig: (C4_CONTEXT | C4_CONTAINER | C4_COMPONENT | C4_DYNAMIC | C4_DEPLOYMENT) NEWLINE statements EOF
/// statements: otherStatements | diagramStatements | otherStatements diagramStatements
/// otherStatements: otherStatement | otherStatement NEWLINE | otherStatement NEWLINE otherStatements
/// otherStatement: title | accDescription | acc_title acc_title_value | acc_descr acc_descr_value
///     | acc_descr_multiline_value
/// diagramStatements: diagramStatement | diagramStatement NEWLINE | diagramStatement NEWLINE statements
/// diagramStatement: (PERSON | … | UPDATE_LAYOUT_CONFIG) attributes | boundaryStatement
/// boundaryStatement: boundaryStart (LBRACE NEWLINE | NEWLINE LBRACE | NEWLINE LBRACE NEWLINE) diagramStatements RBRACE
/// boundaryStart: (ENTERPRISE_BOUNDARY | SYSTEM_BOUNDARY | BOUNDARY | CONTAINER_BOUNDARY | NODE | NODE_L | NODE_R) attributes
/// attributes: attribute+
/// attribute: STR | STR_KEY STR_VALUE | ATTRIBUTE | ATTRIBUTE_EMPTY
/// </code>
/// So statements are separated by a line end, except that an <c>otherStatement</c> may run straight into a
/// <c>diagramStatement</c>, and a boundary's braces hold at least one statement, the first a <c>diagramStatement</c>.
/// No grammar rule accepts the direction tokens before <c>$end</c>, and the lexer never returns <c>ATTRIBUTE</c>. The
/// text is prepared as mermaid.js prepares it for every jison diagram, by <see cref="FlowchartText.Prepare"/>. The lexer
/// does not depend on the parser, so the whole text is lexed first.
/// </summary>
internal ref struct C4Parser
{
	private readonly ReadOnlySpan<char> _text;
	private readonly ReadOnlySpan<C4Token> _tokens;
	private readonly C4Document _document;
	private ResourceGuard _guard;
	private SourceCursor _cursor;
	private int _at;
	private int _depth;
	private string? _expected;
	private int _errorAt;

	private PooledStringTable _strings;
	private PooledList<C4Statement> _statements;
	private PooledList<C4Argument> _arguments;
	private PooledList<char> _scratch;

	private C4Parser(ReadOnlySpan<char> text, ReadOnlySpan<C4Token> tokens, DiagramParseContext context, C4Document document)
	{
		_text = text;
		_tokens = tokens;
		_document = document;
		_guard = ResourceGuard.Start(text, context.Limits);
		_cursor = new SourceCursor(text);
	}

	public static C4Document? Parse(ReadOnlySpan<char> source, DiagramParseContext context)
	{
		var body = source[context.BodyStart..];
		var text = new PooledList<char>(body.Length + 16);
		var tokens = new PooledList<C4Token>(64);
		try
		{
			FlowchartText.Prepare(body, ref text, foldSpaceAfterBraces: false);
			C4Lexer.Lex(text.AsSpan(), ref tokens);
			var document = new C4Document();
			var parser = new C4Parser(text.AsSpan(), tokens.AsSpan(), context, document);
			try
			{
				if (!parser.ParseStart())
				{
					parser.Report(source, context);
					return null;
				}

				document.Statements.AddRange(parser._statements.AsSpan());
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
		_strings.Dispose();
		_statements.Dispose();
		_arguments.Dispose();
		_scratch.Dispose();
	}

	private readonly C4Token Current => _tokens[Math.Min(_at, _tokens.Length - 1)];

	private readonly Kind Peek => Current.Kind;

	private C4Token Take()
	{
		var token = Current;
		_at = Math.Min(_at + 1, _tokens.Length - 1);
		return token;
	}

	private static bool IsOther(Kind kind) =>
		kind is Kind.Title or Kind.AccDescription or Kind.AccTitle or Kind.AccDescr or Kind.AccDescrMultilineValue;

	private static bool IsDiagram(Kind kind) => kind is Kind.Element or Kind.Boundary or Kind.Relation or Kind.Update;

	private bool ParseStart()
	{
		if (Peek != Kind.Header)
		{
			return Fail("'C4_CONTEXT', 'C4_CONTAINER', 'C4_COMPONENT', 'C4_DYNAMIC', 'C4_DEPLOYMENT', 'direction_tb', 'direction_bt', 'direction_rl', 'direction_lr'");
		}

		_document.Kind = (C4DiagramKind)Take().Value;
		if (Peek != Kind.Newline)
		{
			return Fail("'NEWLINE'");
		}

		_ = Take();
		if (!ParseStatements(inBoundary: false))
		{
			return false;
		}

		if (Peek != Kind.Eof)
		{
			return Fail("'EOF'");
		}

		_ = Take();
		return Peek == Kind.End || Fail("'$end'");
	}

	/// <summary>
	/// <c>statements</c>, or in a boundary <c>diagramStatements</c>, up to the token after them, which the caller
	/// checks.
	/// </summary>
	private bool ParseStatements(bool inBoundary)
	{
		var first = true;
		while (true)
		{
			if (IsOther(Peek))
			{
				if (first && inBoundary)
				{
					return Fail(DiagramStatementStarts);
				}

				if (!ParseOther())
				{
					return false;
				}

				first = false;
				if (Peek == Kind.Newline)
				{
					_ = Take();
					continue;
				}

				if (!IsDiagram(Peek))
				{
					return true;
				}
			}
			else if (!IsDiagram(Peek))
			{
				return !first || Fail(inBoundary ? DiagramStatementStarts : "a statement");
			}

			if (!ParseDiagram())
			{
				return false;
			}

			first = false;
			_cursor.Advance(Math.Min(Current.Start, _text.Length) - _cursor.Position);
			_guard.Statement(_cursor);
			if (Peek != Kind.Newline)
			{
				return true;
			}

			_ = Take();
		}
	}

	private const string DiagramStatementStarts = "'PERSON', 'SYSTEM', 'BOUNDARY', 'CONTAINER', 'COMPONENT', 'NODE', 'REL', 'UPDATE_EL_STYLE'";

	/// <summary><c>otherStatement</c>: the title and the accessibility description, of which the last one wins.</summary>
	private bool ParseOther()
	{
		var token = Take();
		switch (token.Kind)
		{
			case Kind.Title:
				_document.DiagramTitle = NullIfEmpty(Plain(token.Text(_text)[6..]));
				return true;
			case Kind.AccDescription:
				_document.AccessibilityDescription = NullIfEmpty(Plain(token.Text(_text)[15..]));
				return true;
			case Kind.AccTitle:
				if (Peek != Kind.AccTitleValue)
				{
					return Fail("'acc_title_value'");
				}

				_document.DiagramTitle = NullIfEmpty(Plain(StateLexer.Trim(Take().Text(_text))));
				return true;
			case Kind.AccDescr:
				if (Peek != Kind.AccDescrValue)
				{
					return Fail("'acc_descr_value'");
				}

				_document.AccessibilityDescription = NullIfEmpty(Plain(StateLexer.Trim(Take().Text(_text))));
				return true;
			default:
				_document.AccessibilityDescription = NullIfEmpty(Plain(StateLexer.Trim(token.Text(_text))));
				return true;
		}
	}

	private static string? NullIfEmpty(string text) => text.Length == 0 ? null : text;

	/// <summary>A macro call, with a boundary's braces and the calls inside them.</summary>
	private bool ParseDiagram()
	{
		var macro = Take();
		if (!ParseAttributes(out var arguments))
		{
			return false;
		}

		switch (macro.Kind)
		{
			case Kind.Element:
				_statements.Add(new C4Element((C4ElementKind)macro.Value, arguments));
				return true;
			case Kind.Relation:
				_statements.Add(new C4Relation((C4RelationKind)macro.Value, arguments));
				return true;
			case Kind.Update:
				_statements.Add(new C4Update((C4UpdateKind)macro.Value, arguments));
				return true;
		}

		if (Peek == Kind.LBrace)
		{
			_ = Take();
			if (Peek != Kind.Newline)
			{
				return Fail("'NEWLINE'");
			}

			_ = Take();
		}
		else if (Peek == Kind.Newline)
		{
			_ = Take();
			if (Peek != Kind.LBrace)
			{
				return Fail("'LBRACE'");
			}

			_ = Take();
			if (Peek == Kind.Newline)
			{
				_ = Take();
			}
		}
		else
		{
			return Fail("'LBRACE', 'NEWLINE'");
		}

		_depth++;
		_cursor.Advance(Math.Min(Current.Start, _text.Length) - _cursor.Position);
		_guard.Depth(_depth, _cursor);
		var from = _statements.Count;
		if (!ParseStatements(inBoundary: true))
		{
			return false;
		}

		if (Peek != Kind.RBrace)
		{
			return Fail("'RBRACE'");
		}

		_ = Take();
		_depth--;
		var children = _statements.AsSpan()[from..];
		var statements = new List<C4Statement>(children.Length);
		statements.AddRange(children);
		_statements.Truncate(from);
		_statements.Add(new C4Boundary((C4BoundaryKind)macro.Value, arguments) { Statements = statements });
		return true;
	}

	/// <summary><c>attributes</c>: one or more arguments, each trimmed and interned.</summary>
	private bool ParseAttributes(out List<C4Argument> arguments)
	{
		arguments = null!;
		var from = _arguments.Count;
		while (Current.IsAttribute)
		{
			var token = Take();
			switch (token.Kind)
			{
				case Kind.Str:
					_arguments.Add(new C4Argument(Value(token)));
					break;
				case Kind.StrKey:
					if (Peek != Kind.StrValue)
					{
						_arguments.Truncate(from);
						return Fail("'STR_VALUE'");
					}

					var name = Value(token);
					_arguments.Add(new C4Argument(name, Value(Take())));
					break;
				default:
					_arguments.Add(new C4Argument(""));
					break;
			}
		}

		if (_arguments.Count == from)
		{
			return Fail("'ATTRIBUTE', 'ATTRIBUTE_EMPTY', 'STR', 'STR_KEY'");
		}

		var items = _arguments.AsSpan()[from..];
		arguments = new List<C4Argument>(items.Length);
		arguments.AddRange(items);
		_arguments.Truncate(from);
		return true;
	}

	/// <summary>An argument's text, trimmed as the grammar's actions trim it, interned, with entity placeholders restored.</summary>
	private string Value(C4Token token)
	{
		var text = StateLexer.Trim(token.Text(_text));
		if (!FlowchartText.HasPlaceholder(text))
		{
			return _strings[_strings.GetOrAdd(text)];
		}

		_scratch.Truncate(0);
		FlowchartText.Restore(text, ref _scratch);
		return _strings[_strings.GetOrAdd(_scratch.AsSpan())];
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
