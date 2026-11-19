using System.Xml;
using SplashTheMusical.Config;
using SplashTheMusical.Diagrams;
using SplashTheMusical.Diagrams.Class;
using SplashTheMusical.Diagrams.Er;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Icons;
using SplashTheMusical.Diagrams.Info;
using SplashTheMusical.Diagrams.State;
using SplashTheMusical.Parsing;
using SplashTheMusical.Text;

namespace SplashTheMusical.Conformance;

/// <summary>
/// STM through its public services, with the site config tools/oracle/facts.mjs initializes mermaid.js with. The facts
/// come from the parsed document and the boxes from its layout, in the formats of tools/oracle/README.md.
/// </summary>
public sealed class StmFactsProvider : IFactsProvider
{
	public static readonly MermaidValue OracleSite = MermaidValue.FromEntries(new OrderedDictionary<string, MermaidValue>
	{
		["startOnLoad"] = MermaidValue.False,
		["securityLevel"] = MermaidValue.FromString("strict"),
	});

	private static readonly Mermaid DefaultStm = new(new MermaidOptions { SiteConfig = OracleSite, Limits = ResourceLimits.Unlimited });

	private readonly Mermaid _stm;

	public StmFactsProvider()
		: this(DefaultStm) { }

	/// <param name="stm">An instance with <see cref="OracleSite"/> as its site config, such as one with more modules.</param>
	public StmFactsProvider(Mermaid stm) => _stm = stm;

	public string Name => "stm";

	public CaseFacts Facts(string source)
	{
		if (!_stm.TryParse(source, out var document, out var diagnostics))
		{
			return Refused(diagnostics);
		}

		var type = document!.DiagramType;
		string svg;
		try
		{
			svg = _stm.RenderSvg(document);
		}
		catch (MermaidParseException e) when (e is not MermaidResourceLimitException)
		{
			return Refused(e.Diagnostics);
		}
		var preamble = PreambleConfig.Resolve(document, type);
		var facts = new List<string> { ConfigLines.Line("type", type), ConfigLines.Line("drawn", Drawn(svg) ? "yes" : "no") };
		facts.AddRange(ConfigLines.ConfigFacts(ConfigMerge.Site(OracleSite), ConfigMerge.Apply(OracleSite, preamble, type)));
		if (document.Title is not null)
		{
			facts.Add(ConfigLines.Line("preamble-title", document.Title));
		}

		ConfigLines.Flatten(facts, "preamble-config", "", preamble);
		var config = MermaidConfig.Resolve(OracleSite, document, type);
		if (preamble is { Kind: MermaidValueKind.Mapping } && preamble.Entries.ContainsKey("themeVariables"))
		{
			ConfigLines.Flatten(facts, "theme-variable", "", config.ThemeVariables!);
		}

		var boxes = new List<string>();
		string? canvas = null;
		if (document is InfoDocument)
		{
			canvas = Info(facts, boxes, config);
		}
		else if (document is FlowchartDocument flowchart)
		{
			var context = new DiagramRenderContext(config, "mermaid", TextMeasurer.Default, NoIcons.Instance, [], ResourceLimits.Unlimited);
			var layout = new FlowchartModule(flowchart.DiagramType).Layout(flowchart, context);
			Flowchart(facts, flowchart, config, layout);
			canvas = FlowchartBoxes(boxes, layout);
		}
		else if (document is StateDocument state)
		{
			StateFacts.Add(facts, state, preamble, config);
			var context = new DiagramRenderContext(config, "mermaid", TextMeasurer.Default, NoIcons.Instance, [], ResourceLimits.Unlimited);
			canvas = StateFacts.Boxes(boxes, state, new StateModule().Layout(state, context));
		}
		else if (document is ClassDocument classDocument)
		{
			ClassFacts.Add(facts, classDocument, config);
			var context = new DiagramRenderContext(config, "mermaid", TextMeasurer.Default, NoIcons.Instance, [], ResourceLimits.Unlimited);
			canvas = ClassFacts.Boxes(boxes, classDocument, config, new ClassModule().Layout(classDocument, context));
		}
		else if (document is ErDocument er)
		{
			ErFacts.Add(facts, er);
		}

		return new CaseFacts(true, null, facts, boxes, canvas, svg);
	}

	/// <summary>The first line of the first error, as the oracle writes mermaid.js's.</summary>
	private static CaseFacts Refused(IReadOnlyList<MermaidDiagnostic> diagnostics)
	{
		var error = diagnostics.First(d => d.Severity == DiagnosticSeverity.Error).Message;
		var lineEnd = error.IndexOf('\n', StringComparison.Ordinal);
		return CaseFacts.Refused(lineEnd < 0 ? error : error[..lineEnd]);
	}

