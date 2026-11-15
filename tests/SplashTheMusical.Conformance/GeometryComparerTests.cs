namespace SplashTheMusical.Conformance;

public class GeometryComparerTests
{
	private static string Fixtures => Path.Combine(CorpusCheck.RepositoryRoot, "tests", "SplashTheMusical.Conformance", "Fixtures", "L3");

	[Test]
	public async Task BoxesWithinTheTolerancesOnAMovedCanvasPass()
	{
		var result = Compare("pass");

		await Assert.That(result.Outcome).IsEqualTo(Outcome.Pass);
		await Assert.That(result.Differences).IsEmpty();
	}

	[Test]
	public async Task ACanvasMoreThanFivePercentWiderFails() =>
		await LineAssert.Equal(Lines(Compare("canvas")), "L3 - canvas\t0\t0\t400\t300", "L3 + canvas\t0\t0\t440\t300");

	[Test]
	public async Task ABoxThatLeavesItsRankFails() =>
		await LineAssert.Equal(
			Lines(Compare("order")),
			"L3 + order\ty\tclass\tA\t<\tclass\tB",
			"L3 - order\ty\tclass\tA\t=\tclass\tB",
			"L3 - order\ty\tclass\tB\t=\tnamespace\tN",
			"L3 + order\ty\tclass\tB\t>\tnamespace\tN"
		);

	[Test]
	public async Task ABoxThatCoversTooLittleOfItsCounterpartFails() =>
		await LineAssert.Equal(Lines(Compare("overlap")), "L3 - box\tclass\tC\t110\t180\t100\t80", "L3 + box\tclass\tC\t95\t180\t130\t80");

	[Test]
	public async Task AnOverlapMermaidJsDoesNotHaveFails() =>
		await LineAssert.Equal(Lines(Compare("new-overlap")), "L3 + overlap\tlabel-node\tlabel relation-label 'A --> C' / node class 'A'");

	[Test]
	public async Task AnOverlapMermaidJsAlsoHasPasses()
	{
		var overlapping = Read("new-overlap");

		var result = Comparer.Compare("class/fixture", overlapping, overlapping)[Level.L3];

		await Assert.That(result.Outcome).IsEqualTo(Outcome.Pass);
	}

	[Test]
	public async Task ALooserTypeToleranceLetsTheWiderCanvasPass()
	{
		var result = Comparer.Compare("class/fixture", Read("oracle"), Read("canvas"), new Tolerance(0.1, 0.8, 0.9, Reason: "wider"))[Level.L3];

		await Assert.That(result.Outcome).IsEqualTo(Outcome.Pass);
	}

	[Test]
	public async Task AProviderWithoutACanvasFailsOnTheCanvasAlone()
	{
		var result = Comparer.Compare("class/fixture", Read("oracle"), CaseFacts.Refused("no"))[Level.L3];

		await LineAssert.Equal(Lines(result), "L3 - canvas\t0\t0\t400\t300");
	}

	private static LevelResult Compare(string fixture) => Comparer.Compare("class/fixture", Read("oracle"), Read(fixture))[Level.L3];

	private static CaseFacts Read(string fixture)
	{
		var (boxes, canvas) = OracleFiles.ReadBoxes(Path.Combine(Fixtures, fixture + ".boxes.txt"));
		return new CaseFacts(true, null, [], boxes, canvas);
	}

	private static IEnumerable<string> Lines(LevelResult result) => result.Differences.Select(d => d.ToString());
}
