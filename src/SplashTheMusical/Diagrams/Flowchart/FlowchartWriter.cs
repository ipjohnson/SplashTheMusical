using System.Globalization;
using System.Text;
using SplashTheMusical.Parsing;
using SplashTheMusical.Writing;

namespace SplashTheMusical.Diagrams.Flowchart;

/// <summary>
/// Writes a <see cref="FlowchartDocument"/> as <c>flow.jison</c> source that <see cref="FlowchartParser"/> reads back as
/// the same document. The model does not keep the order of the statements, so the writer uses one canonical order in
/// which the parser's actions give the model's order back:
/// <list type="number">
/// <item>the header and the accessibility statements;</item>
/// <item>every node, in the model's order, so the nodes are created in that order, with its <c>style</c> statements;</item>
/// <item>every edge, in the model's order, so the generated ids <c>L_start_end_n</c> come out the same, then the
/// <c>linkStyle</c> statements and the edges' <c>@{ }</c> blocks;</item>
/// <item>the subgraphs, nested as their members say, in the order of their <c>end</c>, each listing its members, then
/// their <c>@{ }</c> blocks;</item>
/// <item>the <c>classDef</c> statements.</item>
/// </list>
/// <c>class</c> and <c>click</c> statements follow the nodes, the edges or the subgraphs, so a node, an edge and a
/// subgraph that share an id each end with their own classes. A label that brackets cannot hold goes in the node's
/// <c>@{ }</c> block as <c>label</c> and <c>labelType</c>.
/// </summary>
internal static class FlowchartWriter
{
	private const string Indent = "    ";

	/// <summary>A label written in brackets only to open them. The node's <c>@{ }</c> block replaces it.</summary>
	private const string Placeholder = "\"_\"";

	public static void Write(FlowchartDocument document, StringBuilder output)
	{
		ArgumentNullException.ThrowIfNull(document);
		ArgumentNullException.ThrowIfNull(output);
		new Writer(document, output).Write();
	}

	/// <summary>
	/// Whether <paramref name="label"/> reads back unchanged as the plain text of a <c>[ ]</c>, <c>( )</c> or <c>{ }</c>
	/// shape, or between the pipes of an edge.
	/// </summary>
	public static bool IsPlainText(string label) => IsPlainText(label, trapezoid: false);

	/// <summary>Whether <paramref name="label"/> reads back unchanged as a <c>"string"</c>.</summary>
	public static bool IsStringText(string label) =>
		label.Length > 0
		&& !PreambleParser.IsSpace(label[0])
		&& !PreambleParser.IsSpace(label[^1])
		&& label[0] != '`'
		&& label.AsSpan().IndexOfAny('"', '\r', FlowchartText.Ligature) < 0
		&& SurvivesPreparation(label);

	private static bool IsMarkdownText(string label) => IsStringText(label) && !label.Contains('`');

	private static bool IsPlainText(string label, bool trapezoid)
	{
		if (label.Length == 0
			|| PreambleParser.IsSpace(label[0])
			|| PreambleParser.IsSpace(label[^1])
			|| label[0] is '-' or '/' or '\\' or '>' or '<'
			|| label.AsSpan().IndexOfAny("[](){}|\"\r") >= 0
			|| label.Contains(FlowchartText.Ligature)
			|| label.Contains("~~", StringComparison.Ordinal)
			|| (trapezoid && label.AsSpan().IndexOfAny('/', '\\') >= 0))
		{
			return false;
		}

		return SurvivesPreparation(label);
	}

