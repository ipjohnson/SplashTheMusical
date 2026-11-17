using AwesomeAssertions;
using SplashTheMusical.Rendering.Shapes;

namespace SplashTheMusical.Tests.Rendering.Shapes;

public class PathBoundsTests
{
	[Test]
	public void LinesCountTheirEnds()
	{
		PathBounds.Measure("M1,2 L5,-3 H8 V9 Z").Should().Be(new ShapeBounds(1, -3, 8, 9));
		PathBounds.Measure("M 1 2 l 4 -5 h 3 v 12").Should().Be(new ShapeBounds(1, -3, 8, 9));
	}

	[Test]
	public void AnArcCountsTheExtremesItPasses()
	{
		// From the left of the circle around (10, 10) to its right, with sweep 0 passing below.
		Rounded(PathBounds.Measure("M0,10 a10,10 0,0,0 20,0")).Should().Be(new ShapeBounds(0, 10, 20, 20));
		Rounded(PathBounds.Measure("M0,10 a10,10 0,0,1 20,0")).Should().Be(new ShapeBounds(0, 0, 20, 10));
	}

	[Test]
	public void ARotatedArcCountsTheExtremesOfItsRotatedEllipse()
	{
		// Half of an ellipse with radii 20 and 10, turned 90 degrees, so it is 10 wide and 40 tall.
		Rounded(PathBounds.Measure("M0,0 A20,10 90 0 1 0,40")).Should().Be(new ShapeBounds(0, 0, 10, 40));
	}

	[Test]
	public void ACubicCountsWhereItTurns()
	{
		// Symmetric about x = 5, peaking at 0.75 of its control points' height.
		Rounded(PathBounds.Measure("M0,0 C0,10 10,10 10,0")).Should().Be(new ShapeBounds(0, 0, 10, 7.5));
		// A straight cubic, as d3's curves write between collinear points.
		Rounded(PathBounds.Measure("M0,5 C2,5 4,5 6,5")).Should().Be(new ShapeBounds(0, 5, 6, 5));
		Rounded(PathBounds.Measure("M0,0 c0,10 10,10 10,0")).Should().Be(new ShapeBounds(0, 0, 10, 7.5));
	}

	[Test]
	public void AQuadraticCountsWhereItTurns() =>
		Rounded(PathBounds.Measure("M0,0 Q5,10 10,0")).Should().Be(new ShapeBounds(0, 0, 10, 5));

	private static ShapeBounds Rounded(ShapeBounds bounds) =>
		new(Math.Round(bounds.MinX, 9), Math.Round(bounds.MinY, 9), Math.Round(bounds.MaxX, 9), Math.Round(bounds.MaxY, 9));

	[Test]
	public void AnArcTooSmallForItsEndsIsScaledUp()
	{
		var bounds = PathBounds.Measure("M0,0 A1,1 0 0 1 20,0");

		bounds.MinY.Should().BeApproximately(-10, 1e-9);
		bounds.Width.Should().BeApproximately(20, 1e-9);
	}

	[Test]
	public void ARoughCircleBulgesPastItsEllipse()
	{
		var bounds = RoughEllipse.Bounds(0, 0, 14, 14);

		bounds.Width.Should().BeApproximately(14.013, 0.0005);
		bounds.Height.Should().BeApproximately(14, 0.0005);
	}
}
