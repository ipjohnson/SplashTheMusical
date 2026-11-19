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
