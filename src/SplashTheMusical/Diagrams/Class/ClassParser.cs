// Upstream grammar: packages/mermaid/src/diagrams/class/parser/classDiagram.jison
using System.Globalization;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Parsing;
using Kind = SplashTheMusical.Diagrams.Class.ClassTokenKind;

namespace SplashTheMusical.Diagrams.Class;

/// <summary>
/// The parser of <c>classDiagram.jison</c> at mermaid 12.3.0. It reads the tokens of <see cref="ClassLexer"/> by
/// recursive descent over the grammar:
/// <code>
/// start: CLASS_DIAGRAM NEWLINE statements EOF
/// statements: statement (NEWLINE statement)* NEWLINE?
/// statement: className relation className LABEL? (with a STR before and after the relation)
///     | className LABEL? | namespaceStatement | classStatement | ANNOTATION_START alphaNumToken ANNOTATION_END className
///     | clickStatement | STYLE ALPHA stylesOpt | CSSCLASS STR ALPHA | noteStatement | CLASSDEF classList stylesOpt
///     | direction | acc_title acc_title_value | acc_descr acc_descr_value | acc_descr_multiline_value
/// namespaceStatement: NAMESPACE namespaceName classLabel? STRUCT_START NEWLINE? classStatements STRUCT_STOP
/// classStatements: (classStatement | noteStatement | namespaceStatement) (NEWLINE ...)* NEWLINE?
/// classStatement: CLASS className classLabel? (STYLE_SEPARATOR alphaNumToken (STRUCT_START MEMBER+ STRUCT_STOP)?
///     | ANNOTATION_START alphaNumToken ANNOTATION_END (STRUCT_START MEMBER* STRUCT_STOP)? | STRUCT_START MEMBER* STRUCT_STOP)?
/// className: (alphaNumToken DOT?)* (alphaNumToken | BQUOTE_STR) GENERICTYPE?, with DOT before another name only
/// relation: relationType? lineType relationType?
/// noteStatement: NOTE_FOR className STR | NOTE STR
/// </code>
/// The text is prepared as mermaid.js prepares it for every jison diagram, by <see cref="FlowchartText.Prepare"/>. The
/// lexer does not depend on the parser, so the whole text is lexed first. The source is refused where
/// <c>ClassDB</c> throws: <see cref="ClassData.Error"/> says where.
/// </summary>
internal ref struct ClassParser
{
	private readonly ReadOnlySpan<char> _text;
	private readonly ReadOnlySpan<ClassToken> _tokens;
	private readonly ClassDocument _document;
	private ResourceGuard _guard;
	private SourceCursor _cursor;
	private int _at;
	private int _depth;
	private string? _expected;
	private int _errorAt;

	private PooledStringTable _ids;
	private PooledList<char> _scratch;
	private PooledList<char> _name;

	private ClassParser(ReadOnlySpan<char> text, ReadOnlySpan<ClassToken> tokens, DiagramParseContext context, ClassDocument document)
	{
		_text = text;
		_tokens = tokens;
		_document = document;
		_guard = ResourceGuard.Start(text, context.Limits);
		_cursor = new SourceCursor(text);
	}

	public static ClassDocument? Parse(ReadOnlySpan<char> source, DiagramParseContext context)
	{
		var body = source[context.BodyStart..];
		var text = new PooledList<char>(body.Length + 16);
		var tokens = new PooledList<ClassToken>(64);
		try
		{
			FlowchartText.Prepare(body, ref text, foldSpaceAfterBraces: false);
			ClassLexer.Lex(text.AsSpan(), ref tokens);
			var document = new ClassDocument();
			var parser = new ClassParser(text.AsSpan(), tokens.AsSpan(), context, document);
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

			if (ClassData.MayFail(document) && ClassData.From(document).Error is { } error)
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
		_scratch.Dispose();
		_name.Dispose();
	}

	private readonly ClassToken Current => _tokens[Math.Min(_at, _tokens.Length - 1)];

	private readonly Kind Peek => Current.Kind;

	private ClassToken Take()
	{
		var token = Current;
		_at = Math.Min(_at + 1, _tokens.Length - 1);
		return token;
	}

	private static bool IsAlphaNum(Kind kind) => kind is Kind.Alpha or Kind.Minus or Kind.UnicodeText;

	private static bool IsRelationType(Kind kind) => kind is Kind.Aggregation or Kind.Extension or Kind.Composition or Kind.Dependency or Kind.Lollipop;

	private static bool IsLineType(Kind kind) => kind is Kind.Line or Kind.DottedLine;

	private bool ParseStart()
	{
		if (Peek != Kind.ClassDiagram)
		{
			return Fail("'CLASS_DIAGRAM'");
		}

		_document.Keyword = Take().Text(_text).ToString();
		if (Peek != Kind.Newline)
		{
			return Fail("'NEWLINE'");
		}

		_ = Take();
		if (!ParseStatements(_document.Statements))
		{
			return false;
		}

		if (Peek != Kind.Eof)
		{
			return Fail("'NEWLINE', 'EOF'");
		}

		_ = Take();
		return Peek == Kind.End || Fail("end of input");
	}

	/// <summary><c>statements</c>: at least one statement, each after a line break, and a line break at the end.</summary>
	private bool ParseStatements(List<ClassStatement> statements)
	{
		while (true)
		{
			if (!ParseStatement(statements))
			{
				return false;
			}

			_cursor.Advance(Math.Min(Current.Start, _text.Length) - _cursor.Position);
			_guard.Statement(_cursor);
			if (Peek != Kind.Newline)
			{
				return true;
			}

			_ = Take();
			if (Peek == Kind.Eof)
			{
				return true;
			}
		}
	}

	private bool ParseStatement(List<ClassStatement> statements)
	{
		var token = Current;
		switch (token.Kind)
		{
			case Kind.Alpha or Kind.Minus or Kind.UnicodeText or Kind.BquoteStr:
				return ParseNameStatement(statements);
			case Kind.Namespace:
				return ParseNamespace(statements);
			case Kind.Class:
				return ParseClass(statements);
			case Kind.AnnotationStart:
				{
					_ = Take();
					if (!IsAlphaNum(Peek))
					{
						return Fail("'ALPHA'");
					}

					var annotation = Plain(Take().Text(_text));
					if (Peek != Kind.AnnotationEnd)
					{
						return Fail("'ANNOTATION_END'");
					}

					_ = Take();
					if (ParseClassName() is not { } name)
					{
						return false;
					}

					statements.Add(new ClassAnnotation(annotation, name));
					return true;
				}

			case Kind.Callback or Kind.Link or Kind.Click:
				return ParseInteraction(statements);
			case Kind.Style:
				{
					_ = Take();
					if (Peek != Kind.Alpha)
					{
						return Fail("'ALPHA'");
					}

					var id = Id(Take().Text(_text));
					var styles = new List<string>();
					if (!ParseStyles(styles))
					{
						return false;
					}

					statements.Add(new ClassStyle(id, styles));
					return true;
				}

			case Kind.CssClass:
				{
					_ = Take();
					if (Peek != Kind.Str)
					{
						return Fail("'STR'");
					}

					var ids = Plain(Take().Text(_text));
					if (Peek != Kind.Alpha)
					{
						return Fail("'ALPHA'");
					}

					statements.Add(new ClassCssClass(ids, Id(Take().Text(_text))));
					return true;
				}

			case Kind.NoteFor or Kind.Note:
				return ParseNote(statements);
			case Kind.ClassDef:
				{
					_ = Take();
					if (Peek != Kind.Alpha)
					{
						return Fail("'ALPHA'");
					}

					var names = new List<string> { Id(Take().Text(_text)) };
					while (Peek == Kind.Comma)
					{
						_ = Take();
						if (Peek != Kind.Alpha)
						{
							return Fail("'ALPHA'");
						}

						names.Add(Id(Take().Text(_text)));
					}

					var styles = new List<string>();
					if (!ParseStyles(styles))
					{
						return false;
					}

					statements.Add(new ClassStyleDefinition(names, styles));
					return true;
				}

			case Kind.Direction:
				_ = Take();
				statements.Add(new ClassDirection(token.Direction switch
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

				_document.AccessibilityTitle = Plain(ClassLexer.Trim(Take().Text(_text)));
				return true;
			case Kind.AccDescr:
				_ = Take();
				if (Peek != Kind.AccDescrValue)
				{
					return Fail("'acc_descr_value'");
				}

				_document.AccessibilityDescription = Plain(ClassLexer.Trim(Take().Text(_text)));
				return true;
			case Kind.AccDescrMultilineValue:
				_document.AccessibilityDescription = Plain(ClassLexer.Trim(Take().Text(_text)));
				_ = Take();
				return true;
			default:
				return Fail(null);
		}
	}

	/// <summary>A relation, <c>className LABEL</c> or a name on its own.</summary>
	private bool ParseNameStatement(List<ClassStatement> statements)
	{
		if (ParseClassName() is not { } from)
		{
			return false;
		}

		if (Peek == Kind.Label)
		{
			statements.Add(new ClassMemberStatement(from) { Member = CleanupLabel(Take()) });
			return true;
		}

		if (Peek is not (Kind.Str or Kind.Line or Kind.DottedLine) && !IsRelationType(Peek))
		{
			statements.Add(new ClassMemberStatement(from));
			return true;
		}

		var fromCardinality = Peek == Kind.Str ? Plain(Take().Text(_text)) : null;
		var fromEnd = IsRelationType(Peek) ? End(Take().Kind) : ClassRelationEnd.None;
		if (!IsLineType(Peek))
		{
			return Fail("'LINE', 'DOTTED_LINE'");
		}

		var line = Take().Kind == Kind.DottedLine ? ClassLineStyle.Dotted : ClassLineStyle.Solid;
		var toEnd = IsRelationType(Peek) ? End(Take().Kind) : ClassRelationEnd.None;
		var toCardinality = Peek == Kind.Str ? Plain(Take().Text(_text)) : null;
		if (ParseClassName() is not { } to)
		{
			return false;
		}

		statements.Add(new ClassRelation(from, to)
		{
			FromEnd = fromEnd,
			ToEnd = toEnd,
			Line = line,
			FromCardinality = fromCardinality,
			ToCardinality = toCardinality,
			Label = Peek == Kind.Label ? CleanupLabel(Take()) : null,
		});
		return true;
	}

	private static ClassRelationEnd End(Kind kind) => kind switch
	{
		Kind.Aggregation => ClassRelationEnd.Aggregation,
		Kind.Extension => ClassRelationEnd.Extension,
		Kind.Composition => ClassRelationEnd.Composition,
		Kind.Dependency => ClassRelationEnd.Dependency,
		_ => ClassRelationEnd.Lollipop,
	};

	/// <summary><c>namespaceStatement</c>, whose statements are classes, notes and namespaces.</summary>
	private bool ParseNamespace(List<ClassStatement> statements)
	{
		_ = Take();
		if (ParseName(generic: false) is not { } name)
		{
			return false;
		}

		var space = new ClassNamespace(name);
		if (Peek == Kind.Sqs)
		{
			if (ParseClassLabel() is not { } label)
			{
				return false;
			}

			space.Label = label;
		}

		if (Peek != Kind.StructStart)
		{
			return Fail("'STRUCT_START'");
		}

		_ = Take();
		if (Peek == Kind.Newline)
		{
			_ = Take();
		}

		_depth++;
		_guard.Depth(_depth, _cursor);
		while (true)
		{
			switch (Peek)
			{
				case Kind.Class:
					if (!ParseClass(space.Statements))
					{
						return false;
					}

					break;
				case Kind.NoteFor or Kind.Note:
					if (!ParseNote(space.Statements))
					{
						return false;
					}

					break;
				case Kind.Namespace:
					if (!ParseNamespace(space.Statements))
					{
						return false;
					}

					break;
				default:
					return Fail("'CLASS', 'NOTE', 'NAMESPACE'");
			}

			_cursor.Advance(Math.Min(Current.Start, _text.Length) - _cursor.Position);
			_guard.Statement(_cursor);
			if (Peek != Kind.Newline)
			{
				break;
			}

			_ = Take();
			if (Peek == Kind.StructStop)
			{
				break;
			}
		}

		if (Peek != Kind.StructStop)
		{
			return Fail("'STRUCT_STOP'");
		}

		_ = Take();
		_depth--;
		statements.Add(space);
		return true;
	}

	/// <summary><c>classStatement</c>.</summary>
	private bool ParseClass(List<ClassStatement> statements)
	{
		_ = Take();
		if (ParseClassName() is not { } name)
		{
			return false;
		}

		var definition = new ClassDefinition(name);
		if (Peek == Kind.Sqs)
		{
			if (ParseClassLabel() is not { } label)
			{
				return false;
			}

			definition.Label = label;
		}

		var membersRequired = false;
		if (Peek == Kind.StyleSeparator)
		{
			_ = Take();
			if (!IsAlphaNum(Peek))
			{
				return Fail("'ALPHA'");
			}

			definition.CssClass = Plain(Take().Text(_text));
			membersRequired = true;
		}
		else if (Peek == Kind.AnnotationStart)
		{
			_ = Take();
			if (!IsAlphaNum(Peek))
			{
				return Fail("'ALPHA'");
			}

			definition.Annotation = Plain(Take().Text(_text));
			if (Peek != Kind.AnnotationEnd)
			{
				return Fail("'ANNOTATION_END'");
			}

			_ = Take();
		}

		if (Peek == Kind.StructStart)
		{
			_ = Take();
			var members = new List<string>();
			var start = _at;
			while (Peek == Kind.Member)
			{
				var member = ClassLexer.Trim(Take().Text(_text));
				if (member.Length > 0)
				{
					members.Add(Plain(member));
				}
			}

			if (membersRequired && _at == start)
			{
				return Fail("'MEMBER'");
			}

			if (Peek != Kind.StructStop)
			{
				return Fail("'STRUCT_STOP', 'MEMBER'");
			}

			_ = Take();
			definition.Members = members;
		}

		statements.Add(definition);
		return true;
	}

	/// <summary><c>classLabel</c>: <c>SQS STR SQE</c>.</summary>
	private string? ParseClassLabel()
	{
		_ = Take();
		if (Peek != Kind.Str)
		{
			_ = Fail("'STR'");
			return null;
		}

		var label = Plain(Take().Text(_text));
		if (Peek != Kind.Sqe)
		{
			_ = Fail("'SQE'");
			return null;
		}

		_ = Take();
		return label;
	}

	private bool ParseNote(List<ClassStatement> statements)
	{
		string? name = null;
		if (Take().Kind == Kind.NoteFor && (name = ParseClassName()) is null)
		{
			return false;
		}

		if (Peek != Kind.Str)
		{
			return Fail("'STR'");
		}

		statements.Add(new ClassNote(Plain(Take().Text(_text))) { ClassName = name });
		return true;
	}

	/// <summary><c>clickStatement</c>.</summary>
	private bool ParseInteraction(List<ClassStatement> statements)
	{
		var keyword = Take().Kind;
		if (ParseClassName() is not { } name)
		{
			return false;
		}

		ClassInteraction interaction;
		switch (keyword)
		{
			case Kind.Callback:
				if (Peek != Kind.Str)
				{
					return Fail("'STR'");
				}

				interaction = new ClassInteraction(ClassInteractionKind.Callback, name, Plain(Take().Text(_text)));
				if (Peek == Kind.Str)
				{
					interaction.Tooltip = Plain(Take().Text(_text));
				}

				break;
			case Kind.Link:
				if (Peek != Kind.Str)
				{
					return Fail("'STR'");
				}

				interaction = new ClassInteraction(ClassInteractionKind.Link, name, Plain(Take().Text(_text)));
				LinkTail(interaction);
				break;
			default:
				if (Peek == Kind.CallbackName)
				{
					interaction = new ClassInteraction(ClassInteractionKind.ClickCall, name, Plain(Take().Text(_text)));
					if (Peek == Kind.CallbackArgs)
					{
						interaction.Arguments = Plain(Take().Text(_text));
					}

					if (Peek == Kind.Str)
					{
						interaction.Tooltip = Plain(Take().Text(_text));
					}

					break;
				}

				if (Peek != Kind.Href)
				{
					return Fail("'CALLBACK_NAME', 'HREF'");
				}

				_ = Take();
				if (Peek != Kind.Str)
				{
					return Fail("'STR'");
				}

				interaction = new ClassInteraction(ClassInteractionKind.ClickHref, name, Plain(Take().Text(_text)));
				LinkTail(interaction);
				break;
		}

		statements.Add(interaction);
		return true;
	}

	/// <summary>The <c>STR? LINK_TARGET?</c> after a link's URL.</summary>
	private void LinkTail(ClassInteraction interaction)
	{
		if (Peek == Kind.Str)
		{
			interaction.Tooltip = Plain(Take().Text(_text));
		}

		if (Peek == Kind.LinkTarget)
		{
			interaction.LinkTarget = Id(Take().Text(_text));
		}
	}

	/// <summary><c>stylesOpt</c>: styles between commas, each the text of its tokens joined.</summary>
	private bool ParseStyles(List<string> styles)
	{
		while (true)
		{
			_name.Truncate(0);
			var start = _at;
			while (Peek is Kind.Alpha or Kind.Colon or Kind.Space or Kind.Brkt or Kind.Style or Kind.Pct or Kind.Label)
			{
				_name.AddRange(Take().Text(_text));
			}

			if (_at == start)
			{
				return Fail("'ALPHA', 'COLON', 'SPACE', 'BRKT', 'STYLE', 'PCT', 'LABEL'");
			}

			styles.Add(Plain(_name.AsSpan()));
			if (Peek != Kind.Comma)
			{
				return true;
			}

			_ = Take();
		}
	}

	private string? ParseClassName() => ParseName(generic: true);

	/// <summary>
	/// <c>className</c>, or <c>namespaceName</c> without a generic: the tokens' text joined, a <c>DOT</c> as <c>.</c> and
	/// a <c>GENERICTYPE</c> as <c>~type~</c>.
	/// </summary>
	private string? ParseName(bool generic)
	{
		_name.Truncate(0);
		while (true)
		{
			if (Peek == Kind.BquoteStr)
			{
				_name.AddRange(Take().Text(_text));
				break;
			}

			if (!IsAlphaNum(Peek))
			{
				_ = Fail("'ALPHA', 'BQUOTE_STR'");
				return null;
			}

			_name.AddRange(Take().Text(_text));
			if (Peek == Kind.Dot)
			{
				_ = Take();
				_name.Add('.');
				continue;
			}

			if (!IsAlphaNum(Peek) && Peek != Kind.BquoteStr)
			{
				break;
			}
		}

		if (generic && Peek == Kind.GenericType)
		{
			_name.Add('~');
			_name.AddRange(Take().Text(_text));
			_name.Add('~');
		}

		return Id(_name.AsSpan());
	}

	/// <summary><c>cleanupLabel</c>: the text after the colon, trimmed.</summary>
	private string CleanupLabel(ClassToken token) => Plain(ClassLexer.Trim(token.Text(_text)[1..]));

	/// <summary>A name, interned, with mermaid.js's entity placeholders turned back into the source's text.</summary>
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
