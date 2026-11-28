using SplashTheMusical.Diagrams.C4;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Text;

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

	/// <summary>
	/// The extractor's c4 boxes, from the layout: the title's text, each drawn element's group, the rect of the boundary
	/// whose text is a boundary's label, and the text that is a relation's label. A text is found as <c>byText</c> finds
	/// it: the first, in document order, whose letters and digits are the label's and that no earlier lookup took. A
	/// boundary's or a relation's text is <c>svg &gt; g &gt; text</c>, one per line, and the title's may be any text.
	/// </summary>
	public static string Boxes(List<string> boxes, C4Document document, C4Layout layout, ITextMeasurer measurer)
	{
		var db = C4Db.From(document);
		var texts = new List<DrawnText>();
		foreach (var drawing in layout.Drawings)
		{
			if (drawing is C4ShapeDrawing shape)
			{
				foreach (var section in shape.Sections)
				{
					var x = shape.CenterX + shape.Geometry.LabelTranslate.X + section.X + section.Box.X;
					var y = shape.CenterY + shape.Geometry.LabelTranslate.Y + section.Y + section.Box.Y;
					var content = string.Concat(section.Box.Lines.SelectMany(l => l).Select(r => r.Text));
					texts.Add(new DrawnText(content, false, null, (x, y, section.Box.Width, section.Box.Height)));
				}
			}
			else if (drawing is C4BoundaryDrawing boundary)
			{
				texts.AddRange(boundary.Texts.Select(t => Line(t, boundary, measurer)));
			}
		}

		texts.AddRange(layout.Rels.SelectMany(r => r.Texts).Select(t => Line(t, null, measurer)));
		if (layout.Title is { } title)
		{
			var metrics = measurer.Measure(title.Text, title.Font);
			texts.Add(new DrawnText(title.Text, false, null, (title.X, title.Y - metrics.Ascent, metrics.Width, metrics.Height)));
		}

		var taken = new HashSet<DrawnText>(ReferenceEqualityComparer.Instance);
		DrawnText? ByText(string label, bool direct)
		{
			var want = Key(label);
			if (want.Length == 0)
			{
				return null;
			}

			var found = texts.FirstOrDefault(t => (t.Direct || !direct) && !taken.Contains(t) && Key(t.Content) == want);
			if (found is not null)
			{
				taken.Add(found);
			}

			return found;
		}

		var names = new Dictionary<string, int>(StringComparer.Ordinal);
		void Add(string kind, string name, (double X, double Y, double Width, double Height) box)
		{
			if (box.Width == 0 && box.Height == 0)
			{
				return;
			}

			var key = kind + "\t" + name;
			var n = names[key] = names.GetValueOrDefault(key) + 1;
			boxes.Add(StmFactsProvider.Box(kind, n > 1 ? $"{name} #{n}" : name, box.X, box.Y, box.Width, box.Height));
		}

		if (db.Title.Length > 0 && ByText(Decode(db.Title), direct: false) is { } titleText)
		{
			Add("title", Decode(db.Title), titleText.Box);
		}

		// mermaid.js's computer, database and clock symbols come first in the document with the ids <svg id>-computer and
		// so on, so the extractor's id lookup finds a symbol, which has no box, for an element of one of those aliases.
		foreach (var shape in db.Shapes)
		{
			var alias = shape.StringOf("alias") ?? "";
			if (alias is not ("computer" or "database" or "clock")
				&& layout.Drawings.OfType<C4ShapeDrawing>().FirstOrDefault(s => s.Alias == alias) is { } drawn)
			{
				var bounds = drawn.Geometry.Bounds;
				Add("element", alias, (drawn.CenterX + bounds.MinX, drawn.CenterY + bounds.MinY, bounds.Width, bounds.Height));
			}
		}

		foreach (var boundary in db.Boundaries)
		{
			if (boundary.StringOf("alias") != "global" && ByText(Decode(boundary.TextOf("label")), direct: true)?.Boundary is { } frame)
			{
				Add("boundary", boundary.StringOf("alias") ?? "", (frame.X, frame.Y, frame.Width, frame.Height));
			}
		}

		foreach (var rel in db.Rels)
		{
			if (ByText(Decode(rel.TextOf("label")), direct: true) is { } label)
			{
				Add("rel-label", $"{rel.StringOf("from")} --> {rel.StringOf("to")}", label.Box);
			}
		}

		var viewBox = layout.ViewBox!.Value;
		return string.Join('\t', "canvas", StmFactsProvider.Number(viewBox.X), StmFactsProvider.Number(viewBox.Y), StmFactsProvider.Number(viewBox.Width), StmFactsProvider.Number(viewBox.Height));
	}

	/// <summary>A line of <c>byTspan</c>: its width in its font, centred on its x, with its line box centred on its middle.</summary>
	private static DrawnText Line(C4TextLine line, C4BoundaryDrawing? boundary, ITextMeasurer measurer)
	{
		var metrics = measurer.Measure(line.Text, line.Italic ? line.Font with { Style = FontStyle.Italic } : line.Font);
		return new DrawnText(line.Text, true, boundary, (line.X - (metrics.Width / 2), line.Y + line.Dy - (metrics.Height / 2), metrics.Width, metrics.Height));
	}

	/// <summary>The extractor's <c>key</c>: the letters and digits of the text, in lower case.</summary>
	private static string Key(string text)
	{
		var builder = new System.Text.StringBuilder(text.Length);
		foreach (var rune in text.ToLowerInvariant().EnumerateRunes())
		{
			if (System.Text.Rune.IsLetter(rune) || System.Text.Rune.IsNumber(rune))
			{
				builder.Append(rune.ToString());
			}
		}

		return builder.ToString();
	}

	/// <summary>A drawn <c>text</c>: its content, whether it is a child of a group of the root, the boundary it belongs to and its box.</summary>
	private sealed record DrawnText(string Content, bool Direct, C4BoundaryDrawing? Boundary, (double X, double Y, double Width, double Height) Box);

	/// <summary>The parent boundary, empty for <c>global</c>.</summary>
	private static string Parent(C4DbItem item)
	{
		var parent = item.StringOf("parentBoundary") ?? "";
		return parent == "global" ? "" : parent;
	}

	private static string Decode(string? text) => text is null ? "" : FlowchartLabels.HtmlTextOf(text);
}
