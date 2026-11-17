using System.Buffers;

namespace SplashTheMusical.Parsing;

/// <summary>
/// Maps each distinct string of one parse to an integer, in the order the strings are first added, with an open
/// addressing table over pooled arrays. A lookup by span allocates nothing, and a string is allocated only the first time
/// it is added. Dispose it to return the arrays.
/// </summary>
/// <remarks>A copy shares the arrays, so pass it by reference.</remarks>
internal struct PooledStringTable : IDisposable
{
	private PooledList<string> _strings;
	private PooledList<int> _hashes;

	// Each slot holds a string's index plus one, or 0 when it is empty. The table uses the first _mask + 1 slots.
	private int[]? _slots;
	private int _mask;

	public readonly int Count => _strings.Count;

	public readonly string this[int index] => _strings[index];

	public readonly bool TryGetIndex(ReadOnlySpan<char> text, out int index)
	{
		index = Find(text, string.GetHashCode(text));
		return index >= 0;
	}

	public int GetOrAdd(ReadOnlySpan<char> text)
	{
		var hash = string.GetHashCode(text);
		var index = Find(text, hash);
		return index >= 0 ? index : Add(text.ToString(), hash);
	}

	/// <summary>Adds <paramref name="text"/> without copying it, or returns the index of an equal string.</summary>
	public int GetOrAdd(string text)
	{
		var hash = string.GetHashCode(text.AsSpan());
		var index = Find(text, hash);
		return index >= 0 ? index : Add(text, hash);
	}

	public void Dispose()
	{
		_strings.Dispose();
		_hashes.Dispose();
		if (_slots is not null)
		{
			ArrayPool<int>.Shared.Return(_slots);
			_slots = null;
		}
	}

	private readonly int Find(ReadOnlySpan<char> text, int hash)
	{
		if (_slots is null)
		{
			return -1;
		}

		for (var slot = hash & _mask; ; slot = (slot + 1) & _mask)
		{
			var entry = _slots[slot] - 1;
			if (entry < 0)
			{
				return -1;
			}

			if (_hashes[entry] == hash && text.SequenceEqual(_strings[entry]))
			{
				return entry;
			}
		}
	}

	private int Add(string text, int hash)
	{
		var index = _strings.Count;
		_strings.Add(text);
		_hashes.Add(hash);
		if (_slots is null || (index + 1) * 2 > _mask + 1)
		{
			Rehash();
		}
		else
		{
			Place(hash, index);
		}

		return index;
	}

	private void Rehash()
	{
		var size = 16;
		while (size < _strings.Count * 4)
		{
			size *= 2;
		}

		if (_slots is not null)
		{
			ArrayPool<int>.Shared.Return(_slots);
		}

		_slots = ArrayPool<int>.Shared.Rent(size);
		_slots.AsSpan(0, size).Clear();
		_mask = size - 1;
		for (var i = 0; i < _strings.Count; i++)
		{
			Place(_hashes[i], i);
		}
	}

	private readonly void Place(int hash, int index)
	{
		var slot = hash & _mask;
		while (_slots![slot] != 0)
		{
			slot = (slot + 1) & _mask;
		}

		_slots[slot] = index + 1;
	}
}
