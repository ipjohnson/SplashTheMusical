using System.Text;
using SplashTheMusical.Layout.Markers;

namespace SplashTheMusical.Layout.Paths;

/// <summary>
/// The path mermaid's <c>insertEdge</c> in <c>rendering-elements/edges.js</c> draws through an edge's points, once the
/// points meet the node outlines and the cluster borders. It drops points with a NaN y, rounds the right-angle corners
/// with <c>fixCorners</c> unless the curve is rounded, and moves the end points back by the marker offsets, through
/// <c>getLineFunctionsWithOffset</c> for the d3 curves and <c>applyMarkerOffsetsToPoints</c> for the rounded curve.
/// </summary>
internal static class EdgePath
{
	public static string ToString(ReadOnlySpan<PathPoint> points, Curve curve, string? arrowTypeStart, string? arrowTypeEnd)
	{
		var text = new StringBuilder();
		Write(points, curve, arrowTypeStart, arrowTypeEnd, text);
		return text.ToString();
	}

	public static void Write(ReadOnlySpan<PathPoint> points, Curve curve, string? arrowTypeStart, string? arrowTypeEnd, StringBuilder text)
	{
		var lineData = new List<PathPoint>(points.Length);
		foreach (var point in points)
		{
			if (!double.IsNaN(point.Y))
			{
				lineData.Add(point);
			}
		}

		if (curve == Curve.Rounded)
		{
			RoundedPath.Write(ApplyMarkerOffsetsToPoints(lineData, arrowTypeStart, arrowTypeEnd), RoundedPath.Radius, text);
			return;
		}

		var data = FixCorners(lineData);
		var shifted = new PathPoint[data.Count];
		for (var i = 0; i < data.Count; i++)
		{
			shifted[i] = new PathPoint(OffsetX(data, i, arrowTypeStart, arrowTypeEnd), OffsetY(data, i, arrowTypeStart, arrowTypeEnd));
		}

		CurveWriter.Write(curve, shifted, text);
	}

	// The x accessor of getLineFunctionsWithOffset.
	private static double OffsetX(List<PathPoint> data, int i, string? arrowTypeStart, string? arrowTypeEnd)
	{
		var d = data[i];
		var first = data[0];
		var last = data[^1];
		var offset = 0.0;
		var right = !(first.X < last.X);
		var startOffset = MarkerOffsets.LineOffset(arrowTypeStart);
		var endOffset = MarkerOffsets.LineOffset(arrowTypeEnd);
		if (i == 0 && startOffset is { } start)
		{
			var (angle, deltaX, _) = DeltaAndAngle(data, 0, 1);
			offset = start * JsMath.Cos(angle) * (deltaX >= 0 ? 1 : -1);
		}
		else if (i == data.Count - 1 && endOffset is { } end)
		{
			var (angle, deltaX, _) = DeltaAndAngle(data, data.Count - 1, data.Count - 2);
			offset = end * JsMath.Cos(angle) * (deltaX >= 0 ? 1 : -1);
		}

		var differenceToEnd = Math.Abs(d.X - last.X);
		var differenceInYEnd = Math.Abs(d.Y - last.Y);
		var differenceToStart = Math.Abs(d.X - first.X);
		var differenceInYStart = Math.Abs(d.Y - first.Y);
		if (differenceToEnd < endOffset && differenceToEnd > 0 && differenceInYEnd < endOffset)
		{
			var adjustment = endOffset.Value + 1 - differenceToEnd;
			offset -= right ? -adjustment : adjustment;
		}

		if (differenceToStart < startOffset && differenceToStart > 0 && differenceInYStart < startOffset)
		{
			var adjustment = startOffset.Value + 1 - differenceToStart;
			offset += right ? -adjustment : adjustment;
		}

		return d.X + offset;
	}

	// The y accessor of getLineFunctionsWithOffset.
	private static double OffsetY(List<PathPoint> data, int i, string? arrowTypeStart, string? arrowTypeEnd)
	{
		var d = data[i];
		var first = data[0];
		var last = data[^1];
		var offset = 0.0;
		var up = !(first.Y < last.Y);
		var startOffset = MarkerOffsets.LineOffset(arrowTypeStart);
		var endOffset = MarkerOffsets.LineOffset(arrowTypeEnd);
		if (i == 0 && startOffset is { } start)
		{
			var (angle, _, deltaY) = DeltaAndAngle(data, 0, 1);
			offset = start * Math.Abs(JsMath.Sin(angle)) * (deltaY >= 0 ? 1 : -1);
		}
		else if (i == data.Count - 1 && endOffset is { } end)
		{
			var (angle, _, deltaY) = DeltaAndAngle(data, data.Count - 1, data.Count - 2);
			offset = end * Math.Abs(JsMath.Sin(angle)) * (deltaY >= 0 ? 1 : -1);
		}

		var differenceToEnd = Math.Abs(d.Y - last.Y);
		var differenceInXEnd = Math.Abs(d.X - last.X);
		var differenceToStart = Math.Abs(d.Y - first.Y);
		var differenceInXStart = Math.Abs(d.X - first.X);
		if (differenceToEnd < endOffset && differenceToEnd > 0 && differenceInXEnd < endOffset)
		{
			var adjustment = endOffset.Value + 1 - differenceToEnd;
			offset -= up ? -adjustment : adjustment;
		}

		if (differenceToStart < startOffset && differenceToStart > 0 && differenceInXStart < startOffset)
		{
			var adjustment = startOffset.Value + 1 - differenceToStart;
			offset += up ? -adjustment : adjustment;
		}

		return d.Y + offset;
	}

