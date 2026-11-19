using SplashTheMusical.Config;

namespace SplashTheMusical.Conformance;

/// <summary>A new <see cref="MermaidConfig"/> against corpus/config.txt, the oracle's dump of mermaid.js's default config.</summary>
public class DefaultConfigTests
{
	private const string KeyPrefix = "key\t";

	/// <summary>The paths of config.txt that MermaidConfig does not hold, by prefix, with the reason.</summary>
	private static readonly (string Prefix, string Reason)[] Ignored =
	[
		("themeVariables.", "the default theme's variables, which the themes compute"),
		("c4.flowchart.", "defaultConfig.ts copies the flowchart config into c4, which the schema does not allow"),
		("treemap.", "defaultConfig.ts sets treemap, which is not in the schema"),
		("xyChart.xAxis.$ref\t", "the schema's xAxis default is { $ref: ... }, which ajv copies as a value"),
		("xyChart.yAxis.$ref\t", "the schema's yAxis default is { $ref: ... }, which ajv copies as a value"),
	];

	[Test]
	public void TheDefaultConfigIsMermaidsDefaultConfig()
	{
		var oracle = File.ReadLines(Path.Combine(CorpusCheck.CorpusRoot, "config.txt"))
			.Where(line => line.StartsWith(KeyPrefix, StringComparison.Ordinal))
			.ToList();

		// A null property is both an undefined key and a key set to null.
		var expected = oracle
			.Where(line => !Ignored.Any(i => line.AsSpan(KeyPrefix.Length).StartsWith(i.Prefix, StringComparison.Ordinal)))
			.Where(line => !line.EndsWith("\tundefined", StringComparison.Ordinal) && !line.EndsWith("\tnull", StringComparison.Ordinal))
			.ToHashSet(StringComparer.Ordinal);
		var actual = new List<string>();
		ConfigLines.Flatten(actual, "key", "", new MermaidConfig().ToValue());

		var problems = new List<string>();
		problems.AddRange(expected.Except(actual, StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(line => "missing: " + line));
		problems.AddRange(actual.Except(expected, StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(line => "extra:   " + line));
		foreach (var (prefix, reason) in Ignored)
		{
			if (!oracle.Any(line => line.AsSpan(KeyPrefix.Length).StartsWith(prefix, StringComparison.Ordinal)))
			{
				problems.Add($"stale: config.txt has no {prefix} key, so the entry for {reason} can go");
			}
		}

		Verify(!problems.Any(), $"{string.Join('\n', problems)}");
		Verify(() => expected.Count >= 524);
	}
}
