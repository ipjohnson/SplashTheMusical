using System.Globalization;
using SplashTheMusical.Text;

namespace SplashTheMusical.Conformance;

/// <summary>
/// The default measurer against the widths Chromium measured in the oracle's bundled font. The labels are SVG texts on
/// one line in mermaid's default font, whose box is as wide as the text's advance: the message texts of the corpus's
/// sequence diagrams, and the task texts and section names of its gantt charts.
/// </summary>
public class TextMeasurementTests
{
	/// <summary>mermaid's default font list. The oracle draws every family in the bundled font.</summary>
	private const string Family = "\"trebuchet ms\", verdana, arial, sans-serif";

	/// <summary>mermaid's default <c>sequence.messageFontSize</c>.</summary>
	private const double MessageFontSize = 16;

	/// <summary>mermaid's default <c>gantt.fontSize</c> and <c>gantt.sectionFontSize</c>.</summary>
	private const double GanttFontSize = 11;

	private const double Tolerance = 0.01;

	private const int MinimumSample = 1000;

	/// <summary>Config keys that change the label font or wrap the text, so a case that sets one is left out.</summary>
	private static readonly string[] FontKeys =
	[
		"wrap",
		"fontSize",
		"themeCSS",
		"sequence.wrap",
		"sequence.fontSize",
		"sequence.fontWeight",
		"sequence.fontFamily",
		"sequence.messageFontSize",
		"sequence.messageFontWeight",
		"sequence.messageFontFamily",
		"gantt.fontSize",
		"gantt.sectionFontSize",
	];

	[Test]
	public async Task MeasuresTheOracleLabelsWithinOnePercent()
	{
		var sample = SequenceLabels().Concat(GanttLabels()).ToList();
		var misses = sample
			.Select(label => (label, measured: TextMeasurer.Default.Measure(label.Text, new FontSpec(Family, label.Size, label.Weight)).Width))
			.Where(pair => Math.Abs(pair.measured - pair.label.Width) > Tolerance * pair.label.Width)
			.Select(pair => string.Create(CultureInfo.InvariantCulture, $"{pair.label.Case}: \"{pair.label.Text}\" is {pair.measured:0.###}, the oracle's {pair.label.Width}"))
			.ToList();

		await Assert.That(sample.Count).IsGreaterThanOrEqualTo(MinimumSample);
		await Assert.That(string.Join("\n", misses)).IsEqualTo("");
	}

	/// <summary>The message texts whose box the facts name without doubt: the only message between its two actors.</summary>
	private static IEnumerable<Label> SequenceLabels()
	{
		foreach (var (name, facts) in Cases("sequence/"))
		{
			var messages = facts.Facts
				.Select(line => line.Split('\t'))
				.Where(fields => fields[0] == "message" && fields.Length == 6)
				.GroupBy(fields => fields[1] + " --> " + fields[2])
				.Where(group => group.Count() == 1)
				.ToDictionary(group => group.Key, group => group.Single()[5]);
			foreach (var box in facts.Boxes.Select(Box.Parse).Where(box => box.Kind == "message-text"))
			{
				if (messages.TryGetValue(box.Name, out var text) && IsPlain(text))
				{
					yield return new Label(name, text, MessageFontSize, box.Rect.Width);
				}
			}
		}
	}

	/// <summary>
	/// The task texts, named by section and task, and the section names. Milestones and <c>vert</c> markers, the tasks
	/// that take no time, are drawn beside their marker and are left out. mermaid's gantt stylesheet draws the text of a
	/// task with a <c>click</c> in bold.
	/// </summary>
	private static IEnumerable<Label> GanttLabels()
	{
		foreach (var (name, facts) in Cases("gantt/"))
		{
			var clicked = File.ReadLines(Path.Combine(CorpusCheck.CorpusRoot, name + ".mmd"))
				.Select(line => line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries))
				.Where(words => words.Length >= 2 && words[0] == "click")
				.Select(words => words[1])
				.ToHashSet(StringComparer.Ordinal);
			var tasks = facts.Facts
				.Select(line => line.Split('\t'))
				.Where(fields => fields[0] == "task" && fields.Length >= 6 && fields[4] != fields[5])
				.GroupBy(fields => fields[1] + " / " + fields[2])
				.Where(group => group.Count() == 1)
				.ToDictionary(group => group.Key, group => group.Single());
			foreach (var box in facts.Boxes.Select(Box.Parse))
			{
				if (box.Kind == "task-text" && tasks.TryGetValue(box.Name, out var task) && IsPlain(task[2]))
				{
					var weight = clicked.Contains(task[3]) ? 700 : 400;
					yield return new Label(name, task[2], GanttFontSize, box.Rect.Width, weight);
				}
				else if (box.Kind == "section" && !box.Name.Contains(" #", StringComparison.Ordinal) && IsPlain(box.Name))
				{
					yield return new Label(name, box.Name, GanttFontSize, box.Rect.Width);
				}
			}
		}
	}

	/// <summary>The accepted cases of a type that set no font key and break no line with a <c>br</c> tag.</summary>
	private static IEnumerable<(string Name, CaseFacts Facts)> Cases(string folder)
	{
		foreach (var name in CorpusCheck.Cases(CorpusCheck.CorpusRoot).Where(name => name.Replace('\\', '/').StartsWith(folder, StringComparison.Ordinal)))
		{
			var facts = OracleFiles.Read(CorpusCheck.CorpusRoot, name);
			var source = File.ReadAllText(Path.Combine(CorpusCheck.CorpusRoot, name + ".mmd"));
			if (facts.Accepted
				&& !source.Contains("<br", StringComparison.OrdinalIgnoreCase)
				&& !source.Contains("wrap", StringComparison.OrdinalIgnoreCase)
				&& !facts.Facts.Any(line => FontKeys.Any(key => line.StartsWith("config\t" + key + "\t", StringComparison.Ordinal))))
			{
				yield return (name, facts);
			}
		}
	}

	/// <summary>True for text with no markup, entity or math, which the facts would not show as drawn.</summary>
	private static bool IsPlain(string text) =>
		text.Length > 0
		&& text.Trim().Length == text.Length
		&& !text.Contains('<', StringComparison.Ordinal)
		&& !text.Contains('#', StringComparison.Ordinal)
		&& !text.Contains('&', StringComparison.Ordinal)
		&& !text.Contains("$$", StringComparison.Ordinal)
		&& !text.Contains("\\n", StringComparison.Ordinal);

	private sealed record Label(string Case, string Text, double Size, double Width, int Weight = 400);
}
