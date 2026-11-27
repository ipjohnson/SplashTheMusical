namespace SplashTheMusical.Diagrams.Requirement;

/// <summary>
/// A short fluent way to build a <see cref="RequirementDocument"/> for the common case: requirements, elements and the
/// relationships between them. Each call adds the statement the source would have, in order. For styles and classes,
/// add statements to <see cref="RequirementDocument.Statements"/>.
/// </summary>
/// <example>
/// <code>
/// var document = new RequirementBuilder()
///     .Requirement("login", RequirementKind.FunctionalRequirement, id: "R1", text: "Users sign in", risk: RequirementRisk.High)
///     .Element("auth_service", type: "service")
///     .Relationship("auth_service", RequirementRelationshipType.Satisfies, "login")
///     .Build();
/// </code>
/// </example>
public sealed class RequirementBuilder
{
	private readonly RequirementDocument _document = new();

	/// <param name="direction"><c>TB</c>, <c>BT</c>, <c>LR</c> or <c>RL</c>. A direction other than <c>TB</c> adds a <c>direction</c> statement.</param>
	public RequirementBuilder(string direction = "TB")
	{
		if (direction != "TB")
		{
			_document.Statements.Add(new RequirementDirection(direction));
		}
	}

	/// <summary>Adds <c>requirement name { … }</c>, or another kind's keyword, with the fields that are not <c>null</c>.</summary>
	public RequirementBuilder Requirement(
		string name,
		RequirementKind kind = RequirementKind.Requirement,
		string? id = null,
		string? text = null,
		RequirementRisk? risk = null,
		RequirementVerifyMethod? verifyMethod = null)
	{
		_document.Statements.Add(new RequirementDefinition(name, kind) { Id = id, Text = text, Risk = risk, VerifyMethod = verifyMethod });
		return this;
	}

	/// <summary>Adds <c>element name { type: … docRef: … }</c> with the fields that are not <c>null</c>.</summary>
	public RequirementBuilder Element(string name, string? type = null, string? docRef = null)
	{
		_document.Statements.Add(new RequirementElement(name) { Type = type, DocRef = docRef });
		return this;
	}

	/// <summary>Adds <c>source - type -&gt; target</c>.</summary>
	public RequirementBuilder Relationship(string source, RequirementRelationshipType type, string target)
	{
		_document.Statements.Add(new RequirementRelationship(source, target, type));
		return this;
	}

	public RequirementDocument Build() => _document;
}
