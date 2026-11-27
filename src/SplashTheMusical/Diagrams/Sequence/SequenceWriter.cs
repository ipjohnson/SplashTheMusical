using System.Globalization;
using System.Text;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Diagrams.State;
using SplashTheMusical.Parsing;
using SplashTheMusical.Writing;

namespace SplashTheMusical.Diagrams.Sequence;

/// <summary>
/// Writes a <see cref="SequenceDocument"/> as <c>sequenceDiagram.jison</c> reads it: one statement per line, indented
/// two spaces a level, the statements of a box and of each block inside it, and <c>end</c> after them. Messages are
/// written as <c>from-&gt;&gt;to: text</c> and notes as <c>Note over a,b: text</c>. A <c>wrap:</c> or <c>nowrap:</c>
/// goes before the text whose <c>Wrap</c> is set.
/// </summary>
/// <remarks>
/// The grammar has no quotes, so a value that does not read back as itself is refused: an id the lexer would read as
/// another token or cut short where it is written, text with a <c>#</c> or <c>;</c> outside an entity code such as
/// <c>#quot;</c>, a line break in one-line text, space around text the db trims, text that starts with
/// <c>wrap:</c> when its <c>Wrap</c> is not set, a box whose line reads back as another colour or title, and text
/// that mermaid.js changes before the lexer reads it.
/// </remarks>
internal static class SequenceWriter
{
	public static void Write(SequenceDocument document, StringBuilder output)
	{
		ArgumentNullException.ThrowIfNull(document);
		ArgumentNullException.ThrowIfNull(output);
		var start = output.Length;
		output.Append("sequenceDiagram\n");
		if (document.DiagramTitle is { } title)
		{
			if (title.Length == 0)
			{
				throw new ArgumentException("A title needs text.", nameof(document));
			}

			CheckLine(title, trimmed: false);
			output.Append("  title ").Append(title).Append('\n');
		}

		CheckAccessibility(document);
		PreambleWriter.WriteAccessibility(output, document, "  ");
		WriteStatements(document.Statements, output, 1, inBox: false);
		CheckSemicolons(output.ToString(start, output.Length - start));
	}

	private static void WriteStatements(List<SequenceStatement> statements, StringBuilder output, int depth, bool inBox)
	{
		foreach (var statement in statements)
		{
			output.Append(' ', depth * 2);
			if (inBox && statement is not (SequenceParticipant { Created: false } or SequenceDestroy))
			{
				throw new ArgumentException($"A box holds only participants that are not created and destroy statements, not {statement?.GetType().Name}.", nameof(statements));
			}

			WriteStatement(statement, output, depth);
		}
	}

	private static void WriteStatement(SequenceStatement statement, StringBuilder output, int depth)
	{
		switch (statement)
		{
			case SequenceParticipant participant:
				WriteParticipant(participant, output);
				break;
			case SequenceDestroy destroy:
				output.Append("destroy ");
				WriteId(destroy.Id, "\n", SequenceLexer.IdNext.Pop, output);
				output.Append('\n');
				break;
			case SequenceBox box:
				WriteBox(box, output, depth);
				break;
			case SequenceMessage message:
				WriteMessage(message, output);
				break;
			case SequenceNote note:
				output.Append("Note ").Append(note.Placement switch
				{
					SequenceNotePlacement.LeftOf => "left of ",
					SequenceNotePlacement.RightOf => "right of ",
					SequenceNotePlacement.Over => "over ",
					_ => throw new ArgumentOutOfRangeException(nameof(statement), note.Placement, null),
				});
				WriteActor(note.Actor, note.SecondActor is null ? ":" : ",", output);
				if (note.SecondActor is { } second)
				{
					output.Append(',');
					WriteActor(second, ":", output);
				}

				WriteText(note.Text, note.Wrap, output);
				break;
			case SequenceActivation activation:
				output.Append(activation.Active ? "activate " : "deactivate ");
				WriteId(activation.Id, "\n", SequenceLexer.IdNext.Pop, output);
				output.Append('\n');
				break;
			case SequenceAutonumber autonumber:
				WriteAutonumber(autonumber, output);
				break;
			case SequenceActorData data:
				output.Append(data.Kind switch
				{
					SequenceActorDataKind.Links => "links ",
					SequenceActorDataKind.Link => "link ",
					SequenceActorDataKind.Properties => "properties ",
					SequenceActorDataKind.Details => "details ",
					_ => throw new ArgumentOutOfRangeException(nameof(statement), data.Kind, null),
				});
				WriteActor(data.Actor, ":", output);
				WriteText(data.Text, null, output);
				break;
			case SequenceBlock block:
				WriteBlock(block, output, depth);
				break;
			default:
				throw new ArgumentException($"The statement {statement?.GetType().Name} is not one of the sequence diagram's.", nameof(statement));
		}
	}

