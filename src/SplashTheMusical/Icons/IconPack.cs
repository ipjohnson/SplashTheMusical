using System.Collections.Frozen;
using System.Text;
using System.Text.Json;

namespace SplashTheMusical.Icons;

/// <summary>
/// A set of icons under one prefix, such as an Iconify JSON icon set, as mermaid's <c>registerIconPacks</c> takes one.
/// Its icons are sanitized when the pack is made.
/// </summary>
/// <remarks>A pack is immutable and thread safe.</remarks>
public sealed class IconPack
{
	private const int MaxAliasDepth = 24;

	private readonly FrozenDictionary<string, MermaidIcon> _icons;

	/// <summary>A pack of <paramref name="icons"/> under <paramref name="prefix"/>.</summary>
	/// <exception cref="ArgumentException">The prefix is empty.</exception>
	public IconPack(string prefix, IEnumerable<KeyValuePair<string, MermaidIcon>> icons)
	{
		ArgumentException.ThrowIfNullOrEmpty(prefix);
		ArgumentNullException.ThrowIfNull(icons);
		Prefix = prefix;
		var map = new Dictionary<string, MermaidIcon>(StringComparer.Ordinal);
		foreach (var (name, icon) in icons)
		{
			ArgumentNullException.ThrowIfNull(name, nameof(icons));
			ArgumentNullException.ThrowIfNull(icon, nameof(icons));
			map[name] = icon;
		}

		_icons = map.ToFrozenDictionary(StringComparer.Ordinal);
	}

	/// <summary>The prefix a diagram names the pack's icons with, as in <c>prefix:name</c>.</summary>
	public string Prefix { get; }

	/// <summary>The names of the pack's icons and aliases.</summary>
	public IReadOnlyCollection<string> Names => _icons.Keys;

	/// <summary>The icon or alias named <paramref name="name"/>, or <c>null</c>.</summary>
	public MermaidIcon? GetIcon(string name)
	{
		ArgumentNullException.ThrowIfNull(name);
		return _icons.GetValueOrDefault(name);
	}

	/// <inheritdoc cref="FromIconifyJson(ReadOnlySpan{byte}, string?)"/>
	public static IconPack FromIconifyJson(string json, string? prefix = null)
	{
		ArgumentNullException.ThrowIfNull(json);
		return FromIconifyJson(Encoding.UTF8.GetBytes(json), prefix);
	}

	/// <summary>
	/// Reads an Iconify JSON icon set: its <c>prefix</c>, the <c>icons</c> and <c>aliases</c> objects, and the default
	/// <c>width</c>, <c>height</c>, <c>left</c> and <c>top</c>. Other fields are skipped. Each alias is resolved to its
	/// parent's body with the alias's fields merged, as Iconify's <c>getIconData</c> does, and an alias whose parents do
	/// not lead to an icon is left out.
	/// </summary>
	/// <param name="utf8Json">The icon set as UTF-8 JSON.</param>
	/// <param name="prefix">The prefix to register the pack under, as <c>registerIconPacks</c>' <c>name</c>. Default: the set's <c>prefix</c>.</param>
	/// <exception cref="FormatException">The JSON is malformed, is not an icon set, or has no prefix and none is given.</exception>
	public static IconPack FromIconifyJson(ReadOnlySpan<byte> utf8Json, string? prefix = null)
	{
		IconifySet set;
		try
		{
			set = IconifyReader.Read(utf8Json);
		}
		catch (JsonException e)
		{
			throw new FormatException("The Iconify JSON icon set is malformed: " + e.Message, e);
		}

		var name = string.IsNullOrEmpty(prefix) ? set.Prefix : prefix;
		if (string.IsNullOrEmpty(name))
		{
			throw new FormatException("The Iconify JSON icon set has no prefix, and none was given.");
		}

		var icons = new Dictionary<string, MermaidIcon>(StringComparer.Ordinal);
		foreach (var (iconName, raw) in set.Icons)
		{
			if (raw.Body is { } body)
			{
				icons[iconName] = new MermaidIcon(
					body,
					raw.Width ?? set.Width ?? 16,
					raw.Height ?? set.Height ?? 16,
					raw.Left ?? set.Left ?? 0,
					raw.Top ?? set.Top ?? 0,
					raw.Rotate,
					raw.HorizontalFlip,
					raw.VerticalFlip);
			}
		}

		foreach (var aliasName in set.Aliases.Keys)
		{
			if (!icons.ContainsKey(aliasName) && Resolve(aliasName, set, icons, 0) is { } icon)
			{
				icons[aliasName] = icon;
			}
		}

		return new IconPack(name, icons);
	}

	private static MermaidIcon? Resolve(string name, IconifySet set, Dictionary<string, MermaidIcon> icons, int depth)
	{
		if (icons.TryGetValue(name, out var icon))
		{
			return icon;
		}

		if (depth > MaxAliasDepth || !set.Aliases.TryGetValue(name, out var alias) || alias.Parent is not { } parentName
			|| Resolve(parentName, set, icons, depth + 1) is not { } parent)
		{
			return null;
		}

		return parent.WithAlias(alias.Width, alias.Height, alias.Left, alias.Top, alias.Rotate, alias.HorizontalFlip, alias.VerticalFlip);
	}
}

