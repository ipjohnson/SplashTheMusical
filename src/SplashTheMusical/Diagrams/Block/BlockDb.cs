// Upstream: packages/mermaid/src/diagrams/block/blockDB.ts
using System.Runtime.InteropServices;
using SplashTheMusical.Diagrams.State;
using SplashTheMusical.Parsing;

namespace SplashTheMusical.Diagrams.Block;

/// <summary>
/// One of the db's <c>Block</c> objects: a block, a space, a composite, an edge, or a <c>classDef</c>,
/// <c>class</c>, <c>style</c> or <c>columns</c> statement on its way into the db. <see cref="Type"/> is the db's type
/// name, such as <c>square</c> or <c>composite</c>, and is <c>null</c> where the grammar's action reads it from a
/// list.
/// </summary>
internal sealed class BlockDbBlock
{
	public string? Id { get; set; }

	public string? Label { get; set; }

	public string? Type { get; set; }

	public int? WidthInColumns { get; set; }

	/// <summary>A space's width.</summary>
	public int? Width { get; set; }

	public List<BlockDbBlock>? Children { get; set; }

	public int? Columns { get; set; }

	public List<string>? Classes { get; set; }

	public List<string>? Styles { get; set; }

	public List<string>? Directions { get; set; }

	/// <summary>The palette slot of a composite, in the order the db first meets them.</summary>
	public int? ColorIndex { get; set; }

	public string? Start { get; set; }

	public string? End { get; set; }

	public string? ArrowTypeStart { get; set; }

	public string? ArrowTypeEnd { get; set; }

	public string? Thickness { get; set; }

	public string? Pattern { get; set; }

	public string? Css { get; set; }

	public string? StyleClass { get; set; }

	public string? StylesStr { get; set; }

	/// <summary>es-toolkit's <c>clone</c>: a copy that shares the lists.</summary>
	public BlockDbBlock Clone() => (BlockDbBlock)MemberwiseClone();
}

/// <summary>A <c>classDef</c>: its styles, and the text styles of those that set a colour.</summary>
internal sealed record BlockDbClass(string Id, List<string> Styles, List<string> TextStyles);

/// <summary>
/// The port of <c>blockDB.ts</c>: the objects the grammar's actions build from a <see cref="BlockDocument"/>, run
/// through <c>setHierarchy</c> and <c>populateBlockDatabase</c>. A block goes into the hierarchy where its id first
/// appears, and a later block with the id changes its type, unless it is <c>na</c>, and its label, unless that is the
/// id. The ids the db generates for composites and spaces are <c>id-stm&lt;n&gt;-&lt;n&gt;</c>.
/// </summary>
internal sealed class BlockDb
{
	private readonly OrderedDictionary<string, BlockDbBlock> _blocks = new(StringComparer.Ordinal);
	private readonly Dictionary<string, int> _edgeCount = new(StringComparer.Ordinal);

	// The JavaScript Map's entry for the id undefined, which a chain of three nodes or more gives.
	private BlockDbBlock? _undefined;
	private int _generated;
	private int _nextColorIndex;

	private BlockDb()
	{
		Root = new BlockDbBlock { Id = "root", Type = "composite", Children = [], Columns = -1 };
		_blocks["root"] = Root;
	}

	public BlockDbBlock Root { get; }

	/// <summary><c>getEdges</c>.</summary>
	public List<BlockDbBlock> Edges { get; } = [];

	/// <summary><c>getClasses</c>.</summary>
	public Dictionary<string, BlockDbClass> Classes { get; } = new(StringComparer.Ordinal);

	/// <summary><c>getBlocks</c>: the root's children.</summary>
	public List<BlockDbBlock> Blocks => Root.Children ?? [];

	public static BlockDb From(BlockDocument document)
	{
		ArgumentNullException.ThrowIfNull(document);
		var db = new BlockDb();
		var items = db.Items(document.Statements);
		db.Root.Children = items;
		db.Populate(items, db.Root);
		return db;
	}

