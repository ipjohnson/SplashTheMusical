namespace SplashTheMusical.Diagrams.Flowchart;

/// <summary>The node shape names of mermaid.js 12.3.0's <c>shapes</c> map in <c>rendering-elements/shapes.ts</c>.</summary>
internal static class FlowchartShapes
{
	/// <summary>
	/// <c>isValidShape</c>: whether the name is a key of the map, which holds every shape's short name, name, aliases and
	/// internal aliases, and the shapes mermaid.js's diagrams use internally. <c>corpus/shapes.txt</c> lists them as
	/// <c>key</c> lines.
	/// </summary>
	public static bool IsValid(ReadOnlySpan<char> name) => name switch
	{
		"anchor" or "bang" or "block_arrow" or "bolt" or "bow-rect" or "bow-tie-rectangle" or "bpmn-activity" or "bpmn-annotation" or
		"bpmn-boundary" or "bpmn-data" or "bpmn-data-store" or "bpmn-end" or "bpmn-gateway" or "bpmn-intermediate" or "bpmn-start" or "brace" or
		"brace-l" or "brace-r" or "braces" or "browser" or "bucket" or "card" or "choice" or "circ" or
		"circle" or "classBox" or "cloud" or "collapsedGroup" or "collate" or "com-link" or "comment" or "composite" or
		"console" or "cross-circ" or "crossed-circle" or "curv-trap" or "curved-trapezoid" or "cyl" or "cylinder" or "das" or
		"data-store" or "database" or "datastore" or "db" or "dbl-circ" or "decision" or "defaultMindmapNode" or "delay" or
		"diam" or "diamond" or "directory" or "disk" or "display" or "div-proc" or "div-rect" or "divided-process" or
		"divided-rectangle" or "doc" or "docs" or "document" or "documents" or "double-circle" or "doublecircle" or "erBox" or
		"event" or "extract" or "f-circ" or "filled-circle" or "flag" or "flip-tri" or "flipped-triangle" or "folder" or
		"fork" or "forkJoin" or "fr-circ" or "fr-rect" or "framed-circle" or "framed-rectangle" or "h-cyl" or "half-rounded-rectangle" or
		"hex" or "hexagon" or "horizontal-cylinder" or "hourglass" or "icon" or "iconCircle" or "iconRounded" or "iconSquare" or
		"imageSquare" or "in-out" or "internal-storage" or "inv-trapezoid" or "inv_trapezoid" or "join" or "junction" or "kanbanItem" or
		"labelRect" or "lean-l" or "lean-left" or "lean-r" or "lean-right" or "lean_left" or "lean_right" or "lightning-bolt" or
		"lin-cyl" or "lin-doc" or "lin-proc" or "lin-rect" or "lined-cylinder" or "lined-document" or "lined-process" or "lined-rectangle" or
		"loop-limit" or "manual" or "manual-file" or "manual-input" or "mindmapCircle" or "notch-pent" or "notch-rect" or "notched-pentagon" or
		"notched-rectangle" or "note" or "odd" or "out-in" or "paper-tape" or "person" or "pill" or "prepare" or
		"priority" or "proc" or "process" or "processes" or "procs" or "question" or "rect" or "rectWithTitle" or
		"rect_left_inv_arrow" or "rectangle" or "requirementBox" or "rounded" or "roundedRect" or "shaded-process" or "sl-rect" or "sloped-rectangle" or
		"sm-circ" or "small-circle" or "squareRect" or "st-doc" or "st-rect" or "stacked-document" or "stacked-rectangle" or "stadium" or
		"start" or "state" or "stateEnd" or "stateStart" or "stop" or "stored-data" or "subproc" or "subprocess" or
		"subroutine" or "summary" or "tag-doc" or "tag-proc" or "tag-rect" or "tagged-document" or "tagged-process" or "tagged-rectangle" or
		"terminal" or "text" or "trap-b" or "trap-t" or "trapezoid" or "trapezoid-bottom" or "trapezoid-top" or "tri" or
		"triangle" or "usecaseActor" or "usecaseActorAwesome" or "usecaseActorHollow" or "usecaseActorIcon" or "usecaseBusiness" or "usecaseEllipse" or "usecaseJsonTable" or
		"win-pane" or "window-pane" => true,
		_ => false,
	};

	/// <summary>
	/// <c>getTypeFromVertex</c> in <c>flowDb.ts</c>: the shape mermaid.js draws a node with. An image node is
	/// <c>imageSquare</c>, an icon node is <c>icon</c> or the icon shape of its form, and a node without a shape is
	/// <c>squareRect</c>.
	/// </summary>
	public static string Drawn(FlowchartNode node) =>
		node.Image is not null ? "imageSquare"
		: node.Icon is not null ? node.Form switch
		{
			"circle" => "iconCircle",
			"square" => "iconSquare",
			"rounded" => "iconRounded",
			_ => "icon",
		}
		: node.Shape switch
		{
			null or "square" => "squareRect",
			"round" => "roundedRect",
			var shape => shape,
		};
}
