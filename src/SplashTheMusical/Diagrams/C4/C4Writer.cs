using System.Text;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Diagrams.State;
using SplashTheMusical.Parsing;

namespace SplashTheMusical.Diagrams.C4;

/// <summary>
/// Writes a <see cref="C4Document"/> as <c>c4Diagram.jison</c> reads it: the header, the title and the accessibility
/// description, then one macro call per line, indented two spaces per boundary, with a boundary's calls between
/// <c>{</c> at the end of its line and <c>}</c> on a line of its own. A positional argument is quoted, and a named one is
/// written <c>$name="value"</c>. An argument with a <c>"</c>, which no quoted text holds, is written bare, which only
/// a comma ends.
/// </summary>
/// <remarks>
/// A document that no text reads back as itself is refused: one with no statement, title or description, a boundary
/// with no calls, a call with no arguments, a value with space at either end, a value with a <c>"</c> that is last, has
/// a comma or starts as another argument does, a name with an <c>=</c>, text that mermaid.js changes before the lexer
/// reads it, and text that the rule for <c>direction TB</c> would read as a direction. C4 has no accessibility title:
/// the grammar hands <c>accTitle</c> to the db's <c>setTitle</c>, so <see cref="C4Document.DiagramTitle"/> holds it.
/// </remarks>
internal static class C4Writer
{
	public static void Write(C4Document document, StringBuilder output)
	{
		ArgumentNullException.ThrowIfNull(document);
		ArgumentNullException.ThrowIfNull(output);
		if (document.AccessibilityTitle is not null)
		{
			throw new ArgumentException("A C4 diagram has no accessibility title. Its accTitle sets DiagramTitle.", nameof(document));
		}

		if (document.Statements.Count == 0 && document.DiagramTitle is null && document.AccessibilityDescription is null)
		{
			throw new ArgumentException("A C4 diagram needs a statement, a title or a description.", nameof(document));
		}

		var start = output.Length;
		output.Append(Header(document.Kind)).Append('\n');
		if (document.DiagramTitle is { } title)
		{
			WriteTitle(title, output);
		}

		if (document.AccessibilityDescription is { } description)
		{
			WriteDescription(description, output);
		}

		WriteStatements(document.Statements, output, 1);
		var text = output.ToString(start, output.Length - start);
		CheckPrepared(text);
		CheckDirections(text);
	}

	private static void WriteTitle(string title, StringBuilder output)
	{
		if (IsLineText(title))
		{
			output.Append("title ").Append(title).Append('\n');
		}
		else if (IsTrimmedLine(title))
		{
			output.Append("accTitle: ").Append(title).Append('\n');
		}
		else
		{
			throw new ArgumentException($"The title {title} cannot be written so that it reads back as itself.", nameof(title));
		}
	}

	private static void WriteDescription(string description, StringBuilder output)
	{
		if (IsLineText(description))
		{
			output.Append("accDescription ").Append(description).Append('\n');
		}
		else if (IsTrimmedLine(description))
		{
			output.Append("accDescr: ").Append(description).Append('\n');
		}
		else if (description.Length > 0 && StateLexer.Trim(description).Length == description.Length && !description.Contains('}', StringComparison.Ordinal))
		{
			output.Append("accDescr {\n").Append(description).Append("\n}\n");
		}
		else
		{
			throw new ArgumentException($"The description {description} cannot be written so that it reads back as itself.", nameof(description));
		}
	}

	private static void WriteStatements(List<C4Statement> statements, StringBuilder output, int depth)
	{
		foreach (var statement in statements)
		{
			ArgumentNullException.ThrowIfNull(statement);
			output.Append(' ', depth * 2).Append(Macro(statement));
			WriteArguments(statement.Arguments, output);
			if (statement is C4Boundary boundary)
			{
				if (boundary.Statements.Count == 0)
				{
					throw new ArgumentException("A boundary needs a statement inside its braces.", nameof(statements));
				}

				output.Append(" {\n");
				WriteStatements(boundary.Statements, output, depth + 1);
				output.Append(' ', depth * 2).Append('}');
			}

			output.Append('\n');
		}
	}

