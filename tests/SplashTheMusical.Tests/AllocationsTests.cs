using System.Runtime.CompilerServices;
using AwesomeAssertions;

namespace SplashTheMusical.Tests;

public class AllocationsTests
{
	// An object with no fields is 24 bytes on a 64-bit runtime: the header, the method table pointer and one minimum-size slot.
	private const long ObjectBytes = 24;

	private static object? Sink;

	[Test]
	public void MeasuresOneObject() => Allocations.Measure(() => Sink = OneObject()).Should().Be(ObjectBytes);

	[Test]
	public void MeasuresNothingForACallThatDoesNotAllocate() => Allocations.Measure(() => Sink = null).Should().Be(0);

	[Test]
	public void PassesWhenTheCallAllocatesExactlyTheBytes() =>
		FluentActions.Invoking(() => Allocations.ShouldAllocateExactly(ObjectBytes, () => Sink = OneObject())).Should().NotThrow();

	[Test]
	public void FailsWhenTheCallAllocatesOneMoreObject() =>
		FluentActions.Invoking(() => Allocations.ShouldAllocateExactly(ObjectBytes, () => Sink = TwoObjects()))
			.Should().Throw<AllocationException>()
			.WithMessage($"Expected the call to allocate {ObjectBytes} bytes, but it allocated {2 * ObjectBytes}.");

	// NoInlining and the static sink keep the JIT from allocating the objects on the stack.
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static object OneObject() => new();

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static object TwoObjects()
	{
		Sink = new object();
		return new object();
	}
}
