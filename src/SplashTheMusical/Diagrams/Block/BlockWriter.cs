using System.Globalization;
using System.Text;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Diagrams.State;
using SplashTheMusical.Parsing;

namespace SplashTheMusical.Diagrams.Block;

/// <summary>
/// Writes a <see cref="BlockDocument"/> as <c>block.jison</c> reads it: one statement per line, indented two spaces
/// for each composite around it, with a composite's statements between <c>block</c> or <c>block:id</c> and
/// <c>end</c>. Each shape has one pair of brackets, a <see cref="BlockShape.Na"/> block with a label is written
/// <c>id["label"/]</c>, a link without a label is written in its shortest form, and a labelled link as
/// <c>-- "label" --&gt;</c>.
/// </summary>
/// <remarks>
/// A value that no text reads back as itself is refused: an id that is not one <c>NODE_ID</c> or that a keyword rule
/// would read, a label with a <c>"</c> or that starts with <c>`</c>, a link without a label that starts with
/// <c>x</c> or <c>o</c>, which the lexer reads as an id, text that mermaid.js changes before the lexer reads it, and
/// the accessibility statements, which <c>block.jison</c> has no rule for. A line whose last <c>;</c>
/// <c>encodeEntities</c> would take out gets a <c>;</c> more for it to take.
/// </remarks>
internal static class BlockWriter
{
	public static void Write(BlockDocument document, StringBuilder output)
	{
		ArgumentNullException.ThrowIfNull(document);
		ArgumentNullException.ThrowIfNull(output);
		if (document.AccessibilityTitle is not null || document.AccessibilityDescription is not null)
		{
			throw new ArgumentException("A block diagram has no accessibility title or description.", nameof(document));
		}

		if (document.Statements.Count == 0)
		{
			throw new ArgumentException("A block diagram needs a statement.", nameof(document));
		}

		var body = new StringBuilder();
		body.Append("block\n");
		var added = new List<int>();
		WriteStatements(document.Statements, 1, body, added);
		var written = body.ToString();
		var expected = new StringBuilder(written);
		for (var i = added.Count - 1; i >= 0; i--)
		{
			var end = written.IndexOf('\n', added[i]);
			expected.Remove(added[i], end - added[i]);
		}

		CheckPrepared(written, expected.ToString());
		output.Append(written);
	}

	private static void WriteStatements(List<BlockStatement> statements, int depth, StringBuilder output, List<int> added)
	{
		foreach (var statement in statements)
		{
			var lineStart = output.Length;
			output.Append(' ', depth * 2);
			if (statement is BlockComposite composite)
			{
				if (composite.Statements.Count == 0)
				{
					throw new ArgumentException("A composite block needs a statement.", nameof(statements));
				}

				switch (composite.Header)
				{
					case null:
						output.Append("block");
						break;
					case BlockNode or BlockEdge:
						output.Append("block:");
						WriteNodeStatement(composite.Header, output);
						break;
					default:
						throw new ArgumentException($"A composite's header is a node or an edge, not {composite.Header.GetType().Name}.", nameof(statements));
				}

				EndLine(output, lineStart, added);
				WriteStatements(composite.Statements, depth + 1, output, added);
				output.Append(' ', depth * 2).Append("end\n");
				continue;
			}

			WriteStatement(statement, output);
			EndLine(output, lineStart, added);
		}
	}

