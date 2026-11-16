using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace SplashTheMusical.Benchmarks;

/// <summary>
/// Compares the size of shipped binaries with the committed sizes. The committed file is a markdown table of file, runtime
/// identifier and bytes. The core assembly is IL, so its row has the identifier <c>any</c>.
/// </summary>
public static class SizeCheck
{
	/// <summary>A binary fails when it is more than this fraction larger than its committed size.</summary>
	public const double Tolerance = 0.05;

	private const string Header = "| File | RID | Bytes |";

	/// <summary>
	/// Measures each binary and compares it with its row, or with <paramref name="accept"/> rewrites the rows of the measured binaries.
	/// </summary>
	/// <param name="binaries">Each binary's name in the table, its runtime identifier and its path.</param>
	/// <returns>0 when every binary is within <see cref="Tolerance"/> of its row or the rows were rewritten, and 1 otherwise.</returns>
	public static int Run(string sizesPath, IReadOnlyList<(string Name, string Rid, string Path)> binaries, bool accept, TextWriter output)
	{
		var sizes = File.Exists(sizesPath) ? Read(sizesPath) : [];
		var failures = 0;
		foreach (var (name, rid, path) in binaries)
		{
			var actual = new FileInfo(path).Length;
			var key = (name, rid);
			if (accept)
			{
				sizes[key] = actual;
				output.WriteLine($"{name} ({rid}): {actual} bytes accepted");
				continue;
			}

			if (!sizes.TryGetValue(key, out var expected))
			{
				output.WriteLine($"{name} ({rid}): {actual} bytes, and {sizesPath} has no row for it");
				failures++;
				continue;
			}

			var change = (double)(actual - expected) / expected;
			var line = string.Create(CultureInfo.InvariantCulture, $"{name} ({rid}): {expected} -> {actual} bytes ({change:+0.0%;-0.0%;0.0%})");
			if (actual > expected * (1 + Tolerance))
			{
				output.WriteLine(line + $", more than {Tolerance:0%} larger");
				failures++;
			}
			else
			{
				output.WriteLine(line);
			}
		}

		if (accept)
		{
			Write(sizesPath, sizes);
			return 0;
		}

		if (failures > 0)
		{
			output.WriteLine("Accept the new sizes with: mise run size -- --accept");
		}

		return failures == 0 ? 0 : 1;
	}

	/// <summary>The runtime identifier of the running process, which is the one a plain <c>dotnet publish</c> targets.</summary>
	public static string HostRid => RuntimeInformation.RuntimeIdentifier;

	private static Dictionary<(string Name, string Rid), long> Read(string path)
	{
		var sizes = new Dictionary<(string Name, string Rid), long>();
		var inTable = false;
		foreach (var line in File.ReadLines(path))
		{
			if (line == Header)
			{
				inTable = true;
				continue;
			}

			if (!inTable || line.StartsWith("|---", StringComparison.Ordinal))
			{
				continue;
			}

			if (!line.StartsWith('|'))
			{
				break;
			}

			var cells = line.Trim('|').Split('|', StringSplitOptions.TrimEntries);
			sizes[(cells[0], cells[1])] = long.Parse(cells[2], CultureInfo.InvariantCulture);
		}

		return sizes;
	}

	private static void Write(string path, Dictionary<(string Name, string Rid), long> sizes)
	{
		var text = new StringBuilder();
		text.Append("# Binary sizes\n\n")
			.Append("The size of the core assembly, release build, and of the native AOT `stm` CLI. CI fails a change that grows either\n")
			.Append("by more than 5% over the size here. `mise run size` checks, and `mise run size -- --accept` rewrites the rows of\n")
			.Append("the machine it runs on. CI checks on each runner of its build job, which reads the `stm` rows of `linux-x64`,\n")
			.Append("`osx-arm64` and `win-x64`.\n\n")
			.Append(Header).Append("\n|---|---|---:|\n");
		foreach (var ((name, rid), bytes) in sizes.OrderBy(s => s.Key.Name, StringComparer.Ordinal).ThenBy(s => s.Key.Rid, StringComparer.Ordinal))
		{
			text.Append(CultureInfo.InvariantCulture, $"| {name} | {rid} | {bytes} |\n");
		}

		File.WriteAllText(path, text.ToString());
	}
}
