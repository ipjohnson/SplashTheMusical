using System.Text;
using SplashTheMusical.Writing;

namespace SplashTheMusical.Diagrams.Er;

/// <summary>
/// Writes an <see cref="ErDocument"/> as <c>erDiagram.jison</c> reads it: one statement per line, the statements of a
/// subgraph up to its <c>end</c> and indented two spaces per level, and each attribute on a line of its own. A name is
/// quoted unless it lexes as one bare <c>entityName</c>, and a label is always quoted. An attribute with a <c>~</c> has
/// its parts on lines of their own, because the rule for <c>List~T~</c> runs to the last <c>~</c> of the line.
/// </summary>
internal static class ErWriter
{
	public static void Write(ErDocument document, StringBuilder output)
	{
		ArgumentNullException.ThrowIfNull(document);
		ArgumentNullException.ThrowIfNull(output);
		output.Append("erDiagram\n");
		PreambleWriter.WriteAccessibility(output, document, "  ");
		WriteStatements(document.Statements, output, 1);
	}

	private static void WriteStatements(List<ErStatement> statements, StringBuilder output, int depth)
	{
		foreach (var statement in statements)
		{
			Indent(output, depth);
			switch (statement)
			{
				case ErEntity entity:
					WriteEntity(entity, output, depth);
					break;
				case ErRelationship relationship:
					WriteName(relationship.EntityA, output);
					WriteClasses(relationship.ClassesA, output);
					output.Append(' ').Append(CardinalityA(relationship.CardinalityA)).Append(relationship.Identifying ? "--" : "..");
					WriteEntityB(relationship, output);
					WriteClasses(relationship.ClassesB, output);
					output.Append(" : \"").Append(relationship.Role).Append('"');
					break;
				case ErClassDefinition definition:
					output.Append("classDef ");
					WriteList(definition.Names, output);
					output.Append(' ');
					WriteList(definition.Styles, output);
					break;
				case ErStyle style:
					output.Append("style ");
					WriteList(style.Ids, output);
					output.Append(' ');
					WriteList(style.Styles, output);
					break;
				case ErClassAssignment assignment:
					output.Append("class ");
					WriteList(assignment.Ids, output);
					output.Append(' ');
					WriteList(assignment.Classes, output);
					break;
				case ErDirection direction:
					output.Append("direction ").Append(direction.Direction);
					break;
				case ErSubgraph subgraph:
					output.Append("subgraph ");
					WriteName(subgraph.Id, output);
					if (subgraph.Title is { } title)
					{
						output.Append(" [\"").Append(title).Append("\"]");
					}

					output.Append('\n');
					WriteStatements(subgraph.Statements, output, depth + 1);
					Indent(output, depth);
					output.Append("end");
					break;
				default:
					throw new ArgumentException($"The {statement.GetType().Name} statement is not one the writer knows.", nameof(statements));
			}

			output.Append('\n');
		}
	}

	private static void WriteEntity(ErEntity entity, StringBuilder output, int depth)
	{
		WriteName(entity.Name, output);
		if (entity.Alias is { } alias)
		{
			output.Append('[');
			WriteName(alias, output);
			output.Append(']');
		}

		WriteClasses(entity.Classes, output);
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
				output.AppendJoin(", ", attribute.Keys);
			}

			if (attribute.Comment is { } comment)
			{
				Separate(output, separate, depth);
				output.Append('"').Append(comment).Append('"');
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
		if (ErLexer.IsBareAttributeWord(text) || ErLexer.IsTildeWord(text))
		{
			output.Append(text);
			return;
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
			WriteList(classes, output);
		}
	}

	private static void WriteList(List<string> items, StringBuilder output) => output.AppendJoin(',', items);

	/// <summary>An <c>entityName</c>: bare when it lexes as one <c>UNICODE_TEXT</c>, and otherwise an <c>ENTITY_NAME</c> in quotes.</summary>
	private static void WriteName(string name, StringBuilder output)
	{
		if (ErLexer.IsBareEntityName(name)
			&& !name.StartsWith("accTitle", StringComparison.OrdinalIgnoreCase)
			&& !name.StartsWith("accDescr", StringComparison.OrdinalIgnoreCase))
		{
			output.Append(name);
			return;
		}

		output.Append('"').Append(name).Append('"');
	}

	private static void Indent(StringBuilder output, int depth) => output.Append(' ', depth * 2);
}
