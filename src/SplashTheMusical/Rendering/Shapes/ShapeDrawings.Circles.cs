namespace SplashTheMusical.Rendering.Shapes;

// Upstream: packages/mermaid/src/rendering-util/rendering-elements/shapes/{circle,doubleCircle,filledCircle,stateStart,
// stateEnd,crossedCircle,forkJoin,anchor}.ts
internal static partial class ShapeDrawings
{
	public static ShapeGeometry Circle(ShapeNode node)
	{
		var label = Measure(node, node.Width, ignoreMinWidth: true);
		var halfPadding = (node.Padding ?? 0) / 2;
		var labelRadius = Math.Sqrt(label.Width * label.Width + label.Height * label.Height) / 2;
		var radius = node.IsNeo ? labelRadius + 16 : labelRadius + halfPadding;
		var element = node.IsHandDrawn ? Circle(0, 0, radius, rough: true) : Circle(0, 0, radius, LabelContainer);
		return Finish(element, label, LabelHelperTranslate(label), ShapeIntersect.Circle(radius));
	}

	public static ShapeGeometry DoubleCircle(ShapeNode node)
	{
		var gap = node.IsNeo ? 12 : 5;
		var labelPadding = node.IsNeo ? 16 : node.Padding ?? 0;
		var label = Measure(node, node.Width, ignoreMinWidth: true);
		var labelRadius = Math.Sqrt(label.Width * label.Width + label.Height * label.Height) / 2;
		var innerRadius = (Truthy(node.Width) ? node.Width!.Value / 2 : labelRadius) + labelPadding;
		var outerRadius = innerRadius + gap;
		var group = node.IsHandDrawn
			? Group(null, Circle(0, 0, outerRadius, rough: true), Circle(0, 0, innerRadius, rough: true))
			: Group(null, Circle(0, 0, outerRadius, "outer-circle"), Circle(0, 0, innerRadius, "inner-circle"));
		return Finish(group, label, LabelHelperTranslate(label), ShapeIntersect.Circle(outerRadius));
	}

	public static ShapeGeometry FilledCircle(ShapeNode node)
	{
		const double radius = 7;
		return Finish(Circle(0, 0, radius, rough: true), null, default, ShapeIntersect.Circle(radius));
	}

	public static ShapeGeometry StateStart(ShapeNode node)
	{
		var width = StateSize(node.Width, node.Height);
		var circle = new ShapeElement { Kind = ShapeElementKind.Circle, Rx = width / 2, Class = "state-start", Rough = node.IsHandDrawn };
		return Finish(circle, null, default, ShapeIntersect.CircleOfWidth);
	}

	public static ShapeGeometry StateEnd(ShapeNode node)
	{
		var width = StateSize(node.Width, node.Height);
		var outer = Circle(0, 0, width / 2, rough: true);
		var inner = Circle(0, 0, width * 5 / 14 / 2, rough: true);
		var group = Group(node.IsHandDrawn ? null : "outer-path", outer, inner);
		return Finish(group, null, default, ShapeIntersect.CircleOfWidth);
	}

	/// <summary>The width the state start and end give a node: 14, or the node's own when it is larger.</summary>
	private static double StateSize(double? width, double? height)
	{
		if ((Truthy(width) || Truthy(height)) && (width ?? 0) < 14)
		{
			width = 14;
		}

		return OrElse(width, 14);
	}

	public static ShapeGeometry CrossedCircle(ShapeNode node)
	{
		var radius = Math.Max(30, node.Width ?? 0);
		var half = radius * 2 / 2;
		var x45 = Math.Cos(Math.PI / 4);
		var y45 = Math.Sin(Math.PI / 4);
		var lines = $"M {N(-half * x45)},{N(half * y45)} L {N(half * x45)},{N(-half * y45)}"
			+ $"\n                   M {N(half * x45)},{N(half * y45)} L {N(-half * x45)},{N(-half * y45)}";
		var group = Group("outer-path", Circle(0, 0, radius, rough: true), RoughPath(lines));
		return Finish(group, null, default, ShapeIntersect.Circle(radius));
	}

	/// <summary>
	/// The fork bar. mermaid.js grows the node by half of <c>state.padding</c> after drawing it, but
	/// <c>insertMeasuredNode</c> then sizes the node by its group, which drops that growth.
	/// </summary>
	public static ShapeGeometry ForkJoin(ShapeNode node)
	{
		var upright = node.Direction is "LR" or "RL";
		var width = Math.Max(upright ? 10 : 70, node.Width ?? 0);
		var height = Math.Max(upright ? 70 : 10, node.Height ?? 0);
		return Finish(Rect(-width / 2, -height / 2, width, height, null, rough: true), null, default, ShapeIntersect.Rect);
	}

	public static ShapeGeometry Anchor(ShapeNode node)
	{
		const double radius = 1;
		return Finish(Circle(0, 0, radius, rough: true), null, default, ShapeIntersect.Circle(radius));
	}
}
