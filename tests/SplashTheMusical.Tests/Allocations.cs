namespace SplashTheMusical.Tests;

/// <summary>Measures the bytes one call allocates on the current thread.</summary>
public static class Allocations
{
	/// <summary>
	/// The bytes the second of two calls allocates. The first call runs the static constructors and the JIT, which allocate
	/// on the same thread.
	/// </summary>
	public static long Measure(Action action)
	{
		action();
		var before = GC.GetAllocatedBytesForCurrentThread();
		action();
		return GC.GetAllocatedBytesForCurrentThread() - before;
	}

	/// <summary>Throws when one call does not allocate exactly <paramref name="bytes"/> bytes.</summary>
	public static void ShouldAllocateExactly(long bytes, Action action)
	{
		var actual = Measure(action);
		if (actual != bytes)
		{
			throw new AllocationException($"Expected the call to allocate {bytes} bytes, but it allocated {actual}.");
		}
	}
}

/// <summary>A call allocated a different number of bytes than the test expects.</summary>
public sealed class AllocationException(string message) : Exception(message);
