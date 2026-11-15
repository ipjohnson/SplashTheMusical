namespace SplashTheMusical;

public enum MermaidValueKind : byte
{
	Null,
	Boolean,
	Number,
	String,
	Sequence,
	Mapping,
}

/// <summary>
/// A value read from YAML front matter, <c>@{ ... }</c> node metadata or the JSON of a <c>%%{init}%%</c> directive. The
/// kinds are the JSON types, because mermaid.js reads YAML with js-yaml's <c>JSON_SCHEMA</c>. Numbers are doubles, as in
/// JavaScript. A mapping keeps its keys in source order.
/// </summary>
/// <remarks>Two values are equal when they have the same kind and equal contents, with mapping keys in the same order.</remarks>
public sealed class MermaidValue : IEquatable<MermaidValue>
{
	private readonly bool _boolean;
	private readonly double _number;
	private readonly string? _string;
	private readonly List<MermaidValue>? _items;
	private readonly OrderedDictionary<string, MermaidValue>? _entries;

	private MermaidValue(MermaidValueKind kind) => Kind = kind;

	private MermaidValue(bool value)
		: this(MermaidValueKind.Boolean) => _boolean = value;

	private MermaidValue(double value)
		: this(MermaidValueKind.Number) => _number = value;

	private MermaidValue(string value)
		: this(MermaidValueKind.String) => _string = value;

	private MermaidValue(List<MermaidValue> items)
		: this(MermaidValueKind.Sequence) => _items = items;

	private MermaidValue(OrderedDictionary<string, MermaidValue> entries)
		: this(MermaidValueKind.Mapping) => _entries = entries;

	public static MermaidValue Null { get; } = new(MermaidValueKind.Null);

	public static MermaidValue True { get; } = new(true);

	public static MermaidValue False { get; } = new(false);

	public MermaidValueKind Kind { get; }

	public bool Boolean => Kind == MermaidValueKind.Boolean ? _boolean : throw WrongKind(MermaidValueKind.Boolean);

	public double Number => Kind == MermaidValueKind.Number ? _number : throw WrongKind(MermaidValueKind.Number);

	public string String => _string ?? throw WrongKind(MermaidValueKind.String);

	public IReadOnlyList<MermaidValue> Items => _items ?? throw WrongKind(MermaidValueKind.Sequence);

	public IReadOnlyDictionary<string, MermaidValue> Entries => _entries ?? throw WrongKind(MermaidValueKind.Mapping);

	/// <summary>The value of <paramref name="key"/> in a mapping, or <c>null</c> when the key is missing or this is not a mapping.</summary>
	public MermaidValue? this[string key] => _entries is not null && _entries.TryGetValue(key, out var value) ? value : null;

	public static MermaidValue FromBoolean(bool value) => value ? True : False;

	public static MermaidValue FromNumber(double value) => new(value);

	public static MermaidValue FromString(string value) => new(value);

	public static MermaidValue FromItems(List<MermaidValue> items) => new(items);

	public static MermaidValue FromEntries(OrderedDictionary<string, MermaidValue> entries) => new(entries);

	public bool Equals(MermaidValue? other)
	{
		if (ReferenceEquals(this, other))
		{
			return true;
		}

		if (other is null || other.Kind != Kind)
		{
			return false;
		}

		switch (Kind)
		{
			case MermaidValueKind.Boolean:
				return _boolean == other._boolean;
			case MermaidValueKind.Number:
				return _number.Equals(other._number);
			case MermaidValueKind.String:
				return string.Equals(_string, other._string, StringComparison.Ordinal);
			case MermaidValueKind.Sequence:
				return _items!.SequenceEqual(other._items!);
			case MermaidValueKind.Mapping:
				if (_entries!.Count != other._entries!.Count)
				{
					return false;
				}

				for (var i = 0; i < _entries.Count; i++)
				{
					var (key, value) = _entries.GetAt(i);
					var (otherKey, otherValue) = other._entries.GetAt(i);
					if (!string.Equals(key, otherKey, StringComparison.Ordinal) || !value.Equals(otherValue))
					{
						return false;
					}
				}

				return true;
			default:
				return true;
		}
	}

	public override bool Equals(object? obj) => obj is MermaidValue other && Equals(other);

	public override int GetHashCode() => Kind switch
	{
		MermaidValueKind.Boolean => HashCode.Combine(Kind, _boolean),
		MermaidValueKind.Number => HashCode.Combine(Kind, _number),
		MermaidValueKind.String => HashCode.Combine(Kind, StringComparer.Ordinal.GetHashCode(_string!)),
		MermaidValueKind.Sequence => HashCode.Combine(Kind, _items!.Count),
		MermaidValueKind.Mapping => HashCode.Combine(Kind, _entries!.Count),
		_ => Kind.GetHashCode(),
	};

	private InvalidOperationException WrongKind(MermaidValueKind wanted) => new($"The value is a {Kind}, not a {wanted}.");
}
