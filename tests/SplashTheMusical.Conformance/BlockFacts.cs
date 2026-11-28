using SplashTheMusical.Diagrams.Block;
using SplashTheMusical.Diagrams.Flowchart;

namespace SplashTheMusical.Conformance;

/// <summary>
/// <c>extract.mjs</c>'s block extractor over <see cref="BlockDb"/>, the port of <c>blockDB</c>: the hierarchy under
/// the root, each block with its label, shape, columns and parent, each space with its parent and width, then the
/// edges. A block whose id the db generated, which matches <c>/^id-[a-z0-9]+-\d+$/</c>, is named
/// <c>anonymous-&lt;n&gt;</c> in the order the walk meets it. A label is what the extractor's <c>decode</c> makes of
/// the db's: entity codes and HTML read as HTML.
/// </summary>
internal static class BlockFacts
{
	public static void Add(List<string> facts, BlockDocument document, string svg)
	{
		var db = BlockDb.From(document);
		var names = Names();
		var shown = new List<string>();
		void Walk(BlockDbBlock block, string parent)
		{
			foreach (var child in block.Children ?? [])
			{
				if (child.Type == "space")
				{
					facts.Add(ConfigLines.Line("space", parent, Number(child.Width ?? child.WidthInColumns ?? 1)));
				}
				else
				{
					var shape = child.Type switch
					{
						"square" or "na" => "rect",
						"round" => "rounded",
						"composite" => "composite",
						null => "",
						var type => StmFactsProvider.ShortShapeName(type),
					};
					facts.Add(ConfigLines.Line(
						"block",
						names(child.Id),
						child.Type == "composite" ? "" : Decode(child.Label),
						shape,
						Number(child.WidthInColumns ?? 1),
						parent));
					if (child.Type != "composite")
					{
						shown.Add(Decode(child.Label));
					}
				}

				Walk(child, names(child.Id));
			}
		}

		Walk(db.Root, "");
		foreach (var edge in db.Edges)
		{
			facts.Add(ConfigLines.Line("edge", edge.Start ?? "", edge.End ?? "", End(edge.ArrowTypeStart), End(edge.ArrowTypeEnd), Decode(edge.Label)));
			shown.Add(Decode(edge.Label));
		}

		ErFacts.Labels(facts, shown, svg);
	}

	/// <summary>
	/// The extractor's block boxes from the module's layout: each drawn block by its name in the facts, and each edge's
	/// path and label by its ends, in the db's order of the edges. A block whose box is empty has none. The canvas is
	/// the viewBox.
	/// </summary>
	public static string Boxes(List<string> boxes, BlockDocument document, BlockLayout layout)
	{
		var db = BlockDb.From(document);
		var names = Names();
		void Walk(BlockDbBlock block)
		{
			foreach (var child in block.Children ?? [])
			{
				names(child.Id);
				Walk(child);
			}
		}

		Walk(db.Root);
		foreach (var box in FlowchartLayout.BoxesOf(layout.Root))
		{
			if (box.Width == 0 && box.Height == 0)
			{
				continue;
			}

			var kind = box.Kind == "node" ? "block" : box.Kind;
			var name = box.Kind == "node" ? names(box.Name) : box.Name;
			boxes.Add(StmFactsProvider.Box(kind, name, box.X, box.Y, box.Width, box.Height));
		}

		var viewBox = layout.ViewBox!.Value;
		return string.Join('\t', "canvas", StmFactsProvider.Number(viewBox.X), StmFactsProvider.Number(viewBox.Y), StmFactsProvider.Number(viewBox.Width), StmFactsProvider.Number(viewBox.Height));
	}

	/// <summary>
	/// The extractor's names: a block whose id the db generated, which matches <c>/^id-[a-z0-9]+-\d+$/</c>, is
	/// <c>anonymous-&lt;n&gt;</c> in the order it is first named.
	/// </summary>
	private static Func<string?, string> Names()
	{
		var anonymous = new Dictionary<string, string>(StringComparer.Ordinal);
		return id =>
		{
			if (id is null || !IsGenerated(id))
			{
				return id ?? "";
			}

			if (!anonymous.TryGetValue(id, out var name))
			{
				anonymous[id] = name = $"anonymous-{anonymous.Count + 1}";
			}

			return name;
		};
	}

	private static bool IsGenerated(string id)
	{
		if (!id.StartsWith("id-", StringComparison.Ordinal))
		{
			return false;
		}

		var dash = id.IndexOf('-', 3);
		if (dash <= 3 || dash == id.Length - 1)
		{
			return false;
		}

		return id.AsSpan(3, dash - 3).IndexOfAnyExcept("abcdefghijklmnopqrstuvwxyz0123456789") < 0
			&& id.AsSpan(dash + 1).IndexOfAnyExceptInRange('0', '9') < 0;
	}

	/// <summary>The extractor's <c>end</c> of the db's arrow types.</summary>
	private static string End(string? type) => type switch
	{
		"arrow_point" => "arrow",
		"arrow_circle" => "circle",
		"arrow_cross" => "cross",
		null or "none" or "arrow_open" => "none",
		_ => type,
	};

	private static string Number(int value) => StmFactsProvider.Number(value);

	private static string Decode(string? text) => text is null ? "" : FlowchartLabels.HtmlTextOf(text);
}
