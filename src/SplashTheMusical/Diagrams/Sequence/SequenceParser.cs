// Upstream grammar: packages/mermaid/src/diagrams/sequence/parser/sequenceDiagram.jison
using System.Globalization;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Diagrams.State;
using SplashTheMusical.Parsing;
using Kind = SplashTheMusical.Diagrams.Sequence.SequenceTokenKind;

namespace SplashTheMusical.Diagrams.Sequence;

/// <summary>
/// The parser of <c>sequenceDiagram.jison</c> at mermaid 12.3.0. It reads the tokens of <see cref="SequenceLexer"/> by
/// recursive descent over the grammar:
/// <code>
/// start: NEWLINE* SD document
/// document: (statement | NEWLINE | INVALID)*
/// statement: participant_statement | 'create' participant_statement | 'box' restOfLine box_section end
///     | signal NEWLINE | autonumber (NUM NUM? | off)? NEWLINE | ('activate' | 'deactivate') actor NEWLINE
///     | (note_statement | links_statement | link_statement | properties_statement | details_statement) NEWLINE
///     | title | legacy_title | acc_title acc_title_value | acc_descr acc_descr_value | acc_descr_multiline_value
///     | ('loop' | 'rect' | opt | break) restOfLine document end | alt restOfLine document (else restOfLine document)* end
///     | (par | par_over) restOfLine document (and restOfLine document)* end
///     | critical restOfLine document (option restOfLine document)* end
/// box_section: (participant_statement | NEWLINE)*
/// participant_statement: ('participant' | 'participant_actor') actor config_object? ('AS' restOfLine)? NEWLINE
///     | 'destroy' actor NEWLINE
/// note_statement: 'note' ('left_of' | 'right_of') actor text2 | 'note' 'over' actor (',' actor)? text2
/// links_statement, link_statement, properties_statement, details_statement: keyword actor text2
/// signal: actor signaltype ('+' | '-' | '()')? actor text2 | actor '()' signaltype '()'? actor text2
/// actor: ACTOR | 'link'
/// config_object: CONFIG_START CONFIG_CONTENT CONFIG_END
/// text2: TXT
/// </code>
/// The grammar's actions build a list the db's <c>apply</c> runs once the whole text has parsed, and <c>apply</c>
/// throws on a participant in two boxes, a <c>create</c> of a participant that exists, a created or destroyed
/// participant whose next message does not involve it, the deactivation of an inactive participant and <c>@{ }</c>
/// that is not YAML. The parser runs those checks as it goes, over pooled tables, and reports a parse error before
/// them. The text is prepared as mermaid.js prepares it for every jison diagram, by <see cref="FlowchartText.Prepare"/>.
/// </summary>
internal ref struct SequenceParser
{
	private const int NotAnActor = int.MinValue;

	private readonly ReadOnlySpan<char> _text;
	private readonly ReadOnlySpan<SequenceToken> _tokens;
	private readonly SequenceDocument _document;
	private readonly DiagramParseContext _context;
	private ResourceGuard _guard;
	private SourceCursor _cursor;
	private int _at;
	private int _depth;
	private string? _expected;
	private int _errorAt;

	private PooledStringTable _ids;
	private PooledList<SequenceStatement> _statements;
	private PooledList<SequenceBlockSection> _sections;
	private PooledList<char> _scratch;

	// The db's state for its checks, by id: the box of each actor, or NotAnActor, and its activation count.
	private PooledList<int> _actorBox;
	private PooledList<int> _activations;
	private PooledList<string?> _boxTitles;
	private int _currentBox;
	private int _lastCreated;
	private int _lastDestroyed;
	private string? _dbError;

	private SequenceParser(ReadOnlySpan<char> text, ReadOnlySpan<SequenceToken> tokens, DiagramParseContext context, SequenceDocument document)
	{
		_text = text;
		_tokens = tokens;
		_document = document;
		_context = context;
		_guard = ResourceGuard.Start(text, context.Limits);
		_cursor = new SourceCursor(text);
		_currentBox = -1;
		_lastCreated = -1;
		_lastDestroyed = -1;
	}

	public static SequenceDocument? Parse(ReadOnlySpan<char> source, DiagramParseContext context)
	{
		var body = source[context.BodyStart..];
		var text = new PooledList<char>(body.Length + 16);
		var tokens = new PooledList<SequenceToken>(64);
		try
		{
			FlowchartText.Prepare(body, ref text, foldSpaceAfterBraces: false);
			SequenceLexer.Lex(text.AsSpan(), ref tokens);
			var document = new SequenceDocument();
			var parser = new SequenceParser(text.AsSpan(), tokens.AsSpan(), context, document);
			try
			{
				if (!parser.ParseStart())
				{
					parser.Report(source, context);
					return null;
				}

				if (parser._dbError is { } error)
				{
					var (bodyLine, _) = TextPosition.Of(source, context.BodyStart);
					context.AddError(error, bodyLine, 1);
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
		_sections.Dispose();
		_scratch.Dispose();
		_actorBox.Dispose();
		_activations.Dispose();
		_boxTitles.Dispose();
	}

	private readonly SequenceToken Current => _tokens[Math.Min(_at, _tokens.Length - 1)];

	private readonly Kind Peek => Current.Kind;

	private readonly Kind PeekAt(int offset) => _tokens[Math.Min(_at + offset, _tokens.Length - 1)].Kind;

	private SequenceToken Take()
	{
		var token = Current;
		_at = Math.Min(_at + 1, _tokens.Length - 1);
		return token;
	}

	private bool ParseStart()
	{
		while (Peek == Kind.Newline)
		{
			_ = Take();
		}

		if (Peek != Kind.Sd)
		{
			return Fail("'SD'");
		}

		_ = Take();
		if (!ParseDocument())
		{
			return false;
		}

		return Peek == Kind.End || Fail(null);
	}

	/// <summary><c>document</c>: lines up to a token no line starts with.</summary>
	private bool ParseDocument()
	{
		while (true)
		{
			switch (Peek)
			{
				case Kind.Newline or Kind.Invalid:
					_ = Take();
					continue;
				case Kind.Participant or Kind.ParticipantActor or Kind.Destroy or Kind.Create or Kind.Box or Kind.Actor or Kind.Link
					or Kind.Autonumber or Kind.Activate or Kind.Deactivate or Kind.Note or Kind.Links or Kind.Properties or Kind.Details
					or Kind.Title or Kind.LegacyTitle or Kind.AccTitle or Kind.AccDescr or Kind.AccDescrMultilineValue
					or Kind.Loop or Kind.Rect or Kind.Opt or Kind.Alt or Kind.Par or Kind.ParOver or Kind.Critical or Kind.Break:
					if (!ParseStatement())
					{
						return false;
					}

					_cursor.Advance(Math.Min(Current.Start, _text.Length) - _cursor.Position);
					_guard.Statement(_cursor);
					continue;
				default:
					return true;
			}
		}
	}

	private bool ParseStatement()
	{
		switch (Peek)
		{
			case Kind.Participant or Kind.ParticipantActor or Kind.Destroy:
				return ParseParticipant(created: false);
			case Kind.Create:
				_ = Take();
				return Peek is Kind.Participant or Kind.ParticipantActor or Kind.Destroy
					? ParseParticipant(created: true)
					: Fail("'participant', 'participant_actor', 'destroy'");
			case Kind.Box:
				return ParseBox();
			case Kind.Link when PeekAt(1) is not (Kind.Arrow or Kind.Central):
				return ParseActorData(SequenceActorDataKind.Link);
			case Kind.Actor or Kind.Link:
				return ParseSignal();
			case Kind.Autonumber:
				return ParseAutonumber();
			case Kind.Activate or Kind.Deactivate:
				return ParseActivation();
			case Kind.Note:
				return ParseNote();
			case Kind.Links:
				return ParseActorData(SequenceActorDataKind.Links);
			case Kind.Properties:
				return ParseActorData(SequenceActorDataKind.Properties);
			case Kind.Details:
				return ParseActorData(SequenceActorDataKind.Details);
			case Kind.Title or Kind.LegacyTitle:
				{
					var token = Take();
					_document.DiagramTitle = Plain(token.Text(_text)[(token.Kind == Kind.Title ? 6 : 7)..]);
					return true;
				}

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
			case Kind.Loop:
				return ParseBlock(SequenceBlockKind.Loop, Kind.End);
			case Kind.Rect:
				return ParseBlock(SequenceBlockKind.Rect, Kind.End);
			case Kind.Opt:
				return ParseBlock(SequenceBlockKind.Opt, Kind.End);
			case Kind.Break:
				return ParseBlock(SequenceBlockKind.Break, Kind.End);
			case Kind.Alt:
				return ParseBlock(SequenceBlockKind.Alt, Kind.Else);
			case Kind.Par:
				return ParseBlock(SequenceBlockKind.Par, Kind.And);
			case Kind.ParOver:
				return ParseBlock(SequenceBlockKind.ParOver, Kind.And);
			case Kind.Critical:
				return ParseBlock(SequenceBlockKind.Critical, Kind.Option);
			default:
				return Fail(null);
		}
	}

	/// <summary>
	/// <c>participant_statement</c>, then the db's <c>addActor</c>, or for <c>create</c> its <c>createParticipant</c>,
	/// or for <c>destroy</c> its <c>destroyParticipant</c>. <c>create destroy id</c> adds the participant as
	/// <c>create participant id</c> does.
	/// </summary>
	private bool ParseParticipant(bool created)
	{
		var keyword = Take().Kind;
		if (Peek != Kind.Actor)
		{
			return Fail("'ACTOR'");
		}

		var actor = Take();
		var index = Intern(actor);
		if (keyword == Kind.Destroy)
		{
			if (Peek != Kind.Newline)
			{
				return Fail("'NEWLINE'");
			}

			_ = Take();
			if (created)
			{
				Create(index);
				_statements.Add(new SequenceParticipant(_ids[index]) { Created = true });
			}
			else
			{
				_lastDestroyed = index;
				_statements.Add(new SequenceDestroy(_ids[index]));
			}

			return true;
		}

		ReadOnlySpan<char> metadata = [];
		var hasMetadata = false;
		if (Peek == Kind.ConfigStart)
		{
			_ = Take();
			if (Peek != Kind.ConfigContent)
			{
				return Fail("'CONFIG_CONTENT'");
			}

			metadata = StateLexer.Trim(Take().Text(_text));
			if (Peek != Kind.ConfigEnd)
			{
				return Fail("'CONFIG_END'");
			}

			_ = Take();
			hasMetadata = true;
		}

		string? description = null;
		bool? wrap = null;
		if (Peek == Kind.As)
		{
			_ = Take();
			if (Peek != Kind.RestOfLine)
			{
				return Fail("'restOfLine'");
			}

			description = Plain(SequenceText.ParseMessage(Take().Text(_text), out wrap));
		}

		if (Peek != Kind.Newline)
		{
			return Fail("'NEWLINE'");
		}

		_ = Take();
		if (hasMetadata)
		{
			CheckMetadata(metadata);
		}

		if (created)
		{
			Create(index);
		}
		else
		{
			AddActor(index);
		}

		_statements.Add(new SequenceParticipant(_ids[index], keyword == Kind.ParticipantActor ? SequenceParticipantKind.Actor : SequenceParticipantKind.Participant)
		{
			Created = created,
			Description = description,
			Wrap = wrap,
			Metadata = hasMetadata ? Plain(metadata) : null,
		});
		return true;
	}

	/// <summary><c>'box' restOfLine box_section end</c>, then the db's <c>addBox</c> and <c>boxEnd</c> around its participants.</summary>
	private bool ParseBox()
	{
		_ = Take();
		if (Peek != Kind.RestOfLine)
		{
			return Fail("'restOfLine'");
		}

		SequenceBoxData.Parse(Take().Text(_text), out var colorText, out var titleText, out var wrap);
		var color = colorText.IsEmpty ? SequenceBoxData.Transparent : Plain(colorText);
		var title = titleText.IsEmpty ? null : Plain(titleText);
		_currentBox = _boxTitles.Count;
		_boxTitles.Add(title);
		var from = _statements.Count;
		while (true)
		{
			if (Peek == Kind.Newline)
			{
				_ = Take();
			}
			else if (Peek is Kind.Participant or Kind.ParticipantActor or Kind.Destroy)
			{
				if (!ParseParticipant(created: false))
				{
					return false;
				}
			}
			else
			{
				break;
			}
		}

		if (Peek != Kind.EndKeyword)
		{
			return Fail("'end'");
		}

		_ = Take();
		_currentBox = -1;
		_statements.Add(new SequenceBox { Color = color, Title = title, Wrap = wrap, Statements = Statements(from) });
		return true;
	}

	/// <summary><c>signal</c> and its <c>NEWLINE</c>, then the db's <c>addActor</c> for both ends and <c>addMessage</c>.</summary>
	private bool ParseSignal()
	{
		var from = Intern(Take());
		var modifier = SequenceMessageModifier.None;
		if (Peek == Kind.Central)
		{
			_ = Take();
			modifier = SequenceMessageModifier.CentralConnectionReverse;
		}

		if (Peek != Kind.Arrow)
		{
			return Fail(modifier == SequenceMessageModifier.None ? "'SOLID_ARROW', 'DOTTED_ARROW', '()'" : "'SOLID_ARROW', 'DOTTED_ARROW'");
		}

		var arrow = (SequenceArrow)Take().Value;
		switch (Peek)
		{
			case Kind.Central:
				_ = Take();
				modifier = modifier == SequenceMessageModifier.None ? SequenceMessageModifier.CentralConnection : SequenceMessageModifier.CentralConnectionDual;
				break;
			case Kind.Plus when modifier == SequenceMessageModifier.None:
				_ = Take();
				modifier = SequenceMessageModifier.Activate;
				break;
			case Kind.Minus when modifier == SequenceMessageModifier.None:
				_ = Take();
				modifier = SequenceMessageModifier.Deactivate;
				break;
		}

		if (Peek is not (Kind.Actor or Kind.Link))
		{
			return Fail("'ACTOR'");
		}

		var to = Intern(Take());
		if (!ReadText(out var text, out var wrap))
		{
			return false;
		}

		AddActor(from);
		AddActor(to);
		AddMessage(from, to);
		if (modifier == SequenceMessageModifier.Activate)
		{
			_activations[to]++;
		}
		else if (modifier == SequenceMessageModifier.Deactivate)
		{
			Deactivate(from);
		}

		_statements.Add(new SequenceMessage(_ids[from], _ids[to], arrow) { Modifier = modifier, Text = text, Wrap = wrap });
		return true;
	}

	/// <summary><c>text2 NEWLINE</c>: the text after the <c>:</c>, read by <c>parseMessage</c>.</summary>
	private bool ReadText(out string text, out bool? wrap)
	{
		text = "";
		wrap = null;
		if (Peek != Kind.Txt)
		{
			return Fail("'TXT'");
		}

		var token = Take().Text(_text);
		if (Peek != Kind.Newline)
		{
			return Fail("'NEWLINE'");
		}

		_ = Take();
		var message = SequenceText.ParseMessage(StateLexer.TrimEnd(token)[1..], out wrap);
		text = message.IsEmpty ? "" : Plain(message);
		return true;
	}

	/// <summary><c>autonumber</c>, with a start, a start and a step, or <c>off</c>, and its <c>NEWLINE</c>.</summary>
	private bool ParseAutonumber()
	{
		_ = Take();
		double? start = null;
		double? step = null;
		var visible = true;
		if (Peek == Kind.Num)
		{
			start = Number(Take());
			if (Peek == Kind.Num)
			{
				step = Number(Take());
			}
		}
		else if (Peek == Kind.Off)
		{
			_ = Take();
			visible = false;
		}

		if (Peek != Kind.Newline)
		{
			return Fail(start is null && visible ? "'NUM', 'off', 'NEWLINE'" : "'NEWLINE'");
		}

		_ = Take();
		_statements.Add(new SequenceAutonumber { Start = start, Step = step, Visible = visible });
		return true;
	}

	private readonly double Number(SequenceToken token) => double.Parse(token.Text(_text), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);

	/// <summary><c>'activate' actor NEWLINE</c> or <c>'deactivate' actor NEWLINE</c>, which add no participant.</summary>
	private bool ParseActivation()
	{
		var active = Take().Kind == Kind.Activate;
		if (Peek != Kind.Actor)
		{
			return Fail("'ACTOR'");
		}

		var index = Intern(Take());
		if (Peek != Kind.Newline)
		{
			return Fail("'NEWLINE'");
		}

		_ = Take();
		if (active)
		{
			_activations[index]++;
		}
		else
		{
			Deactivate(index);
		}

		_statements.Add(new SequenceActivation(_ids[index], active));
		return true;
	}

	/// <summary><c>note_statement NEWLINE</c>, then the db's <c>addActor</c> for each participant and <c>addNote</c>.</summary>
	private bool ParseNote()
	{
		_ = Take();
		SequenceNotePlacement placement;
		switch (Peek)
		{
			case Kind.LeftOf:
				placement = SequenceNotePlacement.LeftOf;
				break;
			case Kind.RightOf:
				placement = SequenceNotePlacement.RightOf;
				break;
			case Kind.Over:
				placement = SequenceNotePlacement.Over;
				break;
			default:
				return Fail("'left_of', 'right_of', 'over'");
		}

		_ = Take();
		if (Peek is not (Kind.Actor or Kind.Link))
		{
			return Fail("'ACTOR'");
		}

		var first = Intern(Take());
		var second = -1;
		if (placement == SequenceNotePlacement.Over && Peek == Kind.Comma)
		{
			_ = Take();
			if (Peek is not (Kind.Actor or Kind.Link))
			{
				return Fail("'ACTOR'");
			}

			second = Intern(Take());
		}

		if (!ReadText(out var text, out var wrap))
		{
			return false;
		}

		AddActor(first);
		if (second >= 0)
		{
			AddActor(second);
		}

		_statements.Add(new SequenceNote(placement, _ids[first], second >= 0 ? _ids[second] : null) { Text = text, Wrap = wrap });
		return true;
	}

	/// <summary><c>links</c>, <c>link</c>, <c>properties</c> or <c>details</c>, an actor and <c>text2 NEWLINE</c>.</summary>
	private bool ParseActorData(SequenceActorDataKind kind)
	{
		_ = Take();
		if (Peek is not (Kind.Actor or Kind.Link))
		{
			return Fail("'ACTOR'");
		}

		var index = Intern(Take());
		if (!ReadText(out var text, out _))
		{
			return false;
		}

		AddActor(index);
		_statements.Add(new SequenceActorData(kind, _ids[index], text));
		return true;
	}

	/// <summary>
	/// A block: its keyword, <c>restOfLine</c>, its statements, each section after <paramref name="separator"/> with its
	/// <c>restOfLine</c> and statements, and <c>end</c>.
	/// </summary>
	private bool ParseBlock(SequenceBlockKind kind, Kind separator)
	{
		_ = Take();
		if (Peek != Kind.RestOfLine)
		{
			return Fail("'restOfLine'");
		}

		var text = Plain(SequenceText.ParseMessage(Take().Text(_text), out var wrap));
		_depth++;
		_guard.Depth(_depth, _cursor);
		var from = _statements.Count;
		if (!ParseDocument())
		{
			return false;
		}

		var statements = Statements(from);
		var sectionsFrom = _sections.Count;
		while (separator != Kind.End && Peek == separator)
		{
			_ = Take();
			if (Peek != Kind.RestOfLine)
			{
				return Fail("'restOfLine'");
			}

			var sectionText = Plain(SequenceText.ParseMessage(Take().Text(_text), out var sectionWrap));
			var sectionFrom = _statements.Count;
			if (!ParseDocument())
			{
				return false;
			}

			_sections.Add(new SequenceBlockSection { Text = sectionText, Wrap = sectionWrap, Statements = Statements(sectionFrom) });
		}

		if (Peek != Kind.EndKeyword)
		{
			return Fail("'end'");
		}

		_ = Take();
		_depth--;
		var block = sectionsFrom == _sections.Count
			? new SequenceBlock(kind) { Text = text, Wrap = wrap, Statements = statements }
			: new SequenceBlock(kind) { Text = text, Wrap = wrap, Statements = statements, Sections = Sections(sectionsFrom) };
		_statements.Add(block);
		return true;
	}

	/// <summary>The statements of <see cref="_statements"/> from <paramref name="from"/> on as a list, taken off it.</summary>
	private List<SequenceStatement> Statements(int from)
	{
		var items = _statements.AsSpan()[from..];
		var list = new List<SequenceStatement>(items.Length);
		list.AddRange(items);
		_statements.Truncate(from);
		return list;
	}

	/// <summary>The sections of <see cref="_sections"/> from <paramref name="from"/> on as a list, taken off it.</summary>
	private List<SequenceBlockSection> Sections(int from)
	{
		var items = _sections.AsSpan()[from..];
		var list = new List<SequenceBlockSection>(items.Length);
		list.AddRange(items);
		_sections.Truncate(from);
		return list;
	}

	/// <summary>
	/// The db's <c>addActor</c> checks: a participant that has a box keeps it, and one in another box is an error. A
	/// participant without a box takes the current one.
	/// </summary>
	private void AddActor(int index)
	{
		var box = _actorBox[index];
		if (box == NotAnActor)
		{
			_actorBox[index] = _currentBox;
			return;
		}

		if (_currentBox >= 0 && box >= 0 && box != _currentBox)
		{
			_dbError ??= $"A same participant should only be defined in one Box: {_ids[index]} can't be in '{_boxTitles[box] ?? "undefined"}' and in '{_boxTitles[_currentBox] ?? "undefined"}' at the same time.";
		}

		if (box < 0)
		{
			_actorBox[index] = _currentBox;
		}
	}

	/// <summary>The db's <c>createParticipant</c>.</summary>
	private void Create(int index)
	{
		if (_actorBox[index] != NotAnActor)
		{
			_dbError ??= "It is not possible to have actors with the same id, even if one is destroyed before the next is created. Use 'AS' aliases to simulate the behavior";
		}

		_lastCreated = index;
		AddActor(index);
	}

	/// <summary>The db's checks of <c>addMessage</c> for the last created and the last destroyed participant.</summary>
	private void AddMessage(int from, int to)
	{
		if (_lastCreated >= 0)
		{
			if (to != _lastCreated)
			{
				_dbError ??= "The created participant undefined does not have an associated creating message after its declaration. Please check the sequence diagram.";
			}
			else
			{
				_lastCreated = -1;
			}
		}
		else if (_lastDestroyed >= 0)
		{
			if (to != _lastDestroyed && from != _lastDestroyed)
			{
				_dbError ??= "The destroyed participant undefined does not have an associated destroying message after its declaration. Please check the sequence diagram.";
			}
			else
			{
				_lastDestroyed = -1;
			}
		}
	}

	/// <summary>The db's <c>addSignal</c> for <c>ACTIVE_END</c>, which counts the participant's activations so far.</summary>
	private void Deactivate(int index)
	{
		if (_activations[index] < 1)
		{
			_dbError ??= $"Trying to inactivate an inactive participant ({_ids[index]})";
		}

		_activations[index]--;
	}

	/// <summary>
	/// The db's <c>yaml.load</c> of the <c>@{ }</c> text: in braces when it is one line, and as a document when it spans
	/// lines. The text keeps mermaid.js's entity placeholders, so the <c>#</c> of an entity is not a comment.
	/// </summary>
	private void CheckMetadata(ReadOnlySpan<char> metadata)
	{
		var yaml = new PooledList<char>(metadata.Length + 4);
		try
		{
			if (metadata.Contains('\n'))
			{
				yaml.AddRange(metadata);
				yaml.Add('\n');
			}
			else
			{
				yaml.AddRange("{\n");
				yaml.AddRange(metadata);
				yaml.AddRange("\n}");
			}

			var diagnostics = new List<MermaidDiagnostic>();
			if (YamlReader.Read(yaml.AsSpan(), diagnostics, _context.Limits) is null)
			{
				_dbError ??= diagnostics.Count > 0 ? diagnostics[0].Message : "The @{ } block is not valid YAML.";
			}
		}
		finally
		{
			yaml.Dispose();
		}
	}

	/// <summary>An id, interned, with mermaid.js's entity placeholders turned back into the source's text.</summary>
	private int Intern(SequenceToken token)
	{
		var text = token.Text(_text);
		int index;
		if (!FlowchartText.HasPlaceholder(text))
		{
			index = _ids.GetOrAdd(text);
		}
		else
		{
			_scratch.Truncate(0);
			FlowchartText.Restore(text, ref _scratch);
			index = _ids.GetOrAdd(_scratch.AsSpan());
		}

		while (_actorBox.Count < _ids.Count)
		{
			_actorBox.Add(NotAnActor);
			_activations.Add(0);
		}

		return index;
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
