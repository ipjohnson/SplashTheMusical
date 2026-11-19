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
			Verify(SplashTheMusical.Layout.Paths.JsNumber.ToString(value) == SplashTheMusical.JsNumber.ToString(value)));

	[Test]
	public void D3CurvesRoundToThreeDecimalsWithJavaScriptsRounding() =>
		Verify(CurveWriter.ToString(Curve.Linear, [new(0.0005, -0.0005), new(1.0015, 2e21)]) == "M0.001,0L1.002,2e+21");

	[Test]
	public void TheRoundedCurveWritesNumbersInFull() =>
		Verify(CurveWriter.ToString(Curve.Rounded, [new(0, 0), new(10, 0), new(10, 10.123456789)]) == "M0,0L5,0Q10,0 10,5L10,10.123456789");

	[Test]
	public void OnePointClosesAndNoPointsWriteNothing()
	{
		Verify(CurveWriter.ToString(Curve.Basis, [new(5, 5)]) == "M5,5Z");
		Verify(!(CurveWriter.ToString(Curve.Natural, [])).Any());
	}

	[Test]
	[Arguments("monotoneY", "monotoneY")]
	[Arguments("rounded", "rounded")]
	[Arguments("Basis", "basis")]
	[Arguments("curveLinear", "basis")]
	[Arguments(null, "basis")]
	public void UnknownNamesDrawBasisAsInsertEdgeDoes(string? name, string expected) =>
		Verify(CurveNames.Name(CurveNames.Parse(name)) == expected);
}
