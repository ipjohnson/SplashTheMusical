using System.Text;
using SplashTheMusical.Parsing;
using SplashTheMusical.Writing;

namespace SplashTheMusical.Diagrams.Er;

/// <summary>
/// Writes an <see cref="ErDocument"/> as <c>erDiagram.jison</c> reads it: one statement per line, the statements of a
/// subgraph up to its <c>end</c> and indented two spaces per level, and each attribute on a line of its own. A name is
/// quoted unless it lexes as one bare <c>entityName</c>, and a label is always quoted. An attribute with a <c>~</c> has
/// its parts on lines of their own, because the rule for <c>List~T~</c> runs to the last <c>~</c> of the line. A label
/// writes <c>"</c> as <c>#quot;</c>, which mermaid.js draws as the same text. A value that no text reads back as itself
/// is refused, and so is a statement whose text the rule for <c>direction TB</c> would read as a direction.
/// </summary>
internal static class ErWriter
{
	public static void Write(ErDocument document, StringBuilder output)
	{
		ArgumentNullException.ThrowIfNull(document);
		ArgumentNullException.ThrowIfNull(output);
		output.Append("erDiagram\n");
		PreambleWriter.WriteAccessibility(output, document, "  ");
		var initial = new List<(int Start, int End)>();
		WriteStatements(document.Statements, output, 1, initial);
		CheckDirections(output, initial);
	}

	/// <param name="initial">
	/// The text of each statement the lexer reads in its initial condition, where the rule for <c>direction</c> runs, and
	/// that has <c>direction</c> in one of its values.
	/// </param>
	private static void WriteStatements(List<ErStatement> statements, StringBuilder output, int depth, List<(int Start, int End)> initial)
	{
		foreach (var statement in statements)
		{
			Indent(output, depth);
			var start = output.Length;
			switch (statement)
			{
				case ErEntity entity:
					WriteEntity(entity, output, depth, initial);
					break;
				case ErRelationship relationship:
					WriteName(relationship.EntityA, output);
					WriteClasses(relationship.ClassesA, output);
					output.Append(' ').Append(CardinalityA(relationship.CardinalityA)).Append(relationship.Identifying ? "--" : "..");
					WriteEntityB(relationship, output);
					WriteClasses(relationship.ClassesB, output);
					output.Append(" : ");
					WriteQuoted(relationship.Role, output);
					Record(statement, start, output.Length, initial);
					break;
				case ErClassDefinition definition:
					output.Append("classDef ");
					WriteStyleIds(definition.Names, output);
					output.Append(' ');
					WriteStyles(definition.Styles, output);
					break;
				case ErStyle style:
					output.Append("style ");
					WriteStyleIds(style.Ids, output);
					output.Append(' ');
					WriteStyles(style.Styles, output);
					break;
				case ErClassAssignment assignment:
					output.Append("class ");
					WriteIds(assignment.Ids, output);
					output.Append(' ');
					WriteIds(assignment.Classes, output);
					Record(statement, start, output.Length, initial);
					break;
				case ErDirection direction:
					if (direction.Direction is not ("TB" or "BT" or "RL" or "LR"))
					{
						throw new ArgumentException($"The direction {direction.Direction} is not TB, BT, RL or LR.", nameof(statements));
					}

					output.Append("direction ").Append(direction.Direction);
					break;
				case ErSubgraph subgraph:
					output.Append("subgraph ");
					WriteName(subgraph.Id, output);
					if (subgraph.Title is { } title)
					{
						CheckText(title, "subgraph title");
						if (!ErLexer.IsQuotedEntityName(title))
						{
							throw new ArgumentException($"The subgraph title {title} cannot be written so that it reads back as itself.", nameof(statements));
						}

						output.Append(" [\"").Append(title).Append("\"]");
					}

					Record(statement, start, output.Length, initial);
					output.Append('\n');
					WriteStatements(subgraph.Statements, output, depth + 1, initial);
					Indent(output, depth);
					output.Append("end");
					break;
				default:
					throw new ArgumentException($"The {statement.GetType().Name} statement is not one the writer knows.", nameof(statements));
			}

			output.Append('\n');
		}
	}