	/// <summary><c>getBlock</c>.</summary>
	public BlockDbBlock? Block(string? id) => id is null ? _undefined : _blocks.GetValueOrDefault(id);

	/// <summary>
	/// The first <c>style</c> statement with styles whose id no statement before it gives a block, in the order
	/// <c>populateBlockDatabase</c> reads them, or <c>null</c>. <c>addStyle2Node</c> fails on it, so mermaid.js refuses
	/// the diagram. It allocates nothing.
	/// </summary>
	public static BlockStyle? FindUndefinedStyle(ReadOnlySpan<BlockStatement> statements)
	{
		var defined = new PooledList<ReadOnlyMemory<char>>(16);
		try
		{
			defined.Add("root".AsMemory());
			return FindUndefinedStyle(statements, ref defined);
		}
		finally
		{
			defined.Dispose();
		}
	}

	private static BlockStyle? FindUndefinedStyle(ReadOnlySpan<BlockStatement> statements, ref PooledList<ReadOnlyMemory<char>> defined)
	{
		foreach (var statement in statements)
		{
			switch (statement)
			{
				case BlockNode node:
					defined.Add(node.Id.AsMemory());
					break;
				case BlockEdge edge when edge.Nodes.Count > 0:
					if (edge.Nodes.Count == 2)
					{
						defined.Add(edge.Nodes[0].Id.AsMemory());
					}

					defined.Add(edge.Nodes[^1].Id.AsMemory());
					break;
				case BlockComposite composite:
					if (composite.Header is BlockNode header)
					{
						defined.Add(header.Id.AsMemory());
					}

					if (FindUndefinedStyle(CollectionsMarshal.AsSpan(composite.Statements), ref defined) is { } found)
					{
						return found;
					}

					break;
				case BlockClassAssignment assignment:
					foreach (var id in assignment.Ids)
					{
						if (!IsDefined(defined.AsSpan(), id))
						{
							defined.Add(id.AsMemory(Trimmed(id)));
						}
					}

					break;
				case BlockStyle style when HasStyles(style.Styles) && !IsDefined(defined.AsSpan(), style.Id):
					return style;
			}
		}

		return null;
	}

	private static Range Trimmed(string text)
	{
		var trimmed = StateLexer.Trim(text);
		var start = trimmed.IsEmpty ? 0 : text.AsSpan().IndexOf(trimmed);
		return start..(start + trimmed.Length);
	}

