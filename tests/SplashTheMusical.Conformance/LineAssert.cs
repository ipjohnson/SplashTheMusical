namespace SplashTheMusical.Conformance;

/// <summary>
/// Ordered equality of lines under native AOT. TUnit's collection <c>IsEquivalentTo</c> needs reflection, so the lines
/// are joined and compared as one string.
/// </summary>
internal static class LineAssert
{
	public static async Task Equal(IEnumerable<string> actual, params IEnumerable<string> expected) =>
		await Assert.That(string.Join('\n', actual)).IsEqualTo(string.Join('\n', expected));
}
