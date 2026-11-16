using AwesomeAssertions;
using SplashTheMusical.Text;
using SplashTheMusical.Upstream;

namespace SplashTheMusical.Tests.Text;

public class FontTableTextMeasurerTests
{
	private const string MermaidFamily = "\"trebuchet ms\", verdana, arial, sans-serif";

	private static readonly FontSpec Regular = new(MermaidFamily, 16);

	private static double Sink;

	private static double Width(string text, FontSpec font) => TextMeasurer.Default.Measure(text, font).Width;

	[Test]
	public void MeasuresTheAdvancesOfTheHarnessFont() =>
		// Chromium measured 180.563 for this message text in the oracle's corpus/sequence/docs-sequenceDiagram-001.
		Width("Hello John, how are you?", Regular).Should().BeApproximately(180.563, 0.05);

	[Test]
	public void AppliesKerning() =>
		Width("AV", Regular).Should().BeLessThan(Width("A", Regular) + Width("V", Regular));

	[Test]
	public void TakesTheBoldFaceOverWeight500() =>
		Width("Hello", Regular with { Weight = 600 }).Should().BeGreaterThan(Width("Hello", Regular with { Weight = 500 }));

	[Test]
	[Arguments(400, FontStyle.Normal, 0)]
	[Arguments(501, FontStyle.Normal, 1)]
	[Arguments(400, FontStyle.Italic, 2)]
	[Arguments(400, FontStyle.Oblique, 2)]
	[Arguments(900, FontStyle.Italic, 3)]
	public void TakesTheFaceOfTheWeightAndStyle(int weight, FontStyle style, int face) =>
		FontTableTextMeasurer.FaceIndex(Regular with { Weight = weight, Style = style }).Should().Be(face);

	[Test]
	public void ScalesWithTheSize() => Width("Hello", Regular with { Size = 32 }).Should().BeApproximately(2 * Width("Hello", Regular), 1e-9);

	[Test]
	[Arguments("Liberation Sans")]
	[Arguments("'Arial'")]
	[Arguments("  helvetica , serif")]
	[Arguments("Unknown Font")]
	[Arguments("")]
	public void FallsBackByFamilyNameThenToSansSerif(string family) =>
		Width("Hello", Regular with { Family = family }).Should().Be(Width("Hello", Regular));

	[Test]
	public void GivesTheLineBoxOfTheFont()
	{
		var metrics = TextMeasurer.Default.Measure("Hello", Regular);
		metrics.Ascent.Should().BeApproximately(1854.0 / 2048 * 16, 1e-9);
		metrics.Descent.Should().BeApproximately(434.0 / 2048 * 16, 1e-9);
	}

	[Test]
	public void GivesWideCharactersOneEm() => Width("漢字", Regular).Should().Be(32);

	[Test]
	public void GivesAnEmojiOneEm() => Width("\U0001F600", Regular).Should().Be(16);

	[Test]
	public void GivesMarksAndFormatCharactersNoWidth() => Width("é‍", Regular).Should().Be(Width("e", Regular));

	[Test]
	public void GivesOtherScriptsTheMeanLetterWidth() =>
		Width("क", Regular).Should().BeApproximately(1003.0 / 2048 * 16, 1e-9);

	[Test]
	public void MeasuringAStringAllocatesNothing()
	{
		var measurer = TextMeasurer.Default;
		var font = new FontSpec("\"Trebuchet MS\", Unknown, Arial", 14, 700, FontStyle.Italic);
		Allocations.ShouldAllocateExactly(0, () => Sink = measurer.Measure("AVATAR, Ünïcödé 漢字 \U0001F600 क", font).Width);
	}

	[Test]
	public void TheTablesAreStaticData()
	{
		typeof(FontTableTextMeasurer).TypeInitializer.Should().BeNull();
		typeof(FontTables).TypeInitializer.Should().BeNull();
	}

	[Test]
	public void TheGeneratorRewritesTheCommittedTablesWithNoDiff()
	{
		var repository = Repository();
		var committed = File.ReadAllText(Path.Combine(repository, "src", "SplashTheMusical", "Text", "Generated", "FontTables.g.cs"));
		FontTableWriter.Write(repository, FontTableWriter.Families).Should().Be(committed);
	}

	private static string Repository()
	{
		for (var folder = AppContext.BaseDirectory; folder is not null; folder = Path.GetDirectoryName(folder))
		{
			if (File.Exists(Path.Combine(folder, "SplashTheMusical.slnx")))
			{
				return folder;
			}
		}

		throw new InvalidOperationException("The tests run outside the repository.");
	}
}
