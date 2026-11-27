using CsCheck;
using SplashTheMusical.Diagrams.C4;

namespace SplashTheMusical.Tests.Generation;

/// <summary>
/// Random C4 diagrams in the form <see cref="C4Parser"/> gives them: every header, every element, boundary, relation
/// and update macro, boundaries nested two deep, positional and named arguments, empty arguments, and the title and
/// description in each form the writer has. Values are <see cref="GenText.Label"/> text, with line breaks, entity codes
/// and HTML line breaks.
/// </summary>
/// <remarks>
/// <para>
/// No value holds <c>"</c>, which only a bare argument holds, or <c>%</c>, which starts a comment at the start of a
/// line. Named arguments come after the label, and a boundary's after its type, and name no field the db keys on, such
/// as <c>alias</c> or <c>from</c>, because mermaid.js fails to draw a label, a boundary type or an end that is an
/// object.
/// </para>
/// <para>
/// mermaid.js also fails to draw a relation whose end is no element or boundary, and recurses for ever on a boundary
/// inside one of its own alias, so each relation's ends are aliases the diagram defines, and each boundary's alias is
/// its own.
/// </para>
/// </remarks>
public static class C4Generator
{
	private static readonly string[] Aliases = ["a", "b", "customer", "Person", "x_1", "日本"];

	private static readonly string[] ElementNames = ["techn", "descr", "sprite", "tags", "link"];

	private static readonly string[] ElementStyleNames = ["bgColor", "fontColor", "borderColor", "shape", "sprite", "techn", "legendText"];

	private static readonly string[] RelStyleNames = ["textColor", "lineColor", "offsetX", "offsetY"];

	private static readonly string[] Numbers = ["0", "1", "2", "3", "-10", "5"];

	private static readonly Gen<string> Text = GenText.Label.Select(t => GenText.WithoutTags(GenText.Without(t, "\"%")));

	private static readonly Gen<string> Value = Gen.Frequency((8, Text), (1, Gen.Const("")));

	private static readonly Gen<string> Alias = Gen.OneOf(Aliases);

	private static readonly Gen<C4Argument> Positional = Value.Select(v => new C4Argument(v));

	private static Gen<C4Argument> Named(string[] names, Gen<string> value) =>
		Gen.Select(Gen.OneOf(names), value, (name, v) => new C4Argument(name, v));

	private static readonly Gen<C4Argument> Extra = Gen.Frequency((3, Positional), (1, Named(ElementNames, Value)));

	private static readonly Gen<C4Statement> Element =
		Gen.Select(
			Gen.OneOf(Enum.GetValues<C4ElementKind>()),
			Alias,
			Positional.Array[0, 1],
			Extra.Array[0, 4],
			(kind, alias, label, extra) => (C4Statement)new C4Element(kind, Arguments([new C4Argument(alias), .. label, .. (label.Length > 0 ? extra : [])])));

	private static readonly Gen<C4Statement> Relation =
		Gen.Select(
			Gen.OneOf(Enum.GetValues<C4RelationKind>()),
			Alias,
			Alias,
			Positional,
			Extra.Array[0, 3],
			(kind, from, to, label, extra) => (C4Statement)new C4Relation(
				kind,
				Arguments([.. (kind == C4RelationKind.RelIndex ? [new C4Argument("1")] : Array.Empty<C4Argument>()), new(from), new(to), label, .. extra])));

