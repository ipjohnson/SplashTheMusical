using System.Text;
using SplashTheMusical.Config;
using SplashTheMusical.Parsing;

namespace SplashTheMusical.Conformance;

/// <summary>
/// The config STM applies to every accepted corpus case against the oracle's <c>config</c> facts: the paths where the
/// config mermaid.js drew the case with differs from its site config. The oracle's site config is the
/// <c>initialize</c> options of tools/oracle/facts.mjs.
/// </summary>
public class AppliedConfigTests
{
	private const string Kind = "config";

	/// <summary>
	/// The paths of config facts that MermaidConfig does not hold, with the reason. A path that ends in a dot is a prefix.
	/// </summary>
	private static readonly (string Path, string Reason)[] NotBound =
	[
		("treemap.", "defaultConfig.ts sets treemap, which is not in the schema"),
		("sequence.fontSize", "the sequence schema has no fontSize, which sanitizeDirective keeps because other sections have it"),
		("sequence.fontFamily", "the sequence schema has no fontFamily, which sanitizeDirective keeps because other sections have it"),
		("sequence.fontWeight", "the sequence schema has no fontWeight, which sanitizeDirective keeps because other sections have it"),
		("sequence.sequence.", "the sequence schema has no sequence, which sanitizeDirective keeps because the top level has it"),
	];

	private static readonly MermaidValue OracleSite = MermaidValue.FromEntries(new OrderedDictionary<string, MermaidValue>
	{
		["startOnLoad"] = MermaidValue.False,
		["securityLevel"] = MermaidValue.FromString("strict"),
	});

	[Test]
	public void EveryCaseMergesToTheConfigTheOracleRecorded()
	{
		var site = ConfigMerge.Site(OracleSite);
		var checkedCases = 0;
		var problems = new List<string>();
		foreach (var (name, type, document, oracle) in Cases.Value)
		{
			var applied = ConfigMerge.Apply(OracleSite, PreambleConfig.Resolve(document, type), type);
			Compare(problems, name, oracle, Facts(site, applied));
			if (oracle.Count > 0)
			{
				checkedCases++;
			}
		}

		Verify(!problems.Any(), $"{string.Join('\n', problems)}");
		Verify(() => checkedCases >= 3258);
	}

	[Test]
	public void EveryCaseResolvesToAMermaidConfigWithTheValuesTheOracleRecorded()
	{
		var site = MermaidConfig.FromValue(ConfigMerge.Site(OracleSite)).ToValue();
		var checkedCases = 0;
		var notBoundSeen = new HashSet<string>(StringComparer.Ordinal);
		var problems = new List<string>();
		foreach (var (name, type, document, oracle) in Cases.Value)
		{
			var stm = Facts(site, MermaidConfig.Resolve(OracleSite, document, type).ToValue());
			var expected = new List<string>();
			foreach (var line in oracle)
			{
				var fields = line.Split('\t', 3);
				var notBound = NotBound.FirstOrDefault(n => n.Path.EndsWith('.') ? fields[1].StartsWith(n.Path, StringComparison.Ordinal) : fields[1] == n.Path).Path;
				if (notBound is not null)
				{
					notBoundSeen.Add(notBound);
				}
				else
				{
					expected.Add(NumberRead(fields, stm));
				}
			}

			Compare(problems, name, expected, stm);
			if (expected.Count > 0)
			{
				checkedCases++;
			}
		}

		foreach (var (path, reason) in NotBound)
		{
			if (!notBoundSeen.Contains(path))
			{
				problems.Add($"stale: no config fact has a {path} path, so the entry for {reason} can go");
			}
		}

		Verify(!problems.Any(), $"{string.Join('\n', problems)}");
		Verify(() => checkedCases >= 3249);
	}

	[Test]
	public void ConfigKeysAreTheNamesOfConfigTxt()
	{
		var names = File.ReadLines(Path.Combine(CorpusCheck.CorpusRoot, "config.txt"))
			.Where(line => line.StartsWith("name\t", StringComparison.Ordinal))
			.Select(line => line["name\t".Length..])
			.Order(StringComparer.Ordinal);
		LineAssert.Equal(ConfigKeys.All, names);
	}

