// Upstream grammar: packages/mermaid/src/diagrams/requirement/parser/requirementDiagram.jison
using System.Globalization;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Diagrams.State;
using SplashTheMusical.Parsing;
using Kind = SplashTheMusical.Diagrams.Requirement.RequirementTokenKind;

namespace SplashTheMusical.Diagrams.Requirement;

/// <summary>
/// The parser of <c>requirementDiagram.jison</c> at mermaid 12.3.0. It reads the tokens of
/// <see cref="RequirementLexer"/> by recursive descent over the grammar:
/// <code>
/// start: directive NEWLINE? start | RD NEWLINE diagram EOF
/// directive: acc_title acc_title_value | acc_descr acc_descr_value | acc_descr_multiline_value
/// diagram: (requirementDef | elementDef | relationshipDef | directive | direction | styleStatement
///     | classDefStatement | classStatement | NEWLINE)*
/// requirementDef: requirementType requirementName (STYLE_SEPARATOR idList)? STRUCT_START NEWLINE requirementBody
/// requirementBody: ((ID | TEXT) COLONSEP string NEWLINE | RISK COLONSEP riskLevel NEWLINE
///     | VERIFYMTHD COLONSEP verifyType NEWLINE | NEWLINE)* STRUCT_STOP
/// elementDef: ELEMENT elementName (STYLE_SEPARATOR idList)? STRUCT_START NEWLINE elementBody
/// elementBody: ((TYPE | DOCREF) COLONSEP string NEWLINE | NEWLINE)* STRUCT_STOP
/// relationshipDef: id END_ARROW_L relationship LINE id | id LINE relationship END_ARROW_R id
/// classDefStatement: CLASSDEF idList stylesOpt
/// classStatement: CLASS idList idList | id STYLE_SEPARATOR idList
/// styleStatement: STYLE idList stylesOpt
/// idList: (ALPHA | id) (COMMA (ALPHA | id))*
/// stylesOpt: style (COMMA style)*
/// style: (ALPHA | COLON | BRKT | MINUS | SEMICOLON)+
/// id, requirementName, elementName, string: unqString | qString
/// </code>
/// The body rules are right recursive, so their actions run from the last line to the first, and the db keeps the
/// first value of a field. The lexer has no rule for the grammar's <c>NUM</c>, <c>UNIT</c>, <c>SPACE</c>, <c>PCT</c>
/// and <c>LABEL</c>, and its <c>title</c> and <c>PERCENT</c> are in no grammar rule. The text is prepared as mermaid.js
/// prepares it for every jison diagram, by <see cref="FlowchartText.Prepare"/>. The lexer does not depend on the
/// parser, so the whole text is lexed first.
/// </summary>
internal ref struct RequirementParser
{
	private readonly ReadOnlySpan<char> _text;
	private readonly ReadOnlySpan<RequirementToken> _tokens;
	private readonly RequirementDocument _document;
	private ResourceGuard _guard;
	private SourceCursor _cursor;
	private int _at;
	private string? _expected;
	private int _errorAt;

	private PooledStringTable _ids;
	private PooledList<RequirementStatement> _statements;
	private PooledList<string> _list;
	private PooledList<char> _scratch;

	private RequirementParser(ReadOnlySpan<char> text, ReadOnlySpan<RequirementToken> tokens, DiagramParseContext context, RequirementDocument document)
	{
		_text = text;
		_tokens = tokens;
		_document = document;
		_guard = ResourceGuard.Start(text, context.Limits);
		_cursor = new SourceCursor(text);
	}

	public static RequirementDocument? Parse(ReadOnlySpan<char> source, DiagramParseContext context)
	{
		var body = source[context.BodyStart..];
		var text = new PooledList<char>(body.Length + 16);
		var tokens = new PooledList<RequirementToken>(64);
		try
		{
			FlowchartText.Prepare(body, ref text, foldSpaceAfterBraces: false);
			RequirementLexer.Lex(text.AsSpan(), ref tokens);
			var document = new RequirementDocument();
			var parser = new RequirementParser(text.AsSpan(), tokens.AsSpan(), context, document);
			try
			{
				if (!parser.ParseStart())
				{
					parser.Report(source, context);
					return null;
				}

				if (!parser.EveryRelationshipEndIsDefined())
				{
					var (bodyLine, _) = TextPosition.Of(source, context.BodyStart);
					context.AddError(UndefinedEndMessage, bodyLine, 1);
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
		_ids.Dispose();
		_statements.Dispose();
		_list.Dispose();
		_scratch.Dispose();
	}

	private readonly RequirementToken Current => _tokens[Math.Min(_at, _tokens.Length - 1)];

	private readonly Kind Peek => Current.Kind;

	private RequirementToken Take()
	{
		var token = Current;
		_at = Math.Min(_at + 1, _tokens.Length - 1);
		return token;
	}

	private bool ParseStart()
	{
		while (Peek is Kind.AccTitle or Kind.AccDescr or Kind.AccDescrMultilineValue)
		{
			if (!ParseDirective())
			{
				return false;
			}

			if (Peek == Kind.Newline)
			{
				_ = Take();
			}
		}

		if (Peek != Kind.Rd)
		{
			return Fail("'RD'");
		}

		_ = Take();
		if (Peek != Kind.Newline)
		{
			return Fail("'NEWLINE'");
		}

		_ = Take();
		while (Peek != Kind.Eof)
		{
			if (!ParseStatement())
			{
				return false;
			}

			_cursor.Advance(Math.Min(Current.Start, _text.Length) - _cursor.Position);
			_guard.Statement(_cursor);
		}

		_ = Take();
		return Peek == Kind.End || Fail("'$end'");
	}

	private bool ParseStatement()
	{
		var token = Current;
		switch (token.Kind)
		{
			case Kind.Newline:
				_ = Take();
				return true;
			case Kind.RequirementType:
				return ParseRequirement();
			case Kind.Element:
				return ParseElement();
			case Kind.UnqString or Kind.QString:
				return ParseIdStatement();
			case Kind.AccTitle or Kind.AccDescr or Kind.AccDescrMultilineValue:
				return ParseDirective();
			case Kind.Direction:
				_ = Take();
				_statements.Add(new RequirementDirection(token.Value switch
				{
					0 => "TB",
					1 => "BT",
					2 => "RL",
					_ => "LR",
				}));
				return true;
			case Kind.Style or Kind.ClassDef:
				return ParseStyle();
			case Kind.Class:
				return ParseClass();
			default:
				return Fail(null);
		}
	}

	private bool ParseDirective()
	{
		var token = Take();
		switch (token.Kind)
		{
			case Kind.AccTitle:
				if (Peek != Kind.AccTitleValue)
				{
					return Fail("'acc_title_value'");
				}

				_document.AccessibilityTitle = Plain(StateLexer.Trim(Take().Text(_text)));
				return true;
			case Kind.AccDescr:
				if (Peek != Kind.AccDescrValue)
				{
					return Fail("'acc_descr_value'");
				}

				_document.AccessibilityDescription = Plain(StateLexer.Trim(Take().Text(_text)));
				return true;
			default:
				_document.AccessibilityDescription = Plain(StateLexer.Trim(token.Text(_text)));
				return true;
		}
	}

	/// <summary><c>requirementDef</c>, then the db's <c>addRequirement</c> and, with <c>:::</c>, <c>setClass</c>.</summary>
	private bool ParseRequirement()
	{
		var kind = (RequirementKind)Take().Value;
		if (!Current.IsString)
		{
			return Fail("'unqString', 'qString'");
		}

		var name = Id(Take());
		if (!ParseBodyStart(out var classes))
		{
			return false;
		}

		string? id = null;
		string? text = null;
		RequirementRisk? risk = null;
		RequirementVerifyMethod? verifyMethod = null;
		while (true)
		{
			var field = Take();
			switch (field.Kind)
			{
				case Kind.StructStop:
					_statements.Add(new RequirementDefinition(name, kind)
					{
						Id = id,
						Text = text,
						Risk = risk,
						VerifyMethod = verifyMethod,
						Classes = classes,
					});
					return true;
				case Kind.Newline:
					continue;
				case Kind.Id or Kind.Text:
					if (!Colon() || !Current.IsString)
					{
						return Fail("'unqString', 'qString'");
					}

					var value = Take();
					if (field.Kind == Kind.Id)
					{
						id ??= Plain(value.Text(_text));
					}
					else
					{
						text ??= Plain(value.Text(_text));
					}

					break;
				case Kind.Risk:
					if (!Colon() || Peek != Kind.RiskLevel)
					{
						return Fail("'LOW_RISK', 'MED_RISK', 'HIGH_RISK'");
					}

					// Take the level before ??=, which would skip it on a repeated field.
					var level = (RequirementRisk)Take().Value;
					risk ??= level;
					break;
				case Kind.VerifyMethod:
					if (!Colon() || Peek != Kind.VerifyType)
					{
						return Fail("'VERIFY_ANALYSIS', 'VERIFY_DEMONSTRATION', 'VERIFY_INSPECTION', 'VERIFY_TEST'");
					}

					var method = (RequirementVerifyMethod)Take().Value;
					verifyMethod ??= method;
					break;
				default:
					_at--;
					return Fail("'NEWLINE', 'STRUCT_STOP', 'ID', 'TEXT', 'RISK', 'VERIFYMTHD'");
			}

			if (Peek != Kind.Newline)
			{
				return Fail("'NEWLINE'");
			}

			_ = Take();
		}
	}

	/// <summary><c>elementDef</c>, then the db's <c>addElement</c> and, with <c>:::</c>, <c>setClass</c>.</summary>
	private bool ParseElement()
	{
		_ = Take();
		if (!Current.IsString)
		{
			return Fail("'unqString', 'qString'");
		}

		var name = Id(Take());
		if (!ParseBodyStart(out var classes))
		{
			return false;
		}

		string? type = null;
		string? docRef = null;
		while (true)
		{
			var field = Take();
			switch (field.Kind)
			{
				case Kind.StructStop:
					_statements.Add(new RequirementElement(name) { Type = type, DocRef = docRef, Classes = classes });
					return true;
				case Kind.Newline:
					continue;
				case Kind.Type or Kind.DocRef:
					if (!Colon() || !Current.IsString)
					{
						return Fail("'unqString', 'qString'");
					}

					var value = Take();
					if (field.Kind == Kind.Type)
					{
						type ??= Plain(value.Text(_text));
					}
					else
					{
						docRef ??= Plain(value.Text(_text));
					}

					break;
				default:
					_at--;
					return Fail("'NEWLINE', 'STRUCT_STOP', 'TYPE', 'DOCREF'");
			}

			if (Peek != Kind.Newline)
			{
				return Fail("'NEWLINE'");
			}

			_ = Take();
		}
	}

	/// <summary><c>(STYLE_SEPARATOR idList)? STRUCT_START NEWLINE</c>.</summary>
	private bool ParseBodyStart(out List<string>? classes)
	{
		classes = null;
		if (Peek == Kind.StyleSeparator)
		{
			_ = Take();
			if (!ParseIdList(out var list))
			{
				return false;
			}

			classes = list;
		}

		if (Peek != Kind.StructStart)
		{
			return Fail("'STRUCT_START'");
		}

		_ = Take();
		return Peek == Kind.Newline ? Take().Kind == Kind.Newline : Fail("'NEWLINE'");
	}

	private bool Colon()
	{
		if (Peek != Kind.ColonSep)
		{
			return false;
		}

		_ = Take();
		return true;
	}

	/// <summary><c>relationshipDef</c>, or <c>id STYLE_SEPARATOR idList</c>.</summary>
	private bool ParseIdStatement()
	{
		var first = Take();
		switch (Peek)
		{
			case Kind.Line or Kind.EndArrowL:
				{
					var left = Take().Kind == Kind.EndArrowL;
					if (Peek != Kind.Relationship)
					{
						return Fail("'CONTAINS', 'COPIES', 'DERIVES', 'SATISFIES', 'VERIFIES', 'REFINES', 'TRACES'");
					}

					var type = (RequirementRelationshipType)Take().Value;
					if (Peek != (left ? Kind.Line : Kind.EndArrowR))
					{
						return Fail(left ? "'LINE'" : "'END_ARROW_R'");
					}

					_ = Take();
					if (!Current.IsString)
					{
						return Fail("'unqString', 'qString'");
					}

					var second = Take();
					var a = Id(first);
					var b = Id(second);
					_statements.Add(left ? new RequirementRelationship(b, a, type) : new RequirementRelationship(a, b, type));
					return true;
				}

			case Kind.StyleSeparator:
				{
					_ = Take();
					var id = Id(first);
					if (!ParseIdList(out var classes))
					{
						return false;
					}

					var ids = new List<string>(1);
					ids.Add(id);
					_statements.Add(new RequirementClassAssignment(ids, classes));
					return true;
				}

			default:
				return Fail("'END_ARROW_L', 'LINE', 'STYLE_SEPARATOR'");
		}
	}

	/// <summary><c>STYLE idList stylesOpt</c> or <c>CLASSDEF idList stylesOpt</c>.</summary>
	private bool ParseStyle()
	{
		var isStyle = Take().Kind == Kind.Style;
		if (!ParseIdList(out var ids))
		{
			return false;
		}

		var from = _list.Count;
		while (true)
		{
			_scratch.Truncate(0);
			while (Peek is Kind.Alpha or Kind.Colon or Kind.Brkt or Kind.Minus or Kind.Semicolon)
			{
				_scratch.AddRange(Take().Text(_text));
			}

			if (_scratch.Count == 0)
			{
				return Fail("'ALPHA', 'COLON', 'BRKT', 'MINUS', 'SEMICOLON'");
			}

			_list.Add(_scratch.AsSpan().ToString());
			if (Peek != Kind.Comma)
			{
				break;
			}

			_ = Take();
		}

		var styles = List(from);
		_statements.Add(isStyle ? new RequirementStyle(ids, styles) : new RequirementClassDefinition(ids, styles));
		return true;
	}

	/// <summary><c>CLASS idList idList</c>.</summary>
	private bool ParseClass()
	{
		_ = Take();
		if (!ParseIdList(out var ids) || !ParseIdList(out var classes))
		{
			return false;
		}

		_statements.Add(new RequirementClassAssignment(ids, classes));
		return true;
	}

	/// <summary><c>idList</c>: ids separated by commas, each interned.</summary>
	private bool ParseIdList(out List<string> ids)
	{
		ids = null!;
		var from = _list.Count;
		while (true)
		{
			if (Peek is not (Kind.Alpha or Kind.UnqString or Kind.QString))
			{
				_list.Truncate(from);
				return Fail("'ALPHA', 'unqString', 'qString'");
			}

			_list.Add(Id(Take()));
			if (Peek != Kind.Comma)
			{
				break;
			}

			_ = Take();
		}

		ids = List(from);
		return true;
	}

	/// <summary>The strings of <see cref="_list"/> from <paramref name="from"/> on as a list, taken off it.</summary>
	private List<string> List(int from)
	{
		var items = _list.AsSpan()[from..];
		var list = new List<string>(items.Length);
		list.AddRange(items);
		_list.Truncate(from);
		return list;
	}

	/// <summary>An id, interned, with mermaid.js's entity placeholders turned back into the source's text.</summary>
	private string Id(RequirementToken token)
	{
		var text = token.Text(_text);
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

	/// <summary>What mermaid.js throws when it lays out a relationship whose end is no requirement or element.</summary>
	private const string UndefinedEndMessage = "org.eclipse.elk.graph.json.JsonImportException: Id must be a string or an integer: 'null'.";

	/// <summary>
	/// mermaid.js parses a relationship to a name that no requirement or element defines, anywhere in the diagram, and
	/// then fails to draw it, because the edge it gives ELK has a null end.
	/// </summary>
	private readonly bool EveryRelationshipEndIsDefined()
	{
		var statements = _statements.AsSpan();
		foreach (var statement in statements)
		{
			if (statement is RequirementRelationship relationship && (!IsDefined(statements, relationship.Source) || !IsDefined(statements, relationship.Target)))
			{
				return false;
			}
		}

		return true;
	}

	private static bool IsDefined(ReadOnlySpan<RequirementStatement> statements, string name)
	{
		foreach (var statement in statements)
		{
			if ((statement is RequirementDefinition definition && definition.Name == name) || (statement is RequirementElement element && element.Name == name))
			{
				return true;
			}
		}

		return false;
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
