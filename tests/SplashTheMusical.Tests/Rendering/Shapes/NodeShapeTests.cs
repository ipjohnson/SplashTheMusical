using AwesomeAssertions;
using SplashTheMusical.Rendering.Shapes;

namespace SplashTheMusical.Tests.Rendering.Shapes;

public class NodeShapeTests
{
	private sealed class FixedLabel(double width, double height) : IShapeLabelMeasurer
	{
		public ShapeLabel Measure(double? nodeWidth, bool ignoreMinWidth) => new(width, height, Html: true);
	}

	private static ShapeNode Node(ShapeLook look = ShapeLook.Classic, double width = 120, double height = 24) =>
		new() { Label = new FixedLabel(width, height), Look = look, Padding = 15 };

	public static IEnumerable<(string Handler, string Look)> EveryShapeInEveryLook() =>
		from shape in NodeShapes.All
		from look in new[] { "classic", "neo", "handDrawn" }
		select (shape.Handler, look);

	[Test]
	[MethodDataSource(nameof(EveryShapeInEveryLook))]
	public void EveryShapeDrawsAnOutlineThatEdgesMeetOnItsBox(string handler, string look)
	{
		var geometry = NodeShapes.ForHandler(handler)!.Draw(Node(look switch
		{
			"neo" => ShapeLook.Neo,
			"handDrawn" => ShapeLook.HandDrawn,
			_ => ShapeLook.Classic,
		}));

		geometry.Width.Should().BeGreaterThan(0);
		geometry.Height.Should().BeGreaterThan(0);
		geometry.Elements.Should().NotBeEmpty();
		foreach (var far in new ShapePoint[] { new(1000, 0), new(-1000, 0), new(0, 1000), new(0, -1000), new(700, 700) })
		{
			var hit = geometry.IntersectFrom(new ShapePoint(0, 0), far);
			Math.Abs(hit.X).Should().BeLessThanOrEqualTo(geometry.Width / 2 + 0.5, $"{handler} towards {far}");
			Math.Abs(hit.Y).Should().BeLessThanOrEqualTo(geometry.Height / 2 + 0.5, $"{handler} towards {far}");
			if (handler != "hourglass")
			{
				// The hourglass's outline crosses itself at the centre, where a horizontal edge meets it.
				(Math.Abs(hit.X) + Math.Abs(hit.Y)).Should().BeGreaterThan(0, $"{handler} towards {far}");
			}
		}
	}

	[Test]
	public void TheRectFitsItsLabelAndMeetsEdgesOnItsSides()
	{
		var geometry = NodeShapes.Find("rect")!.Draw(Node());

		geometry.Width.Should().Be(180);
		geometry.Height.Should().Be(54);
		geometry.LabelTranslate.Should().Be(new ShapePoint(-60, -12));
		geometry.IntersectFrom(new ShapePoint(100, 50), new ShapePoint(300, 50)).Should().Be(new ShapePoint(190, 50));
		geometry.IntersectFrom(new ShapePoint(100, 50), new ShapePoint(100, -100)).Should().Be(new ShapePoint(100, 23));
	}

	[Test]
	public void TheNeoLookPadsTheRectWithItsOwnPaddings()
	{
		var geometry = NodeShapes.Find("rect")!.Draw(Node(ShapeLook.Neo, height: 21));

		geometry.Width.Should().Be(152);
		geometry.Height.Should().Be(45);
	}

	[Test]
	public void TheHexagonMeetsAHorizontalEdgeAtItsPoint()
	{
		var geometry = NodeShapes.Find("hex")!.Draw(Node());
		var m = (24 + 15) / 4.0;

		geometry.Width.Should().BeApproximately(120 + 2 * m + 15, 1e-9);
		geometry.IntersectFrom(new ShapePoint(0, 0), new ShapePoint(500, 0)).X.Should().BeApproximately(geometry.Width / 2, 1e-9);
	}

	[Test]
	public void TheDiamondMeetsEdgesOnItsSides()
	{
		var geometry = NodeShapes.Find("diam")!.Draw(Node());
		var s = 120 + 15 + 24 + 15;
		var hit = geometry.IntersectFrom(new ShapePoint(0, 0), new ShapePoint(300, 100));

		geometry.Width.Should().Be(s);
		(Math.Abs(hit.X) + Math.Abs(hit.Y)).Should().BeApproximately(s / 2.0, 1e-9);
	}

	[Test]
	public void TheCircleMeetsEdgesAtItsRadius()
	{
		var geometry = NodeShapes.Find("circle")!.Draw(Node(width: 30, height: 40));
		var radius = 25 + 7.5;
		var hit = geometry.IntersectFrom(new ShapePoint(10, 10), new ShapePoint(110, 110));

		geometry.Width.Should().BeApproximately(2 * radius, 1e-9);
		Math.Sqrt((hit.X - 10) * (hit.X - 10) + (hit.Y - 10) * (hit.Y - 10)).Should().BeApproximately(radius, 1e-9);
	}

	[Test]
	public void TheCylinderIsAsTallAsItsSidesAndBothRims()
	{
		var geometry = NodeShapes.Find("database")!.Draw(Node());
		var w = 120 + 15.0;
		var ry = w / 2 / (2.5 + w / 50);
		var h = 24 + 15 + ry;
		var top = geometry.IntersectFrom(new ShapePoint(0, 0), new ShapePoint(0, -500));

		geometry.Width.Should().BeApproximately(w, 1e-9);
		geometry.Height.Should().BeApproximately(h + 2 * ry, 1e-9);
		top.Y.Should().BeApproximately(-geometry.Height / 2, 1e-9);
	}

	[Test]
	public void ShapesWithoutALabelMeasureNone()
	{
		foreach (var name in new[] { "f-circ", "fork", "bolt", "cross-circ", "sm-circ", "fr-circ" })
		{
			NodeShapes.Find(name)!.Draw(Node()).Label.Should().BeNull(name);
		}
	}

	[Test]
	public void ANameOutsideTheTableFindsNoShape()
	{
		NodeShapes.Find("hexagonal").Should().BeNull();
		NodeShapes.Find("classBox").Should().BeNull();
		NodeShapes.Handler("classBox").Should().Be("classBox");
	}
}
