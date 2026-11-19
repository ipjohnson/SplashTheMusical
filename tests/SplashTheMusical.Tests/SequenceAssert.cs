namespace SplashTheMusical.Tests;

/// <summary>Compares sequences with a <see cref="VerificationGlue.VerifySyntax.Verify(bool, string?)"/> that lists both sides, which a list's own text does not.</summary>
internal static class SequenceAssert
{
	public static void Equal<T>(IEnumerable<T> actual, params T[] expected) => Equal(actual, (IEnumerable<T>)expected);

	public static void Equal<T>(IEnumerable<T> actual, IEnumerable<T> expected)
	{
		var expectedItems = expected.ToArray();
		var actualItems = actual.ToArray();
		Verify(
			actualItems.SequenceEqual(expectedItems),
			$"expected:\n{string.Join('\n', expectedItems)}\nactual:\n{string.Join('\n', actualItems)}");
	}
}
