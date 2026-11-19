using System.Text;
using SplashTheMusical.Config;
using SplashTheMusical.Parsing;
using SplashTheMusical.Themes;

namespace SplashTheMusical.Conformance;

/// <summary>
/// <see cref="Theme"/> against corpus/themes.txt, the variables mermaid.js computes for each theme with no overrides, and
/// the theme variables of the corpus cases that override them.
/// </summary>
public class ThemeTests
{
	private const string Kind = "theme-variable";

	[Test]
	public void EveryThemeComputesTheVariablesOfThemesTxt()
	{
		var lines = File.ReadAllLines(Path.Combine(CorpusCheck.CorpusRoot, "themes.txt"));
		var oracle = lines
			.Where(line => line.StartsWith("theme\t", StringComparison.Ordinal))
			.GroupBy(line => line.Split('\t')[1], StringComparer.Ordinal)
			.ToList();
		var problems = new List<string>();
		foreach (var theme in oracle)
		{
			var stm = new List<string>();
			ConfigLines.Flatten(stm, theme.Key, "", Theme.Variables(theme.Key, null)!);
			Compare(problems, theme.Key, [.. theme], [.. stm.Select(line => "theme\t" + line)]);
		}

		Verify(!problems.Any(), $"{string.Join('\n', problems)}");
		Verify(() => lines[1] == "# mermaid " + CorpusCheck.PinnedMermaid());
		Verify(() => oracle.Count == 11);
	}

	/// <summary>
	/// The theme variables MermaidConfig.Resolve gives each accepted corpus case whose preamble sets themeVariables,
	/// against the oracle's <c>theme-variable</c> facts. The site config is the <c>initialize</c> options of
	/// tools/oracle/facts.mjs.
	/// </summary>
	[Test]
	public void EveryCaseThatSetsThemeVariablesGetsTheOracleVariables()
	{
		var site = MermaidValue.FromEntries(new OrderedDictionary<string, MermaidValue>
		{
			["startOnLoad"] = MermaidValue.False,
			["securityLevel"] = MermaidValue.FromString("strict"),
		});
		var checkedCases = 0;
		var problems = new List<string>();
		foreach (var name in CorpusCheck.Cases(CorpusCheck.CorpusRoot))
		{
			var facts = OracleFiles.Read(CorpusCheck.CorpusRoot, name);
			var oracle = facts.Facts.Where(f => f.StartsWith(Kind + "\t", StringComparison.Ordinal)).ToList();
			if (!facts.Accepted || oracle.Count == 0)
			{
				continue;
			}

			var source = Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(CorpusCheck.CorpusRoot, name + ".mmd")));
			var document = new PreambleDocument();
			if (!PreambleParser.TryParse(source, document, [], ResourceLimits.Unlimited, out _))
			{
				problems.Add($"{name}: the preamble does not parse");
				continue;
			}

			var type = facts.Facts.First(f => f.StartsWith("type\t", StringComparison.Ordinal))["type\t".Length..];
			var stm = new List<string>();
			ConfigLines.Flatten(stm, Kind, "", MermaidConfig.Resolve(site, document, type).ThemeVariables!);
			Compare(problems, name, oracle, stm);
			checkedCases++;
		}

		Verify(!problems.Any(), $"{string.Join('\n', problems)}");
		Verify(() => checkedCases >= 29);
	}

	[Test]
	public void AThemeNoThemeHasGivesNull()
	{
		Verify(() => Theme.Variables("null", null) is null);
		Verify(() => Theme.Variables("Default", null) is null);
	}

	/// <summary>Adds each line only one side has, under <paramref name="name"/>.</summary>
	public static void Compare(List<string> problems, string name, List<string> oracle, List<string> stm)
	{
		var expected = oracle.ToHashSet(StringComparer.Ordinal);
		var actual = stm.ToHashSet(StringComparer.Ordinal);
		foreach (var line in oracle.Where(line => !actual.Contains(line)))
		{
			problems.Add($"{name}: oracle {line}");
		}

		foreach (var line in stm.Where(line => !expected.Contains(line)))
		{
			problems.Add($"{name}: stm    {line}");
		}
	}

	private sealed class PreambleDocument : MermaidDocument
	{
		public override string DiagramType => "";
	}
}
