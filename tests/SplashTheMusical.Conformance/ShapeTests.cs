using System.Globalization;
using SplashTheMusical.Rendering.Shapes;
using SplashTheMusical.Text;

namespace SplashTheMusical.Conformance;

/// <summary>
/// The shape library against the oracle's shape table, <c>corpus/shapes.txt</c>, and against the node sizes mermaid.js
/// handed dagre for the corpus's flowcharts.
/// </summary>
public class ShapeTests
{
	/// <summary>mermaid's default font list. The oracle draws every family in the bundled font.</summary>
	private const string Family = "\"trebuchet ms\", verdana, arial, sans-serif";

	/// <summary>The default <c>flowchart.wrappingWidth</c> and <c>flowchart.minNodeWidth</c> of mermaid.js 12.3.0.</summary>
	private const double LabelWidth = 120;

	/// <summary>The default <c>flowchart.padding</c>.</summary>
	private const double Padding = 15;

	private const double Tolerance = 0.01;

	private const int MinimumSample = 1000;

	/// <summary>Config keys that change neither the label nor the node, so a case that sets only these is compared.</summary>
	private static readonly string[] NeutralKeys =
	[
		"look", "theme", "layout", "logLevel", "handDrawnSeed", "flowchart.theme", "flowchart.look", "flowchart.titleTopMargin",
		"flowchart.subGraphTitleMargin.top", "flowchart.subGraphTitleMargin.bottom", "flowchart.useMaxWidth", "flowchart.rankSpacing",
		"flowchart.diagramPadding", "flowchart.nodeSpacing", "flowchart.stepDuration", "flowchart.curve", "flowchart.inheritDir",
		"elk.keepEntryNodeOnTop",
	];

	[Test]
	public void EveryShapeOfTheTableHasADrawing()
	{
		var missing = ShapeLines("shape")
			.Where(fields => NodeShapes.ForHandler(fields[5]) is null)
			.Select(fields => fields[1] + " (" + fields[5] + ")")
			.ToList();

		Verify(() => ShapeLines("shape").Count() >= 53);
		Verify(!missing.Any(), $"{string.Join("\n", missing)}");
	}

	[Test]
	public void EveryShapeNameFindsTheTablesHandler()
	{
		var keys = ShapeLines("key").ToList();
		var wrong = keys
			.Where(fields => NodeShapes.Handler(fields[1]) != fields[2])
			.Select(fields => $"{fields[1]}: {NodeShapes.Handler(fields[1]) ?? "none"}, the table's {fields[2]}")
			.ToList();

		Verify(() => keys.Count > 0);
		Verify(!wrong.Any(), $"{string.Join("\n", wrong)}");
		Verify(() => NodeShapes.Handler("rect-ish") is null);
	}

	/// <summary>
	/// The size each shape gives a flowchart node against the size mermaid.js handed dagre, for every node whose label
	/// fits on one line in the default font, in cases that change neither the label nor the padding. The hand-drawn look
	/// is left out, because its size comes from roughjs's random strokes.
	/// </summary>
	[Test]
	public void ShapesSizeFlowchartNodesAsTheOracleDid()
	{
		var fontSizes = ThemeFontSizes();
		var compared = 0;
		var checkedShapes = new HashSet<string>(StringComparer.Ordinal);
		var misses = new List<string>();
		foreach (var file in DagreRecords.Files(CorpusCheck.CorpusRoot).Where(file => file.StartsWith("flowchart/", StringComparison.Ordinal)))
		{
			var name = file[..^".dagre.txt".Length];
			var facts = OracleFiles.Read(CorpusCheck.CorpusRoot, name);
			var source = File.ReadAllText(Path.Combine(CorpusCheck.CorpusRoot, name + ".mmd"));
			var config = facts.Facts
				.Select(line => line.Split('\t'))
				.Where(fields => fields[0] == "config")
				.ToDictionary(fields => fields[1], fields => fields[2]);
			if (!config.Keys.All(NeutralKeys.Contains) || !IsPlainSource(source))
			{
				continue;
			}

			var look = config.GetValueOrDefault("look", "\"classic\"") switch
			{
				"\"neo\"" => ShapeLook.Neo,
				"\"handDrawn\"" => ShapeLook.HandDrawn,
				_ => ShapeLook.Classic,
			};
			if (look == ShapeLook.HandDrawn)
			{
				continue;
			}

			var fontSize = fontSizes[config.GetValueOrDefault("theme", "\"default\"").Trim('"')];
			var direction = Direction(source);
			var nodes = facts.Facts
				.Select(line => line.Split('\t'))
				.Where(fields => fields[0] == "node" && fields.Length == 4)
				.ToDictionary(fields => fields[1], fields => (Label: fields[2], Shape: fields[3]));
			var decorated = facts.Facts
				.Select(line => line.Split('\t'))
				.Where(fields => fields[0] is "icon" or "image")
				.Select(fields => fields[1])
				.ToHashSet(StringComparer.Ordinal);
			foreach (var record in DagreRecords.Read(Path.Combine(CorpusCheck.CorpusRoot, file)))
			{
				foreach (var input in record.Input.Nodes)
				{
					// A collapsed subgraph is not a vertex: it has padding 8 and no minimum width, and FlowchartDagreInputTests compares it.
					if (!nodes.TryGetValue(input.Id, out var node) || decorated.Contains(input.Id) || NodeShapes.Find(node.Shape) is not { } shape || shape.Handler == "collapsedGroup")
					{
						continue;
					}

					var measurer = new OneLineMeasurer(node.Label, fontSize);
					var geometry = shape.Draw(new ShapeNode
					{
						Label = measurer,
						Look = look,
						Padding = Padding,
						Direction = direction,
					});
					if (measurer.Measured && (!IsPlainLabel(node.Label) || measurer.TextWidth >= LabelWidth))
					{
						continue;
					}

					compared++;
					checkedShapes.Add(shape.Handler);
					if (Math.Abs(geometry.Width - input.Width) > Tolerance || Math.Abs(geometry.Height - input.Height) > Tolerance)
					{
						misses.Add(string.Create(
							CultureInfo.InvariantCulture,
							$"{name} {input.Id} ({node.Shape}, {look}): {geometry.Width:0.###} x {geometry.Height:0.###}, the oracle's {input.Width:0.###} x {input.Height:0.###}"));
					}
				}
			}
		}

		var notCompared = ShapeLines("shape").Select(fields => fields[5]).Where(handler => !checkedShapes.Contains(handler)).ToList();
		Verify(() => compared >= MinimumSample);
		Verify(!misses.Any(), $"{string.Join("\n", misses)}");
		Verify(!notCompared.Any(), $"{string.Join(", ", notCompared)}");
	}

