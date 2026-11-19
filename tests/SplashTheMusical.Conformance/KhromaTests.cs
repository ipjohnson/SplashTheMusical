using System.Globalization;
using SplashTheMusical.Themes;

namespace SplashTheMusical.Conformance;

/// <summary><see cref="Khroma"/> against corpus/colors.txt, the oracle's calls of khroma on mermaid's theme colours.</summary>
public class KhromaTests
{
	[Test]
	public void EveryCallMatchesKhroma()
	{
		var lines = File.ReadAllLines(Path.Combine(CorpusCheck.CorpusRoot, "colors.txt"));
		var problems = new List<string>();
		var calls = new Dictionary<string, int>(StringComparer.Ordinal);
		foreach (var line in lines.Where(line => !line.StartsWith('#')))
		{
			var fields = line.Split('\t');
			var (function, color, arguments, expected) = (fields[0], fields[1], fields[2], fields[3]);
			calls[function] = calls.GetValueOrDefault(function) + 1;
			string actual;
			try
			{
				actual = Call(function, color, arguments);
			}
			catch (Exception e) when (e is FormatException or InvalidOperationException)
			{
				actual = "error: " + e.Message;
			}

			if (actual != expected)
			{
				problems.Add($"{function}({color}, {arguments}): expected {expected}, got {actual}");
			}
		}

		Verify(!problems.Take(40).Any(), $"{string.Join('\n', problems.Take(40))}");
		Verify(lines[1] == "# mermaid " + CorpusCheck.PinnedMermaid(), $"was {lines[1]}");
		foreach (var function in new[] { "adjust", "darken", "lighten", "invert", "isDark", "rgba", "mkBorder" })
		{
			Verify(calls.GetValueOrDefault(function) > 100, $"was {calls.GetValueOrDefault(function)}");
		}
	}

	private static string Call(string function, string color, string arguments) => function switch
	{
		"adjust" => Khroma.Adjust(color, Channels(arguments)),
		"darken" => Khroma.Darken(color, Number(arguments)),
		"lighten" => Khroma.Lighten(color, Number(arguments)),
		"invert" => Khroma.Invert(color),
		"isDark" => Khroma.IsDark(color) ? "true" : "false",
		"mkBorder" => Khroma.MkBorder(color, arguments == "true"),
		"rgba" when color.Length > 0 => Khroma.Rgba(color, Number(arguments)),
		"rgba" => Rgba(arguments.Split(',').Select(Number).ToArray()),
		_ => throw new ArgumentException($"colors.txt has a function KhromaTests does not know: {function}"),
	};

	private static string Rgba(double[] channels) => channels.Length switch
	{
		2 => Khroma.Rgba(channels[0], channels[1]),
		3 => Khroma.Rgba(channels[0], channels[1], channels[2]),
		_ => Khroma.Rgba(channels[0], channels[1], channels[2], channels[3]),
	};

	// h=-10,l=5
	private static KhromaChannels Channels(string arguments)
	{
		var channels = new KhromaChannels();
		foreach (var pair in arguments.Split(',', StringSplitOptions.RemoveEmptyEntries))
		{
			var value = Number(pair[2..]);
			channels = pair[0] switch
			{
				'h' => channels with { H = value },
				's' => channels with { S = value },
				'l' => channels with { L = value },
				'r' => channels with { R = value },
				'g' => channels with { G = value },
				'b' => channels with { B = value },
				_ => channels with { A = value },
			};
		}

		return channels;
	}

	// JavaScript's String(number), and undefined for a missing argument.
	private static double Number(string text) =>
		text == "undefined" ? double.NaN : double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
}
