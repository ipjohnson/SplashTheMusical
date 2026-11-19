using SplashTheMusical.Config;
using SplashTheMusical.Diagrams.Class;
using SplashTheMusical.Diagrams.Flowchart;

namespace SplashTheMusical.Conformance;

/// <summary>
/// <c>extract.mjs</c>'s class extractor over <see cref="ClassData"/>, the port of <c>ClassDB</c>. A class's name,
/// annotation and members are the text the class box draws, a lollipop's interface is named <c>()label</c>, and notes
/// and cardinalities are the db's text decoded.
/// </summary>
internal static class ClassFacts
{
	public static void Add(List<string> facts, ClassDocument document, MermaidConfig config)
	{
		var data = ClassData.From(document);
		var html = config.HtmlLabels ?? true;
		var edgeHtml = config.HtmlLabels ?? config.Flowchart.HtmlLabels ?? true;
		var interfaces = data.Interfaces.ToDictionary(i => i.Id, i => "()" + Decode(i.Label), StringComparer.Ordinal);
		string Name(string id) => interfaces.GetValueOrDefault(id) ?? id;

		// getData: without hierarchical namespaces only the declared ones are groups, with no parent, and a class or
		// note is in its nearest declared namespace.
		var hierarchical = config.Class.HierarchicalNamespaces ?? true;
		string? ParentOf(string? parent)
		{
			if (hierarchical)
			{
				return parent;
			}

			while (parent is not null && data.Namespaces.TryGetValue(parent, out var space) && !space.Explicit)
			{
				parent = space.Parent;
			}

			return parent is not null && data.Namespaces.ContainsKey(parent) ? parent : null;
		}

		foreach (var space in data.Namespaces.Values)
		{
			if (!hierarchical && !space.Explicit)
			{
				continue;
			}

			var kids = new List<string>();
			if (hierarchical)
			{
				kids.AddRange(data.Namespaces.Values.Where(n => n.Parent == space.Id).Select(n => n.Id));
			}

			kids.AddRange(data.Classes.Values.Where(c => ParentOf(c.Parent) == space.Id).Select(c => c.Id));
			kids.AddRange(data.Notes.Where(n => ParentOf(n.Parent) == space.Id).Select(n => n.Id));
			kids.Sort(StringComparer.Ordinal);
			facts.Add(ConfigLines.Line("namespace", space.Id, string.Join(',', kids)));
		}

		foreach (var node in data.Classes.Values)
		{
			facts.Add(ConfigLines.Line("class", node.Id, Text(node.Text, html)));
			if (node.Annotations.Count > 0)
			{
				facts.Add(ConfigLines.Line("annotation", node.Id, Text($"«{node.Annotations[0]}»", html)));
			}

			foreach (var member in node.Attributes)
			{
				facts.Add(ConfigLines.Line("attribute", node.Id, Text(member.Text, html)));
			}

			foreach (var member in node.Methods)
			{
				facts.Add(ConfigLines.Line("method", node.Id, Text(member.Text, html)));
			}
		}

		foreach (var note in data.Notes)
		{
			facts.Add(ConfigLines.Line("note", note.Class ?? "", Decode(note.Text)));
		}

		foreach (var relation in data.Relations)
		{
			facts.Add(ConfigLines.Line(
				"relation",
				Name(relation.Id1),
				Name(relation.Id2),
				End(relation.Type1),
				End(relation.Type2),
				relation.Line == ClassLineStyle.Dotted ? "dashed" : "solid",
				relation.Title is { } title ? FlowchartLabels.Text(title, FlowchartLabelType.Markdown, edgeHtml) : "",
				Cardinality(relation.Title1),
				Cardinality(relation.Title2)));
		}
	}

