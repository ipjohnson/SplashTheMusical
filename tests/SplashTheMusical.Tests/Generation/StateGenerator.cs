using CsCheck;
using SplashTheMusical.Diagrams.State;

namespace SplashTheMusical.Tests.Generation;

/// <summary>
/// Random state diagrams in the form <see cref="StateParser"/> gives them: every statement of the model, with
/// <c>[*]</c> and <c>:::class</c> on transitions, composite states nested two levels with <c>--</c> between their
/// regions, forks, joins and choices, and notes on one line and over several. Ids are word characters and text is words
/// and single spaces, which every statement reads alike, so the generator finds writer bugs in the statements and not
/// in the text rules.
/// </summary>
public static class StateGenerator
{
	private static readonly string[] Ids = ["s0", "s1", "s2", "Idle", "Running", "a_b", "X9"];

	private static readonly string[] Words = ["a", "go", "Done", "x1", "wait", "Ünïcödé", "日本語"];

	private static readonly string[] ClassNames = ["c0", "c1", "c2"];

	private static readonly string[] StyleDeclarations = ["fill:#f9f", "stroke:#333", "stroke-width:4px", "color:#fff", "font-weight:bold"];

	private static readonly string[] Directions = ["TB", "BT", "LR", "RL"];

	private static readonly Gen<string> Id = Gen.OneOf(Ids);

	private static readonly Gen<string> Text = Gen.OneOf(Words).Array[1, 4].Select(words => string.Join(' ', words));

	private static readonly Gen<string?> ClassName = Gen.OneOf(ClassNames).OrNull();

	private static readonly Gen<StateRef> NamedRef = Gen.Select(Id, ClassName, (id, className) => new StateRef(id) { ClassName = className });

	private static readonly Gen<StateRef> Ref =
		Gen.Select(
			Gen.Frequency((4, Id), (1, Gen.Const(StateRef.StartOrEnd))),
			ClassName,
			(id, className) => new StateRef(id) { ClassName = className });

	private static readonly Gen<string> Ids2 = Id.Array[1, 3].Select(ids => string.Join(',', ids.Distinct(StringComparer.Ordinal)));

	private static readonly Gen<string> Styles = Gen.OneOf(StyleDeclarations).Array[1, 3].Select(styles => string.Join(',', styles));

	/// <summary>The statements a composite state reads as the document does.</summary>
	private static readonly Gen<StateStatement> Simple = Gen.OneOf<StateStatement>(
		Gen.Select(NamedRef, Text.OrNull(), (state, description) => new StateDeclaration(state) { Description = description }),
		Gen.Select(Ref, Ref, Text.OrNull(), (from, to, label) => new StateTransition(from, to) { Label = label }),
		Gen.Select(Id, Gen.OneOf(StateKind.Fork, StateKind.Join, StateKind.Choice), (id, kind) => new StateBlock(id) { Kind = kind }),
		Gen.Select(Id, Text.OrNull(), (id, description) => new StateBlock(id) { Description = description }),
		Gen.Select(
			Gen.Bool,
			Id,
			Gen.OneOf(Text, Gen.Select(Text, Text, (a, b) => a + ": " + b), Text.Array[2, 3].Select(lines => string.Join('\n', lines))),
			(left, id, text) => new StateNote(left ? StateNotePosition.LeftOf : StateNotePosition.RightOf, id, text)),
		Gen.Select(Text, Id, (text, id) => new StateFloatingNote(text, id)),
		Gen.Select(Gen.OneOf(ClassNames), Styles, (name, styles) => new StateClassDefinition(name, styles)),
		Gen.Select(Ids2, Styles, (ids, styles) => new StateStyle(ids, styles)),
		Gen.Select(Ids2, Gen.OneOf(ClassNames), (ids, className) => new StateClassAssignment(ids, className)),
		Gen.OneOf(Directions).Select(direction => (StateStatement)new StateDirection(direction)));

	/// <summary>
	/// The statements stateDiagram.jison reads only outside a composite state. Inside one it reads their words as state ids.
	/// </summary>
	private static readonly Gen<StateStatement> TopLevel = Gen.OneOf<StateStatement>(
		Gen.Const<StateStatement>(new StateHideEmptyDescription()),
		Gen.Int[1, 999].Select(width => (StateStatement)new StateScale(width.ToString(System.Globalization.CultureInfo.InvariantCulture))),
		Gen.Select(
			Id.Select(id => new StateRef(id)),
			Gen.OneOf("https://example.com", "https://example.com/a?b=1"),
			Text.OrNull(),
			(state, url, tooltip) => new StateClick(state, url) { Tooltip = tooltip }));

	public static Gen<StateDocument> Document { get; } =
		Gen.Select(
			Gen.OneOf("stateDiagram-v2", "stateDiagram"),
			Text.OrNull(),
			Text.OrNull(),
			Statements(2),
			(keyword, accTitle, accDescr, statements) => new StateDocument
			{
				Keyword = keyword,
				AccessibilityTitle = accTitle,
				AccessibilityDescription = accDescr,
				Statements = statements,
			}).Select(Valid);

	/// <summary>
	/// The document with what StateDB or the lexer refuses taken out: each composite state gets an id of its own, as
	/// mermaid.js refuses a group with a second label, and a plain <c>state id</c> before a composite state becomes a
	/// declaration of the state, as the writer cannot write the two in a row.
	/// </summary>
	private static StateDocument Valid(StateDocument document)
	{
		var composites = 0;
		var statements = Rename(document.Statements);
		document.Statements.Clear();
		document.Statements.AddRange(statements);
		return document;

		List<StateStatement> Rename(List<StateStatement> statements)
		{
			var renamed = new List<StateStatement>(statements.Count);
			for (var i = 0; i < statements.Count; i++)
			{
				var statement = statements[i];
				if (statement is StateBlock { Statements: { } inner } composite)
				{
					statement = new StateBlock("G" + composites++) { Description = composite.Description, Statements = Rename(inner) };
				}
				else if (statement is StateBlock { Kind: StateKind.Default, Description: null, Statements: null } plain
					&& i + 1 < statements.Count && statements[i + 1] is StateBlock { Statements: not null })
				{
					statement = new StateDeclaration(new StateRef(plain.Id));
				}

				renamed.Add(statement);
			}

			return renamed;
		}
	}

	/// <summary>
	/// Statements with composite states down to <paramref name="depth"/> levels below them, and the top level's own
	/// statements when <paramref name="top"/>.
	/// </summary>
	private static Gen<List<StateStatement>> Statements(int depth, bool top = true, int minimum = 0)
	{
		var statement = depth == 0
			? Simple
			: Gen.Frequency(
				(6, Simple),
				(1, Gen.Select(Id, Text.OrNull(), Regions(depth - 1), (id, description, statements) =>
					(StateStatement)new StateBlock(id) { Description = description, Statements = statements })));
		return (top ? Gen.Frequency((8, statement), (1, TopLevel)) : statement).List[minimum, 6];
	}

	/// <summary>The statements of a composite state, with <c>--</c> between its regions when it has more than one.</summary>
	private static Gen<List<StateStatement>> Regions(int depth) =>
		Statements(depth, top: false, minimum: 1).List[1, 3].Select(regions =>
		{
			var statements = new List<StateStatement>();
			foreach (var region in regions)
			{
				if (statements.Count > 0)
				{
					statements.Add(new StateDivider());
				}

				statements.AddRange(region);
			}

			return statements;
		});
}