	/// <summary>The lines of a kind in <c>corpus/shapes.txt</c>, split at tabs.</summary>
	private static IEnumerable<string[]> ShapeLines(string kind) =>
		File.ReadLines(Path.Combine(CorpusCheck.CorpusRoot, "shapes.txt"))
			.Select(line => line.Split('\t'))
			.Where(fields => fields[0] == kind);

	/// <summary>Each theme's <c>fontSize</c> from <c>corpus/themes.txt</c>, in pixels.</summary>
	private static Dictionary<string, double> ThemeFontSizes() =>
		File.ReadLines(Path.Combine(CorpusCheck.CorpusRoot, "themes.txt"))
			.Select(line => line.Split('\t'))
			.Where(fields => fields.Length == 4 && fields[0] == "theme" && fields[2] == "fontSize")
			.ToDictionary(fields => fields[1], fields => double.Parse(fields[3].Trim('"').Replace("px", "", StringComparison.Ordinal), CultureInfo.InvariantCulture));

	/// <summary>The direction of the first line, such as <c>flowchart LR</c>, or <c>TB</c>.</summary>
	private static string Direction(string source)
	{
		foreach (var line in source.Split('\n'))
		{
			var words = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (words.Length >= 1 && words[0] is "flowchart" or "graph" or "flowchart-elk")
			{
				return words.Length >= 2 ? words[1].TrimEnd(';').ToUpperInvariant() : "TB";
			}
		}

		return "TB";
	}

	/// <summary>
	/// True for a case without styles, classes, Markdown, markup or entities. The first three change the label's font, and
	/// the facts show a label's text without the last two.
	/// </summary>
	private static bool IsPlainSource(string source) =>
		!source.Contains(":::", StringComparison.Ordinal)
		&& !source.Contains('<', StringComparison.Ordinal)
		&& !source.Contains('&', StringComparison.Ordinal)
		&& !source.Contains('`', StringComparison.Ordinal)
		&& !source.Contains("%%{", StringComparison.Ordinal)
		&& !source.Split('\n').Select(line => line.Trim()).Any(line => line.StartsWith("style ", StringComparison.Ordinal) || line.StartsWith("class", StringComparison.Ordinal));

	/// <summary>True for text with no markup, entity, Markdown or math, which mermaid draws as the text itself.</summary>
	private static bool IsPlainLabel(string text) =>
		text.Length > 0
		&& text.Trim().Length == text.Length
		&& text.IndexOfAny(['<', '>', '&', '#', '*', '_', '`', '\\', '"', '$', '~', '[', ']']) < 0;

	/// <summary>
	/// An HTML label on one line, as <c>labelHelper</c> measures it: as wide as its text and at least
	/// <c>flowchart.minNodeWidth</c> unless the shape asks otherwise, and one line of line height 1.5 tall. Chromium lays
	/// the text out in units of 1/64 px and rounds its width up to one.
	/// </summary>
	private sealed class OneLineMeasurer(string text, double fontSize) : IShapeLabelMeasurer
	{
		public double TextWidth { get; } = Math.Ceiling(TextMeasurer.Default.Measure(text, new FontSpec(Family, fontSize)).Width * 64) / 64;

		/// <summary>Whether the shape measured the label, which the shapes without one do not.</summary>
		public bool Measured { get; private set; }

		public ShapeLabel Measure(double? nodeWidth, bool ignoreMinWidth)
		{
			Measured = true;
			var minWidth = nodeWidth is > 0 || ignoreMinWidth ? 0 : LabelWidth;
			return new ShapeLabel(Math.Max(TextWidth, minWidth), fontSize * 1.5, Html: true);
		}
	}
}