	/// <summary>
	/// <c>extract.mjs</c>'s flowchart boxes: every node, subgraph, edge and edge label of the module's layout, in the
	/// viewBox's space, and the viewBox as the canvas.
	/// </summary>
	private static string FlowchartBoxes(List<string> boxes, FlowchartLayout layout)
	{
		foreach (var box in layout.Boxes())
		{
			boxes.Add(Box(box.Kind, box.Name, box.X, box.Y, box.Width, box.Height));
		}

		var viewBox = layout.ViewBox!.Value;
		return string.Join('\t', "canvas", Number(viewBox.X), Number(viewBox.Y), Number(viewBox.Width), Number(viewBox.Height));
	}

	/// <summary>
	/// <c>extract.mjs</c>'s flowchart extractor over <c>FlowDB.getData</c>: the subgraphs as groups, from the last to the
	/// first, then the nodes, leaving out what a collapsed subgraph hides and a node with a group's id, then the edges.
	/// A node's label is drawn as HTML unless <c>htmlLabels</c> is false, and an edge's and a subgraph's unless
	/// <c>htmlLabels</c>, or <c>flowchart.htmlLabels</c> when it is not set, is false. The text of a node's and an
	/// edge's SVG label is the layout's, which wraps it at the width it was measured at.
	/// </summary>
	private static void Flowchart(List<string> facts, FlowchartDocument document, MermaidConfig config, FlowchartLayout layout)
	{
		var htmlNodeLabels = config.HtmlLabels ?? true;
		var htmlEdgeLabels = config.HtmlLabels ?? config.Flowchart.HtmlLabels ?? true;
		var nodeLabels = new Dictionary<string, FlowchartLabelBox>(StringComparer.Ordinal);
		var edgeLabels = new Dictionary<int, FlowchartLabelBox>();
		SvgLabels(layout.Root, nodeLabels, edgeLabels);
		var lookup = new Dictionary<string, FlowchartSubgraph>(StringComparer.Ordinal);
		foreach (var subgraph in document.Subgraphs)
		{
			lookup[subgraph.Id] = subgraph;
		}

		var subgraphParent = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (var subgraph in document.Subgraphs)
		{
			foreach (var child in subgraph.Nodes)
			{
				if (lookup.ContainsKey(child))
				{
					subgraphParent[child] = subgraph.Id;
				}
			}
		}

		bool IsCollapsed(string id) =>
			lookup.TryGetValue(id, out var s) && s.Metadata is { Kind: MermaidValueKind.Mapping } metadata
			&& metadata.Entries.TryGetValue("view", out var view) && view is { Kind: MermaidValueKind.String, String: "collapsed" };

		string? OutermostCollapsed(string id)
		{
			string? result = null;
			var seen = new HashSet<string>(StringComparer.Ordinal);
			for (string? current = id; current is not null && seen.Add(current); current = subgraphParent.GetValueOrDefault(current))
			{
				if (IsCollapsed(current))
				{
					result = current;
				}
			}

			return result;
		}

		var hidden = new HashSet<string>(StringComparer.Ordinal);
		var collapsedAncestor = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (var subgraph in document.Subgraphs)
		{
			if (OutermostCollapsed(subgraph.Id) is not { } ancestor)
			{
				continue;
			}

			if (subgraph.Id != ancestor)
			{
				_ = hidden.Add(subgraph.Id);
				collapsedAncestor[subgraph.Id] = ancestor;
			}

			foreach (var child in subgraph.Nodes)
			{
				if (child != ancestor)
				{
					_ = hidden.Add(child);
					collapsedAncestor[child] = ancestor;
				}
			}
		}

		var parent = new Dictionary<string, string>(StringComparer.Ordinal);
		for (var i = document.Subgraphs.Count - 1; i >= 0; i--)
		{
			var subgraph = document.Subgraphs[i];
			if (hidden.Contains(subgraph.Id))
			{
				continue;
			}

			foreach (var id in subgraph.Nodes)
			{
				parent[id] = subgraph.Id;
			}
		}

		// The nodes of getData: id, whether it is a group, its label, its label type and its shape.
		var nodes = new List<(string Id, bool IsGroup, string Label, FlowchartLabelType LabelType, string Shape, FlowchartNode? Node)>();
		var nodeIds = new HashSet<string>(StringComparer.Ordinal);
		for (var i = document.Subgraphs.Count - 1; i >= 0; i--)
		{
			var subgraph = document.Subgraphs[i];
			if (!hidden.Contains(subgraph.Id))
			{
				var collapsed = IsCollapsed(subgraph.Id);
				nodes.Add((subgraph.Id, !collapsed, subgraph.Title, subgraph.LabelType, collapsed ? "collapsedGroup" : "rect", null));
				_ = nodeIds.Add(subgraph.Id);
			}
		}

		foreach (var node in document.Nodes)
		{
			if (hidden.Contains(node.Id) || !nodeIds.Add(node.Id))
			{
				continue;
			}

			// An icon or image node's shape is its frame.
			var shape = node.Icon is not null || node.Image is not null ? "rect" : ShortShapeName(FlowchartShapes.Drawn(node));
			nodes.Add((node.Id, false, node.Label, node.LabelType, shape, node));
		}

		var members = new Dictionary<string, List<string>>(StringComparer.Ordinal);
		foreach (var node in nodes)
		{
			if (parent.TryGetValue(node.Id, out var group))
			{
				if (!members.TryGetValue(group, out var list))
				{
					members[group] = list = [];
				}

				list.Add(node.Id);
			}
		}

		var icons = new List<string>();
		foreach (var node in nodes)
		{
			icons.Clear();
			if (node.IsGroup)
			{
				var list = members.TryGetValue(node.Id, out var m) ? m : [];
				list.Sort(StringComparer.Ordinal);
				// The dagre renderer draws a subgraph without members as a node, so it has no cluster label.
				var groupLabel = list.Count == 0 && config.Layout == "dagre" ? "" : FlowchartLabels.Text(node.Label, node.LabelType, htmlEdgeLabels);
				facts.Add(ConfigLines.Line("group", node.Id, groupLabel, string.Join(',', list)));
				continue;
			}

			var label = UnlabelledShapes.Contains(node.Shape) ? ""
				: !htmlNodeLabels && node.Node is { Icon: null, Image: null } && nodeLabels.TryGetValue(node.Id, out var box) ? SvgText(box)
				: FlowchartLabels.Text(node.Label, node.LabelType, htmlNodeLabels, icons);
			facts.Add(ConfigLines.Line("node", node.Id, label, node.Shape));
			if (node.Node?.Icon is { } icon)
			{
				facts.Add(ConfigLines.Line("icon", node.Id, icon));
			}

			if (node.Node?.Image is { } image)
			{
				facts.Add(ConfigLines.Line("image", node.Id, image));
			}

			foreach (var name in icons)
			{
				facts.Add(ConfigLines.Line("icon", node.Id, name));
			}
		}

		for (var index = 0; index < document.Edges.Count; index++)
		{
			var edge = document.Edges[index];
			var start = collapsedAncestor.GetValueOrDefault(edge.Start) ?? edge.Start;
			var end = collapsedAncestor.GetValueOrDefault(edge.End) ?? edge.End;
			if (start == end && (collapsedAncestor.ContainsKey(edge.Start) || collapsedAncestor.ContainsKey(edge.End)))
			{
				continue;
			}

			var (startEnd, endEnd) = edge.Type switch
			{
				_ when edge.Stroke == FlowchartStroke.Invisible => ("none", "none"),
				FlowchartEdgeType.Open => ("none", "none"),
				FlowchartEdgeType.Point => ("none", "arrow"),
				FlowchartEdgeType.Circle => ("none", "circle"),
				FlowchartEdgeType.Cross => ("none", "cross"),
				FlowchartEdgeType.DoublePoint => ("arrow", "arrow"),
				FlowchartEdgeType.DoubleCircle => ("circle", "circle"),
				FlowchartEdgeType.DoubleCross => ("cross", "cross"),
				_ => ("none", "arrow"),
			};
			var line = edge.Stroke switch
			{
				FlowchartStroke.Invisible => "invisible",
				FlowchartStroke.Thick => "thick",
				FlowchartStroke.Dotted => "dotted",
				_ => "solid",
			};
			var edgeLabel = !htmlEdgeLabels && edgeLabels.TryGetValue(index, out var edgeBox) ? SvgText(edgeBox) : FlowchartLabels.Text(edge.Label, edge.LabelType, htmlEdgeLabels);
			facts.Add(ConfigLines.Line("edge", start, end, startEnd, endEnd, line, edgeLabel));
		}
	}