	// calculateDeltaAndAngle of lineWithOffset.ts, which gives zeros when a point is missing.
	private static (double Angle, double DeltaX, double DeltaY) DeltaAndAngle(List<PathPoint> data, int from, int to)
	{
		if (from < 0 || to < 0 || from >= data.Count || to >= data.Count)
		{
			return (0, 0, 0);
		}

		var deltaX = data[to].X - data[from].X;
		var deltaY = data[to].Y - data[from].Y;
		return (JsMath.Atan(deltaY / deltaX), deltaX, deltaY);
	}

	// applyMarkerOffsetsToPoints of edges.js, which reads the offsets as JavaScript truthy values, so a 0 offset is none.
	private static PathPoint[] ApplyMarkerOffsetsToPoints(List<PathPoint> points, string? arrowTypeStart, string? arrowTypeEnd)
	{
		var result = points.ToArray();
		var n = points.Count;
		if (n >= 2 && MarkerOffsets.LineOffset(arrowTypeStart) is { } start && start != 0)
		{
			var angle = JsMath.Atan2(points[1].Y - points[0].Y, points[1].X - points[0].X);
			result[0] = new PathPoint(points[0].X + start * JsMath.Cos(angle), points[0].Y + start * JsMath.Sin(angle));
		}

		if (n >= 2 && MarkerOffsets.LineOffset(arrowTypeEnd) is { } end && end != 0)
		{
			var angle = JsMath.Atan2(points[n - 1].Y - points[n - 2].Y, points[n - 1].X - points[n - 2].X);
			result[n - 1] = new PathPoint(points[n - 1].X - end * JsMath.Cos(angle), points[n - 1].Y - end * JsMath.Sin(angle));
		}

		return result;
	}

	// fixCorners of edges.js: each right-angle corner whose legs are longer than 5 becomes three points.
	private static List<PathPoint> FixCorners(List<PathPoint> lineData)
	{
		var result = new List<PathPoint>(lineData.Count);
		for (var i = 0; i < lineData.Count; i++)
		{
			if (!IsCorner(lineData, i))
			{
				result.Add(lineData[i]);
				continue;
			}

			var previous = lineData[i - 1];
			var next = lineData[i + 1];
			var corner = lineData[i];
			var newPrevious = AdjacentPoint(previous, corner, 5);
			var newNext = AdjacentPoint(next, corner, 5);
			var xDiff = newNext.X - newPrevious.X;
			var yDiff = newNext.Y - newPrevious.Y;
			result.Add(newPrevious);
			var a = Math.Sqrt(2) * 2;
			var newCorner = corner;
			if (Math.Abs(next.X - previous.X) > 10 && Math.Abs(next.Y - previous.Y) >= 10)
			{
				const double R = 5;
				newCorner = corner.X == newPrevious.X
					? new PathPoint(xDiff < 0 ? newPrevious.X - R + a : newPrevious.X + R - a, yDiff < 0 ? newPrevious.Y - a : newPrevious.Y + a)
					: new PathPoint(xDiff < 0 ? newPrevious.X - a : newPrevious.X + a, yDiff < 0 ? newPrevious.Y - R + a : newPrevious.Y + R - a);
			}

			result.Add(newCorner);
			result.Add(newNext);
		}

		return result;
	}

	// extractCornerPoints of edges.js.
	private static bool IsCorner(List<PathPoint> points, int i)
	{
		if (i < 1 || i >= points.Count - 1)
		{
			return false;
		}

		var previous = points[i - 1];
		var current = points[i];
		var next = points[i + 1];
		return (previous.X == current.X && current.Y == next.Y && Math.Abs(current.X - next.X) > 5 && Math.Abs(current.Y - previous.Y) > 5)
			|| (previous.Y == current.Y && current.X == next.X && Math.Abs(current.X - previous.X) > 5 && Math.Abs(current.Y - next.Y) > 5);
	}

	// findAdjacentPoint of edges.js: the point `distance` back from pointB towards pointA.
	private static PathPoint AdjacentPoint(PathPoint pointA, PathPoint pointB, double distance)
	{
		var xDiff = pointB.X - pointA.X;
		var yDiff = pointB.Y - pointA.Y;
		var length = Math.Sqrt(xDiff * xDiff + yDiff * yDiff);
		var ratio = distance / length;
		return new PathPoint(pointB.X - ratio * xDiff, pointB.Y - ratio * yDiff);
	}
}
