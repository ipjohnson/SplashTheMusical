// Upstream: packages/mermaid/src/diagrams/block/layout.ts
// Upstream: packages/mermaid/src/diagrams/block/renderHelpers.ts
// Upstream: packages/mermaid/src/diagrams/block/blockRenderer.ts
using SplashTheMusical.Config;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Layout.Paths;
using SplashTheMusical.Rendering;
using SplashTheMusical.Rendering.Shapes;

namespace SplashTheMusical.Diagrams.Block;

/// <summary>
/// A laid out block diagram: the size of the root <c>svg</c>, its viewBox, and the blocks and edges
/// <c>blockRenderer.ts</c> draws in its <c>g.block</c>.
/// </summary>
public sealed class BlockLayout : DiagramLayout
{
	internal BlockLayout(
		double width,
		double? height,
		SvgViewBox viewBox,
		FlowchartDrawing root,
		FlowchartStyle style,
		IReadOnlyList<CssRule> diagramRules,
		IReadOnlyList<StyleClass> classes)
		: base(width, height)
	{
		ViewBox = viewBox;
		Root = root;
		Style = style;
		DiagramRules = diagramRules;
		Classes = classes;
		ElementRules = FlowchartStyles.ElementRules(root);
	}

	internal override IReadOnlyList<CssRule> DiagramRules { get; }

	internal override IReadOnlyList<StyleClass> Classes { get; }

	internal override IReadOnlyList<CssRule> ElementRules { get; }

	/// <summary>The blocks in the order <c>insertBlocks</c> draws them, then the edges in the db's order.</summary>
	internal FlowchartDrawing Root { get; }

	internal FlowchartStyle Style { get; }
}

/// <summary>A block's <c>size</c>: its width and height, and the centre the grid gives it.</summary>
internal sealed class BlockSize(double width, double height)
{
	public double Width { get; set; } = width;

	public double Height { get; set; } = height;

	public double X { get; set; }

	public double Y { get; set; }
}

/// <summary>
/// <c>blockRenderer.ts</c>'s <c>draw</c>. <c>calculateBlockSizes</c> draws each block at its label's size,
/// <c>layout</c> sizes the blocks to the grid of their parent's columns and places them, <c>insertBlocks</c> draws
/// each block again at its place and size, and <c>insertEdges</c> draws each edge from the centre of its start to the
/// centre of its end through the point between, cut where it leaves the two shapes, with its label at that middle
/// point. The viewBox is the box of the drawing, 5 wider on each side.
/// </summary>
/// <remarks>
/// <c>layout</c> also returns the box of the placed blocks, which <c>draw</c> reads only when the drawing's
/// <c>getBBox</c> is not finite. That does not happen, so STM does not compute it.
/// </remarks>
internal static class BlockLayoutStep
{
	/// <summary>The detector id, which the marker ids carry.</summary>
	public const string DiagramType = "block";

	private const double ViewPadding = 5;

	/// <summary>The classes <c>insertEdges</c> gives every edge after its stroke classes.</summary>
	private const string EdgeClasses = "flowchart-link LS-a1 LE-b1";