	/// <summary>The measured SVG labels of the drawing's nodes, by id, and of its edges, by their place in the document.</summary>
	private static void SvgLabels(FlowchartDrawing drawing, Dictionary<string, FlowchartLabelBox> nodes, Dictionary<int, FlowchartLabelBox> edges)
	{
		foreach (var edge in drawing.Edges)
		{
			if (edge.Label is { Html: false } label && (label.HasText || !edges.ContainsKey(edge.Index)))
			{
				edges[edge.Index] = label;
			}
		}

		foreach (var node in drawing.Nodes)
		{
			if (node.Subgraph is { } subgraph)
			{
				SvgLabels(subgraph, nodes, edges);
			}
			else if (node.Label is { Html: false } label && !node.IsGroup && !node.IsDummy)
			{
				nodes[node.Id] = label;
			}
		}
	}

	/// <summary>The text of a measured SVG label: its lines' words, which hold their spaces, run together.</summary>
	private static string SvgText(FlowchartLabelBox box) => string.Concat(box.Lines.SelectMany(line => line.Select(run => run.Text)));

	/// <summary>The short names of the shapes whose handlers in mermaid.js's <c>shapes</c> draw no label.</summary>
	private static readonly HashSet<string> UnlabelledShapes =
		new(["anchor", "bolt", "choice", "cross-circ", "f-circ", "fork", "fr-circ", "hourglass", "sm-circ"], StringComparer.Ordinal);

