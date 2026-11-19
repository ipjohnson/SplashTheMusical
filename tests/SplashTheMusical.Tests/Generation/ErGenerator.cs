using CsCheck;
using SplashTheMusical.Diagrams.Er;

namespace SplashTheMusical.Tests.Generation;

/// <summary>
/// Random entity relationship diagrams in the form <see cref="ErParser"/> gives them: every statement of the model,
/// entities with aliases, classes and attributes, relationships with every cardinality and both lines, and subgraphs
/// nested two levels. Names are bare and quoted ones, such as <c>end</c> and <c>1</c>, which lex as keywords bare.
/// Attribute words are bare, <c>List~T~</c> and backticked. Labels are words and single spaces.
/// </summary>
/// <remarks>
/// <c>u</c> is only on the first entity's side: it lexes as a cardinality only before <c>.</c>, <c>-</c> or <c>|</c>, so
/// on the second side it would join the entity's name. Subgraphs are named <c>G0</c>, <c>G1</c> and on, which no entity
/// is, so no relationship ends at a subgraph and no subgraph holds itself.
/// </remarks>
public static class ErGenerator
{
	private static readonly string[] Names =
		["CUSTOMER", "ORDER", "LINE-ITEM", "a_b", "x1", "Ünïcödé", "日本語", "Customer Account", "1", "42", "end", "one", "u", "p.q"];

	private static readonly string[] StyleIds = ["CUSTOMER", "ORDER", "LINE-ITEM", "a_b", "x1"];

	private static readonly string[] Words = ["a", "places", "Done", "x1", "has", "Ünïcödé", "日本語"];

	private static readonly string[] Types = ["string", "int", "varchar(255)", "List~int~", "Map~string, int~", "*id", "big int"];

	private static readonly string[] AttributeNames = ["id", "name", "geo.accuracy", "check-in date", "items[]", "x"];

	private static readonly string[] Keys = ["PK", "FK", "UK", "pk"];

	private static readonly string[] ClassNames = ["c0", "c1", "c2"];

	private static readonly string[] StyleDeclarations = ["fill:#f9f", "stroke:#333", "stroke-width:4px", "color:#fff"];

	private static readonly string[] Directions = ["TB", "BT", "LR", "RL"];

	private static readonly Gen<string> Name = Gen.OneOf(Names);

	private static readonly Gen<string> Text = Gen.OneOf(Words).Array[1, 4].Select(words => string.Join(' ', words));

	private static readonly Gen<List<string>?> Classes =
		Gen.OneOf(ClassNames).Array[1, 2].Select(names => new List<string>(names.Distinct(StringComparer.Ordinal))).OrNullList();

	private static readonly Gen<List<string>> StyleIdList =
		Gen.OneOf(StyleIds).Array[1, 3].Select(ids => new List<string>(ids.Distinct(StringComparer.Ordinal)));

	private static readonly Gen<List<string>> Styles = Gen.OneOf(StyleDeclarations).Array[1, 3].Select(styles => new List<string>(styles));

	private static readonly Gen<ErAttribute> Attribute =
		Gen.Select(
			Gen.OneOf(Types),
			Gen.Bool,
			Gen.OneOf(AttributeNames),
			Gen.OneOf(Keys).Array[0, 2],
			Text.OrNull(),
			(type, optional, name, keys, comment) => new ErAttribute(optional ? type + "?" : type, name) { Keys = [.. keys], Comment = comment });

	private static readonly Gen<ErStatement> Entity =
		Gen.Select(
			Name,
			Name.OrNull(),
			Classes,
			Attribute.List[0, 3].OrNullList(),
			(name, alias, classes, attributes) => (ErStatement)new ErEntity(name) { Alias = alias, Classes = classes, Attributes = attributes });

	private static readonly Gen<ErStatement> Relationship =
		Gen.Select(
			Gen.Select(Name, Classes, Gen.OneOf(Enum.GetValues<ErCardinality>()), (name, classes, cardinality) => (name, classes, cardinality)),
			Gen.Select(Name, Classes, Gen.OneOf(ErCardinality.OnlyOne, ErCardinality.ZeroOrOne, ErCardinality.OneOrMore, ErCardinality.ZeroOrMore), (name, classes, cardinality) => (name, classes, cardinality)),
			Gen.Bool,
			Gen.OneOf(Text, Gen.Const("")),
			(a, b, identifying, role) => (ErStatement)new ErRelationship(a.name, b.name, role)
			{
				CardinalityA = a.cardinality,
				CardinalityB = b.cardinality,
				Identifying = identifying,
				ClassesA = a.classes,
				ClassesB = b.classes,
			});

	private static readonly Gen<ErStatement> Simple = Gen.Frequency(
		(4, Entity),
		(4, Relationship),
		(1, Gen.Select(Gen.OneOf(ClassNames).Array[1, 2], Styles, (names, styles) => (ErStatement)new ErClassDefinition([.. names.Distinct(StringComparer.Ordinal)], styles))),
		(1, Gen.Select(StyleIdList, Styles, (ids, styles) => (ErStatement)new ErStyle(ids, styles))),
		(1, Gen.Select(StyleIdList, Gen.OneOf(ClassNames).Array[1, 2], (ids, classes) => (ErStatement)new ErClassAssignment(ids, [.. classes.Distinct(StringComparer.Ordinal)]))),
		(1, Gen.OneOf(Directions).Select(direction => (ErStatement)new ErDirection(direction))));

	public static Gen<ErDocument> Document { get; } =
		Gen.Select(
			Text.OrNull(),
			Text.OrNull(),
			Statements(2),
			(accTitle, accDescr, statements) => new ErDocument
			{
				AccessibilityTitle = accTitle,
				AccessibilityDescription = accDescr,
				Statements = statements,
			}).Select(Named);

	/// <summary>The document with its subgraphs named <c>G0</c>, <c>G1</c> and on, in source order.</summary>
	private static ErDocument Named(ErDocument document)
	{
		var count = 0;
		var statements = Rename(document.Statements);
		document.Statements.Clear();
		document.Statements.AddRange(statements);
		return document;

		List<ErStatement> Rename(List<ErStatement> statements) =>
			[.. statements.Select(statement => statement is ErSubgraph subgraph
				? new ErSubgraph("G" + count++) { Title = subgraph.Title, Statements = Rename(subgraph.Statements) }
				: statement)];
	}

	/// <summary>Statements with subgraphs down to <paramref name="depth"/> levels below them.</summary>
	private static Gen<List<ErStatement>> Statements(int depth)
	{
		var statement = depth == 0
			? Simple
			: Gen.Frequency(
				(8, Simple),
				(1, Gen.Select(Text.OrNull(), Statements(depth - 1), (title, statements) =>
					(ErStatement)new ErSubgraph("G") { Title = title, Statements = statements })));
		return statement.List[0, 6];
	}

	/// <summary>The list or <c>null</c>, one time in three.</summary>
	private static Gen<List<T>?> OrNullList<T>(this Gen<List<T>> list) =>
		Gen.Frequency((1, Gen.Const((List<T>?)null)), (2, list.Select(l => (List<T>?)l)));
}