	private static void WriteStatement(BlockStatement statement, StringBuilder output)
	{
		switch (statement)
		{
			case BlockNode or BlockEdge:
				WriteNodeStatement(statement, output);
				break;
			case BlockColumns columns:
				if (columns.Columns is < 0)
				{
					throw new ArgumentException($"The columns {columns.Columns} are not 0 or more.", nameof(statement));
				}

				output.Append("columns ").Append(columns.Columns is { } count ? count.ToString(CultureInfo.InvariantCulture) : "auto");
				break;
			case BlockSpace space:
				if (space.Width < 0)
				{
					throw new ArgumentException($"The space width {space.Width} is not 0 or more.", nameof(statement));
				}

				output.Append("space");
				if (space.Width != 1)
				{
					output.Append(':').Append(space.Width.ToString(CultureInfo.InvariantCulture));
				}

				break;
			case BlockClassDefinition definition:
				if (BlockLexer.WordLength(definition.Name, 0) != definition.Name.Length || definition.Name == "DEFAULT")
				{
					throw new ArgumentException($"The class name {definition.Name} is not one word other than DEFAULT.", nameof(statement));
				}

				var css = string.Join(',', definition.Styles);
				if (css.Length == 0)
				{
					throw new ArgumentException("A classDef needs a style.", nameof(statement));
				}

				output.Append("classDef ").Append(definition.Name).Append(' ').Append(RestOfLine(css));
				break;
			case BlockClassAssignment assignment:
				output.Append("class ").Append(EntityIds(string.Join(',', assignment.Ids))).Append(' ').Append(RestOfLine(assignment.ClassName));
				break;
			case BlockStyle style:
				output.Append("style ").Append(EntityIds(style.Id)).Append(' ').Append(RestOfLine(string.Join(',', style.Styles)));
				break;
			default:
				throw new ArgumentException($"The statement {statement?.GetType().Name} is not one of the block diagram's.", nameof(statement));
		}
	}

	/// <summary>A <c>nodeStatement</c>: a <see cref="BlockNode"/>, or the nodes and links of a <see cref="BlockEdge"/>.</summary>
	private static void WriteNodeStatement(BlockStatement statement, StringBuilder output)
	{
		if (statement is BlockNode single)
		{
			WriteNode(single, output, first: true);
			return;
		}

		var edge = (BlockEdge)statement;
		if (edge.Nodes.Count < 2 || edge.Links.Count != edge.Nodes.Count - 1)
		{
			throw new ArgumentException("An edge needs two nodes or more and one link fewer.", nameof(statement));
		}

		for (var i = 0; i < edge.Nodes.Count; i++)
		{
			if (i > 0)
			{
				output.Append(' ');
				WriteLink(edge.Links[i - 1], output);
				output.Append(' ');
			}

			WriteNode(edge.Nodes[i], output, first: i == 0);
		}
	}

	private static void WriteNode(BlockNode node, StringBuilder output, bool first)
	{
		CheckId(node.Id);
		output.Append(node.Id);
		if (node.Shape == BlockShape.BlockArrow)
		{
			output.Append("<[");
			WriteLabel(node.Label, output);
			output.Append("]>(");
			WriteDirections(node.Directions, output);
			output.Append(')');
		}
		else if (node.Directions is not null)
		{
			throw new ArgumentException($"The block {node.Id} has directions but is not a block arrow.", nameof(node));
		}
		else if (node.Label is not null)
		{
			var (start, end) = Brackets(node.Shape);
			output.Append(start);
			WriteLabel(node.Label, output);
			output.Append(end);
		}
		else if (node.Shape != BlockShape.Na)
		{
			throw new ArgumentException($"The block {node.Id} has a shape but no label.", nameof(node));
		}

		if (node.Width != 1)
		{
			if (!first || node.Width < 0)
			{
				throw new ArgumentException($"The block {node.Id} has the width {node.Width}, which only the first node of a statement can have, and not below 0.", nameof(node));
			}

			output.Append(':').Append(node.Width.ToString(CultureInfo.InvariantCulture));
		}
	}

	private static (string Start, string End) Brackets(BlockShape shape) => shape switch
	{
		BlockShape.Na => ("[", "/]"),
		BlockShape.Square => ("[", "]"),
		BlockShape.Round => ("(", ")"),
		BlockShape.Circle => ("((", "))"),
		BlockShape.RectLeftInvArrow => (">", "]"),
		BlockShape.Diamond => ("{", "}"),
		BlockShape.Hexagon => ("{{", "}}"),
		BlockShape.Stadium => ("([", "])"),
		BlockShape.Subroutine => ("[[", "]]"),
		BlockShape.Cylinder => ("[(", ")]"),
		BlockShape.DoubleCircle => ("(((", ")))"),
		BlockShape.LeanRight => ("[/", "/]"),
		BlockShape.LeanLeft => ("[\\", "\\]"),
		BlockShape.Trapezoid => ("[/", "\\]"),
		BlockShape.InvTrapezoid => ("[\\", "/]"),
		_ => throw new ArgumentOutOfRangeException(nameof(shape), shape, null),
	};