	public static BlockLayout Run(BlockDocument document, DiagramRenderContext context)
	{
		var config = context.Config;
		var db = BlockDb.From(document);
		var padding = config.Block.Padding ?? 8;
		var nodePadding = config.Block.Padding ?? 0;

		// getNodeFromBlock gives a block no minimum width and wraps its label at its width, or not at all before it has one.
		var measure = new FlowchartMeasure(config, context.SvgId, DiagramType, context.TextMeasurer, nodeLabels: (double.PositiveInfinity, 0));
		var blocks = PreOrder(db.Blocks);

		foreach (var block in blocks)
		{
			if (block.Type == "group")
			{
				continue;
			}

			var node = Node(block, db, nodePadding);
			Draw(measure, node, block, null, positioned: false);
			if (db.Block(block.Id) is { } registered)
			{
				registered.Size = new BlockSize(node.Width, node.Height);
			}
		}

		SetBlockSizes(db.Root, 0, 0, padding);
		LayoutBlocks(db.Root, padding);

		var drawing = new FlowchartDrawing();
		var drawn = new Dictionary<BlockDbBlock, FlowNode>(ReferenceEqualityComparer.Instance);
		foreach (var block in blocks)
		{
			if (db.Block(block.Id) is not { Type: not "space" })
			{
				continue;
			}

			var node = Node(block, db, nodePadding);
			Draw(measure, node, block, block.Size, positioned: true);
			node.X = block.Size?.X ?? 0;
			node.Y = block.Size?.Y ?? 0;
			drawn[block] = node;
			var own = block.Type is "composite" or "block_arrow";
			var (nodeStyles, labelStyles) = FlowchartStyles.NodeStyles(own ? [] : node.CssCompiledStyles, node.CssStyles);
			drawing.Nodes.Add(new FlowchartNodeDrawing
			{
				Id = node.Id,
				DomId = node.DomId,
				Classes = block.Type == "composite" ? $"node {node.Classes} {node.Classes}" : "node " + node.Classes,
				X = node.X,
				Y = node.Y,
				Geometry = node.Geometry,
				Label = node.LabelBox,
				NodeStyles = nodeStyles,
				LabelStyles = labelStyles,
				ColorId = measure.ColorSlots > 0 && node.ColorIndex is { } index
					? "color-" + (index % measure.ColorSlots).ToString(System.Globalization.CultureInfo.InvariantCulture)
					: null,
			});
		}

		var flowchart = config.Flowchart;
		var dagre = new FlowchartDagre(measure, DiagramType, flowchart.SubGraphTitleMargin.Top ?? 0, flowchart.SubGraphTitleMargin.Bottom ?? 0);
		for (var i = 0; i < db.Edges.Count; i++)
		{
			var edge = db.Edges[i];
			if (string.IsNullOrEmpty(edge.Start) || string.IsNullOrEmpty(edge.End)
				|| db.Block(edge.Start) is not { Size: { } start } startBlock
				|| db.Block(edge.End) is not { Size: { } end } endBlock)
			{
				continue;
			}

			var middle = new PathPoint(start.X + ((end.X - start.X) / 2), start.Y + ((end.Y - start.Y) / 2));
			var flowEdge = new FlowEdge
			{
				Id = context.SvgId + "-" + edge.Id,
				Index = i,
				Start = edge.Start,
				End = edge.End,
				Label = edge.Label ?? "",
				LabelType = FlowchartLabelType.String,
				ArrowTypeStart = edge.ArrowTypeStart ?? "",
				ArrowTypeEnd = edge.ArrowTypeEnd ?? "",
				Stroke = edge.Thickness == "thick" ? FlowchartStroke.Thick : FlowchartStroke.Normal,
				Pattern = edge.Pattern,
				Curve = flowchart.Curve,
				Classes = EdgeClasses,
				Points = [new PathPoint(start.X, start.Y), middle, new PathPoint(end.X, end.Y)],
				X = middle.X,
				Y = middle.Y,
			};
			if (flowEdge.Label.Length > 0)
			{
				measure.Edge(flowEdge);
			}

			drawing.Edges.Add(dagre.PlacedEdge(flowEdge, drawn.GetValueOrDefault(startBlock), drawn.GetValueOrDefault(endBlock)));
		}

		var bounds = drawing.Bounds();
		if (bounds.IsEmpty)
		{
			bounds = new ShapeBounds(0, 0, 0, 0);
		}

		// The height the root svg gets when useMaxWidth is off, with a "magic factor" for wide diagrams. An empty diagram
		// has no aspect, and gets no height.
		var magicFactor = Math.Max(1, Math.Floor((0.125 * (bounds.Width / bounds.Height)) + 0.5));
		double? height = double.IsFinite(magicFactor) && config.Block.UseMaxWidth != true ? bounds.Height + magicFactor + 10 : null;
		var viewBox = new SvgViewBox(bounds.MinX - ViewPadding, bounds.MinY - ViewPadding, bounds.Width + (ViewPadding * 2), bounds.Height + (ViewPadding * 2));
		var style = new FlowchartStyle(
			DiagramType,
			measure.Look,
			Theme(config, "mainBkg"),
			Theme(config, "nodeBorder"),
			Theme(config, "clusterBkg"),
			Theme(config, "clusterBorder"),
			Theme(config, "lineColor"),
			Theme(config, "nodeTextColor") ?? Theme(config, "textColor"),
			measure.Font.Size)
		{
			Theme = config.Theme,
			Gradient = config.ThemeVariables is { Kind: MermaidValueKind.Mapping } variables && variables["useGradient"] is { } useGradient
				&& Parsing.PreambleParser.IsTruthy(useGradient)
				? (Theme(config, "gradientStart"), Theme(config, "gradientStop"))
				: null,
		};
		return new BlockLayout(bounds.Width + 10, height, viewBox, drawing, style, BlockStyles.Rules(config), BlockStyles.Classes(db));
	}

