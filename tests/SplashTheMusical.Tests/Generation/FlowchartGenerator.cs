using CsCheck;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Parsing;

namespace SplashTheMusical.Tests.Generation;

/// <summary>
/// Random flowcharts in the form <see cref="FlowchartParser"/> gives them: every node shape, every edge type and stroke,
/// labels of every label type with the text of <see cref="GenText.Label"/>, edge ids as the parser generates them or as
/// the source gives them, <c>style</c>, <c>linkStyle</c>, <c>class</c> and <c>classDef</c>, curves and edge animation,
/// and nested subgraphs. <see cref="FlowchartGeneratorTests"/> shows the coverage.
/// </summary>
public static class FlowchartGenerator
{
	/// <summary>
	/// The shapes of <c>vertex</c>'s brackets, which the writer writes as brackets. <c>(-text-)</c>, <c>ellipse</c>, is
	/// left out: mermaid.js 12.3.0 has no such shape and refuses it.
	/// </summary>
	public static readonly string[] BracketShapes =
		["square", "round", "circle", "doublecircle", "stadium", "subroutine", "cylinder", "diamond", "hexagon", "odd",
		"trapezoid", "inv_trapezoid", "lean_right", "lean_left"];

	/// <summary>
	/// The keys of mermaid.js 12.3.0's <c>shapes</c> map that a <c>@{ shape }</c> block accepts: the lowercase names
	/// without <c>_</c>, as <c>flowDb.addVertex</c> checks. The <c>key</c> lines of <c>corpus/shapes.txt</c> list the map.
	/// </summary>
	public static readonly string[] BlockShapes =
	[
		"anchor", "bang", "bolt", "bow-rect", "bow-tie-rectangle", "bpmn-activity", "bpmn-annotation", "bpmn-boundary", "bpmn-data",
		"bpmn-data-store", "bpmn-end", "bpmn-gateway", "bpmn-intermediate", "bpmn-start", "brace", "brace-l", "brace-r", "braces",
		"browser", "bucket", "card", "choice", "circ", "circle", "cloud", "collate", "com-link", "comment", "composite", "console",
		"cross-circ", "crossed-circle", "curv-trap", "curved-trapezoid", "cyl", "cylinder", "das", "data-store", "database", "datastore",
		"db", "dbl-circ", "decision", "delay", "diam", "diamond", "directory", "disk", "display", "div-proc", "div-rect",
		"divided-process", "divided-rectangle", "doc", "docs", "document", "documents", "double-circle", "doublecircle", "event",
		"extract", "f-circ", "filled-circle", "flag", "flip-tri", "flipped-triangle", "folder", "fork", "fr-circ", "fr-rect",
		"framed-circle", "framed-rectangle", "h-cyl", "half-rounded-rectangle", "hex", "hexagon", "horizontal-cylinder", "hourglass",
		"icon", "in-out", "internal-storage", "inv-trapezoid", "join", "junction", "lean-l", "lean-left", "lean-r", "lean-right",
		"lightning-bolt", "lin-cyl", "lin-doc", "lin-proc", "lin-rect", "lined-cylinder", "lined-document", "lined-process",
		"lined-rectangle", "loop-limit", "manual", "manual-file", "manual-input", "notch-pent", "notch-rect", "notched-pentagon",
		"notched-rectangle", "note", "odd", "out-in", "paper-tape", "person", "pill", "prepare", "priority", "proc", "process",
		"processes", "procs", "question", "rect", "rectangle", "rounded", "shaded-process", "sl-rect", "sloped-rectangle", "sm-circ",
		"small-circle", "st-doc", "st-rect", "stacked-document", "stacked-rectangle", "stadium", "start", "state", "stop",
		"stored-data", "subproc", "subprocess", "subroutine", "summary", "tag-doc", "tag-proc", "tag-rect", "tagged-document",
		"tagged-process", "tagged-rectangle", "terminal", "text", "trap-b", "trap-t", "trapezoid", "trapezoid-bottom", "trapezoid-top",
		"tri", "triangle", "win-pane", "window-pane",
	];

