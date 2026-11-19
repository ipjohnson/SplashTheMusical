namespace SplashTheMusical.Diagrams.Er;

/// <summary>
/// The port of <c>erDb.ts</c>: the entities, relationships, classes and subgraphs that the actions of
/// <c>erDiagram.jison</c> give an <see cref="ErDocument"/>'s statements, run in source order.
/// </summary>
/// <remarks>
/// A subgraph's members are the values its statements give the grammar's <c>document</c>: the names of entity and
/// relationship statements and the ids of the subgraphs inside it. mermaid.js also adds the text of an
/// <c>accTitle</c> or <c>accDescr</c> and the keyword of a <c>classDef</c>, <c>class</c> or <c>style</c> inside a
/// subgraph, which the model does not keep. They name an entity only when an entity has that name.
/// </remarks>
internal sealed class ErDb
{
	private readonly Dictionary<string, ErDbEntity> _entityLookup = new(StringComparer.Ordinal);
	private readonly Dictionary<string, ErDbSubgraph> _subgraphLookup = new(StringComparer.Ordinal);
	private readonly Dictionary<string, ErDbClass> _classLookup = new(StringComparer.Ordinal);

	private ErDb() { }

	/// <summary>The entities in the order the db added them.</summary>
	public List<ErDbEntity> Entities { get; } = [];

	public List<ErDbRelationship> Relationships { get; } = [];

	/// <summary>The classes of <c>classDef</c>, in the order the db added them.</summary>
	public List<ErDbClass> Classes { get; } = [];

	/// <summary>The subgraphs in the order their <c>end</c> comes, so an inner subgraph comes before the one it is in.</summary>
	public List<ErDbSubgraph> Subgraphs { get; } = [];

	/// <summary>The direction of the last <c>direction</c> statement outside a subgraph, <c>TB</c> by default.</summary>
	public string Direction { get; private set; } = "TB";

	public static ErDb From(ErDocument document)
	{
		ArgumentNullException.ThrowIfNull(document);
		var db = new ErDb();
		db.Run(document.Statements, null);
		return db;
	}

	public ErDbEntity? Entity(string name) => _entityLookup.GetValueOrDefault(name);

	public ErDbClass? Class(string id) => _classLookup.GetValueOrDefault(id);

	public bool IsSubgraph(string id) => _subgraphLookup.ContainsKey(id);

	private void Run(List<ErStatement> statements, List<string>? members)
	{
		foreach (var statement in statements)
		{
			switch (statement)
			{
				case ErEntity entity:
					_ = AddEntity(entity.Name, entity.Alias ?? "");
					if (entity.Attributes is { } attributes)
					{
						AddEntity(entity.Name).Attributes.AddRange(attributes);
					}

					if (entity.Classes is { } classes)
					{
						SetClass(entity.Name, classes);
					}

					members?.Add(entity.Name);
					break;
				case ErRelationship relationship:
					_ = AddEntity(relationship.EntityA);
					_ = AddEntity(relationship.EntityB);
					AddRelationship(relationship);
					if (relationship.ClassesA is { } classesA)
					{
						SetClass(relationship.EntityA, classesA);
					}

					if (relationship.ClassesB is { } classesB)
					{
						SetClass(relationship.EntityB, classesB);
					}

					members?.Add(relationship.EntityA);
					members?.Add(relationship.EntityB);
					break;
				case ErClassDefinition definition:
					AddClass(definition.Names, definition.Styles);
					break;
				case ErStyle style:
					AddCssStyles(style.Ids, style.Styles);
					break;
				case ErClassAssignment assignment:
					foreach (var id in assignment.Ids)
					{
						SetClass(id, assignment.Classes);
					}

					break;
				case ErDirection direction when members is null:
					Direction = direction.Direction;
					break;
				case ErSubgraph subgraph:
					members?.Add(AddSubgraph(subgraph));
					break;
			}
		}
	}

	/// <summary><c>addEntity</c>: a new entity, or the alias for an entity that has none.</summary>
	private ErDbEntity AddEntity(string name, string alias = "")
	{
		if (_entityLookup.TryGetValue(name, out var entity))
		{
			if (entity.Alias.Length == 0 && alias.Length > 0)
			{
				entity.Alias = alias;
			}

			return entity;
		}

		entity = new ErDbEntity(name, $"entity-{name}-{Entities.Count}") { Alias = alias };
		Entities.Add(entity);
		_entityLookup[name] = entity;
		return entity;
	}

	/// <summary><c>addRelationship</c>: an end that names a subgraph by now ends at the subgraph.</summary>
	private void AddRelationship(ErRelationship relationship)
	{
		var a = _subgraphLookup.ContainsKey(relationship.EntityA) ? null : AddEntity(relationship.EntityA);
		var b = _subgraphLookup.ContainsKey(relationship.EntityB) ? null : AddEntity(relationship.EntityB);
		Relationships.Add(new ErDbRelationship(relationship, a?.Id ?? relationship.EntityA, b?.Id ?? relationship.EntityB, a is null, b is null));
	}

