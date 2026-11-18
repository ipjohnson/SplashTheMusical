using System.Text;

namespace SplashTheMusical.Conformance.Fuzzing;

/// <summary>
/// mermaid.js's answers to the sources of a differential run, so a test can replay the run without Node. A recording
/// instance asks the live oracle and keeps each answer. A replaying instance answers from a file and counts the sources
/// the file has no answer for in <see cref="Misses"/>.
/// </summary>
/// <remarks>
/// The file holds the names of the run's seed cases, as <c>seed</c> lines, then one block per source: a <c>source</c>
/// line with the source in base64, the <c>accept</c> line, the <c>error</c> line or the L1 facts, and a <c>.</c> line.
/// A source the oracle gave no facts for has the line <c>none</c> in its block.
/// </remarks>
public sealed class RecordedOracle
{
	private const string NoFacts = "none";

	private readonly Func<string, CaseFacts?>? _live;
	private readonly Dictionary<string, CaseFacts?> _answers = new(StringComparer.Ordinal);
	private readonly List<string> _order = [];

	private RecordedOracle(Func<string, CaseFacts?>? live, IReadOnlyList<string> seeds)
	{
		_live = live;
		Seeds = seeds;
	}

	/// <summary>The names of the seed cases of the recorded run.</summary>
	public IReadOnlyList<string> Seeds { get; }

	/// <summary>The sources a replaying instance has no answer for.</summary>
	public int Misses { get; private set; }

	public static RecordedOracle Recording(Func<string, CaseFacts?> live, IReadOnlyList<string> seeds) => new(live, seeds);

	public static RecordedOracle Read(string path)
	{
		var seeds = new List<string>();
		var oracle = new RecordedOracle(null, seeds);
		string? source = null;
		var lines = new List<string>();
		foreach (var line in File.ReadLines(path))
		{
			if (line.StartsWith("seed\t", StringComparison.Ordinal))
			{
				seeds.Add(line["seed\t".Length..]);
			}
			else if (line.StartsWith("source\t", StringComparison.Ordinal))
			{
				source = Encoding.UTF8.GetString(Convert.FromBase64String(line["source\t".Length..]));
				lines.Clear();
			}
			else if (line == "." && source is not null)
			{
				oracle._answers[source] = lines is [NoFacts] ? null : OracleProcess.Parse(lines);
				source = null;
			}
			else
			{
				lines.Add(line);
			}
		}

		return oracle;
	}

	/// <summary>mermaid.js's facts of the source, or null when it gives none or a replaying instance has no answer.</summary>
	public CaseFacts? Facts(string source)
	{
		if (_answers.TryGetValue(source, out var answer))
		{
			return answer;
		}

		if (_live is null)
		{
			Misses++;
			return null;
		}

		answer = _live(source);
		_answers[source] = answer;
		_order.Add(source);
		return answer;
	}

	/// <summary>Writes the seeds and the answers in the order they were asked.</summary>
	public void Write(string path)
	{
		var text = new StringBuilder();
		foreach (var seed in Seeds)
		{
			_ = text.Append("seed\t").Append(seed).Append('\n');
		}

		foreach (var source in _order)
		{
			_ = text.Append("source\t").Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(source))).Append('\n');
			if (_answers[source] is not { } facts)
			{
				_ = text.Append(NoFacts).Append('\n');
			}
			else
			{
				_ = text.Append(facts.Accepted ? "accept\tyes" : "accept\tno").Append('\n');
				if (facts.Error is { } error)
				{
					_ = text.Append("# error ").Append(error).Append('\n');
				}

				foreach (var line in Comparer.Lines(Level.L1, facts))
				{
					_ = text.Append(line).Append('\n');
				}
			}

			_ = text.Append(".\n");
		}

		File.WriteAllText(path, text.ToString());
	}
}
