using System.Collections.Frozen;

namespace SplashTheMusical.Icons;

/// <summary>
/// The icon provider of registered <see cref="IconPack"/>s, as mermaid's <c>registerIconPacks</c> keeps them. A pack is
/// found by the prefix of the name, and a later pack with the same prefix replaces an earlier one.
/// </summary>
/// <remarks>A registry is immutable and thread safe. <see cref="With"/> gives a new one.</remarks>
public sealed class IconRegistry : IIconProvider
{
	private readonly FrozenDictionary<string, IconPack> _packs;

	public IconRegistry(IEnumerable<IconPack> packs)
	{
		ArgumentNullException.ThrowIfNull(packs);
		var map = new Dictionary<string, IconPack>(StringComparer.Ordinal);
		foreach (var pack in packs)
		{
			ArgumentNullException.ThrowIfNull(pack, nameof(packs));
			map[pack.Prefix] = pack;
		}

		_packs = map.ToFrozenDictionary(StringComparer.Ordinal);
	}

	/// <summary>The icons mermaid.js ships: <see cref="MermaidIcons.Architecture"/>.</summary>
	public static IconRegistry Default { get; } = new([MermaidIcons.Architecture]);

	/// <summary>The prefixes of the registered packs.</summary>
	public IReadOnlyCollection<string> Prefixes => _packs.Keys;

	/// <summary>A registry of this one's packs and <paramref name="packs"/>, which replace packs of the same prefix.</summary>
	public IconRegistry With(params IEnumerable<IconPack> packs)
	{
		ArgumentNullException.ThrowIfNull(packs);
		return new IconRegistry([.. _packs.Values, .. packs]);
	}

	/// <summary>
	/// The icon of a name as Iconify's <c>stringToIcon</c> splits it: <c>prefix:name</c>, <c>@provider:prefix:name</c>
	/// or <c>prefix-name</c>. A name without a prefix has no icon, as in mermaid's shapes.
	/// </summary>
	public MermaidIcon? GetIcon(string name)
	{
		ArgumentNullException.ThrowIfNull(name);
		return Split(name) is var (prefix, icon) && _packs.TryGetValue(prefix, out var pack) ? pack.GetIcon(icon) : null;
	}

	internal static (string Prefix, string Name)? Split(string value)
	{
		var text = value.StartsWith('@') ? value.AsSpan(1) : value.AsSpan();
		var parts = value.Split(':');
		if (parts.Length > 3 || (value.StartsWith('@') && parts.Length < 3))
		{
			return null;
		}

		if (parts.Length > 1)
		{
			var (prefix, name) = (parts[^2], parts[^1]);
			return prefix.Length > 0 && name.Length > 0 ? (prefix, name) : null;
		}

		var dash = text.IndexOf('-');
		return dash > 0 && dash < text.Length - 1 ? (text[..dash].ToString(), text[(dash + 1)..].ToString()) : null;
	}
}
