namespace SplashTheMusical.Conformance;

public class ResultsFileTests
{
	[Test]
	public void EachCaseAndLevelIsALineWithItsDifferencesBelowIt()
	{
		var oracle = new CaseFacts(true, null, ["type\tpie", "label\tA"], ["box\tnode\tA\t0\t0\t10\t10"], "canvas\t0\t0\t10\t10");
		var actual = new CaseFacts(true, null, ["type\tpie", "label\tB"], ["box\tnode\tA\t0\t0\t10\t10"], "canvas\t0\t0\t10\t10");
		var refused = CaseFacts.Refused("no");

		var text = ResultsFile.Text([Comparer.Compare("pie\\b", refused, refused), Comparer.Compare("pie/a", oracle, actual)]);

		LineAssert.Equal(
			text.Split('\n'),
			"pie/a\tL0\tpass",
			"pie/a\tL1\tpass",
			"pie/a\tL2\tfail",
			"\tL2 - label\tA",
			"\tL2 + label\tB",
			"pie/a\tL3\tpass",
			"pie/b\tL0\tpass",
			"pie/b\tL1\tn/a",
			"pie/b\tL2\tn/a",
			"pie/b\tL3\tn/a",
			""
		);
	}
}
