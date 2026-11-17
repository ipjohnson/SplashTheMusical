using AwesomeAssertions;
using CsCheck;
using SplashTheMusical.Layout.Paths;

namespace SplashTheMusical.Tests.Layout;

/// <summary>
/// The curve writers' formatting and names. tests/SplashTheMusical.Conformance/CurveTests.cs compares their paths with
/// d3-shape's and mermaid's.
/// </summary>
public class CurveWriterTests
{
	[Test]
	public void TheLayoutCopyOfJsNumberFormatsAsTheCores() =>
		Gen.Double.Sample(value =>
			SplashTheMusical.Layout.Paths.JsNumber.ToString(value).Should().Be(SplashTheMusical.JsNumber.ToString(value)));

	[Test]
	public void D3CurvesRoundToThreeDecimalsWithJavaScriptsRounding() =>
		CurveWriter.ToString(Curve.Linear, [new(0.0005, -0.0005), new(1.0015, 2e21)]).Should().Be("M0.001,0L1.002,2e+21");

	[Test]
	public void TheRoundedCurveWritesNumbersInFull() =>
		CurveWriter.ToString(Curve.Rounded, [new(0, 0), new(10, 0), new(10, 10.123456789)])
			.Should().Be("M0,0L5,0Q10,0 10,5L10,10.123456789");

	[Test]
	public void OnePointClosesAndNoPointsWriteNothing()
	{
		CurveWriter.ToString(Curve.Basis, [new(5, 5)]).Should().Be("M5,5Z");
		CurveWriter.ToString(Curve.Natural, []).Should().BeEmpty();
	}

	[Test]
	[Arguments("monotoneY", "monotoneY")]
	[Arguments("rounded", "rounded")]
	[Arguments("Basis", "basis")]
	[Arguments("curveLinear", "basis")]
	[Arguments(null, "basis")]
	public void UnknownNamesDrawBasisAsInsertEdgeDoes(string? name, string expected) =>
		CurveNames.Name(CurveNames.Parse(name)).Should().Be(expected);
}
