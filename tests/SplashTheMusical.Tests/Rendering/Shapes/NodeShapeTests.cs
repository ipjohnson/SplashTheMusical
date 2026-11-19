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

		Verify(geometry.Width > 0);
		Verify(geometry.Height > 0);
		Verify(geometry.Elements.Any());
		foreach (var far in new ShapePoint[] { new(1000, 0), new(-1000, 0), new(0, 1000), new(0, -1000), new(700, 700) })
		{
			var hit = geometry.IntersectFrom(new ShapePoint(0, 0), far);
			Verify(Math.Abs(hit.X) <= geometry.Width / 2 + 0.5, $"{handler} towards {far}");
			Verify(Math.Abs(hit.Y) <= geometry.Height / 2 + 0.5, $"{handler} towards {far}");
			if (handler != "hourglass")
			{
				// The hourglass's outline crosses itself at the centre, where a horizontal edge meets it.
				Verify((Math.Abs(hit.X) + Math.Abs(hit.Y)) > 0, $"{handler} towards {far}");
			}
		}
	}

	[Test]
	public void TheRectFitsItsLabelAndMeetsEdgesOnItsSides()
	{
		var geometry = NodeShapes.Find("rect")!.Draw(Node());

		Verify(Math.Abs(geometry.Width - 180) <= 1e-9);
		Verify(Math.Abs(geometry.Height - 54) <= 1e-9);
		Verify(geometry.LabelTranslate == new ShapePoint(-60, -12));
		Verify(geometry.IntersectFrom(new ShapePoint(100, 50), new ShapePoint(300, 50)) == new ShapePoint(190, 50));
		Verify(geometry.IntersectFrom(new ShapePoint(100, 50), new ShapePoint(100, -100)) == new ShapePoint(100, 23));
	}

	[Test]
	public void TheNeoLookPadsTheRectWithItsOwnPaddings()
	{
		var geometry = NodeShapes.Find("rect")!.Draw(Node(ShapeLook.Neo, height: 21));

		Verify(Math.Abs(geometry.Width - 152) <= 1e-9);
		Verify(Math.Abs(geometry.Height - 45) <= 1e-9);
	}

	[Test]
	public void TheHexagonMeetsAHorizontalEdgeAtItsPoint()
	{
		var geometry = NodeShapes.Find("hex")!.Draw(Node());
		var m = (24 + 15) / 4.0;

		Verify(Math.Abs(geometry.Width - (120 + 2 * m + 15)) <= 1e-9);
		Verify(Math.Abs(geometry.IntersectFrom(new ShapePoint(0, 0), new ShapePoint(500, 0)).X - (geometry.Width / 2)) <= 1e-9);
	}

	[Test]
	public void TheDiamondMeetsEdgesOnItsSides()
	{
		var geometry = NodeShapes.Find("diam")!.Draw(Node());
		var s = 120 + 15 + 24 + 15;
		var hit = geometry.IntersectFrom(new ShapePoint(0, 0), new ShapePoint(300, 100));

		Verify(Math.Abs(geometry.Width - s) <= 1e-9);
		Verify(Math.Abs((Math.Abs(hit.X) + Math.Abs(hit.Y)) - (s / 2.0)) <= 1e-9);
	}

	[Test]
	public void TheCircleMeetsEdgesAtItsRadius()
	{
		var geometry = NodeShapes.Find("circle")!.Draw(Node(width: 30, height: 40));
		var radius = 25 + 7.5;
		var hit = geometry.IntersectFrom(new ShapePoint(10, 10), new ShapePoint(110, 110));

		Verify(Math.Abs(geometry.Width - (2 * radius)) <= 1e-9);
		Verify(Math.Abs(Math.Sqrt((hit.X - 10) * (hit.X - 10) + (hit.Y - 10) * (hit.Y - 10)) - radius) <= 1e-9);
	}

	[Test]
	public void TheCylinderIsAsTallAsItsSidesAndBothRims()
	{
		var geometry = NodeShapes.Find("database")!.Draw(Node());
		var w = 120 + 15.0;
		var ry = w / 2 / (2.5 + w / 50);
		var h = 24 + 15 + ry;
		var top = geometry.IntersectFrom(new ShapePoint(0, 0), new ShapePoint(0, -500));

		Verify(Math.Abs(geometry.Width - w) <= 1e-9);
		Verify(Math.Abs(geometry.Height - (h + 2 * ry)) <= 1e-9);
		Verify(Math.Abs(top.Y - (-geometry.Height / 2)) <= 1e-9);
	}

	[Test]
	public void ShapesWithoutALabelMeasureNone()
	{
		foreach (var name in new[] { "f-circ", "fork", "bolt", "cross-circ", "sm-circ", "fr-circ" })
		{
			Verify(NodeShapes.Find(name)!.Draw(Node()).Label is null);
		}
	}

	[Test]
	public void ANameOutsideTheTableFindsNoShape()
	{
		Verify(NodeShapes.Find("hexagonal") is null);
		Verify(NodeShapes.Find("classBox") is null);
		Verify(NodeShapes.Handler("classBox") == "classBox");
	}
}
