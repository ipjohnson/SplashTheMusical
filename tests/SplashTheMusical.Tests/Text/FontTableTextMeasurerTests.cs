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
		Verify(Math.Abs(Width("Hello John, how are you?", Regular) - 180.563) <= 0.05);

	[Test]
	public void AppliesKerning() =>
		Verify(Width("AV", Regular) < Width("A", Regular) + Width("V", Regular));

	[Test]
	public void TakesTheBoldFaceOverWeight500()
	{
		var bold = Width("Hello", Regular with { Weight = 600 });
		var medium = Width("Hello", Regular with { Weight = 500 });

		Verify(bold > medium);
	}

	[Test]
	[Arguments(400, FontStyle.Normal, 0)]
	[Arguments(501, FontStyle.Normal, 1)]
	[Arguments(400, FontStyle.Italic, 2)]
	[Arguments(400, FontStyle.Oblique, 2)]
	[Arguments(900, FontStyle.Italic, 3)]
	public void TakesTheFaceOfTheWeightAndStyle(int weight, FontStyle style, int face)
	{
		var font = Regular with { Weight = weight, Style = style };

		Verify(FontTableTextMeasurer.FaceIndex(font) == face);
	}

	[Test]
	public void ScalesWithTheSize()
	{
		var doubled = Width("Hello", Regular with { Size = 32 });

		Verify(Math.Abs(doubled - (2 * Width("Hello", Regular))) <= 1e-9);
	}

	[Test]
	[Arguments("Liberation Sans")]
	[Arguments("'Arial'")]
	[Arguments("  helvetica , serif")]
	[Arguments("Unknown Font")]
	[Arguments("")]
	public void FallsBackByFamilyNameThenToSansSerif(string family)
	{
		var font = Regular with { Family = family };

		Verify(Math.Abs(Width("Hello", font) - Width("Hello", Regular)) <= 1e-9);
	}

	[Test]
	public void GivesTheLineBoxOfTheFont()
	{
		var metrics = TextMeasurer.Default.Measure("Hello", Regular);
		Verify(Math.Abs(metrics.Ascent - (1854.0 / 2048 * 16)) <= 1e-9);
		Verify(Math.Abs(metrics.Descent - (434.0 / 2048 * 16)) <= 1e-9);
	}

	[Test]
	public void GivesWideCharactersOneEm() => Verify(Math.Abs(Width("漢字", Regular) - 32) <= 1e-9);

	[Test]
	public void GivesAnEmojiOneEm() => Verify(Math.Abs(Width("\U0001F600", Regular) - 16) <= 1e-9);

	[Test]
	public void GivesMarksAndFormatCharactersNoWidth() => Verify(Math.Abs(Width("é‍", Regular) - Width("e", Regular)) <= 1e-9);

	[Test]
	public void GivesOtherScriptsTheMeanLetterWidth() =>
		Verify(Math.Abs(Width("क", Regular) - (1003.0 / 2048 * 16)) <= 1e-9);

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
		Verify(typeof(FontTableTextMeasurer).TypeInitializer is null);
		Verify(typeof(FontTables).TypeInitializer is null);
	}

	[Test]
	public void TheGeneratorRewritesTheCommittedTablesWithNoDiff()
	{
		var repository = Repository();
		var committed = File.ReadAllText(Path.Combine(repository, "src", "SplashTheMusical", "Text", "Generated", "FontTables.g.cs"));
		Verify(FontTableWriter.Write(repository, FontTableWriter.Families) == committed);
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