	/// <summary><c>setClass</c>: the classes on the entity and the subgraph of the id.</summary>
	private void SetClass(string id, List<string> classes)
	{
		if (_entityLookup.TryGetValue(id, out var entity))
		{
			foreach (var name in classes)
			{
				entity.CssClasses += " " + name;
			}
		}

		if (_subgraphLookup.TryGetValue(id, out var subgraph))
		{
			subgraph.Classes.AddRange(classes);
		}
	}

	/// <summary><c>addCssStyles</c>: the styles on the entity and the subgraph of each id.</summary>
	private void AddCssStyles(List<string> ids, List<string> styles)
	{
		foreach (var id in ids)
		{
			if (_entityLookup.TryGetValue(id, out var entity))
			{
				entity.CssStyles.AddRange(styles);
			}

			if (_subgraphLookup.TryGetValue(id, out var subgraph))
			{
				subgraph.CssStyles.AddRange(styles);
			}
		}
	}

	/// <summary><c>addClass</c>: a style with <c>color</c> in it is also a text style, with its first <c>fill</c> as <c>bgFill</c>.</summary>
	private void AddClass(List<string> ids, List<string> styles)
	{
		foreach (var id in ids)
		{
			if (!_classLookup.TryGetValue(id, out var definition))
			{
				definition = new ErDbClass(id);
				Classes.Add(definition);
				_classLookup[id] = definition;
			}

			foreach (var style in styles)
			{
				if (style.Contains("color", StringComparison.Ordinal))
				{
					var fill = style.IndexOf("fill", StringComparison.Ordinal);
					definition.TextStyles.Add(fill < 0 ? style : string.Concat(style.AsSpan(0, fill), "bgFill", style.AsSpan(fill + 4)));
				}

				definition.Styles.Add(style);
			}
		}
	}

	/// <summary>
	/// The grammar's <c>subgraphHeader document END</c> and <c>addSubGraph</c>: the members, each once by its trimmed
	/// name and less those an earlier subgraph holds, and the direction of the last <c>direction</c> inside it.
	/// </summary>
	private string AddSubgraph(ErSubgraph subgraph)
	{
		var list = new List<string>();
		Run(subgraph.Statements, list);
		var id = subgraph.Id.Trim();
		var seen = new HashSet<string>(StringComparer.Ordinal);
		var held = new HashSet<string>(StringComparer.Ordinal);
		foreach (var earlier in Subgraphs)
		{
			held.UnionWith(earlier.Nodes);
		}

		var result = new ErDbSubgraph(id, (subgraph.Title ?? subgraph.Id).Trim());
		foreach (var item in list)
		{
			var trimmed = item.Trim();
			if (trimmed.Length > 0 && seen.Add(trimmed) && !held.Contains(item))
			{
				result.Nodes.Add(item);
			}
		}

		foreach (var statement in subgraph.Statements)
		{
			if (statement is ErDirection direction)
			{
				result.Direction = direction.Direction;
			}
		}

		Subgraphs.Add(result);
		_subgraphLookup[id] = result;
		return id;
	}
}

/// <summary>An entity of <c>ErDB</c>.</summary>
internal sealed class ErDbEntity(string name, string id)
{
	/// <summary>The name the db keys it by.</summary>
	public string Name { get; } = name;

	/// <summary><c>entity-NAME-N</c>, where N counts the entities before it.</summary>
	public string Id { get; } = id;

	/// <summary>The first alias it was given, or the empty string.</summary>
	public string Alias { get; set; } = "";

	public List<ErAttribute> Attributes { get; } = [];

	/// <summary><c>default</c>, then a space and each class it was given.</summary>
	public string CssClasses { get; set; } = "default";

	public List<string> CssStyles { get; } = [];
}

/// <param name="Source">The statement.</param>
/// <param name="EntityA">The internal id of the first entity, or the id of the subgraph it ends at.</param>
/// <param name="EntityB">The internal id of the second entity, or the id of the subgraph it ends at.</param>
internal sealed record ErDbRelationship(ErRelationship Source, string EntityA, string EntityB, bool AIsSubgraph, bool BIsSubgraph);

/// <summary>A class of <c>classDef</c>.</summary>
internal sealed class ErDbClass(string id)
{
	public string Id { get; } = id;

	public List<string> Styles { get; } = [];

	public List<string> TextStyles { get; } = [];
}

/// <summary>A subgraph of <c>ErDB</c>.</summary>
internal sealed class ErDbSubgraph(string id, string title)
{
	public string Id { get; } = id;

	/// <summary>The title, trimmed, before <c>sanitizeText</c>.</summary>
	public string Title { get; } = title;

	public List<string> Nodes { get; } = [];

	public List<string> Classes { get; } = [];

	public List<string> CssStyles { get; } = [];

	/// <summary>The direction of the last <c>direction</c> inside it, or <c>null</c>.</summary>
	public string? Direction { get; set; }
}
