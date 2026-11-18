using SplashTheMusical.Diagrams.State;

namespace SplashTheMusical.Tests.Diagrams;

/// <summary>The statements of a state document as lines, one per statement, indented two spaces per composite level.</summary>
internal static class StateDump
{
	public static List<string> Lines(StateDocument document)
	{
		var lines = new List<string> { $"keyword {document.Keyword}" };
		Statements(document.Statements, lines, "");
		return lines;
	}

	private static void Statements(List<StateStatement> statements, List<string> lines, string indent)
	{
		foreach (var statement in statements)
		{
			lines.Add(indent + statement switch
			{
				StateDeclaration d => $"state {Ref(d.State)} description={d.Description}",
				StateTransition t => $"transition {Ref(t.From)} -> {Ref(t.To)} label={t.Label}",
				StateBlock b => $"block {b.Id} kind={b.Kind} description={b.Description} body={b.Statements is not null}",
				StateDivider => "divider",
				StateNote n => $"note {n.Position} {n.Id} text={n.Text}",
				StateFloatingNote f => $"floating note {f.Id} text={f.Text}",
				StateClassDefinition c => $"classDef {c.Name} {c.Styles}",
				StateStyle s => $"style {s.Ids} {s.Styles}",
				StateClassAssignment a => $"class {a.Ids} {a.ClassName}",
				StateDirection d => $"direction {d.Direction}",
				StateHideEmptyDescription => "hide empty description",
				StateScale s => $"scale {s.Width}",
				StateClick c => $"click {Ref(c.State)} url={c.Url} tooltip={c.Tooltip}",
				_ => statement.GetType().Name,
			});
			if (statement is StateBlock { Statements: { } children })
			{
				Statements(children, lines, indent + "  ");
			}
		}
	}

	private static string Ref(StateRef state) => state.ClassName is null ? state.Id : $"{state.Id}:::{state.ClassName}";
}