	private static void WriteEntity(ErEntity entity, StringBuilder output, int depth, List<(int Start, int End)> initial)
	{
		var start = output.Length;
		WriteName(entity.Name, output);
		if (entity.Alias is { } alias)
		{
			output.Append('[');
			WriteName(alias, output);
			output.Append(']');
		}

		WriteClasses(entity.Classes, output);
		Record(entity, start, output.Length, initial);
		if (entity.Attributes is not { } attributes)
		{
			return;
		}

		output.Append(" {\n");
		foreach (var attribute in attributes)
		{
			var separate = attribute.Type.Contains('~') || attribute.Name.Contains('~') || (attribute.Comment?.Contains('~') ?? false);
			Indent(output, depth + 1);
			var optional = attribute.Type.Length > 1 && attribute.Type[^1] == '?';
			WriteAttributeWord(optional ? attribute.Type[..^1] : attribute.Type, output);
			if (optional)
			{
				output.Append('?');
			}

			Separate(output, separate, depth);
			WriteAttributeWord(attribute.Name, output);
			if (attribute.Keys.Count > 0)
			{
				Separate(output, separate, depth);
				for (var i = 0; i < attribute.Keys.Count; i++)
				{
					var key = attribute.Keys[i];
					if (!ErLexer.IsAttributeKey(key))
					{
						throw new ArgumentException($"The attribute key {key} is not PK, FK or UK.", nameof(entity));
					}

					output.Append(i > 0 ? ", " : "").Append(key);
				}
			}

			if (attribute.Comment is { } comment)
			{
				Separate(output, separate, depth);
				WriteQuoted(comment, output);
			}

			output.Append('\n');
		}

		Indent(output, depth);
		output.Append('}');
	}

	private static void Separate(StringBuilder output, bool newLine, int depth)
	{
		if (newLine)
		{
			output.Append('\n');
			Indent(output, depth + 2);
		}
		else
		{
			output.Append(' ');
		}
	}

	/// <summary>
	/// An attribute's type or name: bare, as a <c>List~T~</c> word, or between backticks. A type of the grammar's
	/// <c>ATTRIBUTE_WORD '?'</c> is written with its <c>?</c> after it.
	/// </summary>
	private static void WriteAttributeWord(string text, StringBuilder output)
	{
		CheckText(text, "attribute word");
		if (text.Contains('"', StringComparison.Ordinal))
		{
			throw new ArgumentException($"The attribute word {text} holds a quote.", nameof(text));
		}

		if (ErLexer.IsBareAttributeWord(text) || (ErLexer.IsTildeWord(text) && !text.StartsWith("%%", StringComparison.Ordinal)))
		{
			output.Append(text);
			return;
		}

		if (text.Length == 0 || text.Contains('`', StringComparison.Ordinal) || ErLexer.StartsTildeWord(text))
		{
			throw new ArgumentException($"The attribute word {text} cannot be written so that it reads back as itself.", nameof(text));
		}

		output.Append('`').Append(text).Append('`');
	}

	/// <summary>
	/// <c>u</c> lexes as a cardinality only before <c>.</c>, <c>-</c> or <c>|</c>, so after the line it is written against
	/// the second entity's name.
	/// </summary>
	private static void WriteEntityB(ErRelationship relationship, StringBuilder output)
	{
		if (relationship.CardinalityB == ErCardinality.MdParent)
		{
			output.Append('u');
			WriteName(relationship.EntityB, output);
			return;
		}

		output.Append(relationship.CardinalityB switch
		{
			ErCardinality.OnlyOne => "|| ",
			ErCardinality.ZeroOrOne => "o| ",
			ErCardinality.OneOrMore => "|{ ",
			_ => "o{ ",
		});
		WriteName(relationship.EntityB, output);
	}