	private static bool IsDefined(ReadOnlySpan<ReadOnlyMemory<char>> defined, string id)
	{
		foreach (var name in defined)
		{
			if (name.Span.SequenceEqual(id))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>Whether the styles joined with <c>,</c>, the rule's <c>stylesStr</c>, are not empty.</summary>
	private static bool HasStyles(List<string> styles) => styles.Count > 1 || (styles.Count == 1 && styles[0].Length > 0);

	/// <summary>The objects the grammar's actions give the statements, with each list flattened as <c>populateBlockDatabase</c> flattens it.</summary>
	private List<BlockDbBlock> Items(List<BlockStatement> statements)
	{
		var items = new List<BlockDbBlock>(statements.Count);
		foreach (var statement in statements)
		{
			switch (statement)
			{
				case BlockNode node:
					items.Add(Node(node, node.Width));
					break;
				case BlockEdge edge:
					AddEdge(edge, items);
					break;
				case BlockComposite composite:
					{
						var block = composite.Header switch
						{
							BlockNode header => Node(header, header.Width),
							BlockEdge => new BlockDbBlock(),
							_ => new BlockDbBlock { Id = GenerateId(), Label = "" },
						};
						block.Type = "composite";
						block.Children = Items(composite.Statements);
						items.Add(block);
						break;
					}

				case BlockColumns columns:
					items.Add(new BlockDbBlock { Type = "column-setting", Columns = columns.Columns ?? -1 });
					break;
				case BlockSpace space:
					items.Add(new BlockDbBlock { Id = GenerateId(), Type = "space", Label = "", Width = space.Width, Children = [] });
					break;
				case BlockClassDefinition definition:
					items.Add(new BlockDbBlock { Type = "classDef", Id = definition.Name, Css = string.Join(',', definition.Styles) });
					break;
				case BlockClassAssignment assignment:
					items.Add(new BlockDbBlock { Type = "applyClass", Id = string.Join(',', assignment.Ids), StyleClass = assignment.ClassName });
					break;
				case BlockStyle style:
					items.Add(new BlockDbBlock { Type = "applyStyles", Id = style.Id, StylesStr = string.Join(',', style.Styles) });
					break;
			}
		}

		return items;
	}

	/// <summary>
	/// <c>nodeStatement link node</c>: the first node, the edge and the last node. Of a longer chain the first is the
	/// list of the chain before the last link, whose <c>id</c>, <c>label</c> and <c>type</c> are undefined.
	/// </summary>
	private static void AddEdge(BlockEdge edge, List<BlockDbBlock> items)
	{
		if (edge.Nodes.Count < 2 || edge.Links.Count == 0)
		{
			return;
		}

		var first = edge.Nodes.Count == 2 ? Node(edge.Nodes[0], null) : new BlockDbBlock();
		var last = edge.Nodes[^1];
		var link = edge.Links[^1];
		items.Add(first);
		items.Add(new BlockDbBlock
		{
			Id = (first.Id ?? "undefined") + "-" + last.Id,
			Start = first.Id,
			End = last.Id,
			Label = link.Label ?? "",
			Type = "edge",
			Thickness = link.Stroke == BlockStroke.Thick ? "thick" : "normal",
			Pattern = link.Stroke == BlockStroke.Dotted ? "dotted" : "solid",
			Directions = last.Directions,
			ArrowTypeEnd = link.End switch
			{
				BlockArrowHead.Cross => "arrow_cross",
				BlockArrowHead.Circle => "arrow_circle",
				BlockArrowHead.Point => "arrow_point",
				_ => "",
			},
			ArrowTypeStart = link.Start switch
			{
				BlockArrowHead.Cross => "arrow_cross",
				BlockArrowHead.Circle => "arrow_circle",
				BlockArrowHead.Point => "arrow_point",
				_ => "arrow_open",
			},
		});
		items.Add(Node(last, null));
	}

	private static BlockDbBlock Node(BlockNode node, int? width) => new()
	{
		Id = node.Id,
		Label = node.Label,
		Type = TypeName(node.Shape),
		Directions = node.Directions,
		WidthInColumns = width,
	};

	/// <summary>The db's <c>typeStr2Type</c> name of a shape.</summary>
	public static string TypeName(BlockShape shape) => shape switch
	{
		BlockShape.Square => "square",
		BlockShape.Round => "round",
		BlockShape.Circle => "circle",
		BlockShape.RectLeftInvArrow => "rect_left_inv_arrow",
		BlockShape.Diamond => "diamond",
		BlockShape.Hexagon => "hexagon",
		BlockShape.Stadium => "stadium",
		BlockShape.Subroutine => "subroutine",
		BlockShape.Cylinder => "cylinder",
		BlockShape.DoubleCircle => "doublecircle",
		BlockShape.LeanRight => "lean_right",
		BlockShape.LeanLeft => "lean_left",
		BlockShape.Trapezoid => "trapezoid",
		BlockShape.InvTrapezoid => "inv_trapezoid",
		BlockShape.BlockArrow => "block_arrow",
		_ => "na",
	};

	private string GenerateId()
	{
		_generated++;
		return $"id-stm{_generated}-{_generated}";
	}

	private void Set(string? id, BlockDbBlock block)
	{
		if (id is null)
		{
			_undefined = block;
		}
		else
		{
			_blocks[id] = block;
		}
	}

	/// <summary><c>populateBlockDatabase</c>.</summary>
	private void Populate(List<BlockDbBlock> blocks, BlockDbBlock parent)
	{
		var children = new List<BlockDbBlock>();
		foreach (var block in blocks)
		{
			switch (block.Type)
			{
				case "classDef":
					AddStyleClass(block.Id!, block.Css);
					continue;
				case "applyClass":
					SetCssClass(block.Id!, block.StyleClass ?? "");
					continue;
				case "applyStyles":
					if (!string.IsNullOrEmpty(block.StylesStr) && Block(block.Id) is { } styled)
					{
						styled.Styles = [.. block.StylesStr.Split(',')];
					}

					continue;
				case "column-setting":
					parent.Columns = block.Columns ?? -1;
					continue;
				case "edge":
					{
						var count = _edgeCount.GetValueOrDefault(block.Id!) + 1;
						_edgeCount[block.Id!] = count;
						block.Id = $"{count}-{block.Id}";
						Edges.Add(block);
						continue;
					}
			}

			if (string.IsNullOrEmpty(block.Label))
			{
				block.Label = block.Type == "composite" ? "" : block.Id;
			}

			var existing = Block(block.Id);
			if (existing is null)
			{
				if (block.Type == "composite")
				{
					block.ColorIndex = _nextColorIndex++;
				}

				Set(block.Id, block);
			}
			else
			{
				if (block.Type != "na")
				{
					existing.Type = block.Type;
				}

				if (block.Label != block.Id)
				{
					existing.Label = block.Label;
				}
			}

			if (block.Children is not null)
			{
				Populate(block.Children, block);
			}

			if (block.Type == "space")
			{
				var width = block.Width ?? 1;
				for (var j = 0; j < width; j++)
				{
					var copy = block.Clone();
					copy.Id = $"{copy.Id}-{j}";
					Set(copy.Id, copy);
					children.Add(copy);
				}
			}
			else if (existing is null)
			{
				children.Add(block);
			}
		}

		parent.Children = children;
	}

	/// <summary><c>addStyleClass</c>: each style without its first <c>;</c>, and the colour styles as text styles.</summary>
	private void AddStyleClass(string id, string? styleAttributes)
	{
		if (!Classes.TryGetValue(id, out var found))
		{
			found = new BlockDbClass(id, [], []);
			Classes[id] = found;
		}

		if (styleAttributes is null)
		{
			return;
		}

		foreach (var attribute in styleAttributes.Split(','))
		{
			var semicolon = attribute.IndexOf(';', StringComparison.Ordinal);
			var fixedAttribute = StateLexer.Trim(semicolon < 0 ? attribute : attribute.Remove(semicolon, 1)).ToString();
			if (attribute.Contains("color", StringComparison.Ordinal))
			{
				found.TextStyles.Add(ReplaceFirst(ReplaceFirst(fixedAttribute, "fill", "bgFill"), "color", "fill"));
			}

			found.Styles.Add(fixedAttribute);
		}
	}

	private static string ReplaceFirst(string text, string value, string replacement)
	{
		var at = text.IndexOf(value, StringComparison.Ordinal);
		return at < 0 ? text : string.Concat(text.AsSpan(0, at), replacement, text.AsSpan(at + value.Length));
	}

	/// <summary>
	/// <c>setCssClass</c>: each id as it is, or a new <c>na</c> block under the trimmed id, which replaces a block of
	/// that id.
	/// </summary>
	private void SetCssClass(string itemIds, string className)
	{
		foreach (var id in itemIds.Split(','))
		{
			var found = Block(id);
			if (found is null)
			{
				var trimmed = StateLexer.Trim(id).ToString();
				found = new BlockDbBlock { Id = trimmed, Type = "na", Children = [] };
				_blocks[trimmed] = found;
			}

			(found.Classes ??= []).Add(className);
		}
	}
}
