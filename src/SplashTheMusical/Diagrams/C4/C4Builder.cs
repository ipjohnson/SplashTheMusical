namespace SplashTheMusical.Diagrams.C4;

/// <summary>
/// A short fluent way to build a <see cref="C4Document"/> for the common case: elements, boundaries and the relations
/// between them, with positional arguments. Each call adds the statement the source would have, in order. For named
/// arguments and the update statements, add statements to <see cref="C4Document.Statements"/>.
/// </summary>
/// <example>
/// <code>
/// var document = new C4Builder(C4DiagramKind.Context, title: "Banking")
///     .Element(C4ElementKind.Person, "customer", "Customer", "A customer of the bank")
///     .Boundary(C4BoundaryKind.EnterpriseBoundary, "bank", "Bank", b => b
///         .Element(C4ElementKind.System, "banking", "Internet Banking System"))
///     .Relation(C4RelationKind.Rel, "customer", "banking", "Uses", "HTTPS")
///     .Build();
/// </code>
/// </example>
public sealed class C4Builder
{
	private readonly C4Document _document;
	private readonly List<C4Statement> _statements;

	public C4Builder(C4DiagramKind kind = C4DiagramKind.Context, string? title = null)
	{
		_document = new C4Document { Kind = kind, DiagramTitle = title };
		_statements = _document.Statements;
	}

	private C4Builder(List<C4Statement> statements)
	{
		_document = new C4Document();
		_statements = statements;
	}

	/// <summary>Adds an element macro, such as <c>Person(alias, "label", …)</c>, with the arguments that follow the alias.</summary>
	/// <param name="arguments">The label, then the macro's other positional arguments, such as the description.</param>
	public C4Builder Element(C4ElementKind kind, string alias, params string[] arguments)
	{
		_statements.Add(new C4Element(kind, Arguments(alias, arguments)));
		return this;
	}

	/// <summary>Adds a boundary or a deployment node, and the statements <paramref name="inside"/> adds within its braces.</summary>
	public C4Builder Boundary(C4BoundaryKind kind, string alias, string label, Action<C4Builder> inside, params string[] arguments)
	{
		ArgumentNullException.ThrowIfNull(inside);
		var boundary = new C4Boundary(kind, Arguments(alias, [label, .. arguments]));
		inside(new C4Builder(boundary.Statements));
		_statements.Add(boundary);
		return this;
	}

	/// <summary>Adds a relation macro, such as <c>Rel(from, to, "label", …)</c>.</summary>
	/// <param name="arguments">The macro's positional arguments after the label, such as the technology.</param>
	public C4Builder Relation(C4RelationKind kind, string from, string to, string label, params string[] arguments)
	{
		_statements.Add(new C4Relation(kind, Arguments(from, [to, label, .. arguments])));
		return this;
	}

	public C4Document Build() => _document;

	private static List<C4Argument> Arguments(string first, string[] rest)
	{
		ArgumentNullException.ThrowIfNull(rest);
		var arguments = new List<C4Argument>(rest.Length + 1) { new(first) };
		foreach (var value in rest)
		{
			arguments.Add(new C4Argument(value));
		}

		return arguments;
	}
}