	private static string CardinalityA(ErCardinality cardinality) => cardinality switch
	{
		ErCardinality.OnlyOne => "||",
		ErCardinality.ZeroOrOne => "|o",
		ErCardinality.OneOrMore => "}|",
		ErCardinality.ZeroOrMore => "}o",
		_ => "u",
	};

	private static void WriteClasses(List<string>? classes, StringBuilder output)
	{
		if (classes is not null)
		{
			output.Append(":::");
			WriteIds(classes, output);
		}
	}

	/// <summary>An <c>idList</c> the lexer reads in its initial condition: each id one <c>UNICODE_TEXT</c>.</summary>
	private static void WriteIds(List<string> ids, StringBuilder output)
	{
		if (ids.Count == 0)
		{
			throw new ArgumentException("An id list needs an id.", nameof(ids));
		}

		for (var i = 0; i < ids.Count; i++)
		{
			var id = ids[i];
			if (!ErLexer.IsBareEntityName(id) || id.Equals("accTitle", StringComparison.OrdinalIgnoreCase) || id.Equals("accDescr", StringComparison.OrdinalIgnoreCase))
			{
				throw new ArgumentException($"The id {id} is not one word.", nameof(ids));
			}

			output.Append(i > 0 ? "," : "").Append(id);
		}
	}

	/// <summary>The <c>idList</c> of a <c>style</c> or <c>classDef</c>: each id one <c>STYLE_TEXT</c>.</summary>
	private static void WriteStyleIds(List<string> ids, StringBuilder output)
	{
		if (ids.Count == 0)
		{
			throw new ArgumentException("An id list needs an id.", nameof(ids));
		}

		for (var i = 0; i < ids.Count; i++)
		{
			if (!ErLexer.IsStyleText(ids[i]))
			{
				throw new ArgumentException($"The id {ids[i]} is not one word.", nameof(ids));
			}

			output.Append(i > 0 ? "," : "").Append(ids[i]);
		}
	}

	private static void WriteStyles(List<string> styles, StringBuilder output)
	{
		if (styles.Count == 0)
		{
			throw new ArgumentException("A style statement needs a style.", nameof(styles));
		}

		for (var i = 0; i < styles.Count; i++)
		{
			if (!ErLexer.IsStyle(styles[i]))
			{
				throw new ArgumentException($"The style {styles[i]} cannot be written so that it reads back as itself.", nameof(styles));
			}

			output.Append(i > 0 ? "," : "").Append(styles[i]);
		}
	}

	/// <summary>A <c>WORD</c> or <c>COMMENT</c> between double quotes, with <c>"</c> written as <c>#quot;</c>.</summary>
	private static void WriteQuoted(string text, StringBuilder output)
	{
		CheckText(text, "label");
		output.Append('"');
		foreach (var c in text)
		{
			_ = c == '"' ? output.Append("#quot;") : output.Append(c);
		}

		output.Append('"');
	}

	/// <summary>
	/// Refuses text that mermaid.js changes before the lexer reads it: a <c>\r</c>, which <c>cleanupText</c> folds, a
	/// <c>%%{</c> directive, a <c>%%</c> comment at the start of a line, and the placeholders of <c>encodeEntities</c>,
	/// which the parser turns into <c>#</c> and <c>;</c>.
	/// </summary>
	private static void CheckText(string text, string what)
	{
		if (text.Contains('\r', StringComparison.Ordinal)
			|| text.Contains("%%{", StringComparison.Ordinal)
			|| text.Contains("ﬂ°", StringComparison.Ordinal)
			|| text.Contains("¶ß", StringComparison.Ordinal)
			|| StartsCommentLine(text))
		{
			throw new ArgumentException($"The {what} {text} cannot be written so that it reads back as itself.", nameof(text));
		}
	}

