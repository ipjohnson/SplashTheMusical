namespace SplashTheMusical.Diagrams.Requirement;

/// <summary>
/// The port of <c>requirementDb.ts</c>: the requirements, elements, relationships and classes that the actions of
/// <c>requirementDiagram.jison</c> give a <see cref="RequirementDocument"/>'s statements, run in source order.
/// </summary>
internal sealed class RequirementDb
{
	private readonly Dictionary<string, RequirementDbNode> _requirementLookup = new(StringComparer.Ordinal);
	private readonly Dictionary<string, RequirementDbNode> _elementLookup = new(StringComparer.Ordinal);
	private readonly Dictionary<string, RequirementDbClass> _classLookup = new(StringComparer.Ordinal);

	private RequirementDb() { }

	/// <summary>The requirements in the order the db added them, the first of each name.</summary>
	public List<RequirementDbNode> Requirements { get; } = [];

	/// <summary>The elements in the order the db added them, the first of each name.</summary>
	public List<RequirementDbNode> Elements { get; } = [];

	public List<RequirementRelationship> Relationships { get; } = [];

	/// <summary>The classes of <c>classDef</c>, in the order the db added them.</summary>
	public List<RequirementDbClass> Classes { get; } = [];

	/// <summary>The direction of the last <c>direction</c> statement, <c>TB</c> by default.</summary>
	public string Direction { get; private set; } = "TB";

	public static RequirementDb From(RequirementDocument document)
	{
		ArgumentNullException.ThrowIfNull(document);
		var db = new RequirementDb();
		foreach (var statement in document.Statements)
		{
			db.Run(statement);
		}

		return db;
	}

	private void Run(RequirementStatement statement)
	{
		switch (statement)
		{
			case RequirementDefinition definition:
				Add(_requirementLookup, Requirements, new RequirementDbNode(definition.Name, true)
				{
					Kind = definition.Kind,
					Id = definition.Id ?? "",
					Text = definition.Text ?? "",
					Risk = definition.Risk,
					VerifyMethod = definition.VerifyMethod,
				});
				if (definition.Classes is { } requirementClasses)
				{
					SetClass([definition.Name], requirementClasses);
				}

				break;
			case RequirementElement element:
				Add(_elementLookup, Elements, new RequirementDbNode(element.Name, false) { Type = element.Type ?? "", DocRef = element.DocRef ?? "" });
				if (element.Classes is { } elementClasses)
				{
					SetClass([element.Name], elementClasses);
				}

				break;
			case RequirementRelationship relationship:
				Relationships.Add(relationship);
				break;
			case RequirementStyle style:
				SetCssStyle(style.Ids, style.Styles);
				break;
			case RequirementClassAssignment assignment:
				SetClass(assignment.Ids, assignment.Classes);
				break;
			case RequirementClassDefinition definition:
				DefineClass(definition.Names, definition.Styles);
				break;
			case RequirementDirection direction:
				Direction = direction.Direction;
				break;
		}
	}

	/// <summary><c>addRequirement</c> and <c>addElement</c>: a name the db has already keeps its first definition.</summary>
	private static void Add(Dictionary<string, RequirementDbNode> lookup, List<RequirementDbNode> nodes, RequirementDbNode node)
	{
		if (lookup.TryAdd(node.Name, node))
		{
			nodes.Add(node);
		}
	}

	/// <summary><c>requirements.get(id) ?? elements.get(id)</c>.</summary>
	private RequirementDbNode? Node(string id) =>
		_requirementLookup.GetValueOrDefault(id) ?? _elementLookup.GetValueOrDefault(id);

	/// <summary><c>setCssStyle</c>, which stops at the first id that names nothing.</summary>
	private void SetCssStyle(List<string> ids, List<string> styles)
	{
		foreach (var id in ids)
		{
			if (Node(id) is not { } node)
			{
				return;
			}

			foreach (var style in styles)
			{
				node.CssStyles.AddRange(style.Split(','));
			}
		}
	}

	private void SetClass(List<string> ids, List<string> classNames)
	{
		foreach (var id in ids)
		{
			if (Node(id) is not { } node)
			{
				continue;
			}

			foreach (var name in classNames)
			{
				node.Classes.Add(name);
				if (_classLookup.GetValueOrDefault(name) is { Styles.Count: > 0 } styleClass)
				{
					node.CssStyles.AddRange(styleClass.Styles);
				}
			}
		}
	}

	/// <summary>
	/// <c>defineClass</c>: each class keeps its styles, and its text styles are those with <c>color</c>, with the first
	/// <c>fill</c> as <c>bgFill</c>. The requirements and elements that have the class by then get its styles.
	/// </summary>
	private void DefineClass(List<string> ids, List<string> styles)
	{
		foreach (var id in ids)
		{
			if (!_classLookup.TryGetValue(id, out var styleClass))
			{
				styleClass = new RequirementDbClass(id);
				_classLookup.Add(id, styleClass);
				Classes.Add(styleClass);
			}

			foreach (var style in styles)
			{
				if (style.Contains("color", StringComparison.Ordinal))
				{
					var fill = style.IndexOf("fill", StringComparison.Ordinal);
					styleClass.TextStyles.Add(fill < 0 ? style : string.Concat(style.AsSpan(0, fill), "bgFill", style.AsSpan(fill + 4)));
				}

				styleClass.Styles.Add(style);
			}

			foreach (var node in Requirements.Concat(Elements))
			{
				if (node.Classes.Contains(id, StringComparer.Ordinal))
				{
					foreach (var style in styles)
					{
						node.CssStyles.AddRange(style.Split(','));
					}
				}
			}
		}
	}
}

/// <summary>A requirement or an element of the db, with the styles and classes the statements gave it.</summary>
internal sealed class RequirementDbNode(string name, bool isRequirement)
{
	public string Name { get; } = name;

	/// <summary>Whether it is a requirement rather than an element.</summary>
	public bool IsRequirement { get; } = isRequirement;

	public RequirementKind Kind { get; init; }

	/// <summary>The requirement's <c>requirementId</c>, empty when the body has none.</summary>
	public string Id { get; init; } = "";

	public string Text { get; init; } = "";

	public RequirementRisk? Risk { get; init; }

	public RequirementVerifyMethod? VerifyMethod { get; init; }

	/// <summary>The element's <c>type</c>, empty when the body has none.</summary>
	public string Type { get; init; } = "";

	public string DocRef { get; init; } = "";

	public List<string> CssStyles { get; } = [];

	/// <summary>The classes, starting with <c>default</c>.</summary>
	public List<string> Classes { get; } = ["default"];
}

/// <summary>A class of <c>classDef</c>.</summary>
internal sealed class RequirementDbClass(string id)
{
	public string Id { get; } = id;

	public List<string> Styles { get; } = [];

	public List<string> TextStyles { get; } = [];
}
