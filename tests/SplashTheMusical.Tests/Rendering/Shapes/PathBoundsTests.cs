using AwesomeAssertions;
using SplashTheMusical.Rendering.Shapes;

namespace SplashTheMusical.Tests.Rendering.Shapes;

public class PathBoundsTests
{
	[Test]
	public void LinesCountTheirEnds()
	{
		Verify(PathBounds.Measure("M1,2 L5,-3 H8 V9 Z") == new ShapeBounds(1, -3, 8, 9));
		Verify(PathBounds.Measure("M 1 2 l 4 -5 h 3 v 12") == new ShapeBounds(1, -3, 8, 9));
	}

	[Test]
	public void AnArcCountsTheExtremesItPasses()
	{
		// From the left of the circle around (10, 10) to its right, with sweep 0 passing below.
		Verify(Rounded(PathBounds.Measure("M0,10 a10,10 0,0,0 20,0")) == new ShapeBounds(0, 10, 20, 20));
		Verify(Rounded(PathBounds.Measure("M0,10 a10,10 0,0,1 20,0")) == new ShapeBounds(0, 0, 20, 10));
	}

	[Test]
	public void ARotatedArcCountsTheExtremesOfItsRotatedEllipse()
	{
		// Half of an ellipse with radii 20 and 10, turned 90 degrees, so it is 10 wide and 40 tall.
		Verify(Rounded(PathBounds.Measure("M0,0 A20,10 90 0 1 0,40")) == new ShapeBounds(0, 0, 10, 40));
	}

	[Test]
	public void ACubicCountsWhereItTurns()
	{
		// Symmetric about x = 5, peaking at 0.75 of its control points' height.
		Verify(Rounded(PathBounds.Measure("M0,0 C0,10 10,10 10,0")) == new ShapeBounds(0, 0, 10, 7.5));
		// A straight cubic, as d3's curves write between collinear points.
		Verify(Rounded(PathBounds.Measure("M0,5 C2,5 4,5 6,5")) == new ShapeBounds(0, 5, 6, 5));
		Verify(Rounded(PathBounds.Measure("M0,0 c0,10 10,10 10,0")) == new ShapeBounds(0, 0, 10, 7.5));
	}

	[Test]
	public void APathEndsAtANumberThatIsNotFinite()
	{
		// JavaScript writes a NaN coordinate, as an edge to a node of NaN width gets, as "NaN".
		Verify(PathBounds.Measure("M1,2 L5,6 LNaN,NaN L20,20") == new ShapeBounds(1, 2, 5, 6));
		Verify(PathBounds.Measure("M1,2 L5,6 C7,7 -Infinity,1 9,9") == new ShapeBounds(1, 2, 5, 6));
		Verify(PathBounds.Measure("MNaN,NaN L5,6") == ShapeBounds.None);
	}

	[Test]
	public void AQuadraticCountsWhereItTurns() =>
		Verify(Rounded(PathBounds.Measure("M0,0 Q5,10 10,0")) == new ShapeBounds(0, 0, 10, 5));

	private static ShapeBounds Rounded(ShapeBounds bounds) =>
		new(Math.Round(bounds.MinX, 9), Math.Round(bounds.MinY, 9), Math.Round(bounds.MaxX, 9), Math.Round(bounds.MaxY, 9));

	[Test]
	public void AnArcTooSmallForItsEndsIsScaledUp()
	{
		var bounds = PathBounds.Measure("M0,0 A1,1 0 0 1 20,0");

		Verify(Math.Abs(bounds.MinY - (-10)) <= 1e-9);
		Verify(Math.Abs(bounds.Width - 20) <= 1e-9);
	}

	[Test]
	public void ARoughCircleBulgesPastItsEllipse()
	{
		var bounds = RoughEllipse.Bounds(0, 0, 14, 14);

		Verify(Math.Abs(bounds.Width - 14.013) <= 0.0005);
		Verify(Math.Abs(bounds.Height - 14) <= 0.0005);
	}
}
