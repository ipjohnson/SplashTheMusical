using System.Text;
using SplashTheMusical.Writing;

namespace SplashTheMusical.Diagrams.Class;

/// <summary>
/// Writes a <see cref="ClassDocument"/> as <c>classDiagram.jison</c> reads it: one statement per line, the statements
/// of a namespace and the members of a class inside <c>{ }</c>, indented two spaces per level. A name is written as it
/// is when its text reads as the tokens of a name, and otherwise between backquotes. Quoted text writes <c>"</c> as
/// <c>#quot;</c>, and the text after a colon writes <c>:</c>, <c>;</c> and a line break as <c>#58;</c>, <c>#59;</c>
/// and a space, which mermaid.js draws as the same text. A name, annotation or class that no text reads back as
/// itself is refused.
/// </summary>
internal static class ClassWriter
{
	public static void Write(ClassDocument document, StringBuilder output)
	{
		ArgumentNullException.ThrowIfNull(document);
		ArgumentNullException.ThrowIfNull(output);
		output.Append(document.Keyword).Append('\n');
		PreambleWriter.WriteAccessibility(output, document, "  ");
		WriteStatements(document.Statements, output, 1, inNamespace: false);
	}

	private static void WriteStatements(List<ClassStatement> statements, StringBuilder output, int depth, bool inNamespace)
	{
		foreach (var statement in statements)
		{
			Indent(output, depth);
			switch (statement)
			{
				case ClassDefinition definition:
					WriteDefinition(definition, output, depth);
					break;
				case ClassNote note:
					output.Append("note ");
					if (note.ClassName is { } noteClass)
					{
						output.Append("for ");
						WriteName(noteClass, output);
						output.Append(' ');
					}

					WriteQuoted(note.Text, output);
					break;
				case ClassNamespace space:
					output.Append("namespace ");
					WriteName(space.Name, output, generic: false);
					if (space.Label is { } label)
					{
						output.Append('[');
						WriteQuoted(label, output);
						output.Append(']');
					}

					output.Append(" {\n");
					WriteStatements(space.Statements, output, depth + 1, inNamespace: true);
					Indent(output, depth);
					output.Append('}');
					break;
				case ClassRelation relation when !inNamespace:
					WriteRelation(relation, output);
					break;
				case ClassMemberStatement member when !inNamespace:
					WriteName(member.ClassName, output);
					if (member.Member is { } text)
					{
						output.Append(" : ");
						WriteLabel(text, output);
					}

					break;
				case ClassAnnotation annotation when !inNamespace:
					output.Append("<<").Append(AlphaNum(annotation.Annotation, "annotation")).Append(">> ");
					WriteName(annotation.ClassName, output);
					break;
				case ClassInteraction interaction when !inNamespace:
					WriteInteraction(interaction, output);
					break;
				case ClassStyle style when !inNamespace:
					output.Append("style ").Append(Alpha(style.Id, "style id")).Append(' ');
					WriteStyles(style.Styles, output);
					break;
				case ClassStyleDefinition definition when !inNamespace:
					output.Append("classDef ");
					for (var i = 0; i < definition.Names.Count; i++)
					{
						output.Append(i > 0 ? "," : "").Append(Alpha(definition.Names[i], "classDef name"));
					}

					output.Append(' ');
					WriteStyles(definition.Styles, output);
					break;
				case ClassCssClass cssClass when !inNamespace:
					output.Append("cssClass ");
					WriteQuoted(cssClass.Ids, output);
					output.Append(' ').Append(Alpha(cssClass.ClassName, "cssClass class"));
					break;
				case ClassDirection direction when !inNamespace:
					output.Append("direction ").Append(direction.Direction);
					break;
				default:
					throw new ArgumentException(
						inNamespace
							? $"A namespace cannot hold a {statement.GetType().Name}."
							: $"A class diagram cannot hold a {statement.GetType().Name}.",
						nameof(statements));
			}

			output.Append('\n');
		}
	}

