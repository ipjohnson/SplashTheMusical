using AwesomeAssertions;
using SplashTheMusical.Parsing;

namespace SplashTheMusical.Tests.Parsing;

public class IdTableTests
{
	private static object? Sink;

	[Test]
	public void NumbersIdsInTheOrderTheyFirstAppear()
	{
		var source = "A --> B --> A --> C";
		var table = new IdTable();
		table.GetOrAdd(source.AsSpan(0, 1)).Should().Be(0);
		table.GetOrAdd(source.AsSpan(6, 1)).Should().Be(1);
		table.GetOrAdd(source.AsSpan(12, 1)).Should().Be(0);
		table.GetOrAdd(source.AsSpan(18, 1)).Should().Be(2);
		table.Count.Should().Be(3);
		table[2].Should().Be("C");
		table.TryGetIndex("B", out var index).Should().BeTrue();
		index.Should().Be(1);
		table.TryGetIndex("D", out _).Should().BeFalse();
	}

	[Test]
	public void IdsAreCaseSensitive()
	{
		var table = new IdTable();
		table.GetOrAdd("a").Should().NotBe(table.GetOrAdd("A"));
	}

	[Test]
	public void AllocatesOneStringPerDistinctId()
	{
		var source = "A node A longer_id node B A";
		string[] distinct = ["A", "node", "longer_id", "B"];

		var empty = Allocations.Measure(() => Sink = new IdTable(8));
		var filled = Allocations.Measure(() =>
		{
			var table = new IdTable(8);
			foreach (var range in source.AsSpan().Split(' '))
			{
				table.GetOrAdd(source.AsSpan()[range]);
			}

			Sink = table;
		});

		(filled - empty).Should().Be(distinct.Sum(StringBytes));
	}

	// A string on a 64-bit runtime: the header, the method table pointer, the length, the characters and a terminating
	// null, rounded up to 8 bytes.
	private static long StringBytes(string text) => (22 + (2 * text.Length) + 7) & ~7;
}
