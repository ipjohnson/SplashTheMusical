using CsCheck;
using SplashTheMusical.Diagrams.Class;

namespace SplashTheMusical.Tests.Generation;

/// <summary>
/// Random class diagrams in the form <see cref="ClassParser"/> gives them: every statement of the model, classes with
/// labels, css classes, annotations and members, generic and backquoted names, relations with every end, line and
/// cardinality, namespaces nested two levels, and notes. Quoted text and labels come from <see cref="GenText"/>
/// without the characters the writer writes as entity codes, so the generator finds writer bugs in the statements and
/// in the text rules.
/// </summary>
public static class ClassGenerator
{
	private static readonly string[] Names = ["Animal", "Duck", "a_b", "C1", "List~int~", "Map~string~", "my class", "Ünïcödé"];

	private static readonly string[] NamespaceNames = ["Ns", "Core", "a.b"];

	private static readonly string[] Words = ["c0", "c1", "c2"];

	private static readonly string[] Annotations = ["interface", "abstract", "Service", "enum"];

	private static readonly string[] MemberLines =
	[
		"+int id", "-String name", "#List~int~ items", "~count", "+getName() String", "-setName(String name)", "+isEmpty()$ bool",
		"area()*", "<<interface>>", "String 日本語",
	];

	private static readonly string[] StyleDeclarations = ["fill:#f9f", "stroke:#333", "stroke-width:4px", "color:#fff", "font-weight:bold"];

	private static readonly string[] Cardinalities = ["1", "*", "0..1", "1..*", "many"];

	private static readonly string[] Directions = ["TB", "BT", "LR", "RL"];

	private static readonly Gen<string> Name = Gen.OneOf(Names);

	/// <summary>
	/// Quoted text: no <c>"</c>, which the writer writes as <c>#quot;</c>, and no <c>%</c>, as mermaid.js drops a line
	/// that starts with <c>%%</c> from the source before it parses, even inside a string.
	/// </summary>
	private static readonly Gen<string> Quoted = GenText.Label.Select(text => GenText.WithoutTags(GenText.Without(text, "\"%")));

	/// <summary>The text after a colon: one line, without the <c>:</c> and <c>;</c> the writer writes as entity codes.</summary>
	private static readonly Gen<string> Label = GenText.Line.Select(text => GenText.Without(text, ":;"));

	/// <summary>
	/// The member of <c>name : member</c>: a <see cref="Label"/> without parentheses, as ClassDB reads text with a
	/// <c>)</c> as a method and refuses one it cannot split.
	/// </summary>
	private static readonly Gen<string> MemberText = Label.Select(text => GenText.Without(text, "()"));

	/// <summary>
	/// Styles as the parser gives them: one style, the declarations joined by commas, the first without a <c>-</c>.
	/// <c>stylesOpt</c> has no <c>MINUS</c>, and the <c>LABEL</c> a colon starts reads the <c>-</c> and the commas
	/// after it, so the declarations after the first colon are one token.
	/// </summary>
	private static readonly Gen<List<string>> Styles =
		Gen.Select(
			Gen.OneOf(StyleDeclarations.Where(s => !s.Contains('-')).ToArray()),
			Gen.OneOf(StyleDeclarations).Array[0, 2],
			(first, rest) => new List<string> { string.Join(',', [first, .. rest]) });

	private static readonly Gen<ClassRelationEnd> End = Gen.Enum<ClassRelationEnd>();

	private static readonly Gen<ClassStatement> Definition =
		Gen.Select(
			Name,
			Quoted.OrNull(),
			Gen.Frequency(
				(2, Gen.Const(((string?)null, (string?)null))),
				(1, Gen.OneOf(Words).Select(c => ((string?)c, (string?)null))),
				(1, Gen.OneOf(Annotations).Select(a => ((string?)null, (string?)a)))),
			Gen.OneOf(MemberLines).List[0, 4].OrNullList(),
			(name, label, extra, members) => (ClassStatement)new ClassDefinition(name)
			{
				Label = label,
				CssClass = extra.Item1,
				Annotation = extra.Item2,
				Members = members,
			});

	private static readonly Gen<ClassStatement> Note =
		Gen.Select(Quoted, Name.OrNull(), (text, name) => (ClassStatement)new ClassNote(text) { ClassName = name });

	private static readonly Gen<ClassStatement> Relation =
		Gen.Select(
			Name,
			Name,
			Gen.Select(End, End, Gen.Bool, (from, to, dotted) => (from, to, dotted)),
			Gen.OneOf(Cardinalities).OrNull(),
			Gen.OneOf(Cardinalities).OrNull(),
			Label.OrNull(),
			(from, to, line, fromCardinality, toCardinality, label) => (ClassStatement)new ClassRelation(from, to)
			{
				FromEnd = line.from,
				ToEnd = line.to,
				Line = line.dotted ? ClassLineStyle.Dotted : ClassLineStyle.Solid,
				FromCardinality = fromCardinality,
				ToCardinality = toCardinality,
				Label = label,
			});