	private static void WriteDefinition(ClassDefinition definition, StringBuilder output, int depth)
	{
		if (definition.CssClass is not null && definition.Annotation is not null)
		{
			throw new ArgumentException($"The class {definition.Name} has both a css class and an annotation, which one class statement cannot hold.", nameof(definition));
		}

		output.Append("class ");
		WriteName(definition.Name, output);
		if (definition.Label is { } label)
		{
			output.Append('[');
			WriteQuoted(label, output);
			output.Append(']');
		}

		if (definition.CssClass is { } cssClass)
		{
			output.Append(":::").Append(AlphaNum(cssClass, "css class"));
		}

		if (definition.Annotation is { } annotation)
		{
			output.Append(" <<").Append(AlphaNum(annotation, "annotation")).Append(">>");
		}

		if (definition.Members is not { } members)
		{
			return;
		}

		output.Append(" {\n");
		foreach (var member in members)
		{
			if (member.AsSpan().IndexOfAny('{', '}', '\n') >= 0)
			{
				throw new ArgumentException($"The member {member} of {definition.Name} holds a brace or a line break.", nameof(definition));
			}

			Indent(output, depth + 1);
			output.Append(member).Append('\n');
		}

		// A class with a css class needs a member line, and a blank one adds no member.
		if (members.Count == 0 && definition.CssClass is not null)
		{
			Indent(output, depth + 1);
			output.Append('\n');
		}

		Indent(output, depth);
		output.Append('}');
	}

	private static void WriteRelation(ClassRelation relation, StringBuilder output)
	{
		WriteName(relation.From, output);
		output.Append(' ');
		if (relation.FromCardinality is { } from)
		{
			WriteQuoted(from, output);
			output.Append(' ');
		}

		output.Append(relation.FromEnd switch
		{
			ClassRelationEnd.Aggregation => "o",
			ClassRelationEnd.Extension => "<|",
			ClassRelationEnd.Composition => "*",
			ClassRelationEnd.Dependency => "<",
			ClassRelationEnd.Lollipop => "()",
			_ => "",
		});
		output.Append(relation.Line == ClassLineStyle.Dotted ? ".." : "--");
		output.Append(relation.ToEnd switch
		{
			ClassRelationEnd.Aggregation => "o",
			ClassRelationEnd.Extension => "|>",
			ClassRelationEnd.Composition => "*",
			ClassRelationEnd.Dependency => ">",
			ClassRelationEnd.Lollipop => "()",
			_ => "",
		});
		output.Append(' ');
		if (relation.ToCardinality is { } to)
		{
			WriteQuoted(to, output);
			output.Append(' ');
		}

		WriteName(relation.To, output);
		if (relation.Label is { } label)
		{
			output.Append(" : ");
			WriteLabel(label, output);
		}
	}

	private static void WriteInteraction(ClassInteraction interaction, StringBuilder output)
	{
		switch (interaction.Kind)
		{
			case ClassInteractionKind.Callback:
				output.Append("callback ");
				WriteName(interaction.ClassName, output);
				output.Append(' ');
				WriteQuoted(interaction.Target, output);
				WriteTooltip(interaction, output);
				break;
			case ClassInteractionKind.Link:
				output.Append("link ");
				WriteName(interaction.ClassName, output);
				output.Append(' ');
				WriteQuoted(interaction.Target, output);
				WriteTooltip(interaction, output);
				WriteLinkTarget(interaction, output);
				break;
			case ClassInteractionKind.ClickCall:
				if (interaction.Target.Length == 0 || ClassLexer.IsSpace(interaction.Target[0]) || interaction.Target.Contains('(', StringComparison.Ordinal)
					|| interaction.Arguments?.Contains(')', StringComparison.Ordinal) == true)
				{
					throw new ArgumentException($"The callback {interaction.Target} of {interaction.ClassName} cannot be written after call.", nameof(interaction));
				}

				output.Append("click ");
				WriteName(interaction.ClassName, output);
				output.Append(" call ").Append(interaction.Target).Append('(').Append(interaction.Arguments).Append(')');
				WriteTooltip(interaction, output);
				break;
			default:
				output.Append("click ");
				WriteName(interaction.ClassName, output);
				output.Append(" href ");
				WriteQuoted(interaction.Target, output);
				WriteTooltip(interaction, output);
				WriteLinkTarget(interaction, output);
				break;
		}
	}