	/// <summary>Every shape a node can have, the bracket shapes first.</summary>
	public static IReadOnlyList<string> Shapes { get; } = [.. BracketShapes, .. BlockShapes.Except(BracketShapes, StringComparer.Ordinal)];

	/// <summary>The arrow type and stroke of every edge the grammar has.</summary>
	public static readonly (FlowchartEdgeType Type, FlowchartStroke Stroke)[] EdgeKinds =
	[
		.. new[] { FlowchartStroke.Normal, FlowchartStroke.Thick, FlowchartStroke.Dotted }.SelectMany(stroke =>
			new[]
			{
				FlowchartEdgeType.Open, FlowchartEdgeType.Point, FlowchartEdgeType.Circle, FlowchartEdgeType.Cross,
				FlowchartEdgeType.DoublePoint, FlowchartEdgeType.DoubleCircle, FlowchartEdgeType.DoubleCross,
			}.Select(type => (type, stroke))),
		(FlowchartEdgeType.Open, FlowchartStroke.Invisible),
		(FlowchartEdgeType.Invalid, FlowchartStroke.Invalid),
	];

	private static readonly string[] Curves =
		["basis", "bumpX", "bumpY", "bundle", "cardinal", "catmullRom", "linear", "monotoneX", "monotoneY", "natural", "step", "stepAfter", "stepBefore"];

	private static readonly string[] ClassNames = ["c0", "c1", "c2", "c3"];

	private static readonly string[] StyleDeclarations = ["fill:#f9f", "stroke:#333", "stroke-width:4px", "color:#fff", "stroke-dasharray: 5 5", "font-weight:bold"];

	private static readonly string[] Directions = ["TB", "BT", "LR", "RL"];

	private sealed record Label(string Text, FlowchartLabelType Type);

	private sealed record NodeSpec(int Shape, Label? Label, string[] Styles, string[] Classes, int Owner);

	private sealed record EdgeSpec(
		int Start, int End, bool UserId, int Kind, int Length, Label? Label, string? Curve, string[]? Styles, string[] Classes, bool? Animate, string? Animation);

	private sealed record SubgraphSpec(Label? Title, string? Direction, string[] Classes, int Parent);

	/// <summary>A node label, which the writer can put in a <c>@{ }</c> block, so it may hold any text.</summary>
	private static readonly Gen<Label> GenLabel = Gen.Select(GenText.Label, Gen.Enum<FlowchartLabelType>(), (text, type) => new Label(text, type));

	/// <summary>
	/// An edge label, whose text comes only from the link: <c>flow.jison</c>'s text between pipes holds no bracket,
	/// <c>|</c> or <c>"</c>, a <c>"string"</c> does not start with <c>`</c>, which would open a <c>"`markdown`"</c>, and
	/// a markdown string holds no <c>`</c>. The <c>-- text --&gt;</c> form also takes brackets, which the writer uses
	/// only when the text has no character of the edge's stroke, so the generator leaves brackets out.
	/// </summary>
	private static readonly Gen<Label> GenEdgeLabel = GenLabel.Select(label => label with { Text = EdgeText(label.Text, label.Type) });

	/// <summary>The text with the rules of <see cref="EdgeTextStep"/> applied until they change nothing.</summary>
	private static string EdgeText(string text, FlowchartLabelType type)
	{
		for (var i = 0; i < 8; i++)
		{
			var next = EdgeTextStep(text, type);
			if (next == text)
			{
				return text;
			}

			text = next;
		}

		return "x";
	}

