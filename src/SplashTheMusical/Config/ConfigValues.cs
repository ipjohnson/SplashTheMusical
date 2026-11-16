using System.Globalization;

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

	public static bool? ReadBoolean(MermaidValue? value) => value is { Kind: MermaidValueKind.Boolean } ? value.Boolean : null;

	/// <summary>A number, or a string that holds a finite decimal number, such as the <c>"100"</c> a directive may give.</summary>
	public static double? ReadNumber(MermaidValue? value)
	{
		if (value is { Kind: MermaidValueKind.Number })
		{
			return value.Number;
		}

		return value is { Kind: MermaidValueKind.String }
			&& double.TryParse(value.String, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
			&& double.IsFinite(number)
				? number
				: null;
	}

	public static string? ReadString(MermaidValue? value) => value is { Kind: MermaidValueKind.String } ? value.String : null;

	public static MermaidValue? ReadValue(MermaidValue? value) => value is { Kind: not MermaidValueKind.Null } ? value : null;

	/// <summary>The strings of a sequence, or null when it is not a sequence or an item is not a string.</summary>
	public static IReadOnlyList<string>? ReadStringList(MermaidValue? value)
	{
		if (value is not { Kind: MermaidValueKind.Sequence })
		{
			return null;
		}

		var list = new string[value.Items.Count];
		for (var i = 0; i < list.Length; i++)
		{
			if (value.Items[i].Kind != MermaidValueKind.String)
			{
				return null;
			}

			list[i] = value.Items[i].String;
		}

		return list;
	}

	/// <summary>The entries of a mapping of strings, or null when it is not a mapping or a value is not a string.</summary>
	public static IReadOnlyDictionary<string, string>? ReadStringMap(MermaidValue? value)
	{
		if (value is not { Kind: MermaidValueKind.Mapping })
		{
			return null;
		}

		var map = new OrderedDictionary<string, string>(value.Entries.Count, StringComparer.Ordinal);
		foreach (var (key, item) in value.Entries)
		{
			if (item.Kind != MermaidValueKind.String)
			{
				return null;
			}

			map[key] = item.String;
		}

		return map;
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