	/// <summary>A <c>STR</c>: anything but <c>"</c>, not empty, and not starting with <c>`</c>, which starts a Markdown string.</summary>
	private static void WriteLabel(string? label, StringBuilder output)
	{
		if (label is null || label.Length == 0 || label.Contains('"', StringComparison.Ordinal) || label[0] == '`')
		{
			throw new ArgumentException($"The label {label} cannot be written so that it reads back as itself.", nameof(label));
		}

		output.Append('"').Append(label).Append('"');
	}

	/// <summary>
	/// The <c>DIR</c> tokens, separated by <c>, </c>. Only the first can have space before its word, because the space
	/// after a word and the <c>,</c> rule's space are not kept.
	/// </summary>
	private static void WriteDirections(List<string>? directions, StringBuilder output)
	{
		if (directions is null || directions.Count == 0)
		{
			throw new ArgumentException("A block arrow needs a direction.", nameof(directions));
		}

		for (var i = 0; i < directions.Count; i++)
		{
			var direction = directions[i];
			var word = StateLexer.Trim(direction);
			var start = word.IsEmpty ? 0 : direction.AsSpan().IndexOf(word);
			var leading = direction.AsSpan(0, start);
			var trailing = direction.AsSpan(start + word.Length);
			if (word is not ("right" or "left" or "x" or "y" or "up" or "down")
				|| (i > 0 && !leading.IsEmpty)
				|| leading.ContainsAnyExcept(' ', '\t')
				|| trailing.ContainsAnyExcept(' ', '\t'))
			{
				throw new ArgumentException($"The direction {direction} is not right, left, up, down, x or y with spaces or tabs around it.", nameof(directions));
			}

			output.Append(i > 0 ? ", " : "").Append(direction);
		}
	}

	private static void WriteLink(BlockLink link, StringBuilder output)
	{
		if (link.Label is not null)
		{
			output.Append(link.Stroke switch
			{
				BlockStroke.Thick => "==",
				BlockStroke.Dotted => "-.",
				_ => "--",
			}).Append(' ');
			WriteLabel(link.Label, output);
			output.Append(' ');
		}
		else if (link.Start is BlockArrowHead.Circle or BlockArrowHead.Cross)
		{
			throw new ArgumentException("A link without a label cannot start with a circle or a cross, which the lexer reads as an id.", nameof(link));
		}

		output.Append(link.Start switch
		{
			BlockArrowHead.Point => "<",
			BlockArrowHead.Circle => "o",
			BlockArrowHead.Cross => "x",
			_ => "",
		});
		var end = link.End switch
		{
			BlockArrowHead.Point => ">",
			BlockArrowHead.Circle => "o",
			BlockArrowHead.Cross => "x",
			_ => "",
		};
		output.Append(link.Stroke switch
		{
			BlockStroke.Thick => end.Length == 0 ? "===" : "==" + end,
			BlockStroke.Dotted => "-.-" + end,
			_ => end.Length == 0 ? "---" : "--" + end,
		});
	}

