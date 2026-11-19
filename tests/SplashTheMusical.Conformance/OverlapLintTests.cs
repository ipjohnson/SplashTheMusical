namespace SplashTheMusical.Conformance;

public class OverlapLintTests
{
	private static readonly Rect Canvas = new(0, 0, 500, 500);

	[Test]
	public void EachCheckFindsItsOverlap()
	{
		Box[] boxes =
		[
			NewBox("class", "A", 0, 0, 100, 40),
			NewBox("class", "B", 90, 0, 100, 40),
			NewBox("relation-label", "A --> B", 50, 30, 40, 20),
			NewBox("relation-label", "B --> A", 80, 45, 40, 20),
			NewBox("note", "a note", 150, 30, 40, 20),
			NewBox("namespace", "M", 0, 200, 100, 100),
			NewBox("namespace", "N", 50, 250, 100, 100),
			NewBox("class", "far", 480, 480, 40, 40),
		];

		var lines = OverlapLint.Run("class", Canvas, boxes).Select(f => f.Line).Order(StringComparer.Ordinal);

		LineAssert.Equal(
			lines,
			"overlap\tcanvas\tnode class 'far'",
			"overlap\tgroup-overlap\tgroup namespace 'M' / group namespace 'N'",
			"overlap\tlabel-label\tlabel relation-label 'A --> B' / label relation-label 'B --> A'",
			"overlap\tlabel-node\tlabel relation-label 'A --> B' / node class 'A'",
			"overlap\tnode-node\tnode class 'A' / node class 'B'",
			"overlap\tnote-overlap\tnode class 'B' / note note 'a note'"
		);
	}

	[Test]
	public void NestedNodesNestedGroupsTouchingBoxesAndUnlintedKindsAreNotOverlaps()
	{
		Box[] boxes =
		[
			NewBox("namespace", "Outer", 0, 0, 300, 300),
			NewBox("namespace", "Inner", 10, 10, 200, 200),
			NewBox("class", "A", 20, 20, 100, 40),
			NewBox("class", "B", 120, 20, 50, 40),
			NewBox("relation", "A --> B", 20, 20, 150, 40),
		];

		var classFindings = OverlapLint.Run("class", Canvas, boxes);
		Verify(classFindings.Count == 0, $"{string.Join('\n', classFindings)}");
		var vennFindings = OverlapLint.Run("venn", Canvas, [NewBox("set", "A", 0, 0, 10, 10), NewBox("set", "B", 5, 5, 10, 10)]);
		Verify(vennFindings.Count == 0, $"{string.Join('\n', vennFindings)}");
	}

	[Test]
	public void OnlyKeepsTheFindingsTheOtherSideLacksAsAMultiset()
	{
		var a = new Finding(OverlapLint.LabelOverNode, "x");
		var b = new Finding(OverlapLint.NodeOverNode, "y");

		var only = OverlapLint.Only([a, a, b], [a]);

		LineAssert.Equal(only.Select(f => f.Line), a.Line, b.Line);
	}

	[Test]
	public void EveryCorpusTypeHasRolesAndEveryRoleNamesAKindItsBoxesUse()
	{
		var corpus = CorpusCheck.CorpusRoot;
		var kinds = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
		foreach (var name in CorpusCheck.Cases(corpus))
		{
			var path = Path.Combine(corpus, name + ".boxes.txt");
			if (!File.Exists(path))
			{
				continue;
			}

			var type = name.Split('/', '\\')[0];
			var seen = kinds.TryGetValue(type, out var set) ? set : kinds[type] = new HashSet<string>(StringComparer.Ordinal);
			seen.UnionWith(OracleFiles.ReadBoxes(path).Boxes.Select(l => Box.Parse(l).Kind));
		}

		var problems = new List<string>();
		foreach (var type in kinds.Keys.Where(t => !OverlapLint.Roles.ContainsKey(t)).Order(StringComparer.Ordinal))
		{
			problems.Add($"{type}: no roles");
		}

		foreach (var (type, roles) in OverlapLint.Roles.OrderBy(r => r.Key, StringComparer.Ordinal))
		{
			foreach (var kind in roles.Keys.Where(k => !kinds.GetValueOrDefault(type, []).Contains(k)).Order(StringComparer.Ordinal))
			{
				problems.Add($"{type}: no {kind} boxes");
			}
		}

		Verify(problems.Count == 0, $"{string.Join('\n', problems)}");
	}

	private static Box NewBox(string kind, string name, double x, double y, double width, double height) =>
		new(kind, name, new Rect(x, y, width, height), "");
}
