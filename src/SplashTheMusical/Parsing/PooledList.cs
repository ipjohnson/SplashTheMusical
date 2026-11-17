using System.Buffers;
using System.Runtime.CompilerServices;

namespace SplashTheMusical.Parsing;

/// <summary>
/// A list over an array from <see cref="ArrayPool{T}.Shared"/>, for a parser's scratch space. <see cref="ToList"/>
/// copies it once into a list of its final size. Dispose it to return the array.
/// </summary>
/// <remarks>A copy shares the array, so pass it by reference.</remarks>
internal struct PooledList<T> : IDisposable
{
	private T[]? _items;
	private int _count;

	public PooledList(int capacity) => _items = capacity > 0 ? ArrayPool<T>.Shared.Rent(capacity) : null;

	public readonly int Count => _count;

	public readonly ref T this[int index]
	{
		get
		{
			if ((uint)index >= (uint)_count)
			{
				throw new ArgumentOutOfRangeException(nameof(index));
			}

			return ref _items![index];
		}
	}

	public readonly Span<T> AsSpan() => _items is null ? [] : _items.AsSpan(0, _count);

	public void Add(T item)
	{
		if (_items is null || _count == _items.Length)
		{
			Grow(_count + 1);
		}

		_items![_count++] = item;
	}

	public void AddRange(ReadOnlySpan<T> items)
	{
		if (_items is null || _count + items.Length > _items.Length)
		{
			Grow(_count + items.Length);
		}

		items.CopyTo(_items.AsSpan(_count));
		_count += items.Length;
	}

	/// <summary>Removes the items from <paramref name="count"/> on.</summary>
	public void Truncate(int count)
	{
		if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
		{
			AsSpan()[count..].Clear();
		}

		_count = count;
	}

	public readonly List<T> ToList()
	{
		var list = new List<T>(_count);
		list.AddRange(AsSpan());
		return list;
	}

	public void Dispose()
	{
		if (_items is not null)
		{
			ArrayPool<T>.Shared.Return(_items, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
			_items = null;
		}

		_count = 0;
	}

	private void Grow(int minimum)
	{
		var grown = ArrayPool<T>.Shared.Rent(Math.Max(minimum, Math.Max(16, (_items?.Length ?? 0) * 2)));
		if (_items is not null)
		{
			_items.AsSpan(0, _count).CopyTo(grown);
			ArrayPool<T>.Shared.Return(_items, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
		}

		_items = grown;
	}
}
