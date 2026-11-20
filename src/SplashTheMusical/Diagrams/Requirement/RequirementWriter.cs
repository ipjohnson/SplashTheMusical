using System.Text;
using SplashTheMusical.Parsing;
using SplashTheMusical.Writing;

namespace SplashTheMusical.Diagrams.Requirement;

/// <summary>
/// Writes a <see cref="RequirementDocument"/> as <c>requirementDiagram.jison</c> reads it: one statement per line,
/// indented two spaces, and each field of a requirement or an element on a line of its own inside the braces. A name or
/// a value is bare when it lexes as one <c>unqString</c> that is all of it, and quoted otherwise. An id of a
/// <c>style</c>, <c>classDef</c> or <c>class</c> statement is bare when it is one word. <c>a:::b</c> on its own line is
/// written as <c>class a b</c>, which the db reads the same way.
/// </summary>
/// <remarks>
/// A value that no text reads back as itself is refused: text with a <c>"</c> that is not one bare
/// <c>unqString</c>, empty text, text that mermaid.js changes before the lexer reads it, and a statement whose text the
/// rule for <c>direction TB</c> would read as a direction.
/// </remarks>
internal static class RequirementWriter
{
	public static void Write(RequirementDocument document, StringBuilder output)
	{
		ArgumentNullException.ThrowIfNull(document);
		ArgumentNullException.ThrowIfNull(output);
		var start = output.Length;
		output.Append("requirementDiagram\n");
		PreambleWriter.WriteAccessibility(output, document, "  ");
		var directions = new List<int>();
		foreach (var statement in document.Statements)
		{
			output.Append("  ");
			WriteStatement(statement, output, directions);
			output.Append('\n');
		}

		var text = output.ToString(start, output.Length - start);
		CheckDirections(text, directions, start);
		CheckSemicolons(text);
	}

	private static void WriteStatement(RequirementStatement statement, StringBuilder output, List<int> directions)
	{
		switch (statement)
		{
			case RequirementDefinition definition:
				output.Append(Keyword(definition.Kind)).Append(' ');
				WriteString(definition.Name, output);
				WriteClasses(definition.Classes, output);
				output.Append(" {\n");
				WriteField("id", definition.Id, output);
				WriteField("text", definition.Text, output);
				if (definition.Risk is { } risk)
				{
					output.Append("    risk: ").Append(Risk(risk)).Append('\n');
				}

				if (definition.VerifyMethod is { } verifyMethod)
				{
					output.Append("    verifymethod: ").Append(VerifyMethod(verifyMethod)).Append('\n');
				}

				output.Append("  }");
				break;
			case RequirementElement element:
				output.Append("element ");
				WriteString(element.Name, output);
				WriteClasses(element.Classes, output);
				output.Append(" {\n");
				WriteField("type", element.Type, output);
				WriteField("docref", element.DocRef, output);
				output.Append("  }");
				break;
			case RequirementRelationship relationship:
				WriteString(relationship.Source, output);
				output.Append(" - ").Append(Relationship(relationship.Type)).Append(" -> ");
				WriteString(relationship.Target, output);
				break;
			case RequirementClassDefinition definition:
				output.Append("classDef ");
				WriteStyleIds(definition.Names, output);
				output.Append(' ');
				WriteStyles(definition.Styles, output);
				break;
			case RequirementStyle style:
				output.Append("style ");
				WriteStyleIds(style.Ids, output);
				output.Append(' ');
				WriteStyles(style.Styles, output);
				break;
			case RequirementClassAssignment assignment:
				output.Append("class ");
				WriteStyleIds(assignment.Ids, output);
				output.Append(' ');
				WriteStyleIds(assignment.Classes, output);
				break;
			case RequirementDirection direction:
				if (direction.Direction is not ("TB" or "BT" or "RL" or "LR"))
				{
					throw new ArgumentException($"The direction {direction.Direction} is not TB, BT, RL or LR.", nameof(statement));
				}

				directions.Add(output.Length);
				output.Append("direction ").Append(direction.Direction);
				break;
			default:
				throw new ArgumentException($"The statement {statement?.GetType().Name} is not one of the requirement diagram's.", nameof(statement));
		}
	}

	private static void WriteField(string name, string? value, StringBuilder output)
	{
		if (value is null)
		{
			return;
		}

		output.Append("    ").Append(name).Append(": ");
		WriteString(value, output);
		output.Append('\n');
	}

	/// <summary>Classes after <c>:::</c>, which the lexer reads in its initial condition.</summary>
	private static void WriteClasses(List<string>? classes, StringBuilder output)
	{
		if (classes is null)
		{
			return;
		}

		if (classes.Count == 0)
		{
			throw new ArgumentException("A class list needs a class.", nameof(classes));
		}

		output.Append(":::");
		for (var i = 0; i < classes.Count; i++)
		{
			if (i > 0)
			{
				output.Append(',');
			}

			WriteString(classes[i], output);
		}
	}

	/// <summary>An <c>unqString</c> or a <c>qString</c> in the initial condition.</summary>
	private static void WriteString(string text, StringBuilder output)
	{
		ArgumentNullException.ThrowIfNull(text);
		if (RequirementLexer.IsUnquoted(text))
		{
			CheckText(text);
			output.Append(text);
			return;
		}

		WriteQuoted(text, output);
	}

