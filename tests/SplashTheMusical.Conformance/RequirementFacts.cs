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
	public static void Add(List<string> facts, RequirementDocument document)
	{
		var db = RequirementDb.From(document);
		foreach (var requirement in db.Requirements)
		{
			facts.Add(ConfigLines.Line(
				"requirement",
				Encode(requirement.Name),
				Lower(Kind(requirement.Kind)),
				Decode(requirement.Id),
				Decode(requirement.Text),
				Lower(requirement.Risk?.ToString() ?? ""),
				Lower(requirement.VerifyMethod?.ToString() ?? "")));
		}

		foreach (var element in db.Elements)
		{
			facts.Add(ConfigLines.Line("element", Encode(element.Name), Decode(element.Type), Decode(element.DocRef)));
		}

		foreach (var relationship in db.Relationships)
		{
			facts.Add(ConfigLines.Line("relation", Encode(relationship.Source), Encode(relationship.Target), relationship.Type.ToString().ToLowerInvariant()));
		}
	}

	/// <summary>The db's <c>RequirementType</c> value of a kind.</summary>
	private static string Kind(RequirementKind kind) => kind switch
	{
		RequirementKind.Requirement => "Requirement",
		RequirementKind.FunctionalRequirement => "Functional Requirement",
		RequirementKind.InterfaceRequirement => "Interface Requirement",
		RequirementKind.PerformanceRequirement => "Performance Requirement",
		RequirementKind.PhysicalRequirement => "Physical Requirement",
		_ => "Design Constraint",
	};

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
