using System.Collections.Frozen;

namespace SplashTheMusical.Conformance;

/// <summary>What a box kind is to <see cref="OverlapLint"/>.</summary>
public enum LintRole
{
	Node,
	Label,
	Note,
	Group,
}

/// <summary>An overlap <see cref="OverlapLint"/> found. The key names the boxes by role and identity.</summary>
public sealed record Finding(string Check, string Key)
{
	public string Line => $"overlap\t{Check}\t{Key}";
}

/// <summary>
/// Finds drawn boxes that cover each other: labels on labels, labels on nodes, nodes on nodes, notes on anything,
/// groups that cross, and boxes outside the canvas. A node inside a node and a group inside a group nest, so they do
/// not overlap. It reads the boxes of the oracle's format, so STM and mermaid.js are judged by the same rules. It is
/// ported from tests/Mermaider.Tests/Snapshots/LayoutSkeleton/OverlapLint.cs in ipjohnson/mermaider.
/// </summary>
public static class OverlapLint
{
	public const string Canvas = "canvas";
	public const string LabelOverLabel = "label-label";
	public const string LabelOverNode = "label-node";
	public const string NodeOverNode = "node-node";
	public const string NoteOverlap = "note-overlap";
	public const string GroupOverlap = "group-overlap";

	/// <summary>How far two boxes must cross, in user units, to overlap. Boxes that touch do not.</summary>
	public const double Margin = 1;

	private const double CanvasMargin = 0.5;

	private static readonly FrozenDictionary<string, LintRole> None = FrozenDictionary<string, LintRole>.Empty;

	/// <summary>
	/// The role of each box kind, per corpus type folder. Kinds that are not listed, such as edges, links and plot
	/// areas, are not linted, because their box is the bounding box of a path.
	/// </summary>
	public static FrozenDictionary<string, FrozenDictionary<string, LintRole>> Roles { get; } =
		new Dictionary<string, FrozenDictionary<string, LintRole>>(StringComparer.Ordinal)
		{
			["agentflow"] = Table(("node", LintRole.Node), ("flow", LintRole.Group), ("edge-label", LintRole.Label)),
			["architecture"] = Table(("service", LintRole.Node), ("junction", LintRole.Node), ("group", LintRole.Group)),
			["block"] = Table(("block", LintRole.Node), ("edge-label", LintRole.Label)),
			["c4"] = Table(("element", LintRole.Node), ("boundary", LintRole.Group), ("rel-label", LintRole.Label), ("title", LintRole.Label)),
			["class"] = Table(("class", LintRole.Node), ("namespace", LintRole.Group), ("note", LintRole.Note), ("relation-label", LintRole.Label)),
			["cynefin"] = Table(
				("item", LintRole.Node),
				("domain-label", LintRole.Label),
				("transition-label", LintRole.Label),
				("title", LintRole.Label)
			),
			["er"] = Table(("entity", LintRole.Node), ("relation-label", LintRole.Label)),
			["eventmodeling"] = Table(("frame", LintRole.Node), ("lane", LintRole.Group)),
			["flowchart"] = Table(("node", LintRole.Node), ("group", LintRole.Group), ("edge-label", LintRole.Label)),
			["gantt"] = Table(("task", LintRole.Node), ("task-text", LintRole.Label), ("section", LintRole.Group), ("title", LintRole.Label)),
			["git"] = Table(("commit", LintRole.Node), ("commit-label", LintRole.Label), ("branch-label", LintRole.Label), ("tag", LintRole.Label)),
			["info"] = Table(("version", LintRole.Label)),
			["ishikawa"] = Table(("head", LintRole.Node), ("cause", LintRole.Label)),
			["kanban"] = Table(("card", LintRole.Node), ("column", LintRole.Group)),
			["mindmap"] = Table(("node", LintRole.Node)),
			["packet"] = Table(("field", LintRole.Node), ("title", LintRole.Label)),
			["pie"] = Table(("legend", LintRole.Label), ("title", LintRole.Label)),
			["quadrant-chart"] = Table(("point", LintRole.Node), ("quadrant", LintRole.Group), ("axis", LintRole.Label), ("title", LintRole.Label)),
			["radar"] = Table(("axis", LintRole.Label), ("legend", LintRole.Label), ("title", LintRole.Label)),
			["railroad"] = Table(
				("terminal", LintRole.Node),
				("nonterminal", LintRole.Node),
				("special", LintRole.Node),
				("rule", LintRole.Group),
				("title", LintRole.Label)
			),
			["requirement"] = Table(("requirement", LintRole.Node), ("element", LintRole.Node), ("relation-label", LintRole.Label)),
			["sankey"] = Table(("node", LintRole.Node)),
			["sequence"] = Table(
				("actor", LintRole.Node),
				("actor-bottom", LintRole.Node),
				("box", LintRole.Group),
				("block", LintRole.Group),
				("note", LintRole.Note),
				("message-text", LintRole.Label)
			),
			["state"] = Table(("node", LintRole.Node), ("group", LintRole.Group), ("note", LintRole.Note), ("edge-label", LintRole.Label)),
			["swimlanes"] = Table(("node", LintRole.Node), ("group", LintRole.Group), ("edge-label", LintRole.Label)),
			["timeline"] = Table(("period", LintRole.Node), ("event", LintRole.Node), ("section", LintRole.Group), ("title", LintRole.Label)),
			["treeView"] = Table(("node", LintRole.Node)),
			["treemap"] = Table(("node", LintRole.Node)),
			["usecase"] = Table(
				("actor", LintRole.Node),
				("usecase", LintRole.Node),
				("json", LintRole.Node),
				("note", LintRole.Note),
				("relation-label", LintRole.Label)
			),
			["user-journey"] = Table(
				("task", LintRole.Node),
				("face", LintRole.Node),
				("section", LintRole.Group),
				("person", LintRole.Label),
				("title", LintRole.Label)
			),
			// Sets and unions overlap by design.
			["venn"] = None,
			["wardley"] = Table(("node", LintRole.Node), ("title", LintRole.Label)),
			["xychart"] = Table(("category", LintRole.Label), ("axis-title", LintRole.Label), ("title", LintRole.Label)),
			["zenuml"] = Table(("participant", LintRole.Node), ("group", LintRole.Group), ("fragment", LintRole.Group), ("title", LintRole.Label)),
		}.ToFrozenDictionary(StringComparer.Ordinal);

