// Upstream grammar: packages/mermaid/src/diagrams/state/parser/stateDiagram.jison
using System.Globalization;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Parsing;
using Kind = SplashTheMusical.Diagrams.State.StateTokenKind;

namespace SplashTheMusical.Diagrams.State;

/// <summary>
/// The parser of <c>stateDiagram.jison</c> at mermaid 12.3.0. It reads the tokens of <see cref="StateLexer"/> by
/// recursive descent over the grammar:
/// <code>
/// start: NL* SD document
/// document: line*
/// line: statement | NL
/// statement: classDef CLASSDEF_ID CLASSDEF_STYLEOPTS | style STYLE_IDS STYLEDEF_STYLEOPTS
///     | class CLASSENTITY_IDS STYLECLASS | idStatement DESCR? | idStatement '-->' idStatement DESCR?
///     | HIDE_EMPTY | scale WIDTH | COMPOSIT_STATE (STRUCT_START document STRUCT_STOP)?
///     | STATE_DESCR AS ID (STRUCT_START document STRUCT_STOP)? | FORK | JOIN | CHOICE | CONCURRENT
///     | note (left_of | right_of) ID NOTE_TEXT | note NOTE_TEXT AS ID | direction
///     | acc_title acc_title_value | acc_descr acc_descr_value | acc_descr_multiline_value
///     | CLICK idStatement STRING STRING NL | CLICK idStatement HREF STRING NL
/// idStatement: (ID | EDGE_STATE) (STYLE_SEPARATOR ID)?
/// </code>
/// The text is prepared as mermaid.js prepares it for every jison diagram, by <see cref="FlowchartText.Prepare"/>. The
/// lexer does not depend on the parser, so the whole text is lexed first. After the parse, the source is refused where
/// <c>StateDB.extract</c> throws.
/// </summary>
internal ref struct StateParser
{
	private readonly ReadOnlySpan<char> _text;
	private readonly ReadOnlySpan<StateToken> _tokens;
	private readonly StateDocument _document;
	private ResourceGuard _guard;
	private SourceCursor _cursor;
	private int _at;
	private int _depth;
	private string? _expected;
	private int _errorAt;

	private PooledStringTable _ids;
	private PooledList<StateStatement> _statements;
	private PooledList<char> _scratch;

	private StateParser(ReadOnlySpan<char> text, ReadOnlySpan<StateToken> tokens, DiagramParseContext context, StateDocument document)
	{
		_text = text;
		_tokens = tokens;
		_document = document;
		_guard = ResourceGuard.Start(text, context.Limits);
		_cursor = new SourceCursor(text);
	}

	public static StateDocument? Parse(ReadOnlySpan<char> source, DiagramParseContext context)
	{
		var body = source[context.BodyStart..];
		var text = new PooledList<char>(body.Length + 16);
		var tokens = new PooledList<StateToken>(64);
		try
		{
			FlowchartText.Prepare(body, ref text, foldSpaceAfterBraces: false);
			StateLexer.Lex(text.AsSpan(), ref tokens);
			var document = new StateDocument();
			var parser = new StateParser(text.AsSpan(), tokens.AsSpan(), context, document);
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

			if (StateData.MayFail(document) && StateData.From(document, null).Error is { } error)
			{
				var (line, column) = TextPosition.Of(source, context.BodyStart);
				context.AddError(error, line, column);
				return null;
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

	private readonly StateToken Current => _tokens[Math.Min(_at, _tokens.Length - 1)];

	private readonly Kind Peek => Current.Kind;

	private StateToken Take()
	{
		var token = Current;
		_at = Math.Min(_at + 1, _tokens.Length - 1);
		return token;
	}

	private bool ParseStart()
	{
		while (Peek == Kind.Nl)
		{
			_ = Take();
		}

		if (Peek != Kind.Sd)
		{
			return Fail("'SPACE', 'NL', 'SD'");
		}

		_document.Keyword = Plain(StateLexer.Trim(Take().Text(_text)));
		return ParseDocument(false, _document.Statements);
	}

	/// <summary>
	/// The lines up to the end of the input, or up to the <c>}</c> of a composite state, added to
	/// <paramref name="statements"/> once they have all parsed.
	/// </summary>
	private bool ParseDocument(bool inStruct, List<StateStatement> statements)
	{
		var frame = _statements.Count;
		while (true)
		{
			switch (Peek)
			{
				case Kind.Nl:
					_ = Take();
					continue;
				case Kind.StructStop when inStruct:
				case Kind.End when !inStruct:
					statements.AddRange(_statements.AsSpan()[frame..]);
					_statements.Truncate(frame);
					return true;
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
			case Kind.ClassDef:
				{
					_ = Take();
					if (Peek != Kind.ClassDefId)
					{
						return Fail("'DEFAULT', 'CLASSDEF_ID'");
					}

					var name = Id(StateLexer.Trim(Take().Text(_text)));
					if (Peek != Kind.ClassDefStyleOpts)
					{
						return Fail("'CLASSDEF_STYLEOPTS'");
					}

					_statements.Add(new StateClassDefinition(name, Plain(StateLexer.Trim(Take().Text(_text)))));
					return true;
				}

			case Kind.Style:
				{
					_ = Take();
					if (Peek != Kind.StyleIds)
					{
						return Fail("'STYLE_IDS'");
					}

					var ids = Plain(StateLexer.Trim(Take().Text(_text)));
					if (Peek != Kind.StyleDefStyleOpts)
					{
						return Fail("'STYLEDEF_STYLEOPTS'");
					}

					_statements.Add(new StateStyle(ids, Plain(StateLexer.Trim(Take().Text(_text)))));
					return true;
				}

			case Kind.Class:
				{
					_ = Take();
					if (Peek != Kind.ClassEntityIds)
					{
						return Fail("'CLASSENTITY_IDS'");
					}

					var ids = Plain(StateLexer.Trim(Take().Text(_text)));
					if (Peek != Kind.StyleClass)
					{
						return Fail("'STYLECLASS'");
					}

					_statements.Add(new StateClassAssignment(ids, Id(StateLexer.Trim(Take().Text(_text)))));
					return true;
				}

			case Kind.Id or Kind.EdgeState:
				{
					var from = ParseIdStatement();
					if (from is null)
					{
						return false;
					}

					if (Peek == Kind.Arrow)
					{
						_ = Take();
						if (Peek is not (Kind.Id or Kind.EdgeState))
						{
							return Fail("'ID', 'EDGE_STATE'");
						}

						var to = ParseIdStatement();
						if (to is null)
						{
							return false;
						}

						_statements.Add(new StateTransition(from, to) { Label = Peek == Kind.Descr ? Description(Take()) : null });
						return true;
					}

					_statements.Add(new StateDeclaration(from) { Description = Peek == Kind.Descr ? Description(Take()) : null });
					return true;
				}

			case Kind.HideEmpty:
				_ = Take();
				_statements.Add(new StateHideEmptyDescription());
				return true;
			case Kind.Scale:
				_ = Take();
				if (Peek != Kind.Width)
				{
					return Fail("'WIDTH'");
				}

				_statements.Add(new StateScale(Plain(Take().Text(_text))));
				return true;
			case Kind.CompositState:
				{
					var block = new StateBlock(Id(Take().Text(_text)));
					_statements.Add(block);
					return ParseBody(block);
				}

			case Kind.StateDescr:
				{
					var description = Plain(Take().Text(_text));
					if (Peek != Kind.As)
					{
						return Fail("'AS'");
					}

					_ = Take();
					if (Peek != Kind.Id)
					{
						return Fail("'ID'");
					}

					var block = new StateBlock(Id(StateLexer.Trim(Take().Text(_text)))) { Description = description };
					_statements.Add(block);
					return ParseBody(block);
				}

			case Kind.Fork or Kind.Join or Kind.Choice:
				_ = Take();
				_statements.Add(new StateBlock(Id(token.Text(_text)))
				{
					Kind = token.Kind switch
					{
						Kind.Fork => StateKind.Fork,
						Kind.Join => StateKind.Join,
						_ => StateKind.Choice,
					},
				});
				return true;
			case Kind.Concurrent:
				_ = Take();
				_statements.Add(new StateDivider());
				return true;
			case Kind.Note:
				return ParseNote();
			case Kind.Direction:
				_ = Take();
				_statements.Add(new StateDirection(token.Direction switch
				{
					0 => "TB",
					1 => "BT",
					2 => "RL",
					_ => "LR",
				}));
				return true;
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
			case Kind.Click:
				return ParseClick();
			default:
				return Fail(null);
		}
	}

	/// <summary>The optional <c>STRUCT_START document STRUCT_STOP</c> after a <c>state</c> statement.</summary>
	private bool ParseBody(StateBlock block)
	{
		if (Peek != Kind.StructStart)
		{
			return true;
		}

		_ = Take();
		_depth++;
		_guard.Depth(_depth, _cursor);
		var statements = new List<StateStatement>();
		if (!ParseDocument(true, statements))
		{
			return false;
		}

		_depth--;
		_ = Take();
		block.Statements = statements;
		return true;
	}

	/// <summary><c>(ID | EDGE_STATE) (STYLE_SEPARATOR ID)?</c>, with each id trimmed.</summary>
	private StateRef? ParseIdStatement()
	{
		var id = Id(StateLexer.Trim(Take().Text(_text)));
		var state = new StateRef(id);
		if (Peek == Kind.StyleSeparator)
		{
			_ = Take();
			if (Peek != Kind.Id)
			{
				_ = Fail("'ID'");
				return null;
			}

			state.ClassName = Id(StateLexer.Trim(Take().Text(_text)));
		}

		return state;
	}

	private bool ParseNote()
	{
		_ = Take();
		if (Peek is Kind.LeftOf or Kind.RightOf)
		{
			var position = Take().Kind == Kind.LeftOf ? StateNotePosition.LeftOf : StateNotePosition.RightOf;
			if (Peek != Kind.Id)
			{
				return Fail("'ID'");
			}

			var id = Id(StateLexer.Trim(Take().Text(_text)));
			if (Peek != Kind.NoteText)
			{
				return Fail("'NOTE_TEXT'");
			}

			_statements.Add(new StateNote(position, id, Plain(StateLexer.Trim(Take().Text(_text)))));
			return true;
		}

		if (Peek != Kind.NoteText)
		{
			return Fail("'left_of', 'right_of', 'NOTE_TEXT'");
		}

		var text = Plain(StateLexer.Trim(Take().Text(_text)));
		if (Peek != Kind.As)
		{
			return Fail("'AS'");
		}

		_ = Take();
		if (Peek != Kind.Id)
		{
			return Fail("'ID'");
		}

		_statements.Add(new StateFloatingNote(text, Id(StateLexer.Trim(Take().Text(_text)))));
		return true;
	}

	private bool ParseClick()
	{
		_ = Take();
		if (Peek is not (Kind.Id or Kind.EdgeState))
		{
			return Fail("'ID', 'EDGE_STATE'");
		}

		var state = ParseIdStatement();
		if (state is null)
		{
			return false;
		}

		var href = Peek == Kind.Href;
		if (href)
		{
			_ = Take();
		}

		if (Peek != Kind.String)
		{
			return Fail(href ? "'STRING'" : "'STRING', 'HREF'");
		}

		var first = Unquote(Take());
		string? tooltip = null;
		if (!href)
		{
			if (Peek != Kind.String)
			{
				return Fail("'STRING'");
			}

			tooltip = Unquote(Take());
		}

		if (Peek != Kind.Nl)
		{
			return Fail("'NL'");
		}

		_ = Take();
		_statements.Add(new StateClick(state, first) { Tooltip = tooltip });
		return true;
	}

	/// <summary><c>trimColon</c> of a <c>DESCR</c>, which the lexer has trimmed: the text after the colon, trimmed.</summary>
	private string Description(StateToken token)
	{
		var text = token.Text(_text);
		return Plain(text.Length > 0 && text[0] == ':' ? StateLexer.Trim(text[1..]) : text);
	}

	private string Unquote(StateToken token)
	{
		var text = token.Text(_text);
		return Plain(text.Length >= 2 ? text[1..^1] : text);
	}

	/// <summary>An id or class name, interned, with mermaid.js's entity placeholders turned back into the source's text.</summary>
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
		var message = token.Kind switch
		{
			Kind.Error => string.Create(CultureInfo.InvariantCulture, $"Lexical error on line {line}. Unrecognized text.\n{Excerpt(offset)}"),
			Kind.MultiWordState => string.Create(
				CultureInfo.InvariantCulture,
				$"Error: State name must be a single word. Found: \"{StateLexer.Trim(token.Text(_text))}\""),
			_ => string.Create(
				CultureInfo.InvariantCulture,
				$"Parse error on line {line}:\n{Excerpt(offset)}\nExpecting {(_expected is { Length: > 0 } ? _expected : "a statement")}, got '{token.Kind}'"),
		};
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