	/// <summary>
	/// <c>participant id</c> or <c>actor id</c>, with <c>create</c>, <c>@{ … }</c> and <c>as description</c>. The id is
	/// read in the <c>ID</c> condition by the rule for what follows it.
	/// </summary>
	private static void WriteParticipant(SequenceParticipant participant, StringBuilder output)
	{
		if (participant.Created)
		{
			output.Append("create ");
		}

		output.Append(participant.Kind == SequenceParticipantKind.Actor ? "actor " : "participant ");
		if (participant.Metadata is { } metadata)
		{
			if (metadata.Contains('}', StringComparison.Ordinal) || StateLexer.Trim(metadata).Length != metadata.Length)
			{
				throw new ArgumentException($"The metadata {metadata} cannot be written so that it reads back as itself.", nameof(participant));
			}

			CheckText(metadata);
			WriteId(participant.Id, "@{", SequenceLexer.IdNext.Stay, output);
			output.Append("@{ ").Append(metadata).Append(" }");
		}
		else if (participant.Description is not null)
		{
			WriteId(participant.Id, " as x", SequenceLexer.IdNext.Alias, output);
		}
		else
		{
			WriteId(participant.Id, "\n", SequenceLexer.IdNext.Pop, output);
		}

		if (participant.Description is { } description)
		{
			output.Append(" as");
			WriteRestOfLine(description, participant.Wrap, output);
		}
		else if (participant.Wrap is not null)
		{
			throw new ArgumentException("Only a participant with a description has a wrap setting.", nameof(participant));
		}

		output.Append('\n');
	}

	private static void WriteBox(SequenceBox box, StringBuilder output, int depth)
	{
		ArgumentException.ThrowIfNullOrEmpty(box.Color);
		var line = new StringBuilder(box.Color);
		if (box.Wrap is not null || box.Title is not null)
		{
			line.Append(' ').Append(WrapPrefix(box.Wrap)).Append(box.Title);
		}

		var text = line.ToString();
		CheckLine(text, trimmed: true);
		SequenceBoxData.Parse(Encode(text), out var color, out var title, out var wrap);
		var readColor = color.IsEmpty ? SequenceBoxData.Transparent : Restore(color);
		var readTitle = title.IsEmpty ? null : Restore(title);
		if (readColor != box.Color || readTitle != box.Title || wrap != box.Wrap)
		{
			throw new ArgumentException($"The box line {text} reads back as another colour or title.", nameof(box));
		}

		output.Append("box ").Append(text).Append('\n');
		WriteStatements(box.Statements, output, depth + 1, inBox: true);
		output.Append(' ', depth * 2).Append("end\n");
	}

	private static void WriteMessage(SequenceMessage message, StringBuilder output)
	{
		var reverse = message.Modifier is SequenceMessageModifier.CentralConnectionReverse or SequenceMessageModifier.CentralConnectionDual;
		var arrow = Arrow(message.Arrow);
		WriteActor(message.From, reverse ? "()" : arrow, output);
		if (reverse)
		{
			output.Append("()");
		}

		output.Append(arrow);
		switch (message.Modifier)
		{
			case SequenceMessageModifier.Activate:
				output.Append('+');
				break;
			case SequenceMessageModifier.Deactivate:
				// A reverse arrow and -, and -x, -|\ and -|/, are longer arrows.
				output.Append(arrow[^1] == '-' ? " -" : "-");
				if (message.To[0] is 'x' or 'X' or '|')
				{
					output.Append(' ');
				}
				break;
			case SequenceMessageModifier.CentralConnection or SequenceMessageModifier.CentralConnectionDual:
				output.Append("()");
				break;
		}

		WriteActor(message.To, ":", output);
		WriteText(message.Text, message.Wrap, output);
	}