	/// <summary><c>performOperations</c>' order: each block, then its children.</summary>
	private static List<BlockDbBlock> PreOrder(List<BlockDbBlock> blocks)
	{
		var order = new List<BlockDbBlock>();
		void Walk(List<BlockDbBlock> list)
		{
			foreach (var block in list)
			{
				order.Add(block);
				if (block.Children is { } children)
				{
					Walk(children);
				}
			}
		}

		Walk(blocks);
		return order;
	}

	/// <summary>
	/// <c>getNodeFromBlock</c>: the block's classes, or <c>default</c>, with <c>flowchart-label</c>, the styles of its
	/// classes, and the shape of its type.
	/// </summary>
	private static FlowNode Node(BlockDbBlock block, BlockDb db, double padding)
	{
		var classes = (block.Classes is { Count: > 0 } names ? string.Join(' ', names) : "default") + " flowchart-label";
		var compiled = new List<string>();
		foreach (var name in block.Classes ?? [])
		{
			if (db.Classes.TryGetValue(name, out var definition))
			{
				compiled.AddRange(definition.Styles);
			}
		}

		var id = block.Id ?? "undefined";
		return new FlowNode
		{
			Id = id,
			DomId = id,
			Label = block.Label ?? "",
			LabelType = FlowchartLabelType.String,
			Shape = ShapeName(block.Type),
			Padding = block.Type == "composite" ? 0 : padding,
			Classes = classes,
			CssStyles = block.Styles ?? [],
			CssCompiledStyles = compiled,
			ColorIndex = block.ColorIndex,
		};
	}

	/// <summary>
	/// The handler <c>getNodeFromBlock</c>'s shape draws for a block type. <c>round</c> is the rect with a radius of 5,
	/// which <c>insertNode</c> draws as <c>roundedRect</c>, and a type without its own shape, such as <c>na</c> or
	/// <c>space</c>, is the plain rect.
	/// </summary>
	internal static string ShapeName(string? type) => type switch
	{
		"round" => "roundedRect",
		"composite" => "composite",
		"diamond" => "question",
		"hexagon" => "hexagon",
		"block_arrow" => "block_arrow",
		"odd" or "rect_left_inv_arrow" => "rect_left_inv_arrow",
		"lean_right" => "lean_right",
		"lean_left" => "lean_left",
		"trapezoid" => "trapezoid",
		"inv_trapezoid" => "inv_trapezoid",
		"circle" => "circle",
		"stadium" => "stadium",
		"subroutine" => "subroutine",
		"cylinder" => "cylinder",
		"doublecircle" => "doublecircle",
		_ => "squareRect",
	};