	/// <summary>
	/// A <c>qString</c>, which holds anything but <c>"</c>. A <c>"</c> after <c>=</c> inside an HTML tag would become
	/// <c>'</c>, so a quoted text does not end with <c>=</c>.
	/// </summary>
	private static void WriteQuoted(string text, StringBuilder output)
	{
		if (text.Length == 0 || text.Contains('"', StringComparison.Ordinal) || text[^1] == '=')
		{
			throw new ArgumentException($"The text {text} cannot be written so that it reads back as itself.", nameof(text));
		}

		CheckText(text);
		output.Append('"').Append(text).Append('"');
	}

	/// <summary>The <c>idList</c> of a <c>style</c>, <c>classDef</c> or <c>class</c>: each id one <c>ALPHA</c> or a <c>qString</c>.</summary>
	private static void WriteStyleIds(List<string> ids, StringBuilder output)
	{
		if (ids.Count == 0)
		{
			throw new ArgumentException("An id list needs an id.", nameof(ids));
		}

		for (var i = 0; i < ids.Count; i++)
		{
			if (i > 0)
			{
				output.Append(',');
			}

			if (RequirementLexer.IsAlpha(ids[i]))
			{
				output.Append(ids[i]);
			}
			else
			{
				WriteQuoted(ids[i], output);
			}
		}
	}

	/// <summary>
	/// Styles separated by commas, each made of the tokens of a <c>style</c>: word characters, <c>:</c>, <c>-</c>,
	/// <c>#</c> and <c>;</c>. A <c>;</c> after a <c>#</c> would be taken out by <c>encodeEntities</c> or start an entity.
	/// </summary>
	private static void WriteStyles(List<string> styles, StringBuilder output)
	{
		if (styles.Count == 0)
		{
			throw new ArgumentException("A style statement needs a style.", nameof(styles));
		}

		for (var i = 0; i < styles.Count; i++)
		{
			var style = styles[i];
			var hash = style.IndexOf('#', StringComparison.Ordinal);
			if (style.Length == 0
				|| !style.All(c => Flowchart.FlowchartText.IsWordChar(c) || c is ':' or '-' or '#' or ';')
				|| (hash >= 0 && style.IndexOf(';', hash) >= 0))
			{
				throw new ArgumentException($"The style {style} cannot be written so that it reads back as itself.", nameof(styles));
			}

			output.Append(i > 0 ? "," : "").Append(style);
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
			|| text.Contains("ﬂ°", StringComparison.Ordinal)
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
	/// Refuses <c>direction</c>, space and <c>TB</c>, <c>BT</c>, <c>RL</c> or <c>LR</c> anywhere but in a
	/// <c>direction</c> statement the writer wrote. The rule <c>.*direction\s+TB[^\n]*</c> runs before every other in
	/// the initial condition, reaches any later text of the line, and its space may run into the next line.
	/// </summary>
	private static void CheckDirections(string text, List<int> directions, int start)
	{
		for (var at = text.IndexOf("direction", StringComparison.OrdinalIgnoreCase); at >= 0; at = text.IndexOf("direction", at + 1, StringComparison.OrdinalIgnoreCase))
		{
			if (directions.Contains(start + at))
			{
				continue;
			}

			var space = at + 9;
			while (space < text.Length && PreambleParser.IsSpace(text[space]))
			{
				space++;
			}

			var next = text.AsSpan(space);
			if (space > at + 9 && next.Length >= 2
				&& (next.StartsWith("TB", StringComparison.OrdinalIgnoreCase) || next.StartsWith("BT", StringComparison.OrdinalIgnoreCase)
					|| next.StartsWith("RL", StringComparison.OrdinalIgnoreCase) || next.StartsWith("LR", StringComparison.OrdinalIgnoreCase)))
			{
				throw new ArgumentException($"The text {text} has a value that would read as a direction.", nameof(text));
			}
		}
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

	private static string Keyword(RequirementKind kind) => kind switch
	{
		RequirementKind.Requirement => "requirement",
		RequirementKind.FunctionalRequirement => "functionalRequirement",
		RequirementKind.InterfaceRequirement => "interfaceRequirement",
		RequirementKind.PerformanceRequirement => "performanceRequirement",
		RequirementKind.PhysicalRequirement => "physicalRequirement",
		RequirementKind.DesignConstraint => "designConstraint",
		_ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
	};

	private static string Risk(RequirementRisk risk) => risk switch
	{
		RequirementRisk.Low => "low",
		RequirementRisk.Medium => "medium",
		RequirementRisk.High => "high",
		_ => throw new ArgumentOutOfRangeException(nameof(risk), risk, null),
	};

	private static string VerifyMethod(RequirementVerifyMethod method) => method switch
	{
		RequirementVerifyMethod.Analysis => "analysis",
		RequirementVerifyMethod.Demonstration => "demonstration",
		RequirementVerifyMethod.Inspection => "inspection",
		RequirementVerifyMethod.Test => "test",
		_ => throw new ArgumentOutOfRangeException(nameof(method), method, null),
	};

	private static string Relationship(RequirementRelationshipType type) => type switch
	{
		RequirementRelationshipType.Contains => "contains",
		RequirementRelationshipType.Copies => "copies",
		RequirementRelationshipType.Derives => "derives",
		RequirementRelationshipType.Satisfies => "satisfies",
		RequirementRelationshipType.Verifies => "verifies",
		RequirementRelationshipType.Refines => "refines",
		RequirementRelationshipType.Traces => "traces",
		_ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
	};
}
