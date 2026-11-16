namespace SplashTheMusical.Layout.Dagre;

/// <summary>
/// A set of handles that iterates in the order of a JavaScript object's keys. graphlib keeps nodes, edges, children,
/// predecessors and successors as object keys, and dagre's results depend on that order. A key that is an array index
/// comes first in numeric order. The other keys follow in insertion order, and a key that is deleted and added again
/// moves to the end. Each key also holds a count, for graphlib's predecessor and successor maps.
/// </summary>
internal sealed class KeyOrder
{
	private const int LookupThreshold = 16;

	private int[] _keys = new int[4];
	private long[] _indexes = new long[4];
	private int[] _counts = new int[4];
	private int _length;
	private int _live;
	private int _indexed;
	private Dictionary<int, int>? _positions;

	public int Count => _live;

	public bool Contains(int key) => Find(key) >= 0;

	/// <summary>Adds the key at the end. Returns false and leaves the order alone when the key is present.</summary>
	/// <param name="arrayIndex">The key's value as an array index, or -1 when the key is not one.</param>
	public bool Add(int key, long arrayIndex)
	{
		if (Find(key) >= 0)
		{
			return false;
		}

		Append(key, arrayIndex, 1);
		return true;
	}

	/// <summary>graphlib's <c>incrementOrInitEntry</c>.</summary>
	public void Increment(int key, long arrayIndex)
	{
		var position = Find(key);
		if (position >= 0)
		{
			_counts[position]++;
		}
		else
		{
			Append(key, arrayIndex, 1);
		}
	}

	/// <summary>graphlib's <c>decrementOrRemoveEntry</c>.</summary>
	public void Decrement(int key)
	{
		var position = Find(key);
		if (position >= 0 && --_counts[position] == 0)
		{
			RemoveAt(position);
		}
	}

	public bool Remove(int key)
	{
		var position = Find(key);
		if (position < 0)
		{
			return false;
		}

		RemoveAt(position);
		return true;
	}

	/// <summary>The keys in JavaScript's order, as <c>_.keys</c> returns them.</summary>
	public int[] ToArray()
	{
		var result = new int[_live];
		CopyTo(result);
		return result;
	}

	public void CopyTo(Span<int> destination)
	{
		var next = 0;
		if (_indexed > 0)
		{
			var indexed = new (long Index, int Key)[_indexed];
			var count = 0;
			for (var i = 0; i < _length; i++)
			{
				if (_counts[i] > 0 && _indexes[i] >= 0)
				{
					indexed[count++] = (_indexes[i], _keys[i]);
				}
			}

			Array.Sort(indexed, static (a, b) => a.Index.CompareTo(b.Index));
			foreach (var (_, key) in indexed)
			{
				destination[next++] = key;
			}
		}

		for (var i = 0; i < _length; i++)
		{
			if (_counts[i] > 0 && _indexes[i] < 0)
			{
				destination[next++] = _keys[i];
			}
		}
	}

	/// <summary>The first key in order, or -1 when the set is empty.</summary>
	public int First()
	{
		if (_live == 0)
		{
			return -1;
		}

		if (_indexed > 0)
		{
			var best = -1;
			for (var i = 0; i < _length; i++)
			{
				if (_counts[i] > 0 && _indexes[i] >= 0 && (best < 0 || _indexes[i] < _indexes[best]))
				{
					best = i;
				}
			}

			return _keys[best];
		}

		for (var i = 0; i < _length; i++)
		{
			if (_counts[i] > 0)
			{
				return _keys[i];
			}
		}

		return -1;
	}

	private int Find(int key)
	{
		if (_positions is not null)
		{
			return _positions.TryGetValue(key, out var position) ? position : -1;
		}

		for (var i = 0; i < _length; i++)
		{
			if (_keys[i] == key && _counts[i] > 0)
			{
				return i;
			}
		}

		return -1;
	}

	private void Append(int key, long arrayIndex, int count)
	{
		if (_length == _keys.Length)
		{
			if (_live * 2 <= _length)
			{
				Compact();
			}
			else
			{
				var size = _keys.Length * 2;
				Array.Resize(ref _keys, size);
				Array.Resize(ref _indexes, size);
				Array.Resize(ref _counts, size);
			}
		}

		_keys[_length] = key;
		_indexes[_length] = arrayIndex;
		_counts[_length] = count;
		_positions?.Add(key, _length);
		_length++;
		_live++;
		if (arrayIndex >= 0)
		{
			_indexed++;
		}

		if (_positions is null && _live > LookupThreshold)
		{
			BuildPositions();
		}
	}

	private void RemoveAt(int position)
	{
		_counts[position] = 0;
		_positions?.Remove(_keys[position]);
		_live--;
		if (_indexes[position] >= 0)
		{
			_indexed--;
		}

		if (position == _length - 1)
		{
			_length--;
		}
	}

	private void Compact()
	{
		var next = 0;
		for (var i = 0; i < _length; i++)
		{
			if (_counts[i] > 0)
			{
				_keys[next] = _keys[i];
				_indexes[next] = _indexes[i];
				_counts[next] = _counts[i];
				next++;
			}
		}

		_length = next;
		if (_positions is not null)
		{
			BuildPositions();
		}
	}

	private void BuildPositions()
	{
		_positions ??= [];
		_positions.Clear();
		for (var i = 0; i < _length; i++)
		{
			if (_counts[i] > 0)
			{
				_positions[_keys[i]] = i;
			}
		}
	}
}
