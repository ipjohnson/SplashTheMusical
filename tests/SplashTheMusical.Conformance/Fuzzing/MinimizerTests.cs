using SplashTheMusical.Conformance.Fuzzing;

namespace SplashTheMusical.Conformance;

public class MinimizerTests
{
	[Test]
	public void RemovesEveryLineAndTokenTheTestDoesNotNeed()
	{
		const string Source = "flowchart LR\n  A-->B\n  B-->C\n  C[needle]-->D\n  D-->E\n  E-->F\n";

		var minimized = Minimizer.Minimize(Source, s => s.Contains("flowchart", StringComparison.Ordinal) && s.Contains("needle", StringComparison.Ordinal), 1000, out var tests);

		Verify(minimized == "flowchartneedle", $"was {minimized}");
		Verify(tests > 0, $"was {tests}");
	}

	[Test]
	public void StopsAfterItsTests()
	{
		var calls = 0;

		var minimized = Minimizer.Minimize("a\nb\nc\nd\ne\nf\n", s =>
		{
			calls++;
			return true;
		}, 3, out var tests);

		Verify(calls == 3, $"was {calls}");
		Verify(tests == 3, $"was {tests}");
		Verify(minimized.Length < 12, $"was {minimized.Length}");
	}

	[Test]
	public void LeavesASourceItCannotShrink()
	{
		var minimized = Minimizer.Minimize("A-->B", s => s == "A-->B", 100, out _);

		Verify(minimized == "A-->B", $"was {minimized}");
	}
}