	[Test]
	public void EveryThemeOfThemesTxtIsATheme()
	{
		var themes = File.ReadLines(Path.Combine(CorpusCheck.CorpusRoot, "themes.txt"))
			.Where(line => line.StartsWith("theme\t", StringComparison.Ordinal))
			.Select(line => line.Split('\t')[1])
			.Distinct()
			.ToList();
		var problems = new List<string>();
		foreach (var theme in themes.Append("null"))
		{
			var options = MermaidValue.FromEntries(new OrderedDictionary<string, MermaidValue> { ["theme"] = MermaidValue.FromString(theme) });
			if (ConfigMerge.Site(options)["theme"]?.String != theme)
			{
				problems.Add(theme);
			}
		}

		Verify(!problems.Any(), $"{string.Join(", ", problems)}");
		Verify(() => themes.Count == 11);
	}

	/// <summary>
	/// An oracle line with a string value, as STM's line when STM holds the number the string reads as. A number property
	/// reads a numeric string as its number.
	/// </summary>
	private static string NumberRead(string[] fields, List<string> stm)
	{
		var line = string.Join('\t', fields);
		if (fields.Length < 3 || !fields[2].StartsWith('"'))
		{
			return line;
		}

		var number = ConfigValues.ReadNumber(MermaidValue.FromString(fields[2].Trim('"')));
		var read = number is { } n ? ConfigLines.Line(Kind, fields[1], JsNumber.ToString(n)) : null;
		return read is not null && stm.Contains(read) ? read : line;
	}

	/// <summary>The cases of <see cref="ReadCases"/>, read and parsed once for both tests, which only read them.</summary>
	private static readonly Lazy<IReadOnlyList<(string Name, string Type, MermaidDocument Document, List<string> Oracle)>> Cases =
		new(() => [.. ReadCases()]);

	/// <summary>
	/// Each accepted case with its diagram type, its preamble and its oracle config facts. A <c>missing</c> fact for a
	/// path that config.txt holds as <c>undefined</c> is left out, because a MermaidValue has no undefined: the site
	/// config's <c>sankey.nodeColors</c> is undefined, and a case that sets colours under it gets that fact.
	/// </summary>
	private static IEnumerable<(string Name, string Type, MermaidDocument Document, List<string> Oracle)> ReadCases()
	{
		var undefined = File.ReadLines(Path.Combine(CorpusCheck.CorpusRoot, "config.txt"))
			.Where(line => line.StartsWith("key\t", StringComparison.Ordinal) && line.EndsWith("\tundefined", StringComparison.Ordinal))
			.Select(line => ConfigLines.Line(Kind, line.Split('\t')[1], "missing"))
			.ToHashSet(StringComparer.Ordinal);
		foreach (var name in CorpusCheck.Cases(CorpusCheck.CorpusRoot))
		{
			var facts = OracleFiles.Read(CorpusCheck.CorpusRoot, name);
			if (!facts.Accepted)
			{
				continue;
			}

			var source = Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(CorpusCheck.CorpusRoot, name + ".mmd")));
			var document = new PreambleDocument();
			if (!PreambleParser.TryParse(source, document, [], ResourceLimits.Unlimited, out _))
			{
				continue;
			}

			var type = facts.Facts.First(f => f.StartsWith("type\t", StringComparison.Ordinal))["type\t".Length..];
			yield return (name, type, document, [.. facts.Facts.Where(f => f.StartsWith(Kind + "\t", StringComparison.Ordinal) && !undefined.Contains(f))]);
		}
	}

	private static List<string> Facts(MermaidValue site, MermaidValue applied) => ConfigLines.ConfigFacts(site, applied);

	private static void Compare(List<string> problems, string name, List<string> oracle, List<string> stm)
	{
		var expected = string.Join('\n', oracle.Order(StringComparer.Ordinal));
		var actual = string.Join('\n', stm.Order(StringComparer.Ordinal));
		if (expected != actual)
		{
			problems.Add($"{name}:\n  oracle:\n    {expected.Replace("\n", "\n    ", StringComparison.Ordinal)}\n  stm:\n    {actual.Replace("\n", "\n    ", StringComparison.Ordinal)}");
		}
	}

	private sealed class PreambleDocument : MermaidDocument
	{
		public override string DiagramType => "";
	}
}