	private static void WriteAutonumber(SequenceAutonumber autonumber, StringBuilder output)
	{
		output.Append("autonumber");
		if (!autonumber.Visible)
		{
			if (autonumber.Start is not null || autonumber.Step is not null)
			{
				throw new ArgumentException("autonumber off has no start or step.", nameof(autonumber));
			}

			output.Append(" off\n");
			return;
		}

		if (autonumber.Start is { } start)
		{
			output.Append(' ').Append(Number(start));
			if (autonumber.Step is { } step)
			{
				output.Append(' ').Append(Number(step));
			}
		}
		else if (autonumber.Step is not null)
		{
			throw new ArgumentException("An autonumber step needs a start.", nameof(autonumber));
		}

		output.Append('\n');
	}

	/// <summary>A number as <c>NUM</c> reads it: digits with at most two after the point.</summary>
	private static string Number(double value)
	{
		var text = value.ToString("0.##", CultureInfo.InvariantCulture);
		if (!double.IsFinite(value) || value < 0 || double.Parse(text, CultureInfo.InvariantCulture) != value)
		{
			throw new ArgumentException($"The number {value} cannot be written with at most two decimals.", nameof(value));
		}

		return text;
	}

	private static void WriteBlock(SequenceBlock block, StringBuilder output, int depth)
	{
		var (keyword, separator) = block.Kind switch
		{
			SequenceBlockKind.Loop => ("loop", null),
			SequenceBlockKind.Rect => ("rect", null),
			SequenceBlockKind.Opt => ("opt", null),
			SequenceBlockKind.Alt => ("alt", "else"),
			SequenceBlockKind.Par => ("par", "and"),
			SequenceBlockKind.ParOver => ("par_over", "and"),
			SequenceBlockKind.Critical => ("critical", "option"),
			SequenceBlockKind.Break => ("break", (string?)null),
			_ => throw new ArgumentOutOfRangeException(nameof(block), block.Kind, null),
		};
		if (separator is null && block.Sections.Count > 0)
		{
			throw new ArgumentException($"A {keyword} block has no sections.", nameof(block));
		}

		output.Append(keyword);
		WriteRestOfLine(block.Text, block.Wrap, output);
		output.Append('\n');
		WriteStatements(block.Statements, output, depth + 1, inBox: false);
		foreach (var section in block.Sections)
		{
			output.Append(' ', depth * 2).Append(separator);
			WriteRestOfLine(section.Text, section.Wrap, output);
			output.Append('\n');
			WriteStatements(section.Statements, output, depth + 1, inBox: false);
		}

		output.Append(' ', depth * 2).Append("end");
		output.Append('\n');
	}

	/// <summary>
	/// An id in the initial condition, which the first <c>ACTOR</c> rule must read up to <paramref name="follow"/>, the
	/// <c>:</c>, <c>,</c>, <c>()</c> or arrow written after it. After a <c>-</c> in the id the rule reads on through a
	/// <c>/</c> or <c>\</c>, so such an id cannot come before a reverse arrow.
	/// </summary>
	private static void WriteActor(string id, string follow, StringBuilder output)
	{
		ArgumentException.ThrowIfNullOrEmpty(id);
		CheckText(id);
		var encoded = Encode(id);
		if (StateLexer.Trim(id).Length != id.Length
			|| SequenceLexer.MatchInitial(encoded + follow, 0, out var length, out _) != SequenceTokenKind.Actor
			|| length != encoded.Length)
		{
			throw new ArgumentException($"The id {id} cannot be written so that it reads back as itself.", nameof(id));
		}

		output.Append(id);
	}

