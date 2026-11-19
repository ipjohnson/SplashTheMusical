namespace SplashTheMusical.Conformance;

/// <summary>
/// Ordered equality of lines. A failure names the first line that differs, counted from 1, with the expected and the
/// actual line. A line past the end of one side shows as <c>(none)</c>.
/// </summary>
internal static class LineAssert
{
	public static void Equal(IEnumerable<string> actual, params IEnumerable<string> expected) =>
		Equal(actual, expected, null);

	/// <summary>Ordered equality of lines, with <paramref name="reason"/> on the failure's first message line.</summary>
	public static void Equal(IEnumerable<string> actual, IEnumerable<string> expected, string? reason)
	{
		var actualLines = actual.ToList();
		var expectedLines = expected.ToList();
		var firstDifference = FirstDifference(actualLines, expectedLines);
		Verify(
			firstDifference is null,
			$"{reason}{(reason is null ? "" : "\n    ")}line {firstDifference + 1} of {expectedLines.Count} expected, {actualLines.Count} actual, differs\n    expected: {Line(expectedLines, firstDifference)}\n    actual:   {Line(actualLines, firstDifference)}");
	}

	private static int? FirstDifference(List<string> actual, List<string> expected)
	{
		var count = Math.Max(actual.Count, expected.Count);
		for (var i = 0; i < count; i++)
		{
			if (i >= actual.Count || i >= expected.Count || !string.Equals(actual[i], expected[i], StringComparison.Ordinal))
			{
				return i;
			}
		}

		return null;
	}

	private static string Line(List<string> lines, int? index) =>
		index is { } i && i < lines.Count ? $"\"{lines[i]}\"" : "(none)";
}
