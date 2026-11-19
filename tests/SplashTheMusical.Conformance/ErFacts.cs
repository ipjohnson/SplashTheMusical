using System.Text;
using System.Xml;
using SplashTheMusical.Diagrams.Er;
using SplashTheMusical.Diagrams.Flowchart;

namespace SplashTheMusical.Conformance;

/// <summary>
/// <c>extract.mjs</c>'s er extractor over <see cref="ErDb"/>, the port of <c>ErDB</c>: each entity with its display
/// name and attributes, then each relationship with the cardinality at each end. Text is what the extractor's
/// <c>decode</c> makes of the db's: entity codes and HTML read as HTML.
/// </summary>
internal static class ErFacts
{
	public static void Add(List<string> facts, ErDocument document, string svg)
	{
		var db = ErDb.From(document);
		var shown = new List<string>();
		foreach (var entity in db.Entities)
		{
			var display = Decode(entity.Alias.Length > 0 ? entity.Alias : entity.Name);
			facts.Add(ConfigLines.Line("entity", entity.Name, display));
			shown.Add(display);
			foreach (var attribute in entity.Attributes)
			{
				var keys = attribute.Keys.ToList();
				keys.Sort(StringComparer.Ordinal);
				facts.Add(ConfigLines.Line("attribute", entity.Name, Decode(attribute.Type), Decode(attribute.Name), string.Join(',', keys), Decode(attribute.Comment ?? "")));
				shown.Add(Decode(attribute.Type));
				shown.Add(Decode(attribute.Name));
				shown.Add(Decode(attribute.Comment ?? ""));
			}
		}

		foreach (var relationship in db.Relationships)
		{
			var source = relationship.Source;
			facts.Add(ConfigLines.Line(
				"relation",
				source.EntityA,
				source.EntityB,
				Cardinality(source.CardinalityA),
				Cardinality(source.CardinalityB),
				source.Identifying ? "identifying" : "non-identifying",
				Decode(source.Role)));
			shown.Add(Decode(source.Role));
		}

		Labels(facts, shown, svg);
	}

	/// <summary>
	/// The extractor's er boxes from the module's layout: each drawn entity by its name, and each relationship's path and
	/// label by the names of its ends, in the order of the relationships. The canvas is the viewBox.
	/// </summary>
	public static string Boxes(List<string> boxes, ErDocument document, ErLayout layout)
	{
		var db = ErDb.From(document);
		var names = db.Entities.ToDictionary(e => e.Id, e => e.Name, StringComparer.Ordinal);
		string Name(string id) => names.GetValueOrDefault(id) ?? id;
		var used = new Dictionary<string, int>(StringComparer.Ordinal);
		void Add(string kind, string name, FlowchartBox box)
		{
			var count = used[kind + "\t" + name] = used.GetValueOrDefault(kind + "\t" + name) + 1;
			boxes.Add(StmFactsProvider.Box(kind, count > 1 ? $"{name} #{count}" : name, box.X, box.Y, box.Width, box.Height));
		}

		foreach (var box in FlowchartLayout.BoxesOf(layout.Root))
		{
			switch (box.Kind)
			{
				case "node" when names.ContainsKey(box.Name):
					Add("entity", Name(box.Name), box);
					break;
				case "edge" or "edge-label":
					var name = box.Name;
					var hash = name.LastIndexOf(" #", StringComparison.Ordinal);
					if (hash > 0 && int.TryParse(name.AsSpan(hash + 2), out _))
					{
						name = name[..hash];
					}

					var arrow = name.IndexOf(" --> ", StringComparison.Ordinal);
					Add(box.Kind == "edge" ? "relation" : "relation-label", Name(name[..arrow]) + " --> " + Name(name[(arrow + 5)..]), box);
					break;
			}
		}

		var viewBox = layout.ViewBox!.Value;
		return string.Join('\t', "canvas", StmFactsProvider.Number(viewBox.X), StmFactsProvider.Number(viewBox.Y), StmFactsProvider.Number(viewBox.Width), StmFactsProvider.Number(viewBox.Height));
	}

	/// <summary>
	/// The extractor's <c>labels</c>: a <c>label</c> fact for each model label whose letters and digits, case-folded,
	/// occur in the drawn text, longest first, each taking the text it matched.
	/// </summary>
	private static void Labels(List<string> facts, List<string> shown, string svg)
	{
		var drawn = Key(DrawnText(svg));
		var labels = shown.Select(Norm).Where(l => Key(l).Length > 0).ToList();
		labels.Sort((a, b) =>
		{
			var byLength = Key(b).Length.CompareTo(Key(a).Length);
			return byLength != 0 ? byLength : string.CompareOrdinal(a, b);
		});
		foreach (var label in labels)
		{
			var key = Key(label);
			var at = drawn.IndexOf(key, StringComparison.Ordinal);
			if (at < 0)
			{
				continue;
			}

			drawn = string.Concat(drawn.AsSpan(0, at), "\0", drawn.AsSpan(at + key.Length));
			facts.Add(ConfigLines.Line("label", label));
		}
	}

	/// <summary>The text of each outermost <c>text</c> element outside <c>defs</c>, <c>marker</c> and <c>style</c>, run together.</summary>
	private static string DrawnText(string svg)
	{
		var text = new StringBuilder();
		using var reader = XmlReader.Create(new StringReader(svg), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
		var hidden = 0;
		var inText = 0;
		while (reader.Read())
		{
			switch (reader.NodeType)
			{
				case XmlNodeType.Element when !reader.IsEmptyElement:
					if (reader.LocalName is "defs" or "marker" or "style" or "title" or "desc" or "clipPath" or "mask" or "pattern" or "symbol")
					{
						hidden++;
					}
					else if (reader.LocalName == "text")
					{
						inText++;
					}

					break;
				case XmlNodeType.EndElement:
					if (reader.LocalName is "defs" or "marker" or "style" or "title" or "desc" or "clipPath" or "mask" or "pattern" or "symbol")
					{
						hidden--;
					}
					else if (reader.LocalName == "text")
					{
						inText--;
					}

					break;
				case XmlNodeType.Text or XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace when hidden == 0 && inText > 0:
					text.Append(reader.Value);
					break;
			}
		}

		return text.ToString();
	}

	/// <summary>The extractor's <c>key</c>: letters and digits, case-folded.</summary>
	private static string Key(string text)
	{
		var builder = new StringBuilder(text.Length);
		foreach (var rune in Norm(text).ToLowerInvariant().EnumerateRunes())
		{
			if (Rune.IsLetter(rune) || Rune.IsNumber(rune) || rune.Value == 0)
			{
				builder.Append(rune.ToString());
			}
		}

		return builder.ToString();
	}

	private static string Norm(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

	private static string Cardinality(ErCardinality cardinality) => cardinality switch
	{
		ErCardinality.OnlyOne => "one",
		ErCardinality.ZeroOrOne => "zero-or-one",
		ErCardinality.OneOrMore => "one-or-more",
		ErCardinality.ZeroOrMore => "zero-or-more",
		_ => "parent",
	};

	private static string Decode(string text) => FlowchartLabels.HtmlTextOf(text);
}
