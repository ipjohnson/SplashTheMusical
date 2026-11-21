using System.Security.Cryptography;
using System.Text;

namespace SplashTheMusical.Benchmarks;

/// <summary>One source of the benchmark set.</summary>
/// <param name="Type">The corpus folder of the diagram type.</param>
/// <param name="Size"><c>small</c>, <c>medium</c> or <c>large</c>.</param>
/// <param name="CorpusCase">The corpus path of the case, without the .mmd extension.</param>
/// <param name="Source">The text of the case.</param>
public sealed record BenchmarkCase(string Type, string Size, string CorpusCase, string Source)
{
	/// <summary>The name a result row carries, such as <c>pie-small</c>.</summary>
	public string Id => Type + "-" + Size;
}

/// <summary>
/// The fixed benchmark set: a small, a medium and a large case of each diagram type, named in <c>corpus/benchmarks/cases.txt</c>.
/// The cases stay in their corpus folders, because the oracle and the harness treat every .mmd under <c>corpus</c> as a case.
/// </summary>
public static class BenchmarkSet
{
	/// <summary>The manifest, relative to the repository root.</summary>
	public const string Manifest = "corpus/benchmarks/cases.txt";

	/// <summary>The sizes, in the order the manifest lists them.</summary>
	public static readonly string[] Sizes = ["small", "medium", "large"];

	/// <summary>Reads the manifest and the cases it names, and fails when a case's text no longer matches its recorded hash.</summary>
	public static IReadOnlyList<BenchmarkCase> Load()
	{
		var corpus = Repository.Resolve("corpus");
		var cases = new List<BenchmarkCase>();
		foreach (var line in File.ReadLines(Repository.Resolve(Manifest)))
		{
			if (line.Length == 0 || line[0] == '#')
			{
				continue;
			}

			var cells = line.Split('\t');
			if (cells.Length != 4)
			{
				throw new FormatException($"{Manifest}: expected type, size, case and hash in '{line}'");
			}

			var source = Read(corpus, cells[2]);
			if (Hash(source) != cells[3])
			{
				throw new InvalidOperationException($"{Manifest}: {cells[2]} hashes to {Hash(source)}, not {cells[3]}. Run mise run bench-select.");
			}

			cases.Add(new BenchmarkCase(cells[0], cells[1], cells[2], source));
		}

		return cases;
	}

	/// <summary>The environment variable that limits a run to the cases whose id contains it, such as <c>pie</c> or <c>-small</c>.</summary>
	public const string FilterVariable = "STM_BENCH_CASES";

	/// <summary>The cases a run measures: the set, or the part of it <see cref="FilterVariable"/> names.</summary>
	public static IEnumerable<BenchmarkCase> Selected() =>
		Environment.GetEnvironmentVariable(FilterVariable) is { Length: > 0 } filter
			? Load().Where(c => c.Id.Contains(filter, StringComparison.Ordinal))
			: Load();

	/// <summary>
	/// The environment variable that limits a run to the <c>case/stage</c> parameters it lists, separated by commas, such as
	/// <c>flowchart-small/parse,state-medium/parse</c>. The budget check sets it to measure the rows it doubts again.
	/// </summary>
	public const string ParameterVariable = "STM_BENCH_PARAMETERS";

	/// <summary>Whether a run measures a <c>case/stage</c> parameter, which it does unless <see cref="ParameterVariable"/> leaves it out.</summary>
	public static bool Measures(string parameter) => Measures(Environment.GetEnvironmentVariable(ParameterVariable), parameter);

	/// <summary>Whether a list of <c>case/stage</c> parameters, as <see cref="ParameterVariable"/> holds it, keeps a parameter.</summary>
	/// <param name="list">The comma-separated list, or null or empty for every parameter.</param>
	/// <param name="parameter">A parameter such as <c>flowchart-small/parse</c>.</param>
	public static bool Measures(string? list, string parameter) =>
		string.IsNullOrEmpty(list) || list.Split(',').Contains(parameter, StringComparer.Ordinal);

	/// <summary>The case with an id such as <c>pie-small</c>.</summary>
	public static BenchmarkCase Find(string id) =>
		Load().FirstOrDefault(c => c.Id == id) ?? throw new ArgumentException($"{Manifest} has no case {id}", nameof(id));

	/// <summary>The text of a corpus case, decoded as the oracle reads it.</summary>
	public static string Read(string corpus, string name) =>
		Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(corpus, name.Replace('/', Path.DirectorySeparatorChar) + ".mmd")));

	/// <summary>The first 16 hex digits of the SHA-256 of the text with CRLF folded to LF, as the oracle's facts files record it.</summary>
	public static string Hash(string source) =>
		Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(source.Replace("\r\n", "\n", StringComparison.Ordinal))))[..16];

	/// <summary>
	/// Picks the set from the corpus and rewrites the manifest. For each type it takes the cases mermaid.js accepts, prefers the ones
	/// <paramref name="baseline"/> renders, orders them by length and picks the 10th, 50th and 95th percentile.
	/// </summary>
	/// <returns>The number of cases written.</returns>
	public static int Select(Func<string, bool> baseline)
	{
		var corpus = Repository.Resolve("corpus");
		var lines = new List<string>
		{
			"# The benchmark set: a small, a medium and a large case of each diagram type, by corpus path.",
			"# Rewrite with: mise run bench-select. The hash is the one the case's facts file records.",
			"# type\tsize\tcase\thash",
		};

		foreach (var folder in Directory.EnumerateDirectories(corpus).Order(StringComparer.Ordinal))
		{
			var type = Path.GetFileName(folder);
			var accepted = Directory.EnumerateFiles(folder, "*.mmd")
				.Select(f => type + "/" + Path.GetFileNameWithoutExtension(f))
				.Where(name => Accepted(corpus, name))
				.Select(name => (Name: name, Source: Read(corpus, name)))
				.OrderBy(c => c.Source.Length)
				.ThenBy(c => c.Name, StringComparer.Ordinal)
				.ToList();
			if (accepted.Count == 0)
			{
				continue;
			}

			var rendered = accepted.Where(c => baseline(c.Source)).ToList();
			var pool = rendered.Count > 0 ? rendered : accepted;
			int[] percentiles = [10, 50, 95];
			for (var i = 0; i < Sizes.Length; i++)
			{
				var pick = pool[Math.Min(pool.Count - 1, pool.Count * percentiles[i] / 100)];
				lines.Add($"{type}\t{Sizes[i]}\t{pick.Name}\t{Hash(pick.Source)}");
			}
		}

		var manifest = Repository.Resolve(Manifest);
		Directory.CreateDirectory(Path.GetDirectoryName(manifest)!);
		File.WriteAllText(manifest, string.Join('\n', lines) + "\n");
		return lines.Count - 3;
	}

	private static bool Accepted(string corpus, string name)
	{
		var facts = Path.Combine(corpus, name.Replace('/', Path.DirectorySeparatorChar) + ".facts.txt");
		return File.Exists(facts) && File.ReadLines(facts).Any(line => line == "accept\tyes");
	}
}
