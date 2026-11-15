using System.Globalization;
using System.Text;

namespace SplashTheMusical.Benchmarks;

/// <summary>The mean time and the allocation of one stage on one case under one job.</summary>
/// <param name="Job"><c>JIT</c> or <c>AOT</c>.</param>
/// <param name="Case">The case id, such as <c>pie-small</c>.</param>
/// <param name="Stage">The stage, such as <c>parse</c> or <c>render</c>.</param>
/// <param name="Chars">The length of the source.</param>
/// <param name="Nanoseconds">The mean time per call.</param>
/// <param name="Bytes">The bytes allocated per call.</param>
public sealed record ResultRow(string Job, string Case, string Stage, int Chars, double Nanoseconds, long Bytes)
{
	/// <summary>The key two result files are joined on.</summary>
	public string Key => $"{Job} {Case} {Stage}";
}

/// <summary>
/// A result file is markdown: a title, a table of properties such as the machine, and the results table, which
/// <see cref="Read"/> parses back. The committed numbers and the output of a run have the same format.
/// </summary>
public static class ResultFile
{
	private const string Header = "| Job | Case | Stage | Chars | ns | Bytes | ns/char | Bytes/char |";

	/// <summary>Writes a result file.</summary>
	public static void Write(string path, string title, string preamble, IEnumerable<(string Name, string Value)> properties, IEnumerable<ResultRow> rows, string? appendix = null)
	{
		var text = new StringBuilder();
		text.Append("# ").Append(title).Append("\n\n").Append(preamble).Append("\n\n");
		text.Append("| Property | Value |\n|---|---|\n");
		foreach (var (name, value) in properties)
		{
			text.Append("| ").Append(name).Append(" | ").Append(value).Append(" |\n");
		}

		text.Append("\n## Results\n\n").Append(Header).Append("\n|---|---|---|---:|---:|---:|---:|---:|\n");
		foreach (var row in rows)
		{
			text.Append(CultureInfo.InvariantCulture,
				$"| {row.Job} | {row.Case} | {row.Stage} | {row.Chars} | {row.Nanoseconds:F1} | {row.Bytes} | {row.Nanoseconds / row.Chars:F2} | {(double)row.Bytes / row.Chars:F2} |\n");
		}

		if (appendix is not null)
		{
			text.Append('\n').Append(appendix).Append('\n');
		}

		Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
		File.WriteAllText(path, text.ToString());
	}

	/// <summary>The rows of the results table of a result file.</summary>
	public static IReadOnlyList<ResultRow> Read(string path)
	{
		var rows = new List<ResultRow>();
		var inTable = false;
		foreach (var line in File.ReadLines(path))
		{
			if (line == Header)
			{
				inTable = true;
				continue;
			}

			if (!inTable)
			{
				continue;
			}

			if (!line.StartsWith('|'))
			{
				break;
			}

			if (line.StartsWith("|---", StringComparison.Ordinal))
			{
				continue;
			}

			var cells = line.Trim('|').Split('|', StringSplitOptions.TrimEntries);
			if (cells.Length != 8)
			{
				throw new FormatException($"{path}: expected 8 cells in '{line}'");
			}

			rows.Add(new ResultRow(
				cells[0],
				cells[1],
				cells[2],
				int.Parse(cells[3], CultureInfo.InvariantCulture),
				double.Parse(cells[4], CultureInfo.InvariantCulture),
				long.Parse(cells[5], CultureInfo.InvariantCulture)));
		}

		return rows;
	}
}
