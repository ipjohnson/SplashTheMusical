namespace SplashTheMusical.Rendering.Shapes;

/// <summary>A node shape: the name of mermaid.js's drawing function for it, and the port of that function.</summary>
internal sealed class NodeShape(string handler, Func<ShapeNode, ShapeGeometry> draw)
{
	/// <summary>The drawing function's name, the handler of <c>corpus/shapes.txt</c>, such as <c>squareRect</c>.</summary>
	public string Handler { get; } = handler;

	public ShapeGeometry Draw(ShapeNode node) => draw(node);
}

/// <summary>
/// The node shapes of mermaid.js 12.3.0's <c>shapes</c> map in <c>rendering-elements/shapes.ts</c>: every shape of
/// its shape table, the undocumented <c>anchor</c>, <c>choice</c> and <c>note</c> that flowcharts can name, and the icon
/// and image shapes of a flowchart's <c>icon</c> and <c>img</c> nodes. The other undocumented shapes belong to other
/// diagram types.
/// </summary>
internal static class NodeShapes
{
	public static IReadOnlyList<NodeShape> All { get; } =
	[
		new("anchor", ShapeDrawings.Anchor),
		new("bang", ShapeDrawings.Bang),
		new("bowTieRect", ShapeDrawings.BowTieRect),
		new("browser", ShapeDrawings.Browser),
		new("bucket", ShapeDrawings.Bucket),
		new("card", ShapeDrawings.Card),
		new("choice", ShapeDrawings.Choice),
		new("circle", ShapeDrawings.Circle),
		new("cloud", ShapeDrawings.Cloud),
		new("consoleWindow", ShapeDrawings.ConsoleWindow),
		new("crossedCircle", ShapeDrawings.CrossedCircle),
		new("curlyBraceLeft", ShapeDrawings.CurlyBraceLeft),
		new("curlyBraceRight", ShapeDrawings.CurlyBraceRight),
		new("curlyBraces", ShapeDrawings.CurlyBraces),
		new("curvedTrapezoid", ShapeDrawings.CurvedTrapezoid),
		new("cylinder", ShapeDrawings.Cylinder),
		new("datastore", ShapeDrawings.Datastore),
		new("dividedRectangle", ShapeDrawings.DividedRectangle),
		new("doublecircle", ShapeDrawings.DoubleCircle),
		new("filledCircle", ShapeDrawings.FilledCircle),
		new("flippedTriangle", ShapeDrawings.FlippedTriangle),
		new("folder", ShapeDrawings.Folder),
		new("forkJoin", ShapeDrawings.ForkJoin),
		new("halfRoundedRectangle", ShapeDrawings.HalfRoundedRectangle),
		new("hexagon", ShapeDrawings.Hexagon),
		new("hourglass", ShapeDrawings.Hourglass),
		new("icon", ShapeDrawings.Icon),
		new("iconCircle", ShapeDrawings.IconCircle),
		new("iconRounded", ShapeDrawings.IconRounded),
		new("iconSquare", ShapeDrawings.IconSquare),
		new("imageSquare", ShapeDrawings.ImageSquare),
		new("inv_trapezoid", ShapeDrawings.InvTrapezoid),
		new("lean_left", ShapeDrawings.LeanLeft),
		new("lean_right", ShapeDrawings.LeanRight),
		new("lightningBolt", ShapeDrawings.LightningBolt),
		new("linedCylinder", ShapeDrawings.LinedCylinder),
		new("linedWaveEdgedRect", ShapeDrawings.LinedWaveEdgedRect),
		new("multiRect", ShapeDrawings.MultiRect),
		new("multiWaveEdgedRectangle", ShapeDrawings.MultiWaveEdgedRectangle),
		new("note", ShapeDrawings.Note),
		new("person", ShapeDrawings.Person),
		new("question", ShapeDrawings.Question),
		new("rect_left_inv_arrow", ShapeDrawings.RectLeftInvArrow),
		new("roundedRect", ShapeDrawings.RoundedRect),
		new("shadedProcess", ShapeDrawings.ShadedProcess),
		new("slopedRect", ShapeDrawings.SlopedRect),
		new("squareRect", ShapeDrawings.SquareRect),
		new("stadium", ShapeDrawings.Stadium),
		new("stateEnd", ShapeDrawings.StateEnd),
		new("stateStart", ShapeDrawings.StateStart),
		new("subroutine", ShapeDrawings.Subroutine),
		new("taggedRect", ShapeDrawings.TaggedRect),
		new("taggedWaveEdgedRectangle", ShapeDrawings.TaggedWaveEdgedRectangle),
		new("text", ShapeDrawings.Text),
		new("tiltedCylinder", ShapeDrawings.TiltedCylinder),
		new("trapezoid", ShapeDrawings.Trapezoid),
		new("trapezoidalPentagon", ShapeDrawings.TrapezoidalPentagon),
		new("triangle", ShapeDrawings.Triangle),
		new("waveEdgedRectangle", ShapeDrawings.WaveEdgedRectangle),
		new("waveRectangle", ShapeDrawings.WaveRectangle),
		new("windowPane", ShapeDrawings.WindowPane),
	];

	/// <summary>The shape drawn by the function named <paramref name="handler"/>, or null when the library has none.</summary>
	public static NodeShape? ForHandler(ReadOnlySpan<char> handler)
	{
		foreach (var shape in All)
		{
			if (handler.SequenceEqual(shape.Handler))
			{
				return shape;
			}
		}

		return null;
	}