	/// <summary>An id in the <c>ID</c> condition, which the rule for <paramref name="follow"/> must read as all of it.</summary>
	private static void WriteId(string id, string follow, SequenceLexer.IdNext next, StringBuilder output)
	{
		ArgumentException.ThrowIfNullOrEmpty(id);
		CheckText(id);
		var encoded = Encode(id);
		var text = encoded + follow;
		if (StateLexer.Trim(id).Length != id.Length
			|| SequenceLexer.MatchId(text, 0, out var length, out var read) != SequenceTokenKind.Actor
			|| read != next
			|| StateLexer.Trim(text.AsSpan(0, length)).Length != encoded.Length)
		{
			throw new ArgumentException($"The id {id} cannot be written so that it reads back as itself.", nameof(id));
		}

		output.Append(id);
	}

	/// <summary><c>: text</c> and the line end, which <c>TXT</c> reads up to a <c>#</c> or <c>;</c>.</summary>
	private static void WriteText(string text, bool? wrap, StringBuilder output)
	{
		ArgumentNullException.ThrowIfNull(text);
		CheckMessage(text, wrap);
		output.Append(':');
		if (text.Length > 0 || wrap is not null)
		{
			output.Append(' ').Append(WrapPrefix(wrap)).Append(text);
		}

		output.Append('\n');
	}

	/// <summary>A space and <c>restOfLine</c>, <c>[^#\n;]*</c>, or nothing for empty text.</summary>
	private static void WriteRestOfLine(string text, bool? wrap, StringBuilder output)
	{
		ArgumentNullException.ThrowIfNull(text);
		CheckMessage(text, wrap);
		if (text.Length > 0 || wrap is not null)
		{
			output.Append(' ').Append(WrapPrefix(wrap)).Append(text);
		}
	}

	private static string WrapPrefix(bool? wrap) => wrap switch
	{
		true => "wrap:",
		false => "nowrap:",
		null => "",
	};

	/// <summary>Text that <c>parseMessage</c> reads back as itself: trimmed, one line, and without a wrap prefix of its own when <paramref name="wrap"/> is not set.</summary>
	private static void CheckMessage(string text, bool? wrap)
	{
		CheckLine(text, trimmed: true);
		if (wrap is null && SequenceText.WrapPrefixLength(text, out _) > 0)
		{
			throw new ArgumentException($"The text {text} would read as a wrap setting.", nameof(text));
		}
	}

	/// <summary>One line of text without a <c>#</c> or <c>;</c> outside an entity code.</summary>
	private static void CheckLine(string text, bool trimmed)
	{
		CheckText(text);
		if (text.Contains('\n', StringComparison.Ordinal) || (trimmed && StateLexer.Trim(text).Length != text.Length) || Encode(text).AsSpan().IndexOfAny('#', ';') >= 0)
		{
			throw new ArgumentException($"The text {text} cannot be written so that it reads back as itself.", nameof(text));
		}
	}

	private static void CheckAccessibility(SequenceDocument document)
	{
		if (document.AccessibilityTitle is { } title)
		{
			CheckText(title);
			if (title.Contains('\n', StringComparison.Ordinal) || StateLexer.Trim(title).Length != title.Length)
			{
				throw new ArgumentException($"The accessibility title {title} cannot be written so that it reads back as itself.", nameof(document));
			}
		}

		if (document.AccessibilityDescription is { } description)
		{
			CheckText(description);
			if (StateLexer.Trim(description).Length != description.Length || (description.Contains('\n', StringComparison.Ordinal) && description.Contains('}', StringComparison.Ordinal)))
			{
				throw new ArgumentException($"The accessibility description {description} cannot be written so that it reads back as itself.", nameof(document));
			}
		}
	}