	private static readonly Lazy<Dictionary<string, string>> ShortShapeNames = new(() =>
	{
		var names = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (var line in File.ReadLines(Path.Combine(CorpusCheck.CorpusRoot, "shapes.txt")))
		{
			var fields = line.Split('\t');
			if (fields is ["shape", var shortName, ..])
			{
				names[shortName] = shortName;
			}
		}

		foreach (var line in File.ReadLines(Path.Combine(CorpusCheck.CorpusRoot, "shapes.txt")))
		{
			var fields = line.Split('\t');
			if (fields is ["alias" or "internal-alias", var alias, var shortName])
			{
				_ = names.TryAdd(alias, shortName);
			}
		}

		return names;
	});

	/// <summary>The documented short name of a shape, as <c>facts.mjs</c> maps it with mermaid.js's shape table.</summary>
	internal static string ShortShapeName(string shape) => ShortShapeNames.Value.GetValueOrDefault(shape) ?? shape;

	/// <summary>
	/// <c>extract.mjs</c>'s info extractor: the version as a label, and the box of the version text. The root has no
	/// viewBox and no height, so the canvas is its width and the 150 a browser gives an SVG without a height.
	/// </summary>
	private static string Info(List<string> facts, List<string> boxes, MermaidConfig config)
	{
		const double FontSize = 32;
		var version = MermaidCompatibility.MermaidJsVersion;
		facts.Add(ConfigLines.Line("label", version));
		var family = config.ThemeVariables?["fontFamily"] is { Kind: MermaidValueKind.String } font ? font.String : "sans-serif";
		var metrics = TextMeasurer.Default.Measure("v" + version, new FontSpec(family, FontSize));
		boxes.Add(Box("version", version, 100 - (metrics.Width / 2), 40 - metrics.Ascent, metrics.Width, metrics.Height));
		return "canvas\t0\t0\t400\t150";
	}

	internal static string Box(string kind, string name, double x, double y, double width, double height) =>
		string.Join('\t', "box", kind, name, Number(x), Number(y), Number(width), Number(height));

	internal static string Number(double value) => JsNumber.ToString(Math.Round(value, 3, MidpointRounding.AwayFromZero));

	/// <summary>
	/// <c>drawn</c>: whether the SVG has a shape, or a text with something other than space, outside <c>defs</c> and
	/// markers.
	/// </summary>
	private static bool Drawn(string svg)
	{
		using var reader = XmlReader.Create(new StringReader(svg), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
		var hidden = 0;
		var inText = 0;
		while (reader.Read())
		{
			switch (reader.NodeType)
			{
				case XmlNodeType.Element:
					var isEmpty = reader.IsEmptyElement;
					switch (reader.LocalName)
					{
						case "defs" or "marker" or "style" or "title" or "desc":
							hidden += isEmpty ? 0 : 1;
							break;
						case "rect" or "circle" or "ellipse" or "polygon" or "polyline" or "line" or "path" or "image" when hidden == 0:
							return true;
						case "text" when !isEmpty:
							inText++;
							break;
					}

					break;
				case XmlNodeType.EndElement:
					switch (reader.LocalName)
					{
						case "defs" or "marker" or "style" or "title" or "desc":
							hidden--;
							break;
						case "text":
							inText--;
							break;
					}

					break;
				case XmlNodeType.Text when hidden == 0 && inText > 0 && !string.IsNullOrWhiteSpace(reader.Value):
					return true;
			}
		}

		return false;
	}
}
