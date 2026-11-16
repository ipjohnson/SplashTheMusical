using System.Buffers;
using System.Globalization;
using SplashTheMusical.Parsing;
using SplashTheMusical.Themes;

namespace SplashTheMusical.Config;

/// <summary>
/// The config mermaid.js draws a diagram with, as <c>packages/mermaid/src/config.ts</c> builds it in <c>render</c>:
/// the default config, then the site config of <c>initialize</c>, then the front matter and directives that
/// <c>addDirective</c> takes as one directive. Values are JavaScript objects there and <see cref="MermaidValue"/> trees
/// here. A missing key is <c>undefined</c> and <see cref="MermaidValue.Null"/> is <c>null</c>.
/// </summary>
internal static class ConfigMerge
{
	/// <summary>The value of <c>sanitizeCss</c> for CSS whose braces do not balance.</summary>
	private const string UnbalancedCss = "{ /* ERROR: Unbalanced CSS */ }";

	private static readonly string[] AppearanceKeys = ["theme", "look", "layout"];

	/// <summary>The keys of <c>packages/mermaid/src/themes/index.js</c>, sorted.</summary>
	private static readonly string[] ThemeNames =
		["base", "dark", "default", "forest", "neo", "neo-dark", "neutral", "redux", "redux-color", "redux-dark", "redux-dark-color"];

	private static readonly SearchValues<char> HexDigits = SearchValues.Create("0123456789abcdefABCDEF");

	private static readonly MermaidValue Defaults = new MermaidConfig().ToValue();

	/// <summary>
	/// The config of a diagram of <paramref name="diagramType"/>, the id of mermaid.js's detector, whose front matter and
	/// directives give <paramref name="preamble"/>. <paramref name="siteOptions"/> is what the host hands to
	/// <c>initialize</c>.
	/// </summary>
	/// <exception cref="FormatException">A theme variable that the theme passes to a colour function is not a colour.</exception>
	public static MermaidValue Apply(MermaidValue? siteOptions, MermaidValue preamble, string diagramType)
	{
		var (site, siteDelta, fromInitialize) = Initialize(siteOptions);
		var directive = AddDirective(preamble);
		var config = Assign(Empty(), site);
		directive = Sanitize(directive, Secure(site));
		var sumOfDirectives = Assign(Empty(), directive);
		config = Assign(config, sumOfDirectives);
		config = ResolveAppearance(config, DiagramConfigKey(diagramType), [sumOfDirectives, siteDelta, Defaults]);
		return ApplyTheme(config, site, sumOfDirectives, fromInitialize);
	}

	/// <summary>The site config <c>initialize</c> sets: the default config with the host's options over it.</summary>
	/// <exception cref="FormatException">A theme variable that the theme passes to a colour function is not a colour.</exception>
	public static MermaidValue Site(MermaidValue? siteOptions) => Initialize(siteOptions).Site;

	/// <summary>
	/// <c>initialize</c> and <c>setSiteConfig</c>. A theme name no theme answers to becomes <c>default</c>, except
	/// <c>"null"</c>, and the theme variables are those the theme computes from the options' theme variables, with the
	/// <c>default</c> theme for a name no theme answers to. The delta is the options without the defaults, which
	/// <c>resolveAppearance</c> reads. <c>FromInitialize</c> is the options before the theme computes the variables.
	/// </summary>
	private static (MermaidValue Site, MermaidValue Delta, MermaidValue FromInitialize) Initialize(MermaidValue? siteOptions)
	{
		var options = siteOptions is { Kind: MermaidValueKind.Mapping } ? Assign(Empty(), siteOptions) : Empty();
		options = CopyFontFamily(options);
		var fromInitialize = Assign(Empty(), options);
		var theme = options["theme"];
		if (theme is not null && Truthy(theme) && IsTheme(theme))
		{
			options = With(options, "themeVariables", Theme.Variables(theme.String, options["themeVariables"])!);
		}
		else
		{
			if (theme is not null && theme.Kind != MermaidValueKind.Null && !IsString(theme, "null"))
			{
				options = With(options, "theme", MermaidValue.FromString("default"));
			}

			options = With(options, "themeVariables", Theme.Variables("default", options["themeVariables"])!);
		}

		var site = Assign(Assign(Empty(), Defaults), options);
		return (site, Assign(Empty(), options), fromInitialize);
	}