	/// <summary>
	/// The extractor's class boxes from the module's layout: each class by its id, a namespace as a group, a note by its
	/// text, leaving out the lollipops' interfaces, and each relation and its label by the names of its ends, leaving
	/// out a note's edge. The canvas is the viewBox.
	/// </summary>
	public static string Boxes(List<string> boxes, ClassDocument document, MermaidConfig config, ClassLayout layout)
	{
		var data = ClassData.From(document);
		var html = config.HtmlLabels ?? true;
		var interfaces = data.Interfaces.ToDictionary(i => i.Id, i => "()" + Decode(i.Label), StringComparer.Ordinal);
		var notes = data.Notes.ToDictionary(n => n.Id, n => n.Text, StringComparer.Ordinal);
		string Name(string id) => interfaces.GetValueOrDefault(id) ?? id;
		var used = new Dictionary<string, int>(StringComparer.Ordinal);
		void Add(string kind, string name, FlowchartBox box)
		{
			// The extractor's norm: runs of white space, such as a class name's line breaks, as one space.
			name = string.Join(' ', name.Split((char[])[' ', '\t', '\n', '\r', '\f', '\v'], StringSplitOptions.RemoveEmptyEntries));
			var count = used[kind + "\t" + name] = used.GetValueOrDefault(kind + "\t" + name) + 1;
			boxes.Add(StmFactsProvider.Box(kind, count > 1 ? $"{name} #{count}" : name, box.X, box.Y, box.Width, box.Height));
		}

		var all = layout.Boxes();
		foreach (var box in all.Where(b => b.Kind is "group" or "node"))
		{
			if (box.Kind == "group")
			{
				Add("namespace", box.Name, box);
			}
			else if (notes.TryGetValue(box.Name, out var text))
			{
				var shown = FlowchartLabels.Text(text, FlowchartLabelType.Markdown, html);
				Add("note", shown.Length > 0 ? shown : Decode(text), box);
			}
			else if (!interfaces.ContainsKey(box.Name))
			{
				Add("class", box.Name, box);
			}
		}

		foreach (var box in all.Where(b => b.Kind is "edge" or "edge-label"))
		{
			var name = box.Name;
			var hash = name.LastIndexOf(" #", StringComparison.Ordinal);
			if (hash > 0 && int.TryParse(name.AsSpan(hash + 2), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out _))
			{
				name = name[..hash];
			}

			var arrow = name.IndexOf(" --> ", StringComparison.Ordinal);
			var start = name[..arrow];
			var end = name[(arrow + 5)..];
			if (notes.ContainsKey(start) || notes.ContainsKey(end))
			{
				continue;
			}

			Add(box.Kind == "edge" ? "relation" : "relation-label", Name(start) + " --> " + Name(end), box);
		}

		var viewBox = layout.ViewBox!.Value;
		return string.Join('\t', "canvas", StmFactsProvider.Number(viewBox.X), StmFactsProvider.Number(viewBox.Y), StmFactsProvider.Number(viewBox.Width), StmFactsProvider.Number(viewBox.Height));
	}

	private static string End(ClassRelationEnd end) => end switch
	{
		ClassRelationEnd.Aggregation => "diamond",
		ClassRelationEnd.Extension => "triangle",
		ClassRelationEnd.Composition => "diamond-filled",
		ClassRelationEnd.Dependency => "arrow",
		ClassRelationEnd.Lollipop => "lollipop",
		_ => "none",
	};

	private static string Cardinality(string text)
	{
		var decoded = Decode(text);
		return decoded == "none" ? "" : decoded;
	}

	/// <summary><c>addText</c>: the text a class box draws, with the backslash before an SVG label's visibility left out.</summary>
	private static string Text(string text, bool html) =>
		FlowchartLabels.Text(!html && text.StartsWith('\\') ? text[1..] : text, FlowchartLabelType.Markdown, html);

	/// <summary>The extractor's <c>decode</c>: entity codes and HTML entities decoded, tags dropped and <c>&lt;br&gt;</c> read as a space.</summary>
	private static string Decode(string text) => FlowchartLabels.Text(text, FlowchartLabelType.Text, true);
}
