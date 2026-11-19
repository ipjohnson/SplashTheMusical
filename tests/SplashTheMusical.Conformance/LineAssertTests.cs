using VerificationGlue;

namespace SplashTheMusical.Conformance;

public class LineAssertTests
{
	[Test]
	public void EqualLinesPass() => LineAssert.Equal(["a", "b"], "a", "b");

	[Test]
	public void AFailureNamesTheFirstDifferingLineWithBothValues()
	{
		var failure = Throws<VerifyException>(() => LineAssert.Equal(["node\tA", "edge\tA\tB", "node\tC"], "node\tA", "edge\tA\tC", "node\tD"));

		Console.WriteLine(failure.Message);
		Verify(failure.Message.Contains("line 2 of 3 expected, 3 actual, differs", StringComparison.Ordinal), $"{failure.Message}");
		Verify(failure.Message.Contains("expected: \"edge\tA\tC\"", StringComparison.Ordinal), $"{failure.Message}");
		Verify(failure.Message.Contains("actual:   \"edge\tA\tB\"", StringComparison.Ordinal), $"{failure.Message}");
	}

	[Test]
	public void AMissingLineShowsAsNone()
	{
		var failure = Throws<VerifyException>(() => LineAssert.Equal(["a"], ["a", "b"], "the reason"));

		Verify(failure.Message.Contains("the reason\n    line 2 of 2 expected, 1 actual, differs", StringComparison.Ordinal), $"{failure.Message}");
		Verify(failure.Message.Contains("actual:   (none)", StringComparison.Ordinal), $"{failure.Message}");
	}
}
