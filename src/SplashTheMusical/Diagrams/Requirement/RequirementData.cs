// Upstream: packages/mermaid/src/diagrams/requirement/requirementDb.ts
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Text;

namespace SplashTheMusical.Diagrams.Requirement;

/// <summary>
/// <c>RequirementDB.getData</c>: each requirement, then each element, as a <c>requirementBox</c> with its index as its
/// colour, and each relationship as an edge from its source to its target, labelled with its type. A <c>contains</c>
/// relationship is a solid line with the crossed circle at its start, and every other one is dashed with an arrow at
/// its end.
/// </summary>
internal static class RequirementData
{
	public static (List<FlowNode> Nodes, List<FlowEdge> Edges) Get(RequirementDb db, string? curve, FontSpec configFont)
	{
		ArgumentNullException.ThrowIfNull(db);
		var nodes = new List<FlowNode>(db.Requirements.Count + db.Elements.Count);
		foreach (var requirement in db.Requirements)
		{
			nodes.Add(Node(requirement, nodes.Count, new RequirementBoxContent(
				$"&lt;&lt;{KindName(requirement.Kind)}&gt;&gt;",
				requirement.Name,
				[
					Line("ID: ", requirement.Id),
					Line("Text: ", requirement.Text),
					Line("Risk: ", requirement.Risk?.ToString() ?? ""),
					Line("Verification: ", requirement.VerifyMethod?.ToString() ?? ""),
				],
				configFont)));
		}

		foreach (var element in db.Elements)
		{
			nodes.Add(Node(element, nodes.Count, new RequirementBoxContent(
				"&lt;&lt;Element&gt;&gt;",
				element.Name,
				[Line("Type: ", element.Type), Line("Doc Ref: ", element.DocRef)],
				configFont)));
		}

		var edges = new List<FlowEdge>(db.Relationships.Count);
		for (var i = 0; i < db.Relationships.Count; i++)
		{
			var relationship = db.Relationships[i];
			var contains = relationship.Type == RequirementRelationshipType.Contains;
			var type = relationship.Type.ToString().ToLowerInvariant();
			edges.Add(new FlowEdge
			{
				Id = $"{relationship.Source}-{relationship.Target}-{i}",
				Index = i,
				Start = relationship.Source,
				End = relationship.Target,
				Label = $"&lt;&lt;{type}&gt;&gt;",
				LabelType = FlowchartLabelType.Markdown,
				Classes = "relationshipLine",
				Styles = ["fill:none", contains ? "" : "stroke-dasharray: 10,7"],
				Pattern = contains ? "normal" : "dashed",
				ArrowTypeStart = contains ? "requirement_contains" : "",
				ArrowTypeEnd = contains ? "" : "requirement_arrow",
				Curve = curve,
			});
		}

		return (nodes, edges);
	}

	/// <summary>The db's <c>RequirementType</c> value of a kind.</summary>
	public static string KindName(RequirementKind kind) => kind switch
	{
		RequirementKind.Requirement => "Requirement",
		RequirementKind.FunctionalRequirement => "Functional Requirement",
		RequirementKind.InterfaceRequirement => "Interface Requirement",
		RequirementKind.PerformanceRequirement => "Performance Requirement",
		RequirementKind.PhysicalRequirement => "Physical Requirement",
		_ => "Design Constraint",
	};

	private static FlowNode Node(RequirementDbNode node, int index, RequirementBoxContent content) => new()
	{
		Id = node.Name,
		DomId = node.Name,
		Shape = "requirementBox",
		CssStyles = node.CssStyles,
		Classes = string.Join(' ', node.Classes),
		ColorIndex = index,
		RequirementBox = content,
	};

	/// <summary>A body line: the caption and the value, or nothing when the value is empty.</summary>
	private static string Line(string caption, string value) => value.Length > 0 ? caption + value : "";
}