	/// <summary>
	/// <c>insertNode</c> of the block's node, at the block's size when <paramref name="positioned"/>. A size of 0 is
	/// none, as <c>bounds.width || undefined</c> reads it.
	/// </summary>
	private static void Draw(FlowchartMeasure measure, FlowNode node, BlockDbBlock block, BlockSize? size, bool positioned)
	{
		var width = size is { Width: var w } && w != 0 && !double.IsNaN(w) ? w : (double?)null;
		var height = size is { Height: var h } && h != 0 && !double.IsNaN(h) ? h : (double?)null;
		Func<ShapeNode, ShapeGeometry>? draw = node.Shape switch
		{
			"composite" => shape => BlockShapes.Composite(shape, positioned),
			"block_arrow" => shape => BlockShapes.BlockArrow(shape, block.Directions ?? [], positioned, block.WidthInColumns ?? 1),
			_ => null,
		};
		measure.Node(node, "TB", width, height, draw);
	}

	/// <summary>
	/// <c>setBlockSizes</c>: each child as wide as the widest child per column it spans, and as tall as the tallest, and
	/// the block as wide as its columns of them with the padding around each. A block narrower than the size its parent
	/// gives it takes that size and shares it out among its children, and one narrower than its own width grows its
	/// children to fill it.
	/// </summary>
	internal static void SetBlockSizes(BlockDbBlock block, double siblingWidth, double siblingHeight, double padding)
	{
		if (!Truthy(block.Size?.Width))
		{
			block.Size = new BlockSize(siblingWidth, siblingHeight);
		}

		if (block.Children is not { Count: > 0 } children)
		{
			return;
		}

		foreach (var child in children)
		{
			SetBlockSizes(child, 0, 0, padding);
		}

		var (maxWidth, maxHeight) = MaxChildSize(children);
		foreach (var child in children)
		{
			if (child.Size is { } size)
			{
				var span = child.WidthInColumns ?? 1;
				size.Width = (maxWidth * span) + (padding * (span - 1));
				size.Height = maxHeight;
				size.X = 0;
				size.Y = 0;
			}
		}

		foreach (var child in children)
		{
			SetBlockSizes(child, maxWidth, maxHeight, padding);
		}

		var columns = block.Columns ?? -1;
		long items = 0;
		foreach (var child in children)
		{
			items += child.WidthInColumns ?? 1;
		}

		long xSize = children.Count;
		if (columns > 0 && columns < items)
		{
			xSize = columns;
		}

		var ySize = Math.Ceiling(items / (double)xSize);
		var width = (xSize * (maxWidth + padding)) + padding;
		var height = (ySize * (maxHeight + padding)) + padding;
		if (width < siblingWidth)
		{
			width = siblingWidth;
			height = siblingHeight;
			var childWidth = (siblingWidth - (xSize * padding) - padding) / xSize;
			var childHeight = (siblingHeight - (ySize * padding) - padding) / ySize;
			foreach (var child in children)
			{
				if (child.Size is { } size)
				{
					size.Width = childWidth;
					size.Height = childHeight;
					size.X = 0;
					size.Y = 0;
				}
			}
		}

		var own = OrZero(block.Size?.Width);
		if (width < own)
		{
			width = own;
			var count = columns > 0 ? Math.Min(children.Count, columns) : children.Count;
			if (count > 0)
			{
				var childWidth = (width - (count * padding) - padding) / count;
				foreach (var child in children)
				{
					if (child.Size is { } size)
					{
						size.Width = childWidth;
					}
				}
			}
		}

		block.Size = new BlockSize(width, height);
	}

	/// <summary><c>getMaxChildSize</c>: the widest width per column and the tallest height of the children that are not spaces.</summary>
	private static (double Width, double Height) MaxChildSize(List<BlockDbBlock> children)
	{
		var maxWidth = 0.0;
		var maxHeight = 0.0;
		foreach (var child in children)
		{
			if (child.Type == "space")
			{
				continue;
			}

			var width = child.Size?.Width ?? 0;
			var height = child.Size?.Height ?? 0;
			var normalized = width / (child.WidthInColumns ?? 1);
			if (normalized > maxWidth)
			{
				maxWidth = normalized;
			}

			if (height > maxHeight)
			{
				maxHeight = height;
			}
		}

		return (maxWidth, maxHeight);
	}

