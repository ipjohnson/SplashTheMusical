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
	public static void Add(List<string> facts, ErDocument document)
	{
		var db = ErDb.From(document);
		foreach (var entity in db.Entities)
		{
			facts.Add(ConfigLines.Line("entity", entity.Name, Decode(entity.Alias.Length > 0 ? entity.Alias : entity.Name)));
			foreach (var attribute in entity.Attributes)
			{
				var keys = attribute.Keys.ToList();
				keys.Sort(StringComparer.Ordinal);
				facts.Add(ConfigLines.Line("attribute", entity.Name, Decode(attribute.Type), Decode(attribute.Name), string.Join(',', keys), Decode(attribute.Comment ?? "")));
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
		}
	}

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