	private static void WriteArguments(List<C4Argument> arguments, StringBuilder output)
	{
		if (arguments.Count == 0)
		{
			throw new ArgumentException("A macro call needs an argument.", nameof(arguments));
		}

		output.Append('(');
		for (var i = 0; i < arguments.Count; i++)
		{
			var argument = arguments[i];
			ArgumentNullException.ThrowIfNull(argument);
			if (i > 0)
			{
				output.Append(", ");
			}

			CheckTrimmed(argument.Value);
			if (argument.Name is { } name)
			{
				CheckTrimmed(name);
				if (name.Contains('=', StringComparison.Ordinal) || argument.Value.Contains('"', StringComparison.Ordinal))
				{
					throw new ArgumentException($"The argument ${name}={argument.Value} cannot be written so that it reads back as itself.", nameof(arguments));
				}

				// STR_VALUE is [^"]+, so an empty value is written as a space, which the grammar trims.
				output.Append('$').Append(name).Append("=\"").Append(argument.Value.Length == 0 ? " " : argument.Value).Append('"');
			}
			else if (!argument.Value.Contains('"', StringComparison.Ordinal))
			{
				output.Append('"').Append(argument.Value).Append('"');
			}
			else if (i < arguments.Count - 1 && !argument.Value.Contains(',', StringComparison.Ordinal) && argument.Value[0] is not ('"' or '$' or ')'))
			{
				output.Append(argument.Value);
			}
			else
			{
				throw new ArgumentException($"The argument {argument.Value} cannot be written so that it reads back as itself.", nameof(arguments));
			}
		}

		output.Append(')');
	}

	/// <summary>Refuses a value the grammar's <c>trim</c> would change.</summary>
	private static void CheckTrimmed(string value)
	{
		if (StateLexer.Trim(value).Length != value.Length)
		{
			throw new ArgumentException($"The value {value} has space at an end, which the grammar trims.", nameof(value));
		}
	}

	/// <summary>
	/// Whether <c>[^#\n;]+</c> reads all of <paramref name="text"/> after <c>title </c> or <c>accDescription </c>: it is
	/// not empty and has no line end, and each <c>#</c> and <c>;</c> is part of an entity code, which
	/// <c>encodeEntities</c> turns into a placeholder first.
	/// </summary>
	private static bool IsLineText(string text)
	{
		if (text.Length == 0 || text.Contains('\n', StringComparison.Ordinal))
		{
			return false;
		}

		for (var at = 0; at < text.Length; at++)
		{
			if (text[at] == ';')
			{
				return false;
			}

			if (text[at] == '#')
			{
				var end = at + 1;
				while (end < text.Length && FlowchartText.IsWordChar(text[end]))
				{
					end++;
				}

				if (end == at + 1 || end >= text.Length || text[end] != ';')
				{
					return false;
				}

				at = end;
			}
		}

		return true;
	}

	/// <summary>Whether <c>[^\n]*</c> and <c>trim</c> read <paramref name="text"/> back as itself after <c>accTitle:</c> or <c>accDescr:</c>.</summary>
	private static bool IsTrimmedLine(string text) =>
		text.Length > 0 && !text.Contains('\n', StringComparison.Ordinal) && StateLexer.Trim(text).Length == text.Length;

	/// <summary>
	/// Refuses text that mermaid.js changes before the lexer reads it: <see cref="FlowchartText.Prepare"/>, with its
	/// entity placeholders turned back, must give the text and the line end it adds.
	/// </summary>
	private static void CheckPrepared(string text)
	{
		var prepared = new PooledList<char>(text.Length + 16);
		var restored = new PooledList<char>(text.Length + 16);
		try
		{
			FlowchartText.Prepare(text, ref prepared, foldSpaceAfterBraces: false);
			FlowchartText.Restore(prepared.AsSpan(), ref restored);
			var same = restored.AsSpan();
			if (same.Length != text.Length + 1 || !same[..text.Length].SequenceEqual(text) || same[^1] != '\n')
			{
				throw new ArgumentException($"The text {text} has a value that mermaid.js changes before it parses it.", nameof(text));
			}
		}
		finally
		{
			prepared.Dispose();
			restored.Dispose();
		}
	}

