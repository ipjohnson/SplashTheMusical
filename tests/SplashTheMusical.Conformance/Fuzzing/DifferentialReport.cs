using System.Globalization;
using System.Text;

namespace SplashTheMusical.Conformance.Fuzzing;

/// <summary>
/// Writes a differential run to a folder: each minimized find as <c>finds/&lt;type&gt;/&lt;name&gt;.mmd</c>, which
/// <c>mise run fuzz-add</c> adds to the corpus, the source it was minimized from as
/// <c>original/&lt;type&gt;/&lt;name&gt;.mmd</c>, and <c>report.md</c>, the body of the issue the scheduled workflow opens.
/// </summary>
public static class DifferentialReport
{
	public const string FindsFolder = "finds";

	public const string OriginalFolder = "original";

	public const string ReportFile = "report.md";

	public static void Write(string folder, IReadOnlyList<DifferentialRun> runs, long seed, string mermaidVersion)
	{
		foreach (var emptied in new[] { FindsFolder, OriginalFolder })
		{
			if (Directory.Exists(Path.Combine(folder, emptied)))
			{
				Directory.Delete(Path.Combine(folder, emptied), recursive: true);
			}
		}

		foreach (var find in runs.SelectMany(r => r.Finds))
		{
			WriteSource(Path.Combine(folder, FindsFolder, find.Type, find.Name + ".mmd"), find.Minimized);
			WriteSource(Path.Combine(folder, OriginalFolder, find.Type, find.Name + ".mmd"), find.Original);
		}

		File.WriteAllText(Path.Combine(folder, ReportFile), Markdown(runs, seed, mermaidVersion));
	}

	public static string Markdown(IReadOnlyList<DifferentialRun> runs, long seed, string mermaidVersion)
	{
		var finds = runs.Sum(r => r.Finds.Count);
		var text = new StringBuilder();
		text.Append(CultureInfo.InvariantCulture, $"Differential fuzzing of STM against mermaid.js {mermaidVersion} with seed {seed}: ");
		text.Append(finds == 0 ? "no finds.\n\n" : finds == 1 ? "1 find.\n\n" : $"{finds} finds.\n\n");
		text.Append("| Type | Seed cases | Sources | Compared | Finds |\n|---|--:|--:|--:|--:|\n");
		foreach (var run in runs)
		{
			text.Append(CultureInfo.InvariantCulture, $"| `{run.Type}` | {run.Seeds} | {run.Sources} | {run.Compared} | {run.Finds.Count} |\n");
		}

		foreach (var find in runs.SelectMany(r => r.Finds))
		{
			text.Append(CultureInfo.InvariantCulture, $"\n## `{find.Type}/{find.Name}`: {find.Disagreement.Signature}\n\n");
			text.Append(CultureInfo.InvariantCulture, $"Source {find.Index} of the run, minimized from {SourceTokens.Lines(find.Original).Count} lines to {SourceTokens.Lines(find.Minimized).Count} in {find.Tests} tests.\n\n");
			text.Append(Fence(find.Minimized));
			text.Append('\n');
			text.Append(Fence(string.Join('\n', find.Disagreement.Lines)));
			text.Append(CultureInfo.InvariantCulture, $"\nAdd it to the corpus with `mise run fuzz-add <run folder>/{FindsFolder}/{find.Type}/{find.Name}.mmd`.\n");
		}

		return text.ToString();
	}

	/// <summary>A text fence longer than any run of backticks in the text.</summary>
	private static string Fence(string text)
	{
		var longest = 0;
		var run = 0;
		foreach (var c in text)
		{
			run = c == '`' ? run + 1 : 0;
			longest = Math.Max(longest, run);
		}

		var fence = new string('`', Math.Max(3, longest + 1));
		return $"{fence}text\n{text.TrimEnd('\n')}\n{fence}\n";
	}

	private static void WriteSource(string path, string source)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.WriteAllText(path, source);
	}
}