	private static string EdgeTextStep(string text, FlowchartLabelType type)
	{
		var kept = type switch
		{
			FlowchartLabelType.String => GenText.Without(text, "\""),
			FlowchartLabelType.Markdown => GenText.Without(text, "\"`"),
			_ => GenText.Without(text, "[](){}|\""),
		};

		// A line of the label that starts with %% is a comment in the source, whatever follows it on the line, %%{ can
		// start a directive, and ~~~ between pipes is a link.
		kept = kept.Contains('\n') || kept.Contains("%%", StringComparison.Ordinal) ? GenText.Without(kept, "%") : kept;
		kept = kept.Contains("~~", StringComparison.Ordinal) ? GenText.Without(kept, "~") : kept;
		kept = GenText.WithoutTags(kept);
		while (type == FlowchartLabelType.String && kept.StartsWith('`'))
		{
			kept = GenText.Without(kept.TrimStart('`'), "");
		}

		return GenText.Without(Prepared(kept), "");
	}

	/// <summary>
	/// The text as <see cref="FlowchartText.Prepare"/> leaves it in the source the lexer reads, which folds the spaces
	/// after a brace, quotes tag attributes and drops comments, with its entity placeholders restored.
	/// </summary>
	private static string Prepared(string text)
	{
		var prepared = new PooledList<char>(text.Length + 8);
		var restored = new PooledList<char>(text.Length + 8);
		try
		{
			FlowchartText.Prepare(text, ref prepared);
			var span = prepared.AsSpan();
			FlowchartText.Restore(span.EndsWith("\n") ? span[..^1] : span, ref restored);
			return new string(restored.AsSpan());
		}
		finally
		{
			prepared.Dispose();
			restored.Dispose();
		}
	}

	private static Gen<T?> OrNull<T>(Gen<T> gen, int nullWeight = 1, int valueWeight = 1)
		where T : class =>
		Gen.Frequency((nullWeight, Gen.Const((T?)null)), (valueWeight, gen.Select(v => (T?)v)));

	private static Gen<string[]> Subset(string[] items, int max) => Gen.OneOf(items).Array[0, max].Select(a => a.Distinct(StringComparer.Ordinal).ToArray());

	private static readonly Gen<NodeSpec> GenNode = Gen.Select(
		Gen.Frequency((1, Gen.Const(-1)), (4, Gen.Int[0, Shapes.Count - 1])),
		OrNull(GenLabel, 1, 3),
		Gen.Frequency((4, Gen.Const(Array.Empty<string>())), (1, Subset(StyleDeclarations, 3))),
		Gen.Frequency((3, Gen.Const(Array.Empty<string>())), (1, Subset(ClassNames, 2))),
		Gen.Int[-3, 3],
		(shape, label, styles, classes, owner) => new NodeSpec(shape, label, styles, classes, owner));

	private static readonly Gen<EdgeSpec> GenEdge =
		from ends in Gen.Select(Gen.Int[0, 99], Gen.Int[0, 99], Gen.Frequency((4, Gen.Const(false)), (1, Gen.Const(true))), Gen.Int[0, EdgeKinds.Length - 1])
		from length in Gen.Frequency((6, Gen.Const(1)), (3, Gen.Int[2, 4]), (1, Gen.Int[5, 12]))
		from label in OrNull(GenEdgeLabel)
		from curve in OrNull(Gen.OneOf(Curves), 4)
		from styles in OrNull(Subset(StyleDeclarations, 3).Where(s => s.Length > 0), 4)
		from classes in Gen.Frequency((4, Gen.Const(Array.Empty<string>())), (1, Subset(ClassNames, 2)))
		from animate in Gen.OneOf<bool?>(null, null, null, true, false)
		from animation in Gen.OneOf<string?>(null, null, null, "fast", "slow")
		select new EdgeSpec(ends.Item1, ends.Item2, ends.Item3, ends.Item4, length, label, curve, styles, classes, animate, animation);

	private static readonly Gen<SubgraphSpec> GenSubgraph = Gen.Select(
		OrNull(GenLabel),
		OrNull(Gen.OneOf(Directions), 3),
		Gen.Frequency((3, Gen.Const(Array.Empty<string>())), (1, Subset(ClassNames, 2))),
		Gen.Int[-2, 3],
		(title, direction, classes, parent) => new SubgraphSpec(title, direction, classes, parent));