	/// <summary>
	/// The shape a node's shape name draws: a short name, a name, an alias or a handler, as the <c>key</c> lines of
	/// <c>corpus/shapes.txt</c> map them. <c>insertNode</c> draws <c>rect</c> as <c>roundedRect</c> when the node has
	/// <c>rx</c> and <c>ry</c>, which the caller decides.
	/// </summary>
	public static NodeShape? Find(ReadOnlySpan<char> name) => Handler(name) is { } handler ? ForHandler(handler) : null;

	/// <summary>The handler of a key of mermaid.js's <c>shapes</c> map, or null for a name that is not one.</summary>
	public static string? Handler(ReadOnlySpan<char> name) => name switch
	{
		"anchor" => "anchor",
		"bang" => "bang",
		"bow-rect" or "bow-tie-rectangle" or "stored-data" => "bowTieRect",
		"browser" => "browser",
		"bucket" => "bucket",
		"card" or "notch-rect" or "notched-rectangle" => "card",
		"choice" => "choice",
		"circ" or "circle" => "circle",
		"cloud" => "cloud",
		"console" => "consoleWindow",
		"cross-circ" or "crossed-circle" or "summary" => "crossedCircle",
		"brace" or "brace-l" or "comment" => "curlyBraceLeft",
		"brace-r" => "curlyBraceRight",
		"braces" => "curlyBraces",
		"curv-trap" or "curved-trapezoid" or "display" => "curvedTrapezoid",
		"cyl" or "cylinder" or "database" or "db" => "cylinder",
		"data-store" or "datastore" => "datastore",
		"div-proc" or "div-rect" or "divided-process" or "divided-rectangle" => "dividedRectangle",
		"dbl-circ" or "double-circle" or "doublecircle" => "doublecircle",
		"f-circ" or "filled-circle" or "junction" => "filledCircle",
		"flip-tri" or "flipped-triangle" or "manual-file" => "flippedTriangle",
		"directory" or "folder" => "folder",
		"fork" or "forkJoin" or "join" => "forkJoin",
		"delay" or "half-rounded-rectangle" => "halfRoundedRectangle",
		"hex" or "hexagon" or "prepare" => "hexagon",
		"collate" or "hourglass" => "hourglass",
		"inv-trapezoid" or "inv_trapezoid" or "manual" or "trap-t" or "trapezoid-top" => "inv_trapezoid",
		"lean-l" or "lean-left" or "lean_left" or "out-in" => "lean_left",
		"in-out" or "lean-r" or "lean-right" or "lean_right" => "lean_right",
		"bolt" or "com-link" or "lightning-bolt" => "lightningBolt",
		"disk" or "lin-cyl" or "lined-cylinder" => "linedCylinder",
		"lin-doc" or "lined-document" => "linedWaveEdgedRect",
		"processes" or "procs" or "st-rect" or "stacked-rectangle" => "multiRect",
		"docs" or "documents" or "st-doc" or "stacked-document" => "multiWaveEdgedRectangle",
		"note" => "note",
		"person" => "person",
		"decision" or "diam" or "diamond" or "question" => "question",
		"odd" or "rect_left_inv_arrow" => "rect_left_inv_arrow",
		"event" or "rounded" or "roundedRect" => "roundedRect",
		"lin-proc" or "lin-rect" or "lined-process" or "lined-rectangle" or "shaded-process" => "shadedProcess",
		"manual-input" or "sl-rect" or "sloped-rectangle" => "slopedRect",
		"proc" or "process" or "rect" or "rectangle" or "squareRect" => "squareRect",
		"pill" or "stadium" or "terminal" => "stadium",
		"fr-circ" or "framed-circle" or "stateEnd" or "stop" => "stateEnd",
		"sm-circ" or "small-circle" or "start" or "stateStart" => "stateStart",
		"fr-rect" or "framed-rectangle" or "subproc" or "subprocess" or "subroutine" => "subroutine",
		"tag-proc" or "tag-rect" or "tagged-process" or "tagged-rectangle" => "taggedRect",
		"tag-doc" or "tagged-document" => "taggedWaveEdgedRectangle",
		"text" => "text",
		"das" or "h-cyl" or "horizontal-cylinder" => "tiltedCylinder",
		"priority" or "trap-b" or "trapezoid" or "trapezoid-bottom" => "trapezoid",
		"loop-limit" or "notch-pent" or "notched-pentagon" => "trapezoidalPentagon",
		"extract" or "tri" or "triangle" => "triangle",
		"doc" or "document" => "waveEdgedRectangle",
		"flag" or "paper-tape" => "waveRectangle",
		"internal-storage" or "win-pane" or "window-pane" => "windowPane",
		"block_arrow" => "block_arrow",
		"bpmn-activity" => "bpmnActivity",
		"bpmn-annotation" => "bpmnAnnotation",
		"bpmn-boundary" => "bpmnBoundary",
		"bpmn-data" => "bpmnDataObject",
		"bpmn-data-store" => "bpmnDataStore",
		"bpmn-end" => "bpmnEnd",
		"bpmn-gateway" => "bpmnGateway",
		"bpmn-intermediate" => "bpmnIntermediate",
		"bpmn-start" => "bpmnStart",
		"classBox" or "collapsedGroup" or "composite" or "defaultMindmapNode" or "erBox" or "icon" or "iconCircle"
			or "iconRounded" or "iconSquare" or "imageSquare" or "kanbanItem" or "labelRect" or "mindmapCircle"
			or "rectWithTitle" or "requirementBox" or "state" or "usecaseActor" or "usecaseActorAwesome"
			or "usecaseActorHollow" or "usecaseActorIcon" or "usecaseBusiness" or "usecaseEllipse" or "usecaseJsonTable" => name.ToString(),
		_ => null,
	};
}