	/// <summary>
	/// Refuses text that mermaid.js changes before the lexer reads it: a <c>\r</c>, which <c>cleanupText</c> folds, a
	/// <c>%%{</c> directive, a <c>%%</c> comment at the start of a line, and the placeholders of <c>encodeEntities</c>,
	/// which the parser turns into <c>#</c> and <c>;</c>.
	/// </summary>
	private static void CheckText(string text)
	{
		if (text.Contains('\r', StringComparison.Ordinal)
			|| text.Contains("%%{", StringComparison.Ordinal)
			|| text.Contains(FlowchartText.Ligature, StringComparison.Ordinal)
			|| text.Contains("¶ß", StringComparison.Ordinal)
			|| StartsCommentLine(text))
		{
			throw new ArgumentException($"The text {text} cannot be written so that it reads back as itself.", nameof(text));
		}
	}

	/// <summary>Whether a line of <paramref name="text"/> after its first starts with space and <c>%%</c>.</summary>
	private static bool StartsCommentLine(string text)
	{
		for (var at = text.IndexOf('\n', StringComparison.Ordinal); at >= 0; at = text.IndexOf('\n', at + 1))
		{
			var rest = text.AsSpan(at + 1);
			var space = 0;
			while (space < rest.Length && PreambleParser.IsSpace(rest[space]))
			{
				space++;
			}

			if (rest[space..].StartsWith("%%", StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Refuses a line that <c>encodeEntities</c>' <c>/style.*:\S*#.*;/</c> or <c>/classDef.*:\S*#.*;/</c> would take
	/// the last <c>;</c> out of.
	/// </summary>
	private static void CheckSemicolons(string text)
	{
		foreach (var range in text.AsSpan().Split('\n'))
		{
			var line = text.AsSpan(range);
			if (DropsSemicolon(line, "style") || DropsSemicolon(line, "classDef"))
			{
				throw new ArgumentException($"The line {line} would lose a ';' before the lexer reads it.", nameof(text));
			}
		}
	}

	private static bool DropsSemicolon(ReadOnlySpan<char> line, string keyword)
	{
		var at = line.IndexOf(keyword, StringComparison.Ordinal);
		if (at < 0)
		{
			return false;
		}

		var rest = line[(at + keyword.Length)..];
		for (var colon = rest.IndexOf(':'); colon >= 0; colon = rest[(colon + 1)..].IndexOf(':') is >= 0 and var next ? colon + 1 + next : -1)
		{
			var hash = colon + 1;
			while (hash < rest.Length && rest[hash] != '#' && !PreambleParser.IsSpace(rest[hash]))
			{
				hash++;
			}

			if (hash < rest.Length && rest[hash] == '#' && rest[hash..].Contains(';'))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// <c>encodeEntities</c>' <c>/#\w+;/g</c>: <c>#123;</c> becomes <c>ﬂ°°123¶ß</c> and <c>#name;</c> becomes
	/// <c>ﬂ°name¶ß</c>, as the lexer reads them.
	/// </summary>
	internal static string Encode(string text)
	{
		if (!text.Contains('#', StringComparison.Ordinal))
		{
			return text;
		}

		var output = new StringBuilder(text.Length + 8);
		var at = 0;
		while (at < text.Length)
		{
			var end = at + 1;
			while (text[at] == '#' && end < text.Length && FlowchartText.IsWordChar(text[end]))
			{
				end++;
			}

			if (text[at] == '#' && end > at + 1 && end < text.Length && text[end] == ';')
			{
				var inner = text.AsSpan(at + 1, end - at - 1);
				output.Append(inner.ContainsAnyExceptInRange('0', '9') ? "ﬂ°" : "ﬂ°°").Append(inner).Append("¶ß");
				at = end + 1;
				continue;
			}

			output.Append(text[at]);
			at++;
		}

		return output.ToString();
	}

	private static string Restore(ReadOnlySpan<char> text)
	{
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

	private static string Arrow(SequenceArrow arrow)
	{
		foreach (var (pattern, value) in SequenceLexer.Arrows)
		{
			if (value == arrow)
			{
				return pattern;
			}
		}

		throw new ArgumentOutOfRangeException(nameof(arrow), arrow, null);
	}
}
