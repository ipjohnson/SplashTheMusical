namespace SplashTheMusical.Config;

/// <summary>
/// Adds a config property to the mapping a generated <c>ToValue</c> builds. A null property is left out, as mermaid.js
/// leaves out an undefined key.
/// </summary>
internal static class ConfigValues
{
	public static void Add(OrderedDictionary<string, MermaidValue> entries, string key, bool? value)
	{
		if (value is { } boolean)
		{
			entries[key] = MermaidValue.FromBoolean(boolean);
		}
	}

	public static void Add(OrderedDictionary<string, MermaidValue> entries, string key, double? value)
	{
		if (value is { } number)
		{
			entries[key] = MermaidValue.FromNumber(number);
		}
	}

	public static void Add(OrderedDictionary<string, MermaidValue> entries, string key, string? value)
	{
		if (value is not null)
		{
			entries[key] = MermaidValue.FromString(value);
		}
	}

	public static void Add(OrderedDictionary<string, MermaidValue> entries, string key, MermaidValue? value)
	{
		if (value is not null)
		{
			entries[key] = value;
		}
	}

	public static void Add(OrderedDictionary<string, MermaidValue> entries, string key, IReadOnlyList<string>? value)
	{
		if (value is not null)
		{
			entries[key] = MermaidValue.FromItems([.. value.Select(MermaidValue.FromString)]);
		}
	}

	public static void Add(OrderedDictionary<string, MermaidValue> entries, string key, IReadOnlyDictionary<string, string>? value)
	{
		if (value is null)
		{
			return;
		}

		var map = new OrderedDictionary<string, MermaidValue>(value.Count, StringComparer.Ordinal);
		foreach (var (name, text) in value)
		{
			map[name] = MermaidValue.FromString(text);
		}

		entries[key] = MermaidValue.FromEntries(map);
	}

	/// <summary>Adds a nested config, unless it has no values, which mermaid.js's default config writes as a missing key.</summary>
	public static void AddObject(OrderedDictionary<string, MermaidValue> entries, string key, MermaidValue value)
	{
		if (value.Entries.Count > 0)
		{
			entries[key] = value;
		}
	}
}
