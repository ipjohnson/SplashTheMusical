namespace SplashTheMusical.Conformance.Fuzzing;

/// <summary>
/// SplitMix64. The fuzzers derive every source from a seed and an index with it, so a find can be made again from the
/// two numbers on any runtime.
/// </summary>
public sealed class FuzzRandom(ulong seed)
{
	private ulong _state = seed;

	/// <summary>The generator of source <paramref name="index"/> of a run with <paramref name="seed"/>.</summary>
	public static FuzzRandom For(long seed, long index) => new(Mix((ulong)seed ^ Mix((ulong)index + 0x9E3779B97F4A7C15)));

	/// <summary>A number from 0 to <paramref name="bound"/>, excluding <paramref name="bound"/>.</summary>
	public int Next(int bound)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bound);
		return (int)(NextUInt64() % (ulong)bound);
	}

	/// <summary>A number from <paramref name="min"/> to <paramref name="max"/>, both included.</summary>
	public int Between(int min, int max) => min + Next(max - min + 1);

	public T Pick<T>(IReadOnlyList<T> items) => items[Next(items.Count)];

	private ulong NextUInt64()
	{
		_state += 0x9E3779B97F4A7C15;
		return Mix(_state);
	}

	private static ulong Mix(ulong z)
	{
		z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9;
		z = (z ^ (z >> 27)) * 0x94D049BB133111EB;
		return z ^ (z >> 31);
	}
}