	/// <summary>
	/// The end of <c>updateCurrentConfig</c>: when the directives set a theme or the resolved theme is not the site's,
	/// the resolved theme computes the variables from those of <c>initialize</c> with the directives' over them.
	/// Otherwise the variables stay the site's with the directives' merged in, and nothing is derived from them.
	/// </summary>
	private static MermaidValue ApplyTheme(MermaidValue config, MermaidValue site, MermaidValue sumOfDirectives, MermaidValue fromInitialize)
	{
		var theme = config["theme"];
		var overridden = (sumOfDirectives["theme"] is { } set && Truthy(set)) || !Equals(theme, site["theme"]);
		if (!overridden || theme is null || !IsTheme(theme))
		{
			return config;
		}

		var initialized = Assign(Empty(), fromInitialize)["themeVariables"];
		var overrides = initialized is not null && Truthy(initialized) ? initialized : Empty();
		if (sumOfDirectives["themeVariables"] is { } directed)
		{
			overrides = Assign(overrides, directed);
		}

		return With(config, "themeVariables", Theme.Variables(theme.String, overrides)!);
	}

	/// <summary><c>addDirective</c>: <c>sanitizeDirective</c>, then the font family copied into the theme variables.</summary>
	private static MermaidValue AddDirective(MermaidValue preamble) =>
		CopyFontFamily(SanitizeDirective(preamble) is { Kind: MermaidValueKind.Mapping } directive ? directive : Empty());

	/// <summary>A truthy <c>fontFamily</c> becomes <c>themeVariables.fontFamily</c> unless that is truthy already.</summary>
	private static MermaidValue CopyFontFamily(MermaidValue config)
	{
		if (config["fontFamily"] is not { } fontFamily || !Truthy(fontFamily) || (config["themeVariables"]?["fontFamily"] is { } set && Truthy(set)))
		{
			return config;
		}

		var variables = config["themeVariables"] is { Kind: MermaidValueKind.Mapping } existing
			? new OrderedDictionary<string, MermaidValue>(existing.Entries, StringComparer.Ordinal)
			: new OrderedDictionary<string, MermaidValue>(StringComparer.Ordinal);
		variables["fontFamily"] = fontFamily;
		return With(config, "themeVariables", MermaidValue.FromEntries(variables));
	}

	/// <summary>
	/// <c>getDiagramConfigKey</c> in <c>packages/mermaid/src/diagram-api/diagramConfigKeys.ts</c>: the config key of a
	/// diagram type whose id is not its key.
	/// </summary>
	public static string DiagramConfigKey(string diagramType) => diagramType switch
	{
		"flowchart-v2" or "flowchart-elk" => "flowchart",
		"classDiagram" => "class",
		"stateDiagram" => "state",
		"xychart" => "xyChart",
		"railroadAbnf" or "railroadEbnf" or "railroadPeg" => "railroad",
		_ => diagramType,
	};