/// <summary>The fields of an Iconify icon or alias, as the JSON gives them.</summary>
internal sealed class IconifyIcon
{
	public string? Body { get; set; }

	public string? Parent { get; set; }

	public double? Width { get; set; }

	public double? Height { get; set; }

	public double? Left { get; set; }

	public double? Top { get; set; }

	public int Rotate { get; set; }

	public bool HorizontalFlip { get; set; }

	public bool VerticalFlip { get; set; }
}

internal sealed class IconifySet
{
	public string? Prefix { get; set; }

	public double? Width { get; set; }

	public double? Height { get; set; }

	public double? Left { get; set; }

	public double? Top { get; set; }

	public Dictionary<string, IconifyIcon> Icons { get; } = new(StringComparer.Ordinal);

	public Dictionary<string, IconifyIcon> Aliases { get; } = new(StringComparer.Ordinal);
}

/// <summary>Reads an Iconify JSON icon set with <see cref="Utf8JsonReader"/>, with no reflection.</summary>
internal static class IconifyReader
{
	public static IconifySet Read(ReadOnlySpan<byte> utf8Json)
	{
		var reader = new Utf8JsonReader(utf8Json, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true, MaxDepth = 16 });
		var set = new IconifySet();
		Expect(ref reader, JsonTokenType.StartObject);
		while (Next(ref reader) == JsonTokenType.PropertyName)
		{
			var property = reader.GetString();
			reader.Read();
			switch (property)
			{
				case "prefix" when reader.TokenType == JsonTokenType.String:
					set.Prefix = reader.GetString();
					break;
				case "width":
					set.Width = Size(ref reader);
					break;
				case "height":
					set.Height = Size(ref reader);
					break;
				case "left":
					set.Left = Number(ref reader);
					break;
				case "top":
					set.Top = Number(ref reader);
					break;
				case "icons":
					ReadIcons(ref reader, set.Icons);
					break;
				case "aliases":
					ReadIcons(ref reader, set.Aliases);
					break;
				default:
					reader.Skip();
					break;
			}
		}

		return reader.TokenType == JsonTokenType.EndObject ? set : throw new JsonException("The icon set is not an object.");
	}

	private static void ReadIcons(ref Utf8JsonReader reader, Dictionary<string, IconifyIcon> icons)
	{
		if (reader.TokenType != JsonTokenType.StartObject)
		{
			throw new JsonException("'icons' and 'aliases' are objects.");
		}

		while (Next(ref reader) == JsonTokenType.PropertyName)
		{
			var name = reader.GetString()!;
			reader.Read();
			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				continue;
			}

			var icon = new IconifyIcon();
			while (Next(ref reader) == JsonTokenType.PropertyName)
			{
				var property = reader.GetString();
				reader.Read();
				switch (property)
				{
					case "body" when reader.TokenType == JsonTokenType.String:
						icon.Body = reader.GetString();
						break;
					case "parent" when reader.TokenType == JsonTokenType.String:
						icon.Parent = reader.GetString();
						break;
					case "width":
						icon.Width = Size(ref reader);
						break;
					case "height":
						icon.Height = Size(ref reader);
						break;
					case "left":
						icon.Left = Number(ref reader);
						break;
					case "top":
						icon.Top = Number(ref reader);
						break;
					case "rotate" when reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var rotate):
						icon.Rotate = rotate;
						break;
					case "hFlip" when reader.TokenType is JsonTokenType.True or JsonTokenType.False:
						icon.HorizontalFlip = reader.GetBoolean();
						break;
					case "vFlip" when reader.TokenType is JsonTokenType.True or JsonTokenType.False:
						icon.VerticalFlip = reader.GetBoolean();
						break;
					default:
						reader.Skip();
						break;
				}
			}

			icons[name] = icon;
		}
	}

	/// <summary>A positive finite number, or <c>null</c> for any other value.</summary>
	private static double? Size(ref Utf8JsonReader reader) => Number(ref reader) is > 0 and var size ? size : null;

	/// <summary>A finite number, or <c>null</c> for any other value.</summary>
	private static double? Number(ref Utf8JsonReader reader)
	{
		if (reader.TokenType != JsonTokenType.Number)
		{
			reader.Skip();
			return null;
		}

		return reader.TryGetDouble(out var value) && double.IsFinite(value) ? value : null;
	}

	private static JsonTokenType Next(ref Utf8JsonReader reader) => reader.Read() ? reader.TokenType : throw new JsonException("The JSON ends early.");

	private static void Expect(ref Utf8JsonReader reader, JsonTokenType type)
	{
		if (Next(ref reader) != type)
		{
			throw new JsonException("The icon set is not an object.");
		}
	}
}