	/// <param name="type">The corpus type folder, which picks the roles. A type without roles has no findings.</param>
	/// <param name="canvas">The canvas the boxes must stay inside.</param>
	public static List<Finding> Run(string type, Rect canvas, IEnumerable<Box> boxes)
	{
		var roles = Roles.GetValueOrDefault(type, None);
		var linted = boxes.Where(b => roles.ContainsKey(b.Kind)).Select(b => (Box: b, Role: roles[b.Kind])).ToList();
		var found = new List<Finding>();
		foreach (var (box, role) in linted)
		{
			var r = box.Rect;
			if (
				r.X < canvas.X - CanvasMargin
				|| r.Y < canvas.Y - CanvasMargin
				|| r.Right > canvas.Right + CanvasMargin
				|| r.Bottom > canvas.Bottom + CanvasMargin
			)
			{
				found.Add(new Finding(Canvas, Show(box, role)));
			}
		}

		for (var i = 0; i < linted.Count; i++)
		{
			for (var j = i + 1; j < linted.Count; j++)
			{
				var (a, b) = (linted[i], linted[j]);
				if (Check(a.Role, b.Role) is not { } check || !a.Box.Rect.Overlaps(b.Box.Rect, Margin))
				{
					continue;
				}

				var nests = check is NodeOverNode or GroupOverlap;
				if (nests && (a.Box.Rect.Contains(b.Box.Rect, Margin) || b.Box.Rect.Contains(a.Box.Rect, Margin)))
				{
					continue;
				}

				var pair = new[] { Show(a.Box, a.Role), Show(b.Box, b.Role) }.Order(StringComparer.Ordinal).ToArray();
				found.Add(new Finding(check, $"{pair[0]} / {pair[1]}"));
			}
		}

		return found;
	}

	/// <summary>The findings of <paramref name="actual"/> that <paramref name="expected"/> lacks, matched as multisets.</summary>
	public static List<Finding> Only(IEnumerable<Finding> actual, IEnumerable<Finding> expected)
	{
		var counts = expected.GroupBy(f => f).ToDictionary(g => g.Key, g => g.Count());
		var result = new List<Finding>();
		foreach (var finding in actual)
		{
			if (counts.TryGetValue(finding, out var n) && n > 0)
			{
				counts[finding] = n - 1;
			}
			else
			{
				result.Add(finding);
			}
		}

		return result;
	}

	private static string? Check(LintRole a, LintRole b) =>
		(a, b) switch
		{
			(LintRole.Label, LintRole.Label) => LabelOverLabel,
			(LintRole.Label, LintRole.Node) or (LintRole.Node, LintRole.Label) => LabelOverNode,
			(LintRole.Node, LintRole.Node) => NodeOverNode,
			(LintRole.Note, LintRole.Note or LintRole.Label or LintRole.Node) or (LintRole.Label or LintRole.Node, LintRole.Note) => NoteOverlap,
			(LintRole.Group, LintRole.Group) => GroupOverlap,
			_ => null,
		};

	private static string Show(Box box, LintRole role) => $"{role.ToString().ToLowerInvariant()} {box.Kind} '{box.Name}'";

	private static FrozenDictionary<string, LintRole> Table(params (string Kind, LintRole Role)[] roles) =>
		roles.ToFrozenDictionary(r => r.Kind, r => r.Role, StringComparer.Ordinal);
}