	/// <summary>
	/// Refuses an id that is not one <c>NODE_ID</c>, or that a rule before <c>NODE_ID</c> would read: a keyword with
	/// its <c>\b</c>, such as <c>end</c>, one that <c>\s+</c> may follow, such as <c>class</c> and <c>columns</c>, and
	/// <c>accTitle</c> and <c>accDescr</c>, which a <c>:</c> or <c>{</c> after them would start.
	/// </summary>
	private static void CheckId(string id)
	{
		ArgumentException.ThrowIfNullOrEmpty(id);
		foreach (var c in id)
		{
			if (!BlockLexer.IsNodeIdChar(c))
			{
				throw new ArgumentException($"The id {id} has a character that ends an id.", nameof(id));
			}
		}

		if (id[0] == '"'
			|| id is "class" or "classDef" or "style" or "columns" or "accTitle" or "accDescr"
			|| StartsKeyword(id, "block") || StartsKeyword(id, "space") || StartsKeyword(id, "default")
			|| StartsKeyword(id, "linkStyle") || StartsKeyword(id, "interpolate") || StartsKeyword(id, "end"))
		{
			throw new ArgumentException($"The id {id} would be read as a keyword.", nameof(id));
		}
	}

	private static bool StartsKeyword(string id, string keyword) =>
		id.StartsWith(keyword, StringComparison.Ordinal) && (id.Length == keyword.Length || !StateLexer.IsWordChar(id[keyword.Length]));

	/// <summary>The ids of a <c>class</c> or <c>style</c> statement, which must be all of <c>(\w+)+((","\s*\w+)*)</c>.</summary>
	private static string EntityIds(string ids)
	{
		if (ids.Length == 0 || BlockLexer.EntityIdsLength(ids) != ids.Length || ids.Contains('\r', StringComparison.Ordinal))
		{
			throw new ArgumentException($"The ids {ids} are not words separated by commas.", nameof(ids));
		}

		return ids;
	}

	/// <summary>The rest of a line, which <c>[^\n]*</c> reads and the action trims.</summary>
	private static string RestOfLine(string text)
	{
		if (text.Contains('\n', StringComparison.Ordinal) || !StateLexer.Trim(text).SequenceEqual(text))
		{
			throw new ArgumentException($"The text {text} is not one trimmed line.", nameof(text));
		}

		return text;
	}

	/// <summary>
	/// Ends the line, with a <c>;</c> or two more where <c>encodeEntities</c> would take the last <c>;</c> of the
	/// line out: what it takes out is then the added one. <paramref name="added"/> gets where each added <c>;</c> starts.
	/// </summary>
	private static void EndLine(StringBuilder output, int lineStart, List<int> added)
	{
		var text = output.ToString(lineStart, output.Length - lineStart);
		var lastLine = text[(text.LastIndexOf('\n') + 1)..];
		foreach (var suffix in (ReadOnlySpan<string>)[";", ";;"])
		{
			if (!Survives(lastLine, lastLine) && Survives(lastLine + suffix, lastLine))
			{
				added.Add(output.Length);
				output.Append(suffix).Append('\n');
				return;
			}
		}

		output.Append('\n');
	}

	/// <summary>Whether mermaid.js's rewrites of <paramref name="line"/> give <paramref name="expected"/> back.</summary>
	private static bool Survives(string line, string expected)
	{
		var prepared = new PooledList<char>(line.Length + 1);
		var restored = new PooledList<char>(line.Length + 1);
		try
		{
			FlowchartText.Prepare(line, ref prepared, foldSpaceAfterBraces: false);
			FlowchartText.Restore(prepared.AsSpan(), ref restored);
			return restored.AsSpan().SequenceEqual(string.Concat(StateLexer.TrimStart(expected), "\n"));
		}
		finally
		{
			prepared.Dispose();
			restored.Dispose();
		}
	}

	/// <summary>
	/// Refuses text that mermaid.js changes before the lexer reads it: a <c>\r</c>, a directive, a comment line, a
	/// <c>="</c> inside an HTML tag, and the placeholders of <c>encodeEntities</c>, which the parser turns into
	/// <c>#</c> and <c>;</c>.
	/// </summary>
	private static void CheckPrepared(string written, string expected)
	{
		if (written.Contains("ﬂ°", StringComparison.Ordinal) || written.Contains("¶ß", StringComparison.Ordinal) || !Survives(written[..^1], expected[..^1]))
		{
			throw new ArgumentException($"The text {written} cannot be written so that it reads back as itself.", nameof(written));
		}
	}
}