	/// <summary>
	/// <c>assignWithDepth</c> in <c>packages/mermaid/src/assignWithDepth.ts</c>, with its default depth of 2. It
	/// returns what the JavaScript returns, which is <paramref name="dst"/> changed in place in most cases.
	/// </summary>
	public static MermaidValue Assign(MermaidValue dst, MermaidValue src, int depth = 2)
	{
		if (src.Kind == MermaidValueKind.Sequence)
		{
			if (dst.Kind == MermaidValueKind.Sequence)
			{
				// Array.prototype.includes compares objects by reference, so only equal scalars are left out.
				var items = new List<MermaidValue>(dst.Items);
				foreach (var item in src.Items)
				{
					if (IsObject(item) || !items.Contains(item))
					{
						items.Add(item);
					}
				}

				return MermaidValue.FromItems(items);
			}

			// Each item is assigned into dst for its change in place. A call that would return its source is dropped.
			foreach (var item in src.Items)
			{
				if (dst.Kind == MermaidValueKind.Mapping && IsObject(item))
				{
					dst = Assign(dst, item, depth);
				}
			}

			return dst;
		}

		if (dst.Kind == MermaidValueKind.Null || depth <= 0)
		{
			if (!IsObject(dst) || src.Kind is not (MermaidValueKind.Mapping or MermaidValueKind.Null))
			{
				return src;
			}

			// Object.assign. A null source and an array target are left as they are.
			if (src.Kind == MermaidValueKind.Null || dst.Kind == MermaidValueKind.Sequence)
			{
				return dst;
			}

			var assigned = new OrderedDictionary<string, MermaidValue>(dst.Entries, StringComparer.Ordinal);
			foreach (var (key, value) in src.Entries)
			{
				assigned[key] = value;
			}

			return MermaidValue.FromEntries(assigned);
		}

		if (src.Kind != MermaidValueKind.Mapping || dst.Kind != MermaidValueKind.Mapping)
		{
			return dst;
		}

		var entries = new OrderedDictionary<string, MermaidValue>(dst.Entries, StringComparer.Ordinal);
		foreach (var (key, value) in src.Entries)
		{
			entries.TryGetValue(key, out var existing);
			if (IsObject(value))
			{
				existing ??= value.Kind == MermaidValueKind.Sequence ? MermaidValue.FromItems([]) : Empty();
				if (IsObject(existing) || existing.Kind == MermaidValueKind.Null)
				{
					entries[key] = Assign(existing, value, depth - 1);
				}
			}
			else if (value.Kind != MermaidValueKind.Null && (existing is null || !(IsObject(existing) || existing.Kind == MermaidValueKind.Null)))
			{
				entries[key] = value;
			}
		}

		return MermaidValue.FromEntries(entries);
	}

	/// <summary>
	/// <c>sanitizeDirective</c> in <c>packages/mermaid/src/utils/sanitizeDirective.ts</c>. It drops keys that are not
	/// config key names, look like prototype keys or hold null, checks the values of the dictionary keys, balances the
	/// braces of CSS keys and empties a theme variable with characters outside its safe set.
	/// </summary>
	public static MermaidValue SanitizeDirective(MermaidValue value)
	{
		if (value.Kind == MermaidValueKind.Sequence)
		{
			return MermaidValue.FromItems([.. value.Items.Select(SanitizeDirective)]);
		}

		if (value.Kind != MermaidValueKind.Mapping)
		{
			return value;
		}

		var entries = new OrderedDictionary<string, MermaidValue>(value.Entries.Count, StringComparer.Ordinal);
		foreach (var (key, item) in value.Entries)
		{
			if (IsPrototypeKey(key) || !ConfigKeys.Contains(key) || item.Kind == MermaidValueKind.Null)
			{
				continue;
			}

			if (IsObject(item))
			{
				entries[key] = key switch
				{
					"nodeColors" => SanitizeDictionary(item, IsColor),
					"filenameIcons" or "extensionIcons" => SanitizeDictionary(item, IsIcon),
					_ => SanitizeDirective(item),
				};
				continue;
			}

			entries[key] = item.Kind == MermaidValueKind.String && IsCssKey(key) ? MermaidValue.FromString(SanitizeCss(item.String)) : item;
		}

		if (entries.TryGetValue("themeVariables", out var variables) && variables.Kind == MermaidValueKind.Mapping)
		{
			var safe = new OrderedDictionary<string, MermaidValue>(variables.Entries, StringComparer.Ordinal);
			foreach (var (name, variable) in variables.Entries)
			{
				if (variable.Kind == MermaidValueKind.String && !IsSafeThemeValue(variable.String))
				{
					safe[name] = MermaidValue.FromString("");
				}
			}

			entries["themeVariables"] = MermaidValue.FromEntries(safe);
		}

		return MermaidValue.FromEntries(entries);
	}

