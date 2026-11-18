using SplashTheMusical.Config;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Diagrams.State;

namespace SplashTheMusical.Conformance;

/// <summary>
/// <c>extract.mjs</c>'s state extractor over <see cref="StateData"/>, the port of <c>StateDB.getData</c>. Start and end
/// states are named <c>[*]start@parent</c> and <c>[*]end@parent</c>, the last concurrent region's generated id is read
/// as <c>divider-id-N</c>, and labels are the text <c>createText</c> draws for a Markdown label.
/// </summary>
internal static class StateFacts
{
	private const string ParentSuffix = "----parent";

	public static void Add(List<string> facts, StateDocument document, MermaidValue? preamble, MermaidConfig config)
	{
		var look = preamble is { Kind: MermaidValueKind.Mapping } mapping && mapping.Entries.TryGetValue("look", out var value) && value.Kind == MermaidValueKind.String
			? value.String
			: null;
		var data = StateData.From(document, look);
		var html = config.HtmlLabels ?? true;
		var edgeHtml = config.HtmlLabels ?? config.Flowchart.HtmlLabels ?? true;
		var nodes = new Dictionary<string, StateDataNode>(StringComparer.Ordinal);
		foreach (var node in data.Nodes)
		{
			nodes[node.Id] = node;
		}

		string? ParentOf(string id)
		{
			var parent = nodes.TryGetValue(id, out var node) ? node.ParentId : null;
			while (parent is not null && parent.EndsWith(ParentSuffix, StringComparison.Ordinal))
			{
				parent = nodes.TryGetValue(parent, out var wrapper) ? wrapper.ParentId : null;
			}

			return parent is null ? null : Region(parent);
		}

		string Name(string id)
		{
			var shape = nodes.TryGetValue(id, out var node) ? node.Shape : null;
			return shape switch
			{
				"stateStart" => $"[*]start@{ParentOf(id) ?? "root"}",
				"stateEnd" => $"[*]end@{ParentOf(id) ?? "root"}",
				_ => Region(id),
			};
		}

		var children = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
		foreach (var node in data.Nodes)
		{
			if (node.Id.EndsWith(ParentSuffix, StringComparison.Ordinal) || IsNote(node) || ParentOf(node.Id) is not { } parent)
			{
				continue;
			}

			if (!children.TryGetValue(parent, out var set))
			{
				children[parent] = set = new SortedSet<string>(StringComparer.Ordinal);
			}

			_ = set.Add(Name(node.Id));
		}

		var seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (var node in data.Nodes)
		{
			if (node.Id.EndsWith(ParentSuffix, StringComparison.Ordinal))
			{
				continue;
			}

			if (IsNote(node))
			{
				facts.Add(ConfigLines.Line("note", node.NoteOf ?? node.Id.Split("----")[0], Text(node.Label[0], html)));
				continue;
			}

			if (node.IsGroup)
			{
				var members = children.TryGetValue(Region(node.Id), out var set) ? string.Join(',', set) : "";
				facts.Add(ConfigLines.Line("group", Region(node.Id), Text(node.Label[0], html), members));
				continue;
			}

			var id = Name(node.Id);
			if (!seen.Add(id))
			{
				continue;
			}

			var shape = node.Shape switch
			{
				"choice" => "diam",
				"fork" or "join" => "fork",
				"rect" => "rounded",
				null => "",
				_ => StmFactsProvider.ShortShapeName(node.Shape),
			};
			facts.Add(ConfigLines.Line("node", id, shape == "rounded" ? Text(node.Label[0], html) : "", shape));
		}

		foreach (var edge in data.Edges)
		{
			if (IsNoteId(edge.Start, nodes) || IsNoteId(edge.End, nodes))
			{
				continue;
			}

			var end = edge.ArrowTypeEnd == "arrow_barb" ? "arrow" : edge.ArrowTypeEnd is { Length: > 0 } arrow ? arrow : "none";
			facts.Add(ConfigLines.Line("edge", Name(edge.Start), Name(edge.End), "none", end, "solid", Text(edge.Label, edgeHtml)));
		}
	}

	private static bool IsNote(StateDataNode node) => node.Shape == "note" || node.Id.Contains("----note", StringComparison.Ordinal);

	private static bool IsNoteId(string id, Dictionary<string, StateDataNode> nodes) => nodes.TryGetValue(id, out var node) && IsNote(node);

	private static string Text(string label, bool html) => FlowchartLabels.Text(label, FlowchartLabelType.Markdown, html);

	/// <summary>The last concurrent region's generated id, <c>id-&lt;random&gt;-N</c>, read as <c>divider-id-N</c>.</summary>
	private static string Region(string id)
	{
		if (!id.StartsWith("id-", StringComparison.Ordinal))
		{
			return id;
		}

		var dash = id.LastIndexOf('-');
		var random = id.AsSpan(3, Math.Max(0, dash - 3));
		var number = id.AsSpan(dash + 1);
		return dash > 3 && random.Length > 0 && !random.ContainsAnyExcept(Lowercase) && number.Length > 0 && !number.ContainsAnyExcept(Digits)
			? "divider-id-" + number.ToString()
			: id;
	}

	private static readonly System.Buffers.SearchValues<char> Lowercase = System.Buffers.SearchValues.Create("abcdefghijklmnopqrstuvwxyz0123456789");

	private static readonly System.Buffers.SearchValues<char> Digits = System.Buffers.SearchValues.Create("0123456789");
}
