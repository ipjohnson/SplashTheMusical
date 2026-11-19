namespace SplashTheMusical.Diagrams.Er;

/// <summary>
/// A short fluent way to build an <see cref="ErDocument"/> for the common case: entities with attributes, relationships
/// and subgraphs. Each call adds the statement the source would have, in order. For the rest of the model, add
/// statements to <see cref="ErDocument.Statements"/>.
/// </summary>
/// <example>
/// <code>
/// var document = new ErBuilder()
///     .Entity("CUSTOMER", e => e.Attribute("string", "name").Attribute("int", "id", "PK"))
///     .Relationship("CUSTOMER", ErCardinality.OnlyOne, "ORDER", ErCardinality.ZeroOrMore, "places")
///     .Build();
/// </code>
/// </example>
public sealed class ErBuilder
{
	private readonly List<ErStatement> _statements;
	private readonly ErDocument? _document;

	/// <param name="direction"><c>TB</c>, <c>BT</c>, <c>LR</c> or <c>RL</c>. A direction other than <c>TB</c> adds a <c>direction</c> statement.</param>
	public ErBuilder(string direction = "TB")
	{
		_document = new ErDocument();
		_statements = _document.Statements;
		if (direction != "TB")
		{
			_ = Direction(direction);
		}
	}

	private ErBuilder(List<ErStatement> statements) => _statements = statements;

	/// <summary>Adds <c>name</c>, or <c>name[alias]</c>, with the attributes <paramref name="attributes"/> adds in braces.</summary>
	public ErBuilder Entity(string name, Action<ErAttributes>? attributes = null, string? alias = null)
	{
		var entity = new ErEntity(name) { Alias = alias };
		if (attributes is not null)
		{
			var list = new ErAttributes();
			attributes(list);
			entity.Attributes = list.Items;
		}

		_statements.Add(entity);
		return this;
	}

	/// <summary>Adds <c>a ||--o{ b : role</c>: the cardinality at each entity, and a dashed line when not identifying.</summary>
	public ErBuilder Relationship(string entityA, ErCardinality cardinalityA, string entityB, ErCardinality cardinalityB, string role, bool identifying = true)
	{
		_statements.Add(new ErRelationship(entityA, entityB, role)
		{
			CardinalityA = cardinalityA,
			CardinalityB = cardinalityB,
			Identifying = identifying,
		});
		return this;
	}

	/// <summary>Adds <c>subgraph id [title]</c>, whose statements <paramref name="body"/> adds, and <c>end</c>.</summary>
	public ErBuilder Subgraph(string id, string? title, Action<ErBuilder> body)
	{
		ArgumentNullException.ThrowIfNull(body);
		var subgraph = new ErSubgraph(id) { Title = title };
		body(new ErBuilder(subgraph.Statements));
		_statements.Add(subgraph);
		return this;
	}

	/// <summary>Adds <c>classDef name styles</c>.</summary>
	public ErBuilder ClassDef(string name, params string[] styles)
	{
		ArgumentNullException.ThrowIfNull(styles);
		_statements.Add(new ErClassDefinition([name], [.. styles]));
		return this;
	}

	/// <summary>Adds <c>class ids className</c>.</summary>
	public ErBuilder Class(string className, params string[] ids)
	{
		ArgumentNullException.ThrowIfNull(ids);
		_statements.Add(new ErClassAssignment([.. ids], [className]));
		return this;
	}

	/// <summary>Adds <c>direction TB</c>, <c>BT</c>, <c>LR</c> or <c>RL</c>.</summary>
	public ErBuilder Direction(string direction)
	{
		_statements.Add(new ErDirection(direction));
		return this;
	}

	/// <summary>The document, which later calls keep adding to.</summary>
	public ErDocument Build() => _document ?? throw new InvalidOperationException("A subgraph's builder has no document of its own.");
}

/// <summary>The attributes of an entity, which <see cref="ErBuilder.Entity"/> adds in order.</summary>
public sealed class ErAttributes
{
	internal List<ErAttribute> Items { get; } = [];

	/// <summary>Adds <c>type name keys "comment"</c>, with keys such as <c>PK</c>, <c>FK</c> and <c>UK</c>.</summary>
	public ErAttributes Attribute(string type, string name, string? keys = null, string? comment = null)
	{
		var attribute = new ErAttribute(type, name) { Comment = comment };
		if (keys is not null)
		{
			foreach (var key in keys.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
			{
				attribute.Keys.Add(key);
			}
		}

		Items.Add(attribute);
		return this;
	}
}