	/// <summary>
	/// <c>sanitize</c> in config.ts: at every depth it drops the locked keys, which are <c>secure</c> and the site
	/// config's <c>secure</c> list, keys that start with <c>__</c>, and strings with <c>&lt;</c>, <c>&gt;</c> or
	/// <c>url(data:</c>.
	/// </summary>
	public static MermaidValue Sanitize(MermaidValue options, IReadOnlyList<string> secure)
	{
		if (options.Kind == MermaidValueKind.Sequence)
		{
			var items = new List<MermaidValue>(options.Items.Count);
			foreach (var item in options.Items)
			{
				if (!IsUnsafeString(item))
				{
					items.Add(Sanitize(item, secure));
				}
			}

			return MermaidValue.FromItems(items);
		}

		if (options.Kind != MermaidValueKind.Mapping)
		{
			return options;
		}

		var entries = new OrderedDictionary<string, MermaidValue>(options.Entries.Count, StringComparer.Ordinal);
		foreach (var (key, item) in options.Entries)
		{
			if (key == "secure" || secure.Contains(key) || key.StartsWith("__", StringComparison.Ordinal) || IsUnsafeString(item))
			{
				continue;
			}

			entries[key] = Sanitize(item, secure);
		}

		return MermaidValue.FromEntries(entries);
	}

	/// <summary>
	/// <c>resolveAppearance</c>: for <c>theme</c>, <c>look</c> and <c>layout</c>, the first usable value of the layers,
	/// each read at the diagram's section and then at the top, goes to the top and to a section key that is set.
	/// </summary>
	private static MermaidValue ResolveAppearance(MermaidValue config, string diagramConfigKey, MermaidValue[] layers)
	{
		var entries = new OrderedDictionary<string, MermaidValue>(config.Entries, StringComparer.Ordinal);
		var section = entries.TryGetValue(diagramConfigKey, out var found) && found.Kind == MermaidValueKind.Mapping
			? new OrderedDictionary<string, MermaidValue>(found.Entries, StringComparer.Ordinal)
			: null;
		foreach (var key in AppearanceKeys)
		{
			foreach (var layer in layers)
			{
				if (ReadAppearance(layer, diagramConfigKey, key) is not { } value)
				{
					continue;
				}

				entries[key] = value;
				if (section is not null && section.ContainsKey(key))
				{
					section[key] = value;
				}

				break;
			}
		}

		if (section is not null)
		{
			entries[diagramConfigKey] = MermaidValue.FromEntries(section);
		}

		return MermaidValue.FromEntries(entries);
	}

	private static MermaidValue? ReadAppearance(MermaidValue layer, string diagramConfigKey, string key)
	{
		if (layer[diagramConfigKey]?[key] is { } scoped && IsUsableAppearance(key, scoped))
		{
			return scoped;
		}

		return layer[key] is { } top && IsUsableAppearance(key, top) ? top : null;
	}

	/// <summary>A theme a theme answers to, or <c>"null"</c>, a look of the schema's enum, or any layout.</summary>
	private static bool IsUsableAppearance(string key, MermaidValue value) => key switch
	{
		"theme" => IsString(value, "null") || IsTheme(value),
		"look" => value.Kind == MermaidValueKind.String && value.String is "classic" or "handDrawn" or "neo",
		_ => true,
	};

	private static bool IsTheme(MermaidValue value) =>
		value.Kind == MermaidValueKind.String && Array.BinarySearch(ThemeNames, value.String, StringComparer.Ordinal) >= 0;

	private static List<string> Secure(MermaidValue site) =>
		site["secure"] is { Kind: MermaidValueKind.Sequence } secure
			? [.. secure.Items.Where(i => i.Kind == MermaidValueKind.String).Select(i => i.String)]
			: [];

	/// <summary><c>sanitizeDictionaryConfig</c>: drops each entry whose key looks like a prototype key or whose value is not a matching string.</summary>
	private static MermaidValue SanitizeDictionary(MermaidValue dictionary, Func<string, bool> matches)
	{
		bool Keep(string key, MermaidValue value) =>
			!IsPrototypeKey(key) && value.Kind == MermaidValueKind.String && matches(value.String);

		if (dictionary.Kind == MermaidValueKind.Sequence)
		{
			return MermaidValue.FromItems([.. dictionary.Items.Where((item, i) => Keep(i.ToString(CultureInfo.InvariantCulture), item))]);
		}

		var entries = new OrderedDictionary<string, MermaidValue>(StringComparer.Ordinal);
		foreach (var (key, value) in dictionary.Entries)
		{
			if (Keep(key, value))
			{
				entries[key] = value;
			}
		}

		return MermaidValue.FromEntries(entries);
	}

