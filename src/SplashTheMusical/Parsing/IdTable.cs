namespace SplashTheMusical.Parsing;

/// <summary>
/// Maps each distinct id in one parse to an integer, in the order the ids first appear. The lookup is keyed by span, so
/// an id allocates its string only the first time it is seen.
/// </summary>
internal sealed class IdTable
{
	private readonly Dictionary<string, int> _indexes;
	private readonly Dictionary<string, int>.AlternateLookup<ReadOnlySpan<char>> _lookup;
	private readonly List<string> _ids;

	public IdTable(int capacity = 0)
	{
		_indexes = new Dictionary<string, int>(capacity, StringComparer.Ordinal);
		_lookup = _indexes.GetAlternateLookup<ReadOnlySpan<char>>();
		_ids = new List<string>(capacity);
	}

	public int Count => _ids.Count;

	public string this[int index] => _ids[index];

	public int GetOrAdd(ReadOnlySpan<char> id)
	{
		if (_lookup.TryGetValue(id, out var index))
		{
			return index;
		}

		var text = id.ToString();
		index = _ids.Count;
		_indexes.Add(text, index);
		_ids.Add(text);
		return index;
	}

	public bool TryGetIndex(ReadOnlySpan<char> id, out int index) => _lookup.TryGetValue(id, out index);
}