	/// <summary>
	/// <c>layoutBlocks</c>: the children left to right from the parent's left edge, a new row at each multiple of the
	/// columns, each row as tall as its tallest child, each child centred in its row. A span that would run past the
	/// end of a row fills only the rest of it. A parent centred on x 0 starts its rows at <c>-padding</c>, because
	/// mermaid.js reads its x of 0 as no x.
	/// </summary>
	internal static void LayoutBlocks(BlockDbBlock block, double padding)
	{
		var columns = block.Columns ?? -1;
		if (block.Children is not { Count: > 0 } children)
		{
			return;
		}

		var rowHeights = new Dictionary<long, double>();
		long position = 0;
		foreach (var child in children)
		{
			if (child.Size is not { } size)
			{
				continue;
			}

			var (_, row) = CalculateBlockPosition(columns, position);
			if (size.Height > rowHeights.GetValueOrDefault(row))
			{
				rowHeights[row] = size.Height;
			}

			position += Filled(child, columns, position);
		}

		var rowOffsets = new Dictionary<long, double>();
		var offset = 0.0;
		foreach (var row in rowHeights.Keys.Order())
		{
			rowOffsets[row] = offset;
			offset += rowHeights[row] + padding;
		}

		position = 0;
		var startX = StartX(block, padding);
		long rowAt = 0;
		foreach (var child in children)
		{
			if (child.Size is not { } size)
			{
				continue;
			}

			var (_, row) = CalculateBlockPosition(columns, position);
			if (row != rowAt)
			{
				rowAt = row;
				startX = StartX(block, padding);
			}

			if (block.Size is { } parent)
			{
				var halfWidth = size.Width / 2;
				size.X = startX + padding + halfWidth;
				startX = size.X + halfWidth;
				var rowHeight = rowHeights.TryGetValue(row, out var tallest) ? tallest : size.Height;
				size.Y = parent.Y - (parent.Height / 2) + rowOffsets.GetValueOrDefault(row) + (rowHeight / 2) + padding;
			}

			if (child.Children is not null)
			{
				LayoutBlocks(child, padding);
			}

			position += Filled(child, columns, position);
		}
	}

	/// <summary>The columns a child takes, no more than the rest of its row.</summary>
	private static long Filled(BlockDbBlock child, int columns, long position)
	{
		long filled = child.WidthInColumns ?? 1;
		return columns > 0 ? Math.Min(filled, columns - (position % columns)) : filled;
	}

	private static double StartX(BlockDbBlock block, double padding) =>
		block.Size is { } size && Truthy(size.X) ? size.X + OrZero(-size.Width / 2) : -padding;

	/// <summary>
	/// <c>calculateBlockPosition</c>: the column and row of a position, all in one row without columns and all in one
	/// column with one. It fails on <c>columns 0</c>, and mermaid.js refuses the diagram.
	/// </summary>
	internal static (long Column, long Row) CalculateBlockPosition(int columns, long position)
	{
		if (columns == 0)
		{
			throw new MermaidParseException(new MermaidDiagnostic(DiagnosticSeverity.Error, "Columns must be an integer !== 0.", 0, 0));
		}

		if (columns < 0)
		{
			return (position, 0);
		}

		if (columns == 1)
		{
			return (0, position);
		}

		return (position % columns, position / columns);
	}

	private static bool Truthy(double? value) => value is { } number && number != 0 && !double.IsNaN(number);

	/// <summary><c>value || 0</c>.</summary>
	private static double OrZero(double? value) => Truthy(value) ? value!.Value : 0;

	private static string? Theme(MermaidConfig config, string name) =>
		config.ThemeVariables is { Kind: MermaidValueKind.Mapping } variables
		&& variables.Entries.TryGetValue(name, out var value) && value.Kind == MermaidValueKind.String
		&& SvgValues.IsAllowedPaint(value.String)
			? value.String
			: null;
}