	/// <summary>Whether a line of <paramref name="text"/> after its first starts with space and <c>%%</c>.</summary>
	private static bool StartsCommentLine(string text)
	{
		for (var at = text.AsSpan().IndexOfAny('\n', (char)0x2028, (char)0x2029); at >= 0 && at < text.Length;)
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

			var next = rest.IndexOfAny('\n', (char)0x2028, (char)0x2029);
			at = next < 0 ? -1 : at + 1 + next;
		}

		return false;
	}

	/// <summary>
	/// Records the text of a statement with <c>direction</c> in one of its values. The writer's own text never forms the
	/// word, so only such a statement can match the rule.
	/// </summary>
	private static void Record(ErStatement statement, int start, int end, List<(int Start, int End)> initial)
	{
		var mentions = statement switch
		{
			ErEntity entity => Mentions(entity.Name) || Mentions(entity.Alias) || Mentions(entity.Classes),
			ErRelationship relationship => Mentions(relationship.EntityA) || Mentions(relationship.EntityB) || Mentions(relationship.Role)
				|| Mentions(relationship.ClassesA) || Mentions(relationship.ClassesB),
			ErClassAssignment assignment => Mentions(assignment.Ids) || Mentions(assignment.Classes),
			ErSubgraph subgraph => Mentions(subgraph.Id) || Mentions(subgraph.Title),
			_ => false,
		};
		if (mentions)
		{
			initial.Add((start, end));
		}
	}

	private static bool Mentions(string? text) => text is not null && text.Contains("direction", StringComparison.OrdinalIgnoreCase);

	private static bool Mentions(List<string>? texts) => texts is not null && texts.Exists(Mentions);

	/// <summary>
	/// Refuses <c>direction</c>, space and <c>TB</c>, <c>BT</c>, <c>RL</c> or <c>LR</c> in the text of a statement the
	/// lexer reads in its initial condition. The rule <c>.*direction\s+TB[^\n]*</c> runs before every other there, and
	/// its space may run into the next line.
	/// </summary>
	private static void CheckDirections(StringBuilder output, List<(int Start, int End)> initial)
	{
		if (initial.Count == 0)
		{
			return;
		}

		var text = output.ToString();
		foreach (var (start, end) in initial)
		{
			for (var at = start; at + 9 <= end; at++)
			{
				if (!text.AsSpan(at, 9).Equals("direction", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				var after = at + 9;
				var space = after;
				while (space < text.Length && PreambleParser.IsSpace(text[space]))
				{
					space++;
				}

				var next = text.AsSpan(space);
				if (space > after && next.Length >= 2
					&& (next.StartsWith("TB", StringComparison.OrdinalIgnoreCase) || next.StartsWith("BT", StringComparison.OrdinalIgnoreCase)
						|| next.StartsWith("RL", StringComparison.OrdinalIgnoreCase) || next.StartsWith("LR", StringComparison.OrdinalIgnoreCase)))
				{
					throw new ArgumentException($"The statement {text[start..end]} would read as a direction.", nameof(initial));
				}
			}
		}
	}

	/// <summary>An <c>entityName</c>: bare when it lexes as one <c>UNICODE_TEXT</c>, and otherwise an <c>ENTITY_NAME</c> in quotes.</summary>
	private static void WriteName(string name, StringBuilder output)
	{
		CheckText(name, "name");
		if (ErLexer.IsBareEntityName(name)
			&& !name.StartsWith("accTitle", StringComparison.OrdinalIgnoreCase)
			&& !name.StartsWith("accDescr", StringComparison.OrdinalIgnoreCase))
		{
			output.Append(name);
			return;
		}

		if (!ErLexer.IsQuotedEntityName(name))
		{
			throw new ArgumentException($"The name {name} cannot be written so that it reads back as itself.", nameof(name));
		}

		output.Append('"').Append(name).Append('"');
	}

	private static void Indent(StringBuilder output, int depth) => output.Append(' ', depth * 2);
}