	private static readonly Gen<FlowchartClass[]> GenClasses = Subset(ClassNames, 4).SelectMany(names =>
		Subset(StyleDeclarations, 3).Where(s => s.Length > 0).Array[names.Length].Select(styles => names.Select((name, i) => Class(name, styles[i])).ToArray()));

	public static Gen<FlowchartDocument> Document { get; } =
		from header in Gen.Select(Gen.OneOf("flowchart", "graph", "flowchart-elk"), Gen.OneOf(Directions), GenText.Line.OrNull(), GenText.Line.OrNull())
		from accDescr in Gen.OneOf<string>(GenText.Line, GenText.Lines).OrNull()
		from nodes in GenNode.Array[1, 8]
		from edges in GenEdge.Array[0, 10]
		from subgraphs in GenSubgraph.Array[0, 3]
		from classes in GenClasses
		from defaults in Gen.Select(OrNull(Gen.OneOf(Curves), 4), OrNull(Subset(StyleDeclarations, 2).Where(s => s.Length > 0), 4))
		select Build(header.Item1, header.Item2, header.Item3, header.Item4, accDescr, nodes, edges, subgraphs, classes, defaults.Item1, defaults.Item2);

	private static FlowchartDocument Build(
		string keyword,
		string direction,
		string? title,
		string? accTitle,
		string? accDescr,
		NodeSpec[] nodeSpecs,
		EdgeSpec[] edgeSpecs,
		SubgraphSpec[] subgraphSpecs,
		FlowchartClass[] classes,
		string? defaultCurve,
		string[]? defaultStyles)
	{
		var document = new FlowchartDocument(keyword == "flowchart-elk" ? "flowchart-elk" : "flowchart-v2")
		{
			Keyword = keyword,
			Direction = direction,
			Title = title,
			AccessibilityTitle = accTitle,
			AccessibilityDescription = accDescr,
			DefaultInterpolate = defaultCurve,
			DefaultEdgeStyles = defaultStyles?.ToList(),
		};

		for (var i = 0; i < nodeSpecs.Length; i++)
		{
			var spec = nodeSpecs[i];
			var node = new FlowchartNode("n" + i) { Shape = spec.Shape < 0 ? null : Shapes[spec.Shape] };
			if (spec.Label is { } label)
			{
				node.Label = label.Text;
				node.LabelType = label.Type;
			}

			node.Styles.AddRange(spec.Styles);
			node.Classes.AddRange(spec.Classes);
			document.Nodes.Add(node);
		}

		var userIds = 0;
		foreach (var spec in edgeSpecs)
		{
			var start = document.Nodes[spec.Start % document.Nodes.Count].Id;
			var end = document.Nodes[spec.End % document.Nodes.Count].Id;
			var (type, stroke) = EdgeKinds[spec.Kind];
			string id;
			if (spec.UserId)
			{
				id = "e" + userIds++;
			}
			else
			{
				// FlowDB.addSingleLink: L_start_end_0 for the first edge between the two, then the count of earlier ones plus one.
				var existing = document.Edges.Count(e => e.Start == start && e.End == end);
				id = $"L_{start}_{end}_{(existing == 0 ? 0 : existing + 1)}";
			}

			var edge = new FlowchartEdge(start, end, id)
			{
				IsUserDefinedId = spec.UserId,
				Type = type,
				Stroke = stroke,
				Length = type == FlowchartEdgeType.Invalid ? null : Math.Min(spec.Length, 10),
				Interpolate = spec.Curve,
				Animate = spec.Animate,
				Animation = spec.Animation,
			};
			// An invalid edge is a -- text --> link whose halves disagree, so it always has a label, and a - would end it.
			var invalidLabel = spec.Label is { } given
				? given with { Text = EdgeText(GenText.Without(given.Text, "-"), given.Type) }
				: new Label("x", FlowchartLabelType.Text);
			if ((type == FlowchartEdgeType.Invalid ? invalidLabel : spec.Label) is { } label)
			{
				edge.Label = label.Text;
				edge.LabelType = label.Type;
			}

			if (spec.Styles is { } styles)
			{
				// updateLink adds fill:none when the styles set no fill.
				edge.Styles = [.. styles, .. styles.Any(s => s.StartsWith("fill", StringComparison.Ordinal)) ? Array.Empty<string>() : ["fill:none"]];
			}

			edge.Classes.AddRange(spec.Classes);
			document.Edges.Add(edge);
		}

		AddSubgraphs(document, nodeSpecs, subgraphSpecs);
		document.Classes.AddRange(classes);
		return document;
	}

