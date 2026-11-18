using System.Text;
using SplashTheMusical.Writing;

namespace SplashTheMusical.Diagrams.State;

/// <summary>
/// Writes a <see cref="StateDocument"/> as <c>stateDiagram.jison</c> reads it: one statement per line, the statements
/// of a composite state inside <c>{ }</c> and indented two spaces per level. A note whose text one line cannot hold is
/// written over lines up to <c>end note</c>. A plain <c>state id</c> before a line that starts with a word and holds a
/// <c>{</c> is refused, as no text reads as the two statements.
/// </summary>
internal static class StateWriter
{
	public static void Write(StateDocument document, StringBuilder output)
	{
		ArgumentNullException.ThrowIfNull(document);
		ArgumentNullException.ThrowIfNull(output);
		output.Append(document.Keyword).Append('\n');
		PreambleWriter.WriteAccessibility(output, document, "  ");
		WriteStatements(document.Statements, output, 1);
	}

	private static void WriteStatements(List<StateStatement> statements, StringBuilder output, int depth)
	{
		string? plainState = null;
		foreach (var statement in statements)
		{
			var lineStart = output.Length;
			Indent(output, depth);
			switch (statement)
			{
				case StateDeclaration declaration:
					WriteRef(declaration.State, output);
					WriteDescription(declaration.Description, output);
					break;
				case StateTransition transition:
					WriteRef(transition.From, output);
					output.Append(" --> ");
					WriteRef(transition.To, output);
					WriteDescription(transition.Label, output);
					break;
				case StateBlock block:
					WriteBlock(block, output, depth);
					break;
				case StateDivider:
					output.Append("--");
					break;
				case StateNote note:
					WriteNote(note, output, depth);
					break;
				case StateFloatingNote floating:
					output.Append("note \"").Append(floating.Text).Append("\" as ").Append(floating.Id);
					break;
				case StateClassDefinition definition:
					output.Append("classDef ").Append(definition.Name).Append(' ').Append(definition.Styles);
					break;
				case StateStyle style:
					output.Append("style ").Append(style.Ids).Append(' ').Append(style.Styles);
					break;
				case StateClassAssignment assignment:
					output.Append("class ").Append(assignment.Ids).Append(' ').Append(assignment.ClassName);
					break;
				case StateDirection direction:
					output.Append("direction ").Append(direction.Direction);
					break;
				case StateHideEmptyDescription:
					output.Append("hide empty description");
					break;
				case StateScale scale:
					output.Append("scale ").Append(scale.Width).Append(" width");
					break;
				case StateClick click:
					output.Append("click ");
					WriteRef(click.State, output);
					if (click.Tooltip is { } tooltip)
					{
						output.Append(" \"").Append(click.Url).Append("\" \"").Append(tooltip).Append('"');
					}
					else
					{
						output.Append(" href \"").Append(click.Url).Append('"');
					}

					break;
				default:
					throw new ArgumentException($"A state diagram cannot hold a {statement.GetType().Name}.", nameof(statements));
			}

			if (plainState is not null && ReadsAsStateName(output, lineStart))
			{
				throw new ArgumentException(
					$"The statement after `state {plainState}` cannot be written: its line starts with a word and holds a `{{`, so "
					+ "stateDiagram.jison reads both lines as a state name of more than one word and refuses it.",
					nameof(statements));
			}

			plainState = statement is StateBlock { Kind: StateKind.Default, Description: null, Statements: null } plain ? plain.Id : null;
			output.Append('\n');
		}
	}

	/// <summary>
	/// Whether the line written from <paramref name="lineStart"/> starts with a word character and holds a <c>{</c>, which
	/// after a plain <c>state id</c> matches the lexer's <c>&lt;STATE&gt;\w+\s+\w+.*?\{</c>: <c>\s+</c> crosses the line break.
	/// </summary>
	private static bool ReadsAsStateName(StringBuilder output, int lineStart)
	{
		var at = lineStart;
		while (at < output.Length && output[at] == ' ')
		{
			at++;
		}

		if (at == output.Length || !StateLexer.IsWordChar(output[at]))
		{
			return false;
		}

		for (; at < output.Length && output[at] != '\n'; at++)
		{
			if (output[at] == '{')
			{
				return true;
			}
		}

		return false;
	}

	private static void WriteBlock(StateBlock block, StringBuilder output, int depth)
	{
		output.Append("state ");
		if (block.Kind != StateKind.Default)
		{
			output.Append(block.Id).Append(' ').Append(block.Kind switch
			{
				StateKind.Fork => "<<fork>>",
				StateKind.Join => "<<join>>",
				_ => "<<choice>>",
			});
			return;
		}

		if (block.Description is { } description)
		{
			output.Append('"').Append(description).Append("\" as ");
		}

		output.Append(block.Id);
		if (block.Statements is { } statements)
		{
			output.Append(" {\n");
			WriteStatements(statements, output, depth + 1);
			Indent(output, depth);
			output.Append('}');
		}
	}

	/// <summary>
	/// The one-line form, <c>: text</c>, holds text without <c>:</c>, <c>;</c> or a line break. Other text goes on the
	/// lines before <c>end note</c>.
	/// </summary>
	private static void WriteNote(StateNote note, StringBuilder output, int depth)
	{
		output.Append(note.Position == StateNotePosition.LeftOf ? "note left of " : "note right of ").Append(note.Id);
		var text = note.Text;
		if (text.Length > 0 && text.AsSpan().IndexOfAny(':', ';', '\n') < 0)
		{
			output.Append(" : ").Append(text);
			return;
		}

		output.Append('\n').Append(text).Append('\n');
		Indent(output, depth);
		output.Append("end note");
	}

	private static void WriteRef(StateRef state, StringBuilder output)
	{
		output.Append(state.Id);
		if (state.ClassName is { } className)
		{
			output.Append(":::").Append(className);
		}
	}

	private static void WriteDescription(string? description, StringBuilder output)
	{
		if (description is not null)
		{
			output.Append(" : ").Append(description);
		}
	}

	private static void Indent(StringBuilder output, int depth) => output.Append(' ', depth * 2);
}
