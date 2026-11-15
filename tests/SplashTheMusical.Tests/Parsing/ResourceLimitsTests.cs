using AwesomeAssertions;
using SplashTheMusical.Parsing;

namespace SplashTheMusical.Tests.Parsing;

public class ResourceLimitsTests
{
	private static readonly Dictionary<string, Action> Exceeding = new()
	{
		[nameof(ResourceLimits.MaxInputLength)] = () => ResourceGuard.Start("abcd", new ResourceLimits { MaxInputLength = 3 }),
		[nameof(ResourceLimits.MaxLines)] = () =>
		{
			var cursor = new SourceCursor("A\nB\nC");
			var guard = ResourceGuard.Start(cursor.Source, new ResourceLimits { MaxLines = 2 });
			for (var i = 0; i < 3; i++)
			{
				cursor.ReadIdentifier();
				guard.Statement(cursor);
				cursor.SkipLineBreak();
			}
		},
		[nameof(ResourceLimits.MaxLineLength)] = () =>
		{
			var cursor = new SourceCursor("A-->B\nABCDEF\n");
			var guard = ResourceGuard.Start(cursor.Source, new ResourceLimits { MaxLineLength = 5 });
			cursor.Advance(6);
			guard.Statement(cursor);
			cursor.Advance(7);
			guard.Statement(cursor);
		},
		[nameof(ResourceLimits.MaxElements)] = () =>
		{
			var cursor = new SourceCursor("A");
			ResourceGuard.Start(cursor.Source, new ResourceLimits { MaxElements = 5 }).Elements(6, cursor);
		},
		[nameof(ResourceLimits.MaxRecursionDepth)] = () =>
		{
			var cursor = new SourceCursor("A");
			ResourceGuard.Start(cursor.Source, new ResourceLimits { MaxRecursionDepth = 2 }).Depth(3, cursor);
		},
		[nameof(ResourceLimits.MaxNodesAfterLayout)] = () =>
			ResourceGuard.NodesAfterLayout(11, new ResourceLimits { MaxNodesAfterLayout = 10 }),
		[nameof(ResourceLimits.MaxOutputLength)] = () => ResourceGuard.OutputLength(101, new ResourceLimits { MaxOutputLength = 100 }),
		[nameof(ResourceLimits.RenderDeadline)] = () =>
		{
			var clock = new ManualClock();
			var cursor = new SourceCursor("A\nB");
			var guard = ResourceGuard.Start(
				cursor.Source,
				new ResourceLimits { RenderDeadline = TimeSpan.FromSeconds(1), TimeProvider = clock });
			guard.Statement(cursor);
			clock.Now += TimeSpan.FromSeconds(2);
			guard.Statement(cursor);
		},
	};

	[Test]
	public void EveryLimitHasACase()
	{
		var limits = typeof(ResourceLimits).GetProperties()
			.Where(property => !property.GetMethod!.IsStatic && property.PropertyType != typeof(TimeProvider))
			.Select(property => property.Name);
		Exceeding.Keys.Should().BeEquivalentTo(limits);
	}

	[Test]
	public void EachLimitThrowsWhenExceeded()
	{
		foreach (var (name, action) in Exceeding)
		{
			FluentActions.Invoking(action).Should().ThrowExactly<MermaidResourceLimitException>()
				.Which.LimitName.Should().Be(name);
		}
	}

	[Test]
	public void ALimitIsNotExceededAtItsValue()
	{
		var limits = new ResourceLimits { MaxInputLength = 3, MaxLines = 1, MaxLineLength = 3, MaxElements = 1 };
		var cursor = new SourceCursor("abc");
		var guard = ResourceGuard.Start(cursor.Source, limits);
		cursor.ReadIdentifier();
		guard.Statement(cursor);
		guard.Elements(1, cursor);
	}

	[Test]
	public void TheExceptionIsAParseExceptionWithThePosition()
	{
		var exception = FluentActions.Invoking(Exceeding[nameof(ResourceLimits.MaxLines)])
			.Should().Throw<MermaidParseException>().Which;
		exception.Diagnostics.Should().ContainSingle();
		var diagnostic = exception.Diagnostics[0];
		diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
		(diagnostic.Line, diagnostic.Column).Should().Be((3, 2));
		exception.Message.Should().Be(
			"3:2: error: Resource limit exceeded. MaxLines: 3 > 2. Raise the limit, or use ResourceLimits.Unlimited for trusted input.");
	}

	[Test]
	public void TheDefaultsAreMermaiders()
	{
		var limits = ResourceLimits.Default;
		limits.MaxInputLength.Should().Be(512 * 1024);
		limits.MaxLines.Should().Be(10_000);
		limits.MaxLineLength.Should().Be(8_000);
		limits.MaxElements.Should().Be(5_000);
		limits.MaxNodesAfterLayout.Should().Be(20_000);
		limits.MaxRecursionDepth.Should().Be(64);
		limits.MaxOutputLength.Should().Be(8 * 1024 * 1024);
		limits.RenderDeadline.Should().Be(TimeSpan.FromSeconds(5));
		limits.TimeProvider.Should().BeSameAs(TimeProvider.System);
	}

	[Test]
	public void UnlimitedTurnsEveryLimitOff()
	{
		var limits = ResourceLimits.Unlimited;
		limits.RenderDeadline.Should().BeNull();
		new[]
		{
			limits.MaxInputLength, limits.MaxLines, limits.MaxLineLength, limits.MaxElements, limits.MaxNodesAfterLayout,
			limits.MaxRecursionDepth, limits.MaxOutputLength,
		}.Should().AllBeEquivalentTo(int.MaxValue);
	}

	private sealed class ManualClock : TimeProvider
	{
		public TimeSpan Now { get; set; }

		public override long TimestampFrequency => TimeSpan.TicksPerSecond;

		public override long GetTimestamp() => Now.Ticks;
	}
}