	/// <summary>
	/// The subgraphs as blocks in the source: a subgraph's <see cref="SubgraphSpec.Parent"/> picks an earlier block to
	/// open it in, and a node's <see cref="NodeSpec.Owner"/> the subgraph that holds it. The document lists them in the
	/// order of their <c>end</c>, so the blocks inside one come before it.
	/// </summary>
	private static void AddSubgraphs(FlowchartDocument document, NodeSpec[] nodeSpecs, SubgraphSpec[] specs)
	{
		var subgraphs = new List<FlowchartSubgraph>();
		var parents = new int[specs.Length];
		for (var i = 0; i < specs.Length; i++)
		{
			var spec = specs[i];
			var id = "s" + i;
			var subgraph = new FlowchartSubgraph(id, id) { Direction = spec.Direction };
			if (spec.Title is { } written && written with { Text = GenText.WithoutTags(written.Text) } is var title && IsWritable(title))
			{
				subgraph.Title = title.Text;
				subgraph.LabelType = title.Type;
			}

			subgraph.Classes.AddRange(spec.Classes);
			subgraphs.Add(subgraph);
			parents[i] = spec.Parent >= 0 && spec.Parent < i ? spec.Parent : -1;
		}

		for (var i = 0; i < nodeSpecs.Length; i++)
		{
			if (nodeSpecs[i].Owner >= 0 && nodeSpecs[i].Owner < subgraphs.Count)
			{
				subgraphs[nodeSpecs[i].Owner].Nodes.Add(document.Nodes[i].Id);
			}
		}

		for (var i = 0; i < specs.Length; i++)
		{
			if (parents[i] >= 0)
			{
				subgraphs[parents[i]].Nodes.Add(subgraphs[i].Id);
			}
		}

		void AddEnded(int index)
		{
			for (var child = 0; child < specs.Length; child++)
			{
				if (parents[child] == index)
				{
					AddEnded(child);
				}
			}

			document.Subgraphs.Add(subgraphs[index]);
		}

		for (var i = 0; i < specs.Length; i++)
		{
			if (parents[i] < 0)
			{
				AddEnded(i);
			}
		}
	}

	/// <summary>Whether a subgraph title can be written in brackets, which the writer needs of a title that is not the id.</summary>
	private static bool IsWritable(Label title) => title.Type switch
	{
		FlowchartLabelType.Text => FlowchartWriter.IsPlainText(title.Text),
		FlowchartLabelType.String => FlowchartWriter.IsStringText(title.Text),
		_ => FlowchartWriter.IsStringText(title.Text) && !title.Text.Contains('`'),
	};

	/// <summary>A <c>classDef</c> as <c>addClass</c> stores it: a style that mentions <c>color</c> is also a text style, with <c>fill</c> renamed <c>bgFill</c>.</summary>
	private static FlowchartClass Class(string name, string[] styles)
	{
		var definition = new FlowchartClass(name);
		foreach (var style in styles)
		{
			definition.Styles.Add(style);
			if (style.Contains("color", StringComparison.Ordinal))
			{
				var fill = style.IndexOf("fill", StringComparison.Ordinal);
				definition.TextStyles.Add(fill < 0 ? style : string.Concat(style.AsSpan(0, fill), "bgFill", style.AsSpan(fill + 4)));
			}
		}

		return definition;
	}
}
