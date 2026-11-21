using CsCheck;
using SplashTheMusical.Diagrams.Requirement;

namespace SplashTheMusical.Tests.Generation;

/// <summary>
/// Random requirement diagrams in the form <see cref="RequirementParser"/> gives them: every statement of the model,
/// requirements of every kind with any of their fields, elements, relationships of every type, styles, classes and
/// directions. Names and values are bare and quoted ones, such as <c>test</c> and <c>a-b</c>, which lex as a keyword or
/// end an <c>unqString</c> bare, and text with a <c>:</c>, which only a quoted value holds.
/// </summary>
/// <remarks>
/// No text holds <c>direction</c>, which the writer refuses when a direction follows it, or <c>"</c>, which only a bare
/// value can hold.
/// </remarks>
public static class RequirementGenerator
{
	private static readonly string[] Names =
		["test_req", "login", "R1", "a1", "1", "1.2.3", "Ünïcödé", "日本語", "my req", "a-b", "test", "element", "low risk", "x<y"];

	private static readonly string[] StyleIds = ["test_req", "login", "R1", "a1", "my req"];

	private static readonly string[] Values =
		["R1", "1.2.3", "the system shall", "test entity", "type: a", "a-b", "Ünïcödé", "日本語", "x = y", "http://x.org/a"];

	private static readonly string[] Words = ["a", "places", "Done", "x1", "has", "Ünïcödé", "日本語"];

	private static readonly string[] ClassNames = ["c0", "c1", "c2"];

	private static readonly string[] StyleDeclarations = ["fill:#f9f", "stroke:#333", "stroke-width:4px", "color:#fff"];

	private static readonly string[] Directions = ["TB", "BT", "LR", "RL"];

	private static readonly Gen<string> Name = Gen.OneOf(Names);

	private static readonly Gen<string?> Value = Gen.OneOf(Values).OrNull();

	private static readonly Gen<string> Text = Gen.OneOf(Words).Array[1, 4].Select(words => string.Join(' ', words));

	private static readonly Gen<List<string>?> Classes =
		Gen.OneOf(ClassNames).Array[1, 2].Select(names => new List<string>(names)).OrNullList();

	private static readonly Gen<List<string>> StyleIdList =
		Gen.OneOf(StyleIds).Array[1, 3].Select(ids => new List<string>(ids.Distinct(StringComparer.Ordinal)));

	private static readonly Gen<List<string>> Styles = Gen.OneOf(StyleDeclarations).Array[1, 3].Select(styles => new List<string>(styles));

	private static readonly Gen<RequirementStatement> Definition =
		Gen.Select(
			Name,
			Gen.OneOf(Enum.GetValues<RequirementKind>()),
			Classes,
			Value,
			Value,
			Gen.OneOf(Enum.GetValues<RequirementRisk>()).Select(r => (RequirementRisk?)r).OrNullValue(),
			Gen.OneOf(Enum.GetValues<RequirementVerifyMethod>()).Select(m => (RequirementVerifyMethod?)m).OrNullValue(),
			(name, kind, classes, id, text, risk, verifyMethod) => (RequirementStatement)new RequirementDefinition(name, kind)
			{
				Classes = classes,
				Id = id,
				Text = text,
				Risk = risk,
				VerifyMethod = verifyMethod,
			});

	private static readonly Gen<RequirementStatement> Element =
		Gen.Select(
			Name,
			Classes,
			Value,
			Value,
			(name, classes, type, docRef) => (RequirementStatement)new RequirementElement(name) { Classes = classes, Type = type, DocRef = docRef });

	private static readonly Gen<RequirementStatement> Relationship =
		Gen.Select(
			Name,
			Name,
			Gen.OneOf(Enum.GetValues<RequirementRelationshipType>()),
			(source, target, type) => (RequirementStatement)new RequirementRelationship(source, target, type));

	private static readonly Gen<RequirementStatement> Statement = Gen.Frequency(
		(4, Definition),
		(3, Element),
		(4, Relationship),
		(1, Gen.Select(Gen.OneOf(ClassNames).Array[1, 2], Styles, (names, styles) => (RequirementStatement)new RequirementClassDefinition([.. names.Distinct(StringComparer.Ordinal)], styles))),
		(1, Gen.Select(StyleIdList, Styles, (ids, styles) => (RequirementStatement)new RequirementStyle(ids, styles))),
		(1, Gen.Select(StyleIdList, Gen.OneOf(ClassNames).Array[1, 2], (ids, classes) => (RequirementStatement)new RequirementClassAssignment(ids, [.. classes]))),
		(1, Gen.OneOf(Directions).Select(direction => (RequirementStatement)new RequirementDirection(direction))));

	public static Gen<RequirementDocument> Document { get; } =
		Gen.Select(
			Text.OrNull(),
			Text.OrNull(),
			Statement.List[0, 8],
			(accTitle, accDescr, statements) => new RequirementDocument
			{
				AccessibilityTitle = accTitle,
				AccessibilityDescription = accDescr,
				Statements = WithDefinedEnds(statements),
			});

	/// <summary>The statements without the relationships to a name no requirement or element defines, which mermaid.js refuses.</summary>
	private static List<RequirementStatement> WithDefinedEnds(List<RequirementStatement> statements)
	{
		var defined = statements
			.Select(s => s switch
			{
				RequirementDefinition d => d.Name,
				RequirementElement e => e.Name,
				_ => null,
			})
			.OfType<string>()
			.ToHashSet(StringComparer.Ordinal);
		return [.. statements.Where(s => s is not RequirementRelationship r || (defined.Contains(r.Source) && defined.Contains(r.Target)))];
	}

	/// <summary>The list or <c>null</c>, one time in three.</summary>
	private static Gen<List<T>?> OrNullList<T>(this Gen<List<T>> list) =>
		Gen.Frequency((1, Gen.Const((List<T>?)null)), (2, list.Select(l => (List<T>?)l)));

	/// <summary>The value or <c>null</c>, one time in three.</summary>
	private static Gen<T?> OrNullValue<T>(this Gen<T?> value)
		where T : struct =>
		Gen.Frequency((1, Gen.Const((T?)null)), (2, value));
}