	/// <summary><c>sanitizeCss</c>: the text, or an error comment when a <c>}</c> comes before its <c>{</c> or the counts differ.</summary>
	private static string SanitizeCss(string css)
	{
		var open = 0;
		var close = 0;
		foreach (var c in css)
		{
			if (open < close)
			{
				return UnbalancedCss;
			}

			if (c == '{')
			{
				open++;
			}
			else if (c == '}')
			{
				close++;
			}
		}

		return open == close ? css : UnbalancedCss;
	}

	private static bool IsCssKey(string key) =>
		key.Contains("themeCSS", StringComparison.Ordinal) || key.Contains("fontFamily", StringComparison.Ordinal) || key.Contains("altFontFamily", StringComparison.Ordinal);

	private static bool IsPrototypeKey(string key) =>
		key.StartsWith("__", StringComparison.Ordinal) || key.Contains("proto", StringComparison.Ordinal) || key.Contains("constr", StringComparison.Ordinal);

	private static bool IsUnsafeString(MermaidValue value) =>
		value.Kind == MermaidValueKind.String
		&& (value.String.Contains('<', StringComparison.Ordinal) || value.String.Contains('>', StringComparison.Ordinal) || value.String.Contains("url(data:", StringComparison.Ordinal));

	/// <summary><c>/^[\d "#%(),.;A-Za-z]+$/</c>.</summary>
	private static bool IsSafeThemeValue(string text)
	{
		foreach (var c in text)
		{
			if (!(char.IsAsciiLetterOrDigit(c) || c is ' ' or '"' or '#' or '%' or '(' or ')' or ',' or '.' or ';'))
			{
				return false;
			}
		}

		return text.Length > 0;
	}

	/// <summary><c>/^#[\da-f]{3,8}$|^rgb\([\d\s%,.]+\)$|^hsl\([\d\s%,.]+\)$|^[a-z]+$/i</c>.</summary>
	private static bool IsColor(string text)
	{
		if (text.Length is >= 4 and <= 9 && text[0] == '#')
		{
			return !text.AsSpan(1).ContainsAnyExcept(HexDigits);
		}

		if (text.Length > 5 && (text.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase) || text.StartsWith("hsl(", StringComparison.OrdinalIgnoreCase)) && text[^1] == ')')
		{
			foreach (var c in text.AsSpan(4, text.Length - 5))
			{
				if (!(char.IsAsciiDigit(c) || c is '%' or ',' or '.' || PreambleParser.IsSpace(c)))
				{
					return false;
				}
			}

			return true;
		}

		foreach (var c in text)
		{
			if (!char.IsAsciiLetter(c))
			{
				return false;
			}
		}

		return text.Length > 0;
	}

	/// <summary><c>/^[\w-]+(?::[\w-]+)?$/</c>: a word, or two words with a colon between.</summary>
	private static bool IsIcon(string text)
	{
		var colon = text.IndexOf(':', StringComparison.Ordinal);
		return colon < 0 ? IsIconWord(text) : IsIconWord(text[..colon]) && IsIconWord(text[(colon + 1)..]);
	}

	private static bool IsIconWord(string text)
	{
		foreach (var c in text)
		{
			if (!(char.IsAsciiLetterOrDigit(c) || c is '_' or '-'))
			{
				return false;
			}
		}

		return text.Length > 0;
	}

	/// <summary>JavaScript's truthiness.</summary>
	private static bool Truthy(MermaidValue value) => value.Kind switch
	{
		MermaidValueKind.Null => false,
		MermaidValueKind.Boolean => value.Boolean,
		MermaidValueKind.Number => value.Number != 0 && !double.IsNaN(value.Number),
		MermaidValueKind.String => value.String.Length > 0,
		_ => true,
	};

	private static bool IsString(MermaidValue value, string text) => value.Kind == MermaidValueKind.String && value.String == text;

	private static bool IsObject(MermaidValue value) => value.Kind is MermaidValueKind.Mapping or MermaidValueKind.Sequence;

	private static MermaidValue Empty() => MermaidValue.FromEntries(new OrderedDictionary<string, MermaidValue>(StringComparer.Ordinal));

	private static MermaidValue With(MermaidValue mapping, string key, MermaidValue value)
	{
		var entries = new OrderedDictionary<string, MermaidValue>(mapping.Entries, StringComparer.Ordinal);
		entries[key] = value;
		return MermaidValue.FromEntries(entries);
	}
}