	/// <summary>
	/// Whether <see cref="FlowchartText.Prepare"/> leaves the text alone: no <c>%%{</c>, which starts a directive, no
	/// <c>%%</c> at the start of a line of the text, which starts a comment, no space between a <c>}</c> and the last line
	/// break of the space after it, which it folds, and no tag the text opens and does not close.
	/// </summary>
	private static bool SurvivesPreparation(string text)
	{
		if (text.Contains("%%{", StringComparison.Ordinal) || OpensTag(text, out _))
		{
			return false;
		}

		for (var at = text.IndexOf("%%", StringComparison.Ordinal); at >= 0; at = text.IndexOf("%%", at + 1, StringComparison.Ordinal))
		{
			var lineStart = at;
			while (lineStart > 0 && text[lineStart - 1] != '\n' && PreambleParser.IsSpace(text[lineStart - 1]))
			{
				lineStart--;
			}

			if (lineStart > 0 && text[lineStart - 1] == '\n')
			{
				return false;
			}
		}

		for (var at = text.IndexOf('}'); at >= 0; at = text.IndexOf('}', at + 1))
		{
			var lastBreak = -1;
			for (var i = at + 1; i < text.Length && PreambleParser.IsSpace(text[i]); i++)
			{
				lastBreak = text[i] == '\n' ? i : lastBreak;
			}

			if (lastBreak > at + 1)
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Whether the text has a <c>&lt;</c> and a word character with no <c>&gt;</c> after them. mermaid.js quotes the
	/// attributes of <c>/&lt;(\w+)([^&gt;]*)&gt;/g</c> in the whole source, so such a tag would reach into the statements
	/// after the text and change their quotes. <paramref name="at"/> is the first such <c>&lt;</c>.
	/// </summary>
	private static bool OpensTag(string text, out int at)
	{
		var close = text.LastIndexOf('>');
		for (at = text.IndexOf('<', close + 1); at >= 0; at = text.IndexOf('<', at + 1))
		{
			if (at + 1 < text.Length && FlowchartText.IsWordChar(text[at + 1]))
			{
				return true;
			}
		}

		return false;
	}

	private static bool IsIdText(string id)
	{
		if (id.Length == 0)
		{
			return false;
		}

		foreach (var c in id)
		{
			if (!char.IsAsciiLetterOrDigit(c) && c is not '_' and not '-')
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>The bracket shapes of <c>vertex</c>, with their opening and closing text.</summary>
	private static (string Open, string Close)? Brackets(string? shape) => shape switch
	{
		"square" => ("[", "]"),
		"round" => ("(", ")"),
		"circle" => ("((", "))"),
		"doublecircle" => ("(((", ")))"),
		"ellipse" => ("(-", "-)"),
		"stadium" => ("([", "])"),
		"subroutine" => ("[[", "]]"),
		"cylinder" => ("[(", ")]"),
		"diamond" => ("{", "}"),
		"hexagon" => ("{{", "}}"),
		"odd" => (">", "]"),
		"trapezoid" => ("[/", "\\]"),
		"inv_trapezoid" => ("[\\", "/]"),
		"lean_right" => ("[/", "/]"),
		"lean_left" => ("[\\", "\\]"),
		_ => null,
	};

	/// <summary>
	/// The text of a label in brackets or pipes, or <c>null</c> when it would not read back the same. An empty plain label
	/// is a space, which the parser trims.
	/// </summary>
	private static string? Encode(string label, FlowchartLabelType type, bool trapezoid) => type switch
	{
		FlowchartLabelType.String => IsStringText(label) ? "\"" + label + "\"" : null,
		FlowchartLabelType.Markdown => IsMarkdownText(label) ? "\"`" + label + "`\"" : null,
		_ when label.Length == 0 => " ",
		_ => IsPlainText(label, trapezoid) ? label : null,
	};

	/// <summary>
	/// A YAML double-quoted string the <c>@{ }</c> lexer passes through unchanged: <c>"</c> would end its string state,
	/// a line break would become <c>&lt;br/&gt;</c>, <c>%</c> could start a directive and a <c>&lt;</c> that opens a tag
	/// the text does not close would reach into the statements after it, so each is escaped.
	/// </summary>
	private static void AppendYamlString(StringBuilder output, string text)
	{
		output.Append('"');
		var openTag = OpensTag(text, out var tag) ? tag : text.Length;
		for (var i = 0; i < text.Length; i++)
		{
			var c = text[i];
			switch (c)
			{
				case '<' when i >= openTag:
					output.Append("\\x3C");
					break;
				case '"':
					output.Append("\\x22");
					break;
				case '%':
					output.Append("\\x25");
					break;
				case '\\':
					output.Append("\\\\");
					break;
				case '\n':
					output.Append("\\n");
					break;
				case '\r':
					output.Append("\\r");
					break;
				case '\t':
					output.Append("\\t");
					break;
				default:
					if (c < 0x20 || (c >= 0x7F && c <= 0x9F) || c == 0x2028 || c == 0x2029 || c == 0xFEFF)
					{
						output.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
					}
					else
					{
						output.Append(c);
					}

					break;
			}
		}

		output.Append('"');
	}

	private static void AppendYamlValue(StringBuilder output, MermaidValue value)
	{
		switch (value.Kind)
		{
			case MermaidValueKind.Null:
				output.Append("null");
				break;
			case MermaidValueKind.Boolean:
				output.Append(value.Boolean ? "true" : "false");
				break;
			case MermaidValueKind.Number:
				var number = value.Number;
				output.Append(
					double.IsNaN(number) ? ".nan"
					: double.IsPositiveInfinity(number) ? ".inf"
					: double.IsNegativeInfinity(number) ? "-.inf"
					: JsNumber.ToString(number));
				break;
			case MermaidValueKind.String:
				AppendYamlString(output, value.String);
				break;
			case MermaidValueKind.Sequence:
				output.Append('[');
				for (var i = 0; i < value.Items.Count; i++)
				{
					output.Append(i > 0 ? ", " : "");
					AppendYamlValue(output, value.Items[i]);
				}

				output.Append(']');
				break;
			default:
				// A } ends the @{ } block, so a nested mapping cannot be written in it.
				output.Append('{');
				var first = true;
				foreach (var (key, entry) in value.Entries)
				{
					output.Append(first ? "" : ", ");
					first = false;
					AppendYamlString(output, key);
					output.Append(": ");
					AppendYamlValue(output, entry);
				}

				output.Append('}');
				break;
		}
	}

	private static string LinkText(FlowchartEdgeType type, FlowchartStroke stroke, int? length)
	{
		var count = Math.Clamp(length ?? 1, 1, 10);
		var (start, end) = type switch
		{
			FlowchartEdgeType.Point => ("", ">"),
			FlowchartEdgeType.Circle => ("", "o"),
			FlowchartEdgeType.Cross => ("", "x"),
			FlowchartEdgeType.DoublePoint => ("<", ">"),
			FlowchartEdgeType.DoubleCircle => ("o", "o"),
			FlowchartEdgeType.DoubleCross => ("x", "x"),
			_ => ("", ""),
		};
		return stroke switch
		{
			FlowchartStroke.Thick => start + new string('=', count + 1) + (end.Length > 0 ? end : "="),
			FlowchartStroke.Dotted => start + "-" + new string('.', count) + "-" + end,
			FlowchartStroke.Invisible => new string('~', count + 2),
			_ => start + new string('-', count + 1) + (end.Length > 0 ? end : "-"),
		};
	}

	/// <summary>The start of a <c>-- text --&gt;</c> link, with the arrowhead of a two-ended edge.</summary>
	private static string StartLinkText(FlowchartEdgeType type, FlowchartStroke stroke)
	{
		var start = type switch
		{
			FlowchartEdgeType.DoublePoint => "<",
			FlowchartEdgeType.DoubleCircle => "o",
			FlowchartEdgeType.DoubleCross => "x",
			_ => "",
		};
		return start + (stroke switch
		{
			FlowchartStroke.Thick => "==",
			FlowchartStroke.Dotted => "-.",
			_ => "--",
		});
	}

	private static FlowchartEdgeType SingleEnded(FlowchartEdgeType type) => type switch
	{
		FlowchartEdgeType.DoublePoint => FlowchartEdgeType.Point,
		FlowchartEdgeType.DoubleCircle => FlowchartEdgeType.Circle,
		FlowchartEdgeType.DoubleCross => FlowchartEdgeType.Cross,
		_ => type,
	};

	/// <summary>Whether the text reads back unchanged between the start and the end of a <c>-- text --&gt;</c> link.</summary>
	private static bool IsEdgeText(string label, FlowchartStroke stroke) =>
		label.Length > 0
		&& !PreambleParser.IsSpace(label[0])
		&& !PreambleParser.IsSpace(label[^1])
		&& label.AsSpan().IndexOfAny('"', '\r', FlowchartText.Ligature) < 0
		&& !label.Contains(stroke switch
		{
			FlowchartStroke.Thick => '=',
			FlowchartStroke.Dotted => '.',
			_ => '-',
		})
		&& SurvivesPreparation(label);

	private sealed class Writer(FlowchartDocument document, StringBuilder output)
	{
		private readonly Dictionary<string, FlowchartNode> _nodes = new(StringComparer.Ordinal);
		private readonly Dictionary<string, FlowchartEdge> _edges = new(StringComparer.Ordinal);
		private readonly Dictionary<string, FlowchartSubgraph> _subgraphs = new(StringComparer.Ordinal);

		// The click statements each node still needs, in the order they are written: true for a link, false for a callback.
		private readonly Dictionary<FlowchartNode, Queue<bool>> _clicks = [];

		// The subgraph statements written so far. The parser numbers a subgraph without an id by the statements that end before it.
		private int _subgraphStatements;

		public void Write()
		{
			foreach (var node in document.Nodes)
			{
				_nodes.TryAdd(node.Id, node);
			}

			// Only the first edge of an id is the one class statements and @{ } blocks reach.
			foreach (var edge in document.Edges)
			{
				_edges.TryAdd(edge.Id, edge);
			}

			foreach (var subgraph in document.Subgraphs)
			{
				_subgraphs.TryAdd(subgraph.Id, subgraph);
			}

			output.Append(document.Keyword).Append(' ').Append(document.Direction).Append('\n');
			PreambleWriter.WriteAccessibility(output, document, Indent);
			WriteLooseTooltips();
			WriteNodes();
			WriteEdges();
			WriteSubgraphs();
			WriteClassDefs();
		}

		private StringBuilder Line(int depth) => output.Append(Indent).Append(' ', depth * Indent.Length);

		/// <summary>
		/// A tooltip of a node without a link or a callback, which a <c>click</c> before the node gave: the tooltip
		/// reaches the node at the end of the parse, and nothing else does.
		/// </summary>
		private void WriteLooseTooltips()
		{
			foreach (var node in document.Nodes)
			{
				if (node.Tooltip is { } tooltip && node.Link is null && node.Callback is null && _nodes[node.Id] == node)
				{
					Line(0).Append("click ").Append(node.Id).Append(" href \"#\" \"").Append(tooltip).Append("\"\n");
				}
			}
		}

		// ---- Nodes ----

		private void WriteNodes()
		{
			foreach (var node in document.Nodes)
			{
				WriteNode(node);
				var clicks = new Queue<bool>();
				if (node.Link is not null)
				{
					clicks.Enqueue(true);
				}

				if (node.Callback is not null)
				{
					clicks.Enqueue(false);
				}

				_clicks.TryAdd(node, clicks);
			}

			foreach (var node in document.Nodes)
			{
				if (node.Styles.Count > 0)
				{
					Line(0).Append("style ").Append(node.Id).Append(' ').AppendJoin(',', node.Styles).Append('\n');
				}
			}

			foreach (var node in document.Nodes)
			{
				if (_nodes[node.Id] == node)
				{
					WriteClasses(node.Id, node.Classes, Later(node.Id, edge: true, subgraph: true));
				}
			}
		}

		private void WriteNode(FlowchartNode node)
		{
			var line = Line(0).Append(node.Id);
			var hasAsset = node.Icon is not null || node.Image is not null;
			var labelWritten = false;
			var shapeWritten = node.Shape is null;
			if (node.Property is { } property)
			{
				var label = node.LabelType == FlowchartLabelType.Text && IsPlainText(node.Label) && !node.Label.Contains('@') ? node.Label : null;
				line.Append("[|").Append(property.Key).Append(':').Append(property.Value).Append('|').Append(label ?? Placeholder).Append(']');
				labelWritten = label is not null;
				shapeWritten = node.Shape == "rect";
			}
			else if (Brackets(node.Shape) is var (open, close))
			{
				// The lexer reads plain text up to an @ as an edge id, LINK_ID's /[^\s"]+@/, so such a label goes in the @{ } block.
				var label = node.LabelType == FlowchartLabelType.Text && node.Label.Contains('@')
					? null
					: Encode(node.Label, node.LabelType, node.Shape is "trapezoid" or "inv_trapezoid" or "lean_right" or "lean_left");
				line.Append(open).Append(label ?? Placeholder).Append(close);
				labelWritten = label is not null;
				shapeWritten = true;
			}
			else
			{
				// A node without brackets keeps its id as its label, and an icon or image node without a label has none.
				labelWritten = hasAsset
					? node.Label.Length == 0
					: node.LabelType == FlowchartLabelType.Text && node.Label == node.Id;
			}

			// An icon or image clears a label that is still the node's id, so such a label is written again.
			if (hasAsset && node.Label.Length > 0)
			{
				labelWritten = false;
			}

			var data = new StringBuilder();
			if (!shapeWritten)
			{
				Entry(data, "shape").Append(node.Shape);
			}

			if (!labelWritten && node.Label.Length > 0)
			{
				AppendYamlString(Entry(data, "label"), node.Label);
				Entry(data, "labelType").Append(node.LabelType switch
				{
					FlowchartLabelType.String => "string",
					FlowchartLabelType.Text => "text",
					_ => "markdown",
				});
			}

			AppendData(data, "icon", node.Icon);
			AppendData(data, "form", node.Form);
			AppendData(data, "pos", node.LabelPosition);
			AppendData(data, "img", node.Image);
			AppendData(data, "constraint", node.Constraint);
			AppendData(data, "w", node.AssetWidth is { } width ? JsNumber.ToString(width) : null);
			AppendData(data, "h", node.AssetHeight is { } height ? JsNumber.ToString(height) : null);
			if (data.Length > 0)
			{
				line.Append("@{ ").Append(data).Append(" }");
			}

			line.Append('\n');
		}

		private static StringBuilder Entry(StringBuilder data, string key) =>
			data.Append(data.Length > 0 ? ", " : "").Append(key).Append(": ");

		/// <summary>A string entry, which the parser reads when it is truthy, so an empty value is left out.</summary>
		private static void AppendData(StringBuilder data, string key, string? value)
		{
			if (!string.IsNullOrEmpty(value))
			{
				AppendYamlString(Entry(data, key), value);
			}
		}

		// ---- Edges ----

		private void WriteEdges()
		{
			foreach (var edge in document.Edges)
			{
				WriteEdge(edge);
			}

			// The default curve applies to the edges added after it, so it follows them, and each edge's own curve is set.
			if (document.DefaultInterpolate is { } curve)
			{
				Line(0).Append("linkStyle default interpolate ").Append(curve).Append('\n');
			}

			if (document.DefaultEdgeStyles is { Count: > 0 } defaultStyles)
			{
				Line(0).Append("linkStyle default ").AppendJoin(',', defaultStyles).Append('\n');
			}

			for (var i = 0; i < document.Edges.Count; i++)
			{
				var edge = document.Edges[i];
				var first = _edges[edge.Id] == edge;
				if (edge.Interpolate is { } interpolate && (IsIdText(interpolate) || !first))
				{
					Line(0).Append("linkStyle ").Append(i.ToString(CultureInfo.InvariantCulture)).Append(" interpolate ").Append(interpolate).Append('\n');
				}

				if (edge.Styles is { Count: > 0 } styles)
				{
					Line(0).Append("linkStyle ").Append(i.ToString(CultureInfo.InvariantCulture)).Append(' ').AppendJoin(',', styles).Append('\n');
				}

				if (first)
				{
					WriteEdgeData(edge);
				}
			}

			foreach (var edge in document.Edges)
			{
				if (_edges[edge.Id] == edge)
				{
					WriteClasses(edge.Id, edge.Classes, Later(edge.Id, edge: false, subgraph: true));
				}
			}
		}

		private void WriteEdge(FlowchartEdge edge)
		{
			var line = Line(0).Append(edge.Start).Append(' ');
			if (edge.IsUserDefinedId)
			{
				line.Append(edge.Id).Append('@');
			}

			var label = edge.Label;
			if (edge.Type == FlowchartEdgeType.Invalid || edge.Stroke == FlowchartStroke.Invalid)
			{
				// The arrowheads of the two halves of a -- text --> link disagree.
				line.Append("x-- ").Append(Encode(label, edge.LabelType, false) ?? label).Append(" -->");
			}
			else if (label.Length == 0)
			{
				line.Append(LinkText(edge.Type, edge.Stroke, edge.Length));
			}
			else if (Encode(label, edge.LabelType, false) is { } encoded)
			{
				line.Append(LinkText(edge.Type, edge.Stroke, edge.Length)).Append('|').Append(encoded).Append('|');
			}
			else if (edge.LabelType == FlowchartLabelType.Text && edge.Stroke != FlowchartStroke.Invisible && IsEdgeText(label, edge.Stroke))
			{
				line.Append(StartLinkText(edge.Type, edge.Stroke)).Append(' ').Append(label).Append(' ')
					.Append(LinkText(SingleEnded(edge.Type), edge.Stroke, edge.Length));
			}
			else
			{
				line.Append(LinkText(edge.Type, edge.Stroke, edge.Length)).Append('|').Append(label).Append('|');
			}

			line.Append(' ').Append(edge.End).Append('\n');
		}

		private void WriteEdgeData(FlowchartEdge edge)
		{
			var data = new StringBuilder();
			if (edge.Animate is { } animate)
			{
				Entry(data, "animate").Append(animate ? "true" : "false");
			}

			AppendData(data, "animation", edge.Animation);
			if (edge.Interpolate is { } curve && !IsIdText(curve))
			{
				AppendData(data, "curve", curve);
			}

			if (edge.Step is { } step)
			{
				Entry(data, "step").Append(step.ToString(CultureInfo.InvariantCulture));
			}

			if (data.Length > 0)
			{
				Line(0).Append(edge.Id).Append("@{ ").Append(data).Append(" }\n");
			}
		}

		// ---- Subgraphs ----

		private void WriteSubgraphs()
		{
			// A subgraph is added at its end, so a nested subgraph comes just before the one that holds it. The children of
			// a subgraph are the blocks written so far, not yet nested, that end the list and that it names as members. A
			// member subgraph that ends after it was nested in a second block of the same id, which adds to the first, so it
			// gets a continuation block after that subgraph.
			var index = new Dictionary<FlowchartSubgraph, int>();
			for (var i = 0; i < document.Subgraphs.Count; i++)
			{
				index[document.Subgraphs[i]] = i;
			}

			var roots = new List<Block>();
			var continued = new Dictionary<string, FlowchartSubgraph>(StringComparer.Ordinal);
			foreach (var subgraph in document.Subgraphs)
			{
				var members = new HashSet<string>(subgraph.Nodes, StringComparer.Ordinal);
				var block = new Block(subgraph, Continuation: false, []);
				while (roots.Count > 0 && members.Contains(roots[^1].Subgraph.Id) && roots[^1].Subgraph.Id != subgraph.Id && !roots[^1].Continuation)
				{
					block.Children.Insert(0, roots[^1]);
					roots.RemoveAt(roots.Count - 1);
				}

				roots.Add(block);
				var holder = Holder(subgraph, index);
				if (holder is not null)
				{
					roots.RemoveAt(roots.Count - 1);
					continued[subgraph.Id] = holder;
					if (roots.Count > 0 && roots[^1].Continuation && roots[^1].Subgraph == holder)
					{
						roots[^1].Children.Add(block);
					}
					else
					{
						roots.Add(new Block(holder, Continuation: true, [block]));
					}
				}
			}

			foreach (var root in roots)
			{
				WriteSubgraph(root, 0, continued);
			}

			foreach (var subgraph in document.Subgraphs)
			{
				if (subgraph.Metadata is { Kind: MermaidValueKind.Mapping, Entries.Count: > 0 } metadata)
				{
					var line = Line(0).Append(subgraph.Id).Append("@{ ");
					var first = true;
					foreach (var (key, value) in metadata.Entries)
					{
						line.Append(first ? "" : ", ");
						first = false;
						AppendYamlString(line, key);
						line.Append(": ");
						AppendYamlValue(line, value);
					}

					line.Append(" }\n");
				}
			}

			foreach (var subgraph in document.Subgraphs)
			{
				WriteClasses(subgraph.Id, subgraph.Classes, null);
			}

			// A link or callback whose click statement no class list placed.
			foreach (var node in document.Nodes)
			{
				if (_clicks.TryGetValue(node, out var clicks))
				{
					while (clicks.Count > 0)
					{
						WriteClick(node, clicks.Dequeue());
					}
				}
			}
		}

		/// <summary>A <c>subgraph</c> block: a subgraph, or a second block of its id that holds only <paramref name="Children"/>.</summary>
		private sealed record Block(FlowchartSubgraph Subgraph, bool Continuation, List<Block> Children);

		/// <summary>
		/// The subgraph before <paramref name="subgraph"/> in the list that names it as a member, if any. A member that is
		/// also a node is written as the node's id, which adds the same member.
		/// </summary>
		private FlowchartSubgraph? Holder(FlowchartSubgraph subgraph, Dictionary<FlowchartSubgraph, int> index)
		{
			if (_nodes.ContainsKey(subgraph.Id))
			{
				return null;
			}

			for (var i = index[subgraph] - 1; i >= 0; i--)
			{
				var candidate = document.Subgraphs[i];
				if (candidate.Id != subgraph.Id && candidate.Nodes.Contains(subgraph.Id))
				{
					return candidate;
				}
			}

			return null;
		}

		private void WriteSubgraph(Block block, int depth, Dictionary<string, FlowchartSubgraph> continued)
		{
			var subgraph = block.Subgraph;
			var line = Line(depth).Append("subgraph ");
			if (subgraph.LabelType == FlowchartLabelType.Text && subgraph.Title == subgraph.Id && IsIdText(subgraph.Id))
			{
				line.Append(subgraph.Id);
			}
			else if (Encode(subgraph.Title, subgraph.LabelType, false) is { } encoded)
			{
				line.Append(subgraph.Id).Append('[').Append(encoded).Append(']');
			}
			else if (subgraph.Title.Length == 0 && subgraph.LabelType != FlowchartLabelType.Text)
			{
				// A blank string is trimmed to the empty title.
				line.Append(subgraph.Id).Append(subgraph.LabelType == FlowchartLabelType.String ? "[\" \"]" : "[\"` `\"]");
			}
			else if (BareTitle(subgraph, _subgraphStatements + Count(block.Children)) is { } bare)
			{
				line.Append(bare);
			}
			else
			{
				throw new ArgumentException(
					$"The title of the subgraph '{subgraph.Id}' cannot be written: a title brackets cannot hold is written as "
					+ "`subgraph \"title\"`, whose id is the title or, when it holds a space, a generated subGraphN.",
					nameof(document));
			}

			line.Append('\n');
			if (subgraph.Direction is { } direction && !block.Continuation)
			{
				Line(depth + 1).Append("direction ").Append(direction).Append('\n');
			}

			foreach (var member in subgraph.Nodes)
			{
				if (block.Children.Find(child => child.Subgraph.Id == member) is { } child)
				{
					WriteSubgraph(child, depth + 1, continued);
				}
				else if (block.Continuation || (continued.TryGetValue(member, out var holder) && holder == subgraph))
				{
					// The continuation block holds only its children, and the first block leaves them to it.
				}
				else if (_nodes.ContainsKey(member) || _edges.ContainsKey(member) || _subgraphs.ContainsKey(member))
				{
					Line(depth + 1).Append(member).Append('\n');
				}
				else if (member == document.AccessibilityTitle)
				{
					// An accessibility statement in a subgraph is one of its members.
					Line(depth + 1).Append("accTitle: ").Append(member).Append('\n');
				}
				else if (member == document.AccessibilityDescription)
				{
					var single = member.AsSpan().IndexOfAny('\r', '\n') < 0;
					Line(depth + 1).Append(single ? "accDescr: " : "accDescr {\n").Append(member).Append(single ? "\n" : "\n}\n");
				}
				else
				{
					Line(depth + 1).Append(member).Append('\n');
				}
			}

			Line(depth).Append("end\n");
			_subgraphStatements++;
		}

		/// <summary>The subgraph blocks in <paramref name="blocks"/> and in the blocks they hold.</summary>
		private static int Count(List<Block> blocks)
		{
			var count = blocks.Count;
			foreach (var block in blocks)
			{
				count += Count(block.Children);
			}

			return count;
		}

		/// <summary>
		/// <c>subgraph "title"</c>, which reads as a text or markdown title, if it reads back as <paramref name="subgraph"/>:
		/// its id is the title, or <c>subGraph</c> and the number of subgraph statements that end before it when the title
		/// holds a space.
		/// </summary>
		private static string? BareTitle(FlowchartSubgraph subgraph, int statement)
		{
			var title = subgraph.Title;
			var id = title.Any(PreambleParser.IsSpace) ? string.Create(CultureInfo.InvariantCulture, $"subGraph{statement}") : title;
			if (subgraph.Id != id)
			{
				return null;
			}

			return subgraph.LabelType switch
			{
				FlowchartLabelType.Text when IsStringText(title) => "\"" + title + "\"",
				FlowchartLabelType.Markdown when IsMarkdownText(title) => "\"`" + title + "`\"",
				_ => null,
			};
		}

		// ---- Classes and clicks ----

		/// <summary>
		/// The classes of the edge or subgraph that comes after the target of an id, which the class statements written
		/// for it later also give the target.
		/// </summary>
		private List<string>? Later(string id, bool edge, bool subgraph) =>
			edge && _edges.TryGetValue(id, out var laterEdge) ? laterEdge.Classes
			: subgraph && _subgraphs.TryGetValue(id, out var laterSubgraph) ? laterSubgraph.Classes
			: null;

		/// <summary>
		/// The class statements of one target. A class statement reaches every node, edge and subgraph of the id that
		/// exists, so when the classes of a later target end this target's list, only the classes before them are
		/// written now.
		/// </summary>
		private void WriteClasses(string id, List<string> classes, List<string>? later)
		{
			var count = classes.Count;
			if (later is not null && later.Count <= count && classes.Skip(count - later.Count).SequenceEqual(later, StringComparer.Ordinal))
			{
				count -= later.Count;
			}

			for (var i = 0; i < count; i++)
			{
				var className = classes[i];
				if (className == "clickable" && _nodes.TryGetValue(id, out var node) && _clicks.TryGetValue(node, out var clicks) && clicks.Count > 0)
				{
					WriteClick(node, clicks.Dequeue());
				}
				else
				{
					Line(0).Append("class ").Append(id).Append(' ').Append(className).Append('\n');
				}
			}
		}

		private void WriteClick(FlowchartNode node, bool link)
		{
			var line = Line(0).Append("click ").Append(node.Id).Append(' ');
			if (link)
			{
				line.Append("href \"").Append(node.Link).Append('"');
			}
			else
			{
				line.Append("call ").Append(node.Callback).Append('(').Append(node.CallbackArguments).Append(')');
			}

			// The tooltip goes on the node's first click.
			if (node.Tooltip is { } tooltip && (link || node.Link is null))
			{
				line.Append(" \"").Append(tooltip).Append('"');
			}

			if (link && node.LinkTarget is { } target)
			{
				line.Append(' ').Append(target);
			}

			line.Append('\n');
		}

		// ---- Class definitions ----

		private void WriteClassDefs()
		{
			foreach (var definition in document.Classes)
			{
				if (definition.Styles.Count == 0)
				{
					continue;
				}

				var line = Line(0).Append("classDef ").Append(definition.Id).Append(' ');
				for (var i = 0; i < definition.Styles.Count; i++)
				{
					// addClass splits the joined styles at each comma that is not escaped.
					line.Append(i > 0 ? "," : "").Append(definition.Styles[i].Replace(",", "\\,", StringComparison.Ordinal));
				}

				line.Append('\n');
			}
		}
	}
}
