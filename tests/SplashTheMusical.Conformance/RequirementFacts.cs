using System.Text;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Diagrams.Requirement;

namespace SplashTheMusical.Conformance;

/// <summary>
/// <c>extract.mjs</c>'s requirement extractor over <see cref="RequirementDb"/>, the port of <c>RequirementDB</c>: each
/// requirement with its kind, id, text, risk and verify method, each element with its type and document reference,
/// then each relationship. Names are the db's, with the placeholders <c>encodeEntities</c> gives entity codes. Other
/// text is what the extractor's <c>decode</c> makes of the db's: entity codes and HTML read as HTML. Kinds, risks and
/// verify methods are the db's words in lower case, joined with <c>-</c>.
/// </summary>
internal static class RequirementFacts
{
	public static void Add(List<string> facts, RequirementDocument document, string svg)
	{
		var db = RequirementDb.From(document);
		var shown = new List<string>();
		foreach (var requirement in db.Requirements)
		{
			shown.Add(Encode(requirement.Name));
			shown.Add(Decode(requirement.Id));
			shown.Add(Decode(requirement.Text));
			facts.Add(ConfigLines.Line(
				"requirement",
				Encode(requirement.Name),
				Lower(RequirementData.KindName(requirement.Kind)),
				Decode(requirement.Id),
				Decode(requirement.Text),
				Lower(requirement.Risk?.ToString() ?? ""),
				Lower(requirement.VerifyMethod?.ToString() ?? "")));
		}

		foreach (var element in db.Elements)
		{
			facts.Add(ConfigLines.Line("element", Encode(element.Name), Decode(element.Type), Decode(element.DocRef)));
			shown.Add(Encode(element.Name));
			shown.Add(Decode(element.Type));
			shown.Add(Decode(element.DocRef));
		}

		foreach (var relationship in db.Relationships)
		{
			var type = relationship.Type.ToString().ToLowerInvariant();
			facts.Add(ConfigLines.Line("relation", Encode(relationship.Source), Encode(relationship.Target), type));
			shown.Add(type);
		}

		ErFacts.Labels(facts, shown, svg);
	}

	/// <summary>
	/// The extractor's requirement boxes from the module's layout: each requirement and element by its name, and each
	/// relationship's path and label by the names of its ends, in the order of the relationships. The canvas is the
	/// viewBox.
	/// </summary>
	public static string Boxes(List<string> boxes, RequirementDocument document, RequirementLayout layout)
	{
		var db = RequirementDb.From(document);
		var requirements = db.Requirements.Select(r => r.Name).ToHashSet(StringComparer.Ordinal);
		var elements = db.Elements.Select(e => e.Name).ToHashSet(StringComparer.Ordinal);
		foreach (var box in FlowchartLayout.BoxesOf(layout.Root))
		{
			var kind = box.Kind switch
			{
				"node" when requirements.Contains(box.Name) => "requirement",
				"node" when elements.Contains(box.Name) => "element",
				"edge" => "relation",
				"edge-label" => "relation-label",
				_ => null,
			};
			if (kind is not null)
			{
				boxes.Add(StmFactsProvider.Box(kind, box.Name, box.X, box.Y, box.Width, box.Height));
			}
		}

		var viewBox = layout.ViewBox!.Value;
		return string.Join('\t', "canvas", StmFactsProvider.Number(viewBox.X), StmFactsProvider.Number(viewBox.Y), StmFactsProvider.Number(viewBox.Width), StmFactsProvider.Number(viewBox.Height));
	}

	/// <summary>The extractor's <c>lower</c>: the text with its space folded, in lower case, with <c>-</c> for each space.</summary>
	private static string Lower(string text) =>
		string.Join('-', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

	private static string Decode(string text) => FlowchartLabels.HtmlTextOf(text);

	/// <summary>
	/// <c>encodeEntities</c>' <c>/#\w+;/g</c>: <c>#123;</c> becomes <c>ﬂ°°123¶ß</c> and <c>#name;</c> becomes
	/// <c>ﬂ°name¶ß</c>, as the db holds them.
	/// </summary>
	private static string Encode(string text)
	{
		if (!text.Contains('#', StringComparison.Ordinal))
		{
			return text;
		}

		var output = new StringBuilder(text.Length + 8);
		var at = 0;
		while (at < text.Length)
		{
			var end = at + 1;
			while (text[at] == '#' && end < text.Length && FlowchartText.IsWordChar(text[end]))
			{
				end++;
			}

			if (text[at] == '#' && end > at + 1 && end < text.Length && text[end] == ';')
			{
				var inner = text.AsSpan(at + 1, end - at - 1);
				output.Append(inner.ContainsAnyExceptInRange('0', '9') ? "ﬂ°" : "ﬂ°°").Append(inner).Append("¶ß");
				at = end + 1;
				continue;
			}

			output.Append(text[at]);
			at++;
		}

		return output.ToString();
	}
}