	private static readonly Gen<C4Statement> Update = Gen.OneOf(
		Gen.Select(Alias, Named(ElementStyleNames, Text).Array[1, 3], (alias, named) => (C4Statement)new C4Update(C4UpdateKind.ElementStyle, Arguments([new C4Argument(alias), .. named]))),
		Gen.Select(Alias, Alias, Named(RelStyleNames, Gen.OneOf(Numbers)).Array[1, 3], (from, to, named) => (C4Statement)new C4Update(C4UpdateKind.RelStyle, Arguments([new C4Argument(from), new C4Argument(to), .. named]))),
		Gen.Frequency(
			(1, Gen.OneOf(Numbers).Array[1, 2].Select(values => values.Select(v => new C4Argument(v)).ToArray())),
			(1, Gen.OneOf(Numbers).Array[1, 2].Select(values => values.Select((v, i) => new C4Argument(i == 0 ? "c4ShapeInRow" : "c4BoundaryInRow", v)).ToArray())))
			.Select(arguments => (C4Statement)new C4Update(C4UpdateKind.LayoutConfig, Arguments(arguments))));

	private static readonly Gen<C4Statement> Flat = Gen.Frequency((5, Element), (3, Relation), (1, Update));

	private static readonly Gen<List<C4Statement>> Inner = Statements(Flat);

	private static readonly Gen<List<C4Statement>> Middle = Statements(Gen.Frequency((6, Flat), (1, Boundary(Inner))));

	private static readonly Gen<List<C4Statement>> Top = Statements(Gen.Frequency((6, Flat), (2, Boundary(Middle))));

	public static Gen<C4Document> Document { get; } =
		Gen.Select(
			Gen.OneOf(Enum.GetValues<C4DiagramKind>()),
			GenText.Line.OrNull(),
			GenText.Lines.OrNull(),
			Top,
			(kind, title, description, statements) => Finish(new C4Document
			{
				Kind = kind,
				DiagramTitle = title,
				AccessibilityDescription = description,
				Statements = statements,
			}));

	private static Gen<List<C4Statement>> Statements(Gen<C4Statement> statement) => statement.List[1, 4];

	private static Gen<C4Statement> Boundary(Gen<List<C4Statement>> children) =>
		Gen.Select(
			Gen.OneOf(Enum.GetValues<C4BoundaryKind>()),
			Alias,
			Positional,
			Positional.Array[0, 1],
			Extra.Array[0, 2],
			children,
			(kind, alias, label, type, extra, statements) => (C4Statement)new C4Boundary(kind, Arguments([new C4Argument(alias), label, .. type, .. (type.Length > 0 ? extra : [])]))
			{
				Statements = statements,
			});

	private static List<C4Argument> Arguments(C4Argument[] arguments) => [.. arguments];

	/// <summary>Gives each boundary an alias of its own and points each relation's ends at aliases the diagram defines.</summary>
	private static C4Document Finish(C4Document document)
	{
		var defined = new List<string>();
		var boundaries = 0;
		Define(document.Statements, defined, ref boundaries);
		if (defined.Count == 0)
		{
			document.Statements.Add(new C4Element(C4ElementKind.Person, [new C4Argument("a")]));
			defined.Add("a");
		}

		Connect(document.Statements, defined);
		return document;
	}

	private static void Define(List<C4Statement> statements, List<string> defined, ref int boundaries)
	{
		foreach (var statement in statements)
		{
			if (statement is C4Boundary boundary)
			{
				boundary.Arguments[0] = new C4Argument($"{boundary.Arguments[0].Value}_b{boundaries++}");
				defined.Add(boundary.Arguments[0].Value);
				Define(boundary.Statements, defined, ref boundaries);
			}
			else if (statement is C4Element element)
			{
				defined.Add(element.Arguments[0].Value);
			}
		}
	}

	private static void Connect(List<C4Statement> statements, List<string> defined)
	{
		foreach (var statement in statements)
		{
			if (statement is C4Boundary boundary)
			{
				Connect(boundary.Statements, defined);
			}
			else if (statement is C4Relation relation)
			{
				var from = relation.Kind == C4RelationKind.RelIndex ? 1 : 0;
				for (var end = from; end < from + 2; end++)
				{
					var alias = relation.Arguments[end].Value;
					if (!defined.Contains(alias))
					{
						relation.Arguments[end] = new C4Argument(defined[alias.Length % defined.Count]);
					}
				}
			}
		}
	}
}
