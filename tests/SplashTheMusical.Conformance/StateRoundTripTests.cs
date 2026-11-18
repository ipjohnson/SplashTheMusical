using SplashTheMusical.Diagrams.State;

namespace SplashTheMusical.Conformance;

/// <summary>Section 7's round trip for state diagrams: for every accepted case, <c>Parse(Write(Parse(text)))</c> equals <c>Parse(text)</c>.</summary>
public class StateRoundTripTests
{
	[Test]
	public async Task EveryAcceptedStateCaseRoundTrips()
	{
		var checkedCases = 0;
		var problems = new List<string>();
		foreach (var name in CorpusCheck.Cases(CorpusCheck.CorpusRoot))
		{
			if (!OracleFiles.Read(CorpusCheck.CorpusRoot, name).Accepted)
			{
				continue;
			}

			var source = CorpusComparison.Source(CorpusCheck.CorpusRoot, name);
			if (!Mermaid.Default.TryParse(source, out var parsed, out _) || parsed is not StateDocument document)
			{
				continue;
			}

			checkedCases++;
			var written = Mermaid.Default.Write(document);
			if (!Mermaid.Default.TryParse(written, out var again, out var diagnostics))
			{
				problems.Add($"{name}: the written state diagram did not parse: {string.Join("; ", diagnostics)}\n{written}");
				continue;
			}

			var expected = StateModel.Lines(document);
			var actual = again is StateDocument state ? StateModel.Lines(state) : [$"a {again?.DiagramType} document"];
			var difference = FlowchartModel.FirstDifference(expected, actual);
			if (difference is not null)
			{
				problems.Add($"{name}: {difference}\n{written}");
			}
		}

		await Assert.That(string.Join("\n\n", problems)).IsEmpty();
		await Assert.That(checkedCases).IsEqualTo(285);
	}
}

/// <summary>Every value of a state document as lines, one per statement, so two documents compare line by line.</summary>
internal static class StateModel
{
	public static List<string> Lines(StateDocument document)
	{
		var lines = new List<string>
		{
			$"keyword {document.Keyword}",
			$"title {document.Title}",
			$"config {(document.FrontMatterConfig is { } config ? config.ToString() : "null")}",
			$"accTitle {document.AccessibilityTitle}",
			$"accDescr {document.AccessibilityDescription}",
		};
		foreach (var directive in document.Directives)
		{
			lines.Add($"directive {directive.Type} {directive.Arguments} {directive.InvalidArguments}");
		}

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
