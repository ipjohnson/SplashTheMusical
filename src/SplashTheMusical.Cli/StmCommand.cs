using System.Text;
using System.Text.Json;
using SplashTheMusical.Themes;

namespace SplashTheMusical.Cli;

/// <summary>The <c>stm</c> command. Arguments are parsed by hand, so the native binary needs no reflection.</summary>
public static class StmCommand
{
	public const string Usage = """
		usage: stm render [input] [-o output] [--theme name] [--config file]
		       stm parse [input]
		       stm format [input] [-o output]
		       stm version

		render  parses the diagram and writes its SVG
		parse   prints the diagnostics of the diagram, and exits with 1 when it does not parse
		format  parses the diagram and writes it back as Mermaid text
		version prints the STM version and the mermaid.js version it was verified against

		input is a .mmd file, or stdin when it is missing or -. Output goes to stdout unless -o names a file.
		--theme names a mermaid theme, such as dark. --config names a JSON file with mermaid's site config, as
		mermaid.initialize takes it. --theme overrides its theme.
		""";

	private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

	/// <returns>0 on success, 1 when the diagram or a file is refused, and 2 on a usage error.</returns>
	public static int Run(IReadOnlyList<string> args, Func<TextReader> stdin, TextWriter stdout, TextWriter stderr)
	{
		if (args.Count == 0)
		{
			return UsageError(stderr, null);
		}

		var command = args[0];
		if (command is "-h" or "--help" or "help")
		{
			stdout.Write(Usage.ReplaceLineEndings("\n") + "\n");
			return 0;
		}

		if (command == "version")
		{
			if (args.Count > 1)
			{
				return UsageError(stderr, "version takes no arguments");
			}

			stdout.Write($"stm {StmVersion.Value}\nmermaid.js {MermaidCompatibility.MermaidJsVersion}\n");
			return 0;
		}

		if (command is not ("render" or "parse" or "format"))
		{
			return UsageError(stderr, $"unknown command '{command}'");
		}

		if (Options.Parse(command, args, out var error) is not { } options)
		{
			return UsageError(stderr, error);
		}

		try
		{
			return Execute(command, options, stdin, stdout, stderr);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or MermaidResourceLimitException)
		{
			stderr.Write($"stm: {e.Message}\n");
			return 1;
		}
	}

	private static int Execute(string command, Options options, Func<TextReader> stdin, TextWriter stdout, TextWriter stderr)
	{
		MermaidValue? site = null;
		if (options.Config is not null || options.Theme is not null)
		{
			if (SiteConfig(options.Config, options.Theme, stderr) is not { } config)
			{
				return 1;
			}

			site = config;
		}

		var source = options.Input is null or "-" ? stdin().ReadToEnd() : File.ReadAllText(options.Input, Utf8);
		var mermaid = new Mermaid(new MermaidOptions { SiteConfig = site });
		var parsed = mermaid.TryParse(source, out var document, out var diagnostics);
		var report = command == "parse" ? stdout : stderr;
		foreach (var diagnostic in diagnostics)
		{
			report.Write(diagnostic + "\n");
		}

		if (!parsed)
		{
			return 1;
		}

		if (command == "parse")
		{
			return 0;
		}

		var text = command == "render" ? mermaid.RenderSvg(document!) + "\n" : mermaid.Write(document!);
		if (options.Output is null or "-")
		{
			stdout.Write(text);
		}
		else
		{
			File.WriteAllText(options.Output, text, Utf8);
		}

		return 0;
	}

	/// <summary>The JSON object of the config file, with <c>theme</c> set to <paramref name="theme"/> when it is given.</summary>
	private static MermaidValue? SiteConfig(string? path, string? theme, TextWriter stderr)
	{
		var entries = new OrderedDictionary<string, MermaidValue>(StringComparer.Ordinal);
		if (path is not null)
		{
			using var json = JsonDocument.Parse(
				File.ReadAllBytes(path),
				new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }
			);
			if (json.RootElement.ValueKind != JsonValueKind.Object)
			{
				stderr.Write($"stm: {path} is not a JSON object\n");
				return null;
			}

			foreach (var property in json.RootElement.EnumerateObject())
			{
				entries[property.Name] = Value(property.Value);
			}
		}

		if (theme is not null)
		{
			if (!MermaidThemes.Default.HasTheme(theme))
			{
				stderr.Write($"stm: no theme is named '{theme}'\n");
				return null;
			}

			entries["theme"] = MermaidValue.FromString(theme);
		}

		return MermaidValue.FromEntries(entries);
	}

	private static MermaidValue Value(JsonElement element) =>
		element.ValueKind switch
		{
			JsonValueKind.Object => MermaidValue.FromEntries(
				new OrderedDictionary<string, MermaidValue>(element.EnumerateObject().Select(p => KeyValuePair.Create(p.Name, Value(p.Value))), StringComparer.Ordinal)
			),
			JsonValueKind.Array => MermaidValue.FromItems([.. element.EnumerateArray().Select(Value)]),
			JsonValueKind.String => MermaidValue.FromString(element.GetString()!),
			JsonValueKind.Number => MermaidValue.FromNumber(element.GetDouble()),
			JsonValueKind.True => MermaidValue.True,
			JsonValueKind.False => MermaidValue.False,
			_ => MermaidValue.Null,
		};

	private static int UsageError(TextWriter stderr, string? message)
	{
		if (message is not null)
		{
			stderr.Write($"stm: {message}\n");
		}

		stderr.Write(Usage.ReplaceLineEndings("\n") + "\n");
		return 2;
	}

	private sealed record Options(string? Input, string? Output, string? Theme, string? Config)
	{
		public static Options? Parse(string command, IReadOnlyList<string> args, out string? error)
		{
			string? input = null, output = null, theme = null, config = null;
			error = null;
			for (var i = 1; i < args.Count && error is null; i++)
			{
				var arg = args[i];
				switch (arg)
				{
					case "-o" or "--output" when command != "parse":
						error = Next(args, ref i, ref output);
						break;
					case "--theme" when command == "render":
						error = Next(args, ref i, ref theme);
						break;
					case "--config" when command == "render":
						error = Next(args, ref i, ref config);
						break;
					case ['-', _, ..]:
						error = $"{command} has no option {arg}";
						break;
					default:
						error = input is null ? null : $"{command} takes one input";
						input = arg;
						break;
				}
			}

			return error is null ? new Options(input, output, theme, config) : null;
		}

		// The value after the option at i, or the error when it is missing or the option was given before.
		private static string? Next(IReadOnlyList<string> args, ref int i, ref string? value)
		{
			var name = args[i];
			if (i + 1 >= args.Count)
			{
				return $"{name} needs a value";
			}

			if (value is not null)
			{
				return $"{name} is given twice";
			}

			value = args[++i];
			return null;
		}
	}
}
