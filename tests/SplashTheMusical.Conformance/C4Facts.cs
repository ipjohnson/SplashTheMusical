using SplashTheMusical.Diagrams.C4;
using SplashTheMusical.Diagrams.Flowchart;

namespace SplashTheMusical.Conformance;

/// <summary>
/// <c>extract.mjs</c>'s c4 extractor over <see cref="C4Db"/>, the port of <c>c4Db.ts</c>: the title, then each shape
/// with its type split into the type and the external flag, its label, technology, description and boundary, then each
/// boundary but <c>global</c> with its type in lower case, its label and its parent, then each relation. Text is what
/// the extractor's <c>decode</c> makes of the db's: entity codes and HTML read as HTML, and a <c>br</c> tag as a space.
/// </summary>
internal static class C4Facts
{
	public static void Add(List<string> facts, C4Document document, string svg)
	{
		var db = C4Db.From(document);
		var shown = new List<string>();
		if (db.Title.Length > 0)
		{
			facts.Add(ConfigLines.Line("title", Decode(db.Title)));
			shown.Add(Decode(db.Title));
		}

		foreach (var shape in db.Shapes)
		{
			var type = shape.TextOf("typeC4Shape") ?? "";
			var external = type.StartsWith("external_", StringComparison.Ordinal);
			var label = Decode(shape.TextOf("label"));
			var techn = Decode(shape.TextOf("techn"));
			var descr = Decode(shape.TextOf("descr"));
			facts.Add(ConfigLines.Line(
				"element",
				shape.StringOf("alias") ?? "",
				external ? type["external_".Length..] : type,
				external ? "external" : "internal",
				label,
				techn,
				descr,
				Parent(shape)));
			shown.Add(label);
			shown.Add(techn);
			shown.Add(descr);
		}

		foreach (var boundary in db.Boundaries)
		{
			if (boundary.StringOf("alias") == "global")
			{
				continue;
			}

			var label = Decode(boundary.TextOf("label"));
			facts.Add(ConfigLines.Line("boundary", boundary.StringOf("alias") ?? "", (boundary.TextOf("type") ?? "").ToLowerInvariant(), label, Parent(boundary)));
			shown.Add(label);
		}

		foreach (var rel in db.Rels)
		{
			var label = Decode(rel.TextOf("label"));
			var techn = Decode(rel.TextOf("techn"));
			facts.Add(ConfigLines.Line("rel", rel.StringOf("from") ?? "", rel.StringOf("to") ?? "", rel.StringOf("type") == "birel" ? "both" : "one", label, techn));
			shown.Add(label);
			shown.Add(techn);
		}

		ErFacts.Labels(facts, shown, svg);
	}

	/// <summary>The parent boundary, empty for <c>global</c>.</summary>
	private static string Parent(C4DbItem item)
	{
		var parent = item.StringOf("parentBoundary") ?? "";
		return parent == "global" ? "" : parent;
	}

	private static string Decode(string? text) => text is null ? "" : FlowchartLabels.HtmlTextOf(text);
}
