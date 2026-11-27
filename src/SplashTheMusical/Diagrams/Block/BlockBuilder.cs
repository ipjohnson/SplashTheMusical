namespace SplashTheMusical.Diagrams.Block;

/// <summary>
/// A short fluent way to build a <see cref="BlockDocument"/> for the common case: columns, blocks, spaces and the
/// edges between blocks. Each call adds the statement the source would have, in order. For composites, styles and
/// classes, add statements to <see cref="BlockDocument.Statements"/>.
/// </summary>
/// <example>
/// <code>
/// var document = new BlockBuilder(columns: 3)
///     .Block("a", "Frontend")
///     .Space()
///     .Block("b", "Backend", BlockShape.Cylinder)
///     .Edge("a", "b", "calls")
///     .Build();
/// </code>
/// </example>
public sealed class BlockBuilder
{
	private readonly BlockDocument _document = new();

	/// <param name="columns">The columns, or <c>null</c> for no <c>columns</c> statement, which lays the blocks out in one row.</param>
	public BlockBuilder(int? columns = null)
	{
		if (columns is { } count)
		{
			_document.Statements.Add(new BlockColumns(count));
		}
	}

	/// <summary>Adds a block, with a label in the shape's brackets when <paramref name="label"/> is not <c>null</c>.</summary>
	public BlockBuilder Block(string id, string? label = null, BlockShape shape = BlockShape.Square, int width = 1)
	{
		_document.Statements.Add(new BlockNode(id)
		{
			Label = label,
			Shape = label is null ? BlockShape.Na : shape,
			Width = width,
		});
		return this;
	}

	/// <summary>Adds <c>space</c>, or <c>space:width</c>.</summary>
	public BlockBuilder Space(int width = 1)
	{
		_document.Statements.Add(new BlockSpace(width));
		return this;
	}

	/// <summary>Adds <c>start --&gt; end</c>, or <c>start -- "label" --&gt; end</c>.</summary>
	public BlockBuilder Edge(string start, string end, string? label = null)
	{
		_document.Statements.Add(new BlockEdge(new BlockNode(start), new BlockLink { Label = label }, new BlockNode(end)));
		return this;
	}

	public BlockDocument Build() => _document;
}