	private static readonly Gen<ClassStatement> Interaction = Gen.OneOf(
		Gen.Select(Name, Quoted.OrNull(), (name, tooltip) =>
			(ClassStatement)new ClassInteraction(ClassInteractionKind.Callback, name, "doIt") { Tooltip = tooltip }),
		Gen.Select(Name, Quoted.OrNull(), Gen.OneOf("_self", "_blank", "_parent", "_top").OrNull(), (name, tooltip, target) =>
			(ClassStatement)new ClassInteraction(ClassInteractionKind.Link, name, "https://example.com") { Tooltip = tooltip, LinkTarget = target }),
		Gen.Select(Name, Gen.OneOf("a", "a, b", "\"x\"").OrNull(), Quoted.OrNull(), (name, arguments, tooltip) =>
			(ClassStatement)new ClassInteraction(ClassInteractionKind.ClickCall, name, "doIt") { Arguments = arguments, Tooltip = tooltip }),
		Gen.Select(Name, Quoted.OrNull(), Gen.OneOf("_self", "_blank", "_parent", "_top").OrNull(), (name, tooltip, target) =>
			(ClassStatement)new ClassInteraction(ClassInteractionKind.ClickHref, name, "https://example.com/a?b=1") { Tooltip = tooltip, LinkTarget = target }));

	/// <summary>The statements the body reads and a namespace does not.</summary>
	private static readonly Gen<ClassStatement> BodyOnly = Gen.OneOf(
		Relation,
		Gen.Select(Name, MemberText.OrNull(), (name, member) => (ClassStatement)new ClassMemberStatement(name) { Member = member }),
		Gen.Select(Gen.OneOf(Annotations), Name, (annotation, name) => (ClassStatement)new ClassAnnotation(annotation, name)),
		Interaction,
		Gen.Select(Gen.OneOf(Words), Styles, (id, styles) => (ClassStatement)new ClassStyle(id, styles)),
		Gen.Select(Gen.OneOf(Words).List[1, 3], Styles, (names, styles) => (ClassStatement)new ClassStyleDefinition(names, styles)),
		Gen.Select(Name.List[1, 3], Gen.OneOf(Words), (ids, className) =>
			(ClassStatement)new ClassCssClass(string.Join(',', ids.Distinct(StringComparer.Ordinal)), className)),
		Gen.OneOf(Directions).Select(direction => (ClassStatement)new ClassDirection(direction)));

	public static Gen<ClassDocument> Document { get; } =
		Gen.Select(
			Gen.OneOf("classDiagram", "classDiagram-v2"),
			GenText.Line.OrNull(),
			GenText.Lines.OrNull(),
			Gen.Frequency((6, Gen.OneOf(Definition, Note)), (6, BodyOnly), (1, Namespace(2))).List[1, 8],
			(keyword, accTitle, accDescr, statements) => new ClassDocument
			{
				Keyword = keyword,
				AccessibilityTitle = accTitle,
				AccessibilityDescription = accDescr,
				Statements = statements,
			}).Select(Valid);

	private static Gen<List<string>?> OrNullList(this Gen<List<string>> members) =>
		Gen.Frequency((1, Gen.Const((List<string>?)null)), (2, members.Select(m => (List<string>?)m)));

	/// <summary>A namespace with one to four definitions, notes and namespaces, which the grammar needs, down to <paramref name="depth"/> levels below it.</summary>
	private static Gen<ClassStatement> Namespace(int depth)
	{
		var inner = depth == 0 ? Gen.OneOf(Definition, Note) : Gen.Frequency((4, Definition), (1, Note), (1, Namespace(depth - 1)));
		return Gen.Select(Gen.OneOf(NamespaceNames), Quoted.OrNull(), inner.List[1, 4], (name, label, statements) =>
			(ClassStatement)new ClassNamespace(name) { Label = label, Statements = statements });
	}

	/// <summary>
	/// The document with what ClassDB refuses taken out: an annotation statement on a class no earlier statement adds
	/// gets a definition of the class before it, as mermaid.js fails to read the annotations of an undefined class. A
	/// relation adds both its classes, except the end of a lollipop with no end on the other side, which is an interface.
	/// </summary>
	private static ClassDocument Valid(ClassDocument document)
	{
		var added = new HashSet<string>(StringComparer.Ordinal);
		var statements = Add(document.Statements);
		document.Statements.Clear();
		document.Statements.AddRange(statements);
		return document;

		List<ClassStatement> Add(List<ClassStatement> statements)
		{
			var result = new List<ClassStatement>(statements.Count);
			foreach (var statement in statements)
			{
				switch (statement)
				{
					case ClassAnnotation annotation when added.Add(BaseName(annotation.ClassName)):
						result.Add(new ClassDefinition(annotation.ClassName));
						break;
					case ClassDefinition definition:
						added.Add(BaseName(definition.Name));
						break;
					// A lollipop at one end and none at the other makes that end an interface, not a class.
					case ClassRelation relation:
						if (relation is not { FromEnd: ClassRelationEnd.Lollipop, ToEnd: ClassRelationEnd.None })
						{
							added.Add(BaseName(relation.From));
						}

						if (relation is not { ToEnd: ClassRelationEnd.Lollipop, FromEnd: ClassRelationEnd.None })
						{
							added.Add(BaseName(relation.To));
						}

						break;
					case ClassMemberStatement { Member: not null } member:
						added.Add(BaseName(member.ClassName));
						break;
					case ClassNamespace space:
						var inner = Add(space.Statements);
						space.Statements.Clear();
						space.Statements.AddRange(inner);
						break;
				}

				result.Add(statement);
			}

			return result;
		}
	}

	private static string BaseName(string name) => name.IndexOf('~', StringComparison.Ordinal) is > 0 and var i ? name[..i] : name;
}