	/// <summary>
	/// Refuses <c>direction</c>, space and <c>TB</c>, <c>BT</c>, <c>RL</c> or <c>LR</c> anywhere. The rule
	/// <c>.*direction\s+TB[^\n]*</c> runs before every other in the initial condition and reaches any later text of the
	/// line, a quoted argument too, and no grammar rule accepts what it returns.
	/// </summary>
	private static void CheckDirections(string text)
	{
		for (var at = text.IndexOf("direction", StringComparison.Ordinal); at >= 0; at = text.IndexOf("direction", at + 1, StringComparison.Ordinal))
		{
			if (C4Lexer.LastDirection(text, at, out _) >= 0)
			{
				throw new ArgumentException($"The text {text} has a value that would read as a direction.", nameof(text));
			}
		}
	}

	private static string Header(C4DiagramKind kind) => kind switch
	{
		C4DiagramKind.Context => "C4Context",
		C4DiagramKind.Container => "C4Container",
		C4DiagramKind.Component => "C4Component",
		C4DiagramKind.Dynamic => "C4Dynamic",
		C4DiagramKind.Deployment => "C4Deployment",
		_ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
	};

	private static string Macro(C4Statement statement) => statement switch
	{
		C4Element element => element.Kind switch
		{
			C4ElementKind.Person => "Person",
			C4ElementKind.PersonExt => "Person_Ext",
			C4ElementKind.System => "System",
			C4ElementKind.SystemDb => "SystemDb",
			C4ElementKind.SystemQueue => "SystemQueue",
			C4ElementKind.SystemExt => "System_Ext",
			C4ElementKind.SystemDbExt => "SystemDb_Ext",
			C4ElementKind.SystemQueueExt => "SystemQueue_Ext",
			C4ElementKind.Container => "Container",
			C4ElementKind.ContainerDb => "ContainerDb",
			C4ElementKind.ContainerQueue => "ContainerQueue",
			C4ElementKind.ContainerExt => "Container_Ext",
			C4ElementKind.ContainerDbExt => "ContainerDb_Ext",
			C4ElementKind.ContainerQueueExt => "ContainerQueue_Ext",
			C4ElementKind.Component => "Component",
			C4ElementKind.ComponentDb => "ComponentDb",
			C4ElementKind.ComponentQueue => "ComponentQueue",
			C4ElementKind.ComponentExt => "Component_Ext",
			C4ElementKind.ComponentDbExt => "ComponentDb_Ext",
			C4ElementKind.ComponentQueueExt => "ComponentQueue_Ext",
			_ => throw new ArgumentOutOfRangeException(nameof(statement), element.Kind, null),
		},
		C4Boundary boundary => boundary.Kind switch
		{
			C4BoundaryKind.Boundary => "Boundary",
			C4BoundaryKind.EnterpriseBoundary => "Enterprise_Boundary",
			C4BoundaryKind.SystemBoundary => "System_Boundary",
			C4BoundaryKind.ContainerBoundary => "Container_Boundary",
			C4BoundaryKind.DeploymentNode => "Deployment_Node",
			C4BoundaryKind.Node => "Node",
			C4BoundaryKind.NodeL => "Node_L",
			C4BoundaryKind.NodeR => "Node_R",
			_ => throw new ArgumentOutOfRangeException(nameof(statement), boundary.Kind, null),
		},
		C4Relation relation => relation.Kind switch
		{
			C4RelationKind.Rel => "Rel",
			C4RelationKind.BiRel => "BiRel",
			C4RelationKind.RelUp => "Rel_Up",
			C4RelationKind.RelU => "Rel_U",
			C4RelationKind.RelDown => "Rel_Down",
			C4RelationKind.RelD => "Rel_D",
			C4RelationKind.RelLeft => "Rel_Left",
			C4RelationKind.RelL => "Rel_L",
			C4RelationKind.RelRight => "Rel_Right",
			C4RelationKind.RelR => "Rel_R",
			C4RelationKind.RelBack => "Rel_Back",
			C4RelationKind.RelIndex => "RelIndex",
			_ => throw new ArgumentOutOfRangeException(nameof(statement), relation.Kind, null),
		},
		C4Update update => update.Kind switch
		{
			C4UpdateKind.ElementStyle => "UpdateElementStyle",
			C4UpdateKind.RelStyle => "UpdateRelStyle",
			C4UpdateKind.LayoutConfig => "UpdateLayoutConfig",
			_ => throw new ArgumentOutOfRangeException(nameof(statement), update.Kind, null),
		},
		_ => throw new ArgumentException($"The statement {statement.GetType().Name} is not one of the C4 diagram's.", nameof(statement)),
	};
}
