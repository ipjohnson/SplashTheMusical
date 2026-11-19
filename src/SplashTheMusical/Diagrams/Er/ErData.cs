// Upstream: packages/mermaid/src/diagrams/er/erDb.ts
using SplashTheMusical.Diagrams.Flowchart;

namespace SplashTheMusical.Diagrams.Er;

/// <summary>
/// <c>ErDB.getData</c>: the subgraphs as groups, from the last to the first, then each entity whose name is not a
/// subgraph's id as an <c>erBox</c>, then each relationship as an edge from <c>entityA</c> to <c>entityB</c> with the
/// cardinality written next to each end as its marker.
/// </summary>
internal static class ErData
{
	public static (List<FlowNode> Nodes, List<FlowEdge> Edges) Get(ErDb db)
	{
		ArgumentNullException.ThrowIfNull(db);
		var parents = new Dictionary<string, string>(StringComparer.Ordinal);
		for (var i = db.Subgraphs.Count - 1; i >= 0; i--)
		{
			foreach (var id in db.Subgraphs[i].Nodes)
			{
				parents[id] = db.Subgraphs[i].Id;
			}
		}

		var nodes = new List<FlowNode>(db.Subgraphs.Count + db.Entities.Count);
		for (var i = db.Subgraphs.Count - 1; i >= 0; i--)
		{
			var subgraph = db.Subgraphs[i];
			nodes.Add(new FlowNode
			{
				Id = subgraph.Id,
				DomId = subgraph.Id,
				Label = subgraph.Title,
				LabelType = FlowchartLabelType.Markdown,
				ParentId = parents.GetValueOrDefault(subgraph.Id),
				Padding = 8,
				CssCompiledStyles = CompiledStyles(db, subgraph.Classes),
				CssStyles = subgraph.CssStyles,
				Classes = string.Join(' ', subgraph.Classes),
				Shape = "rect",
				Dir = subgraph.Direction,
				IsGroup = true,
				IsSubgraph = true,
			});
		}

		var colorIndex = 0;
		foreach (var entity in db.Entities)
		{
			if (db.IsSubgraph(entity.Name))
			{
				continue;
			}

			nodes.Add(new FlowNode
			{
				Id = entity.Id,
				DomId = entity.Id,
				Label = entity.Alias.Length > 0 ? entity.Alias : entity.Name,
				LabelType = FlowchartLabelType.Markdown,
				ParentId = parents.GetValueOrDefault(entity.Name),
				CssCompiledStyles = CompiledStyles(db, entity.CssClasses.Split(' ')),
				CssStyles = entity.CssStyles,
				Classes = entity.CssClasses,
				Shape = "erBox",
				ColorIndex = colorIndex++,
				Attributes = entity.Attributes,
			});
		}

		var edges = new List<FlowEdge>(db.Relationships.Count);
		for (var i = 0; i < db.Relationships.Count; i++)
		{
			var relationship = db.Relationships[i];
			var source = relationship.Source;
			edges.Add(new FlowEdge
			{
				Id = $"id_{relationship.EntityA}_{relationship.EntityB}_{i}",
				Index = i,
				Start = relationship.EntityA,
				End = relationship.EntityB,
				Label = source.Role,
				LabelType = FlowchartLabelType.Markdown,
				ArrowTypeStart = Arrow(source.CardinalityA),
				ArrowTypeEnd = Arrow(source.CardinalityB),
				Curve = "basis",
				Classes = "relationshipLine",
				Pattern = source.Identifying ? "solid" : "dashed",
			});
		}

		return (nodes, edges);
	}

	/// <summary>The arrow type of a cardinality: its <c>relSpec</c> name in lower case.</summary>
	public static string Arrow(ErCardinality cardinality) => cardinality switch
	{
		ErCardinality.OnlyOne => "only_one",
		ErCardinality.ZeroOrOne => "zero_or_one",
		ErCardinality.OneOrMore => "one_or_more",
		ErCardinality.ZeroOrMore => "zero_or_more",
		_ => "md_parent",
	};

	/// <summary><c>getCompiledStyles</c>: the styles, then the text styles, of each class that is defined, trimmed.</summary>
	private static List<string> CompiledStyles(ErDb db, IEnumerable<string> classes)
	{
		var styles = new List<string>();
		foreach (var name in classes)
		{
			if (db.Class(name) is { } definition)
			{
				styles.AddRange(definition.Styles.Select(s => s.Trim()));
				styles.AddRange(definition.TextStyles.Select(s => s.Trim()));
			}
		}

		return styles;
	}
}