	private static void WriteTooltip(ClassInteraction interaction, StringBuilder output)
	{
		if (interaction.Tooltip is { } tooltip)
		{
			output.Append(' ');
			WriteQuoted(tooltip, output);
		}
	}

	private static void WriteLinkTarget(ClassInteraction interaction, StringBuilder output)
	{
		if (interaction.LinkTarget is { } target)
		{
			if (target is not ("_self" or "_blank" or "_parent" or "_top"))
			{
				throw new ArgumentException($"The link target {target} is not _self, _blank, _parent or _top.", nameof(interaction));
			}

			output.Append(' ').Append(target);
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
			output.Append(i > 0 ? "," : "").Append(styles[i]);
		}
	}

	/// <summary>
	/// A class name: as it is when it reads as name tokens, between backquotes otherwise, and a generic
	/// <c>~type~</c> at its end after either.
	/// </summary>
	private static void WriteName(string name, StringBuilder output, bool generic = true)
	{
		if (generic && name.Length > 2 && name[^1] == '~')
		{
			var open = name.LastIndexOf('~', name.Length - 2);
			if (open > 0 && open < name.Length - 2 && IsGenericType(name[(open + 1)..^1]) && TryWriteBase(name[..open], output))
			{
				output.Append(name.AsSpan(open));
				return;
			}
		}

		if (!TryWriteBase(name, output))
		{
			throw new ArgumentException($"The name {name} cannot be written so that it reads back as itself.", nameof(name));
		}
	}

	private static bool TryWriteBase(string name, StringBuilder output)
	{
		if (ClassLexer.ReadsAsName(name))
		{
			output.Append(name);
			return true;
		}

		if (name.Length == 0 || name.Contains('`', StringComparison.Ordinal) || name[0] is '"' or '~' || StartsWithKeyword(name))
		{
			return false;
		}

		output.Append('`').Append(name).Append('`');
		return true;
	}

	/// <summary>Whether the text of <c>~type~</c> reads back as one <c>GENERICTYPE</c>.</summary>
	private static bool IsGenericType(string type) =>
		type.Length > 0 && !type.Contains('~', StringComparison.Ordinal) && type[0] != '"' && !StartsWithKeyword(type);

	/// <summary>The rules every condition tries before the text of a backquote or a generic.</summary>
	private static bool StartsWithKeyword(string text)
	{
		if (text.StartsWith("<<", StringComparison.Ordinal) || text.StartsWith(">>", StringComparison.Ordinal))
		{
			return true;
		}

		foreach (var keyword in (ReadOnlySpan<string>)["cssClass", "callback", "link", "click", "note", "href"])
		{
			if (text.StartsWith(keyword, StringComparison.Ordinal) && (text.Length == keyword.Length || !ClassLexer.IsWordChar(text[keyword.Length])))
			{
				return true;
			}
		}

		return false;
	}

	private static string AlphaNum(string text, string what) =>
		ClassLexer.ReadsAsAlphaNum(text) ? text : throw new ArgumentException($"The {what} {text} is not one word.", nameof(text));

	private static string Alpha(string text, string what) =>
		ClassLexer.ReadsAsAlpha(text) ? text : throw new ArgumentException($"The {what} {text} is not one word.", nameof(text));

	/// <summary>A <c>STR</c>, between double quotes, with <c>"</c> written as <c>#quot;</c>. An empty string has no token, so it is a space.</summary>
	private static void WriteQuoted(string text, StringBuilder output)
	{
		output.Append('"');
		if (text.Length == 0)
		{
			output.Append(' ');
		}

		foreach (var c in text)
		{
			_ = c == '"' ? output.Append("#quot;") : output.Append(c);
		}

		output.Append('"');
	}

	/// <summary>The text of a <c>LABEL</c>, which ends at a colon, a semicolon or a line break.</summary>
	private static void WriteLabel(string text, StringBuilder output)
	{
		foreach (var c in text)
		{
			_ = c switch
			{
				':' => output.Append("#58;"),
				';' => output.Append("#59;"),
				'\n' or '\r' => output.Append(' '),
				_ => output.Append(c),
			};
		}
	}

	private static void Indent(StringBuilder output, int depth) => output.Append(' ', depth * 2);
}
