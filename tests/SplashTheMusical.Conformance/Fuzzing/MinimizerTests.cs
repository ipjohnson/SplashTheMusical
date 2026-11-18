using SplashTheMusical.Conformance.Fuzzing;

namespace SplashTheMusical.Conformance;

public class MinimizerTests
{
	[Test]
	public async Task RemovesEveryLineAndTokenTheTestDoesNotNeed()
	{
		const string Source = "flowchart LR\n  A-->B\n  B-->C\n  C[needle]-->D\n  D-->E\n  E-->F\n";

		var minimized = Minimizer.Minimize(Source, s => s.Contains("flowchart", StringComparison.Ordinal) && s.Contains("needle", StringComparison.Ordinal), 1000, out var tests);

		await Assert.That(minimized).IsEqualTo("flowchartneedle");
		await Assert.That(tests).IsGreaterThan(0);
	}

	[Test]
	public async Task StopsAfterItsTests()
	{
		var calls = 0;

		var minimized = Minimizer.Minimize("a\nb\nc\nd\ne\nf\n", s =>
		{
			calls++;
			return true;
		}, 3, out var tests);

		await Assert.That(calls).IsEqualTo(3);
		await Assert.That(tests).IsEqualTo(3);
		await Assert.That(minimized.Length).IsLessThan(12);
	}

	[Test]
	public async Task LeavesASourceItCannotShrink()
	{
		var minimized = Minimizer.Minimize("A-->B", s => s == "A-->B", 100, out _);

		await Assert.That(minimized).IsEqualTo("A-->B");
	}
}
