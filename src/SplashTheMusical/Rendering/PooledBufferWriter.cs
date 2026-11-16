using System.Buffers;

namespace SplashTheMusical.Rendering;

/// <summary>An <see cref="IBufferWriter{T}"/> over arrays rented from <see cref="ArrayPool{T}.Shared"/>, returned on dispose.</summary>
internal sealed class PooledBufferWriter : IBufferWriter<byte>, IDisposable
{
	private byte[] _buffer;
	private int _written;

	public PooledBufferWriter(int initialCapacity = 4096) => _buffer = ArrayPool<byte>.Shared.Rent(initialCapacity);

	public ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _written);

	public void Advance(int count)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(count);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(count, _buffer.Length - _written);
		_written += count;
	}

	public Memory<byte> GetMemory(int sizeHint = 0)
	{
		Grow(sizeHint);
		return _buffer.AsMemory(_written);
	}

	public Span<byte> GetSpan(int sizeHint = 0)
	{
		Grow(sizeHint);
		return _buffer.AsSpan(_written);
	}

	public void Dispose()
	{
		ArrayPool<byte>.Shared.Return(_buffer);
		_buffer = [];
		_written = 0;
	}

	private void Grow(int sizeHint)
	{
		ObjectDisposedException.ThrowIf(_buffer.Length == 0, this);
		var needed = Math.Max(sizeHint, 1);
		if (_buffer.Length - _written >= needed)
		{
			return;
		}

		var larger = ArrayPool<byte>.Shared.Rent(Math.Max(_buffer.Length * 2, _written + needed));
		_buffer.AsSpan(0, _written).CopyTo(larger);
		ArrayPool<byte>.Shared.Return(_buffer);
		_buffer = larger;
	}
}
