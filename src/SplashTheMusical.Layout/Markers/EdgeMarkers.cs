using System.Collections.Immutable;

namespace SplashTheMusical.Layout.Markers;

/// <summary>A theme variable a neo marker reads for an attribute.</summary>
internal enum MarkerThemeValue
{
	None,
	StrokeWidth,
	MainBackground,
	TransitionColor,
}

/// <summary>
/// An attribute, or with <see cref="IsStyle"/> an inline style property, of a marker or one of its elements. When
/// <see cref="Theme"/> names a theme variable, the renderer writes that variable, and <see cref="Value"/> is what mermaid
/// writes when the theme lacks it.
/// </summary>
internal readonly record struct MarkerAttribute(string Name, string Value, MarkerThemeValue Theme = MarkerThemeValue.None, bool IsStyle = false);

/// <summary>An element inside a marker.</summary>
internal sealed record MarkerElement(string Tag, ImmutableArray<MarkerAttribute> Attributes, ImmutableArray<MarkerElement> Children);

/// <summary>
/// A <c>marker</c> element. Its id is <c>{diagramId}_{diagramType}-{Name}</c>, and its class, when it has one, is
/// <see cref="Class"/> and the diagram type. <see cref="InDefs"/> says whether mermaid wraps it in its own <c>defs</c>.
/// </summary>
internal sealed record MarkerDefinition(string Name, string? Class, bool InDefs, ImmutableArray<MarkerAttribute> Attributes, ImmutableArray<MarkerElement> Children);

/// <summary>The markers one function of mermaid's <c>markers</c> table inserts, named as in the table.</summary>
internal sealed record MarkerSet(string Name, ImmutableArray<MarkerDefinition> Markers);

/// <summary>An arrow type's marker in <c>arrowTypesMap</c>, and whether a stroke colour fills it.</summary>
internal readonly record struct ArrowMarker(string ArrowType, string MarkerType, bool Fill);

internal enum MarkerEnd
{
	Start,
	End,
}

/// <summary>The diagrams whose renderers insert a fixed list of marker sets.</summary>
internal enum MarkerDiagram
{
	Flowchart,
	Class,
	State,
	Er,
}

/// <summary>
/// The edge markers of mermaid 12.3.0's <c>rendering-elements/markers.js</c>, and the marker ids
/// <c>rendering-elements/edgeMarker.ts</c> gives each arrow type.
/// </summary>
internal static class EdgeMarkers
{
	/// <summary>The marker types that have a <c>-margin</c> variant, from <c>arrowTypesWithMarginSupport</c>.</summary>
	public static ImmutableArray<string> MarginTypes { get; } =
		["cross", "point", "circle", "lollipop", "aggregation", "extension", "composition", "dependency", "barb"];

	/// <summary><c>arrowTypesMap</c>, in its order.</summary>
	public static ImmutableArray<ArrowMarker> ArrowTypes { get; } =
	[
		new("arrow_cross", "cross", false),
		new("arrow_point", "point", true),
		new("arrow_barb", "barb", true),
		new("arrow_barb_neo", "barb", true),
		new("arrow_circle", "circle", false),
		new("arrow_open_head", "openArrow", false),
		new("arrow_hollow_circle", "hollowCircle", false),
		new("arrow_hierarchy", "hierarchy", false),
		new("aggregation", "aggregation", false),
		new("extension", "extension", false),
		new("composition", "composition", true),
		new("dependency", "dependency", true),
		new("lollipop", "lollipop", false),
		new("only_one", "onlyOne", false),
		new("zero_or_one", "zeroOrOne", false),
		new("one_or_more", "oneOrMore", false),
		new("zero_or_more", "zeroOrMore", false),
		new("requirement_arrow", "requirement_arrow", false),
		new("requirement_contains", "requirement_contains", false),
	];

	/// <summary>The id of the marker <c>addEdgeMarker</c> puts on an edge end, or null when the arrow type has none.</summary>
	public static string? MarkerId(string diagramId, string diagramType, string? arrowType, MarkerEnd end, bool useMargin)
	{
		if (FindArrowType(arrowType) is not { } marker)
		{
			return null;
		}

		var margin = useMargin && MarginTypes.Contains(marker.MarkerType) ? "-margin" : "";
		return $"{diagramId}_{diagramType}-{marker.MarkerType}{(end == MarkerEnd.Start ? "Start" : "End")}{margin}";
	}

	public static ArrowMarker? FindArrowType(string? arrowType)
	{
		foreach (var marker in ArrowTypes)
		{
			if (string.Equals(marker.ArrowType, arrowType, StringComparison.Ordinal))
			{
				return marker;
			}
		}

		return null;
	}

	/// <summary>The id of <paramref name="marker"/> in a diagram.</summary>
	public static string Id(string diagramId, string diagramType, MarkerDefinition marker) => $"{diagramId}_{diagramType}-{marker.Name}";

	public static MarkerSet? FindSet(string name)
	{
		foreach (var set in Sets)
		{
			if (string.Equals(set.Name, name, StringComparison.Ordinal))
			{
				return set;
			}
		}

		return null;
	}

	/// <summary>The marker sets a diagram's renderer inserts, which for state and ER depend on the neo look.</summary>
	public static ImmutableArray<string> SetsFor(MarkerDiagram diagram, bool neo) => diagram switch
	{
		MarkerDiagram.Flowchart => ["point", "circle", "cross"],
		MarkerDiagram.Class => ["aggregation", "extension", "composition", "dependency", "lollipop"],
		MarkerDiagram.State => neo ? ["barbNeo"] : ["barb"],
		MarkerDiagram.Er => neo
			? ["only_one_neo", "zero_or_one_neo", "one_or_more_neo", "zero_or_more_neo"]
			: ["only_one", "zero_or_one", "one_or_more", "zero_or_more"],
		_ => throw new ArgumentOutOfRangeException(nameof(diagram), diagram, null),
	};

	private const string UserSpace = "userSpaceOnUse";
	private const string ClassPath = "arrowMarkerPath";
	private const string Diamond = "M 18,7 L9,13 L1,7 L9,1 Z";
	private const string RequirementArrow = "M0,0\n      L20,10\n      M20,10\n      L0,20";

	/// <summary>The <c>markers</c> table, in its order. Each set lists its markers in the order mermaid inserts them.</summary>
	public static ImmutableArray<MarkerSet> Sets { get; } =
	[
		Set("extension",
			Marker("extensionStart", "marker extension", true, [A("refX", "18"), A("refY", "7"), A("markerWidth", "20"), A("markerHeight", "28"), A("orient", "auto"), A("markerUnits", UserSpace)],
				E("path", A("d", "M 1,7 L18,13 V 1 Z"))),
			Marker("extensionEnd", "marker extension", true, [A("refX", "1"), A("refY", "7"), A("markerWidth", "20"), A("markerHeight", "28"), A("orient", "auto"), A("markerUnits", UserSpace)],
				E("path", A("d", "M 1,1 V 13 L18,7 Z"))),
			Marker("extensionStart-margin", "marker extension", false, [A("refX", "18"), A("refY", "7"), A("markerWidth", "20"), A("markerHeight", "28"), A("orient", "auto"), A("markerUnits", UserSpace), A("viewBox", "0 0 20 14")],
				E("polygon", A("points", "10,7 18,13 18,1"), S("stroke-width", "2"), S("stroke-dasharray", "0"))),
			Marker("extensionEnd-margin", "marker extension", true, [A("refX", "9"), A("refY", "7"), A("markerWidth", "20"), A("markerHeight", "28"), A("orient", "auto"), A("markerUnits", UserSpace), A("viewBox", "0 0 20 14")],
				E("polygon", A("points", "10,1 10,13 18,7"), S("stroke-width", "2"), S("stroke-dasharray", "0")))),
		Set("composition",
			Marker("compositionStart", "marker composition", true, [A("refX", "18"), A("refY", "7"), A("markerWidth", "190"), A("markerHeight", "240"), A("orient", "auto"), A("markerUnits", UserSpace)],
				E("path", A("d", Diamond))),
			Marker("compositionEnd", "marker composition", true, [A("refX", "1"), A("refY", "7"), A("markerWidth", "20"), A("markerHeight", "28"), A("orient", "auto"), A("markerUnits", UserSpace)],
				E("path", A("d", Diamond))),
			Marker("compositionStart-margin", "marker composition", true, [A("refX", "15"), A("refY", "7"), A("markerWidth", "190"), A("markerHeight", "240"), A("orient", "auto"), A("markerUnits", UserSpace)],
				E("path", S("stroke-width", "0"), A("viewBox", "0 0 15 15"), A("d", Diamond))),
			Marker("compositionEnd-margin", "marker composition", true, [A("refX", "3.5"), A("refY", "7"), A("markerWidth", "20"), A("markerHeight", "28"), A("orient", "auto"), A("markerUnits", UserSpace)],
				E("path", S("stroke-width", "0"), A("d", Diamond)))),
		Set("aggregation",
			Marker("aggregationStart", "marker aggregation", true, [A("refX", "18"), A("refY", "7"), A("markerWidth", "190"), A("markerHeight", "240"), A("orient", "auto"), A("markerUnits", UserSpace)],
				E("path", A("d", Diamond))),
			Marker("aggregationEnd", "marker aggregation", true, [A("refX", "1"), A("refY", "7"), A("markerWidth", "20"), A("markerHeight", "28"), A("orient", "auto"), A("markerUnits", UserSpace)],
				E("path", A("d", Diamond))),
			Marker("aggregationStart-margin", "marker aggregation", true, [A("refX", "15"), A("refY", "7"), A("markerWidth", "190"), A("markerHeight", "240"), A("orient", "auto"), A("markerUnits", UserSpace)],
				E("path", S("stroke-width", "2"), A("d", Diamond))),
			Marker("aggregationEnd-margin", "marker aggregation", true, [A("refX", "1"), A("refY", "7"), A("markerWidth", "20"), A("markerHeight", "28"), A("orient", "auto"), A("markerUnits", UserSpace)],
				E("path", S("stroke-width", "2"), A("d", Diamond)))),
		Set("dependency",
			Marker("dependencyStart", "marker dependency", true, [A("refX", "6"), A("refY", "7"), A("markerWidth", "190"), A("markerHeight", "240"), A("orient", "auto"), A("markerUnits", UserSpace)],
				E("path", A("d", "M 5,7 L9,13 L1,7 L9,1 Z"))),
			Marker("dependencyEnd", "marker dependency", true, [A("refX", "13"), A("refY", "7"), A("markerWidth", "20"), A("markerHeight", "28"), A("orient", "auto"), A("markerUnits", UserSpace)],
				E("path", A("d", "M 18,7 L9,13 L14,7 L9,1 Z"))),
			Marker("dependencyStart-margin", "marker dependency", true, [A("refX", "4"), A("refY", "7"), A("markerWidth", "190"), A("markerHeight", "240"), A("orient", "auto"), A("markerUnits", UserSpace)],
				E("path", S("stroke-width", "0"), A("d", "M 5,7 L9,13 L1,7 L9,1 Z"))),
			Marker("dependencyEnd-margin", "marker dependency", true, [A("refX", "16"), A("refY", "7"), A("markerWidth", "20"), A("markerHeight", "28"), A("orient", "auto"), A("markerUnits", UserSpace)],
				E("path", S("stroke-width", "0"), A("d", "M 18,7 L9,13 L14,7 L9,1 Z")))),
		Set("lollipop",
			Marker("lollipopStart", "marker lollipop", true, [A("refX", "13"), A("refY", "7"), A("markerWidth", "190"), A("markerHeight", "240"), A("orient", "auto"), A("markerUnits", UserSpace)],
				E("circle", A("fill", "transparent"), A("cx", "7"), A("cy", "7"), A("r", "6"))),
			Marker("lollipopEnd", "marker lollipop", true, [A("refX", "1"), A("refY", "7"), A("markerWidth", "190"), A("markerHeight", "240"), A("orient", "auto"), A("markerUnits", UserSpace)],
				E("circle", A("fill", "transparent"), A("cx", "7"), A("cy", "7"), A("r", "6"))),
			Marker("lollipopStart-margin", "marker lollipop", true, [A("refX", "13"), A("refY", "7"), A("markerWidth", "190"), A("markerHeight", "240"), A("orient", "auto"), A("markerUnits", UserSpace)],
				E("circle", A("fill", "transparent"), A("cx", "7"), A("cy", "7"), A("r", "6"), A("stroke-width", "2"))),
			Marker("lollipopEnd-margin", "marker lollipop", true, [A("refX", "1"), A("refY", "7"), A("markerWidth", "190"), A("markerHeight", "240"), A("orient", "auto"), A("markerUnits", UserSpace)],
				E("circle", A("fill", "transparent"), A("cx", "7"), A("cy", "7"), A("r", "6"), A("stroke-width", "2")))),
		Set("point",
			Marker("pointEnd", "marker", false, [A("viewBox", "0 0 10 10"), A("refX", "5"), A("refY", "5"), A("markerUnits", UserSpace), A("markerWidth", "8"), A("markerHeight", "8"), A("orient", "auto")],
				E("path", A("d", "M 0 0 L 10 5 L 0 10 z"), A("class", ClassPath), S("stroke-width", "1"), S("stroke-dasharray", "1,0"))),
			Marker("pointStart", "marker", false, [A("viewBox", "0 0 10 10"), A("refX", "4.5"), A("refY", "5"), A("markerUnits", UserSpace), A("markerWidth", "8"), A("markerHeight", "8"), A("orient", "auto")],
				E("path", A("d", "M 0 5 L 10 10 L 10 0 z"), A("class", ClassPath), S("stroke-width", "1"), S("stroke-dasharray", "1,0"))),
			Marker("pointEnd-margin", "marker", false, [A("viewBox", "0 0 11.5 14"), A("refX", "11.5"), A("refY", "7"), A("markerUnits", UserSpace), A("markerWidth", "10.5"), A("markerHeight", "14"), A("orient", "auto")],
				E("path", A("d", "M 0 0 L 11.5 7 L 0 14 z"), A("class", ClassPath), S("stroke-width", "0"), S("stroke-dasharray", "1,0"))),
			Marker("pointStart-margin", "marker", false, [A("viewBox", "0 0 11.5 14"), A("refX", "1"), A("refY", "7"), A("markerUnits", UserSpace), A("markerWidth", "11.5"), A("markerHeight", "14"), A("orient", "auto")],
				E("polygon", A("points", "0,7 11.5,14 11.5,0"), A("class", ClassPath), S("stroke-width", "0"), S("stroke-dasharray", "1,0")))),
		Set("circle",
			Marker("circleEnd", "marker", false, [A("viewBox", "0 0 10 10"), A("refX", "11"), A("refY", "5"), A("markerUnits", UserSpace), A("markerWidth", "11"), A("markerHeight", "11"), A("orient", "auto")],
				E("circle", A("cx", "5"), A("cy", "5"), A("r", "5"), A("class", ClassPath), S("stroke-width", "1"), S("stroke-dasharray", "1,0"))),
			Marker("circleStart", "marker", false, [A("viewBox", "0 0 10 10"), A("refX", "-1"), A("refY", "5"), A("markerUnits", UserSpace), A("markerWidth", "11"), A("markerHeight", "11"), A("orient", "auto")],
				E("circle", A("cx", "5"), A("cy", "5"), A("r", "5"), A("class", ClassPath), S("stroke-width", "1"), S("stroke-dasharray", "1,0"))),
			Marker("circleEnd-margin", "marker", false, [A("viewBox", "0 0 10 10"), A("refY", "5"), A("refX", "12.25"), A("markerUnits", UserSpace), A("markerWidth", "14"), A("markerHeight", "14"), A("orient", "auto")],
				E("circle", A("cx", "5"), A("cy", "5"), A("r", "5"), A("class", ClassPath), S("stroke-width", "0"), S("stroke-dasharray", "1,0"))),
			Marker("circleStart-margin", "marker", false, [A("viewBox", "0 0 10 10"), A("refX", "-2"), A("refY", "5"), A("markerUnits", UserSpace), A("markerWidth", "14"), A("markerHeight", "14"), A("orient", "auto")],
				E("circle", A("cx", "5"), A("cy", "5"), A("r", "5"), A("class", ClassPath), S("stroke-width", "0"), S("stroke-dasharray", "1,0")))),
		Set("cross",
			Marker("crossEnd", "marker cross", false, [A("viewBox", "0 0 11 11"), A("refX", "12"), A("refY", "5.2"), A("markerUnits", UserSpace), A("markerWidth", "11"), A("markerHeight", "11"), A("orient", "auto")],
				E("path", A("d", "M 1,1 l 9,9 M 10,1 l -9,9"), A("class", ClassPath), S("stroke-width", "2"), S("stroke-dasharray", "1,0"))),
			Marker("crossStart", "marker cross", false, [A("viewBox", "0 0 11 11"), A("refX", "-1"), A("refY", "5.2"), A("markerUnits", UserSpace), A("markerWidth", "11"), A("markerHeight", "11"), A("orient", "auto")],
				E("path", A("d", "M 1,1 l 9,9 M 10,1 l -9,9"), A("class", ClassPath), S("stroke-width", "2"), S("stroke-dasharray", "1,0"))),
			Marker("crossEnd-margin", "marker cross", false, [A("viewBox", "0 0 15 15"), A("refX", "17.7"), A("refY", "7.5"), A("markerUnits", UserSpace), A("markerWidth", "12"), A("markerHeight", "12"), A("orient", "auto")],
				E("path", A("d", "M 1,1 L 14,14 M 1,14 L 14,1"), A("class", ClassPath), S("stroke-width", "2.5"))),
			Marker("crossStart-margin", "marker cross", false, [A("viewBox", "0 0 15 15"), A("refX", "-3.5"), A("refY", "7.5"), A("markerUnits", UserSpace), A("markerWidth", "12"), A("markerHeight", "12"), A("orient", "auto")],
				E("path", A("d", "M 1,1 L 14,14 M 1,14 L 14,1"), A("class", ClassPath), S("stroke-width", "2.5"), S("stroke-dasharray", "1,0")))),
		Set("barb",
			Marker("barbEnd", null, true, [A("refX", "19"), A("refY", "7"), A("markerWidth", "20"), A("markerHeight", "14"), A("markerUnits", UserSpace), A("orient", "auto")],
				E("path", A("d", "M 19,7 L9,13 L14,7 L9,1 Z")))),
		Set("barbNeo",
			Marker("barbEnd", null, true, [A("refX", "19"), A("refY", "7"), A("markerWidth", "20"), A("markerHeight", "14"), A("markerUnits", "strokeWidth"), A("orient", "auto")],
				E("path", A("d", "M 19,7 L11,14 L13,7 L11,0 Z"))),
			Marker("barbEnd-margin", null, true, [A("refX", "17"), A("refY", "7"), A("markerWidth", "20"), A("markerHeight", "14"), A("markerUnits", UserSpace), A("orient", "auto")],
				E("path", A("d", "M 19,7 L11,14 L13,7 L11,0 Z"), T("fill", MarkerThemeValue.TransitionColor)))),
		Set("hierarchy",
			Marker("hierarchyEnd", "marker hierarchy", false, [A("viewBox", "0 0 12 10"), A("refX", "10"), A("refY", "5"), A("markerUnits", UserSpace), A("markerWidth", "12"), A("markerHeight", "10"), A("orient", "auto")],
				E("path", A("d", "M 0 0 L 6 5 L 0 10 M 4 0 L 10 5 L 4 10"), A("class", ClassPath), S("stroke-width", "1"), S("stroke-dasharray", "1,0"), S("fill", "none"))),
			Marker("hierarchyStart", "marker hierarchy", false, [A("viewBox", "0 0 12 10"), A("refX", "2"), A("refY", "5"), A("markerUnits", UserSpace), A("markerWidth", "12"), A("markerHeight", "10"), A("orient", "auto")],
				E("path", A("d", "M 12 0 L 6 5 L 12 10 M 8 0 L 2 5 L 8 10"), A("class", ClassPath), S("stroke-width", "1"), S("stroke-dasharray", "1,0"), S("fill", "none")))),
		Set("only_one",
			Marker("onlyOneStart", "marker onlyOne", true, [A("refX", "0"), A("refY", "9"), A("markerWidth", "18"), A("markerHeight", "18"), A("orient", "auto")],
				E("path", A("d", "M9,0 L9,18 M15,0 L15,18"))),
			Marker("onlyOneEnd", "marker onlyOne", true, [A("refX", "18"), A("refY", "9"), A("markerWidth", "18"), A("markerHeight", "18"), A("orient", "auto")],
				E("path", A("d", "M3,0 L3,18 M9,0 L9,18")))),
		Set("zero_or_one",
			Marker("zeroOrOneStart", "marker zeroOrOne", true, [A("refX", "0"), A("refY", "9"), A("markerWidth", "30"), A("markerHeight", "18"), A("orient", "auto")],
				E("circle", A("fill", "white"), A("cx", "21"), A("cy", "9"), A("r", "6")),
				E("path", A("d", "M9,0 L9,18"))),
			Marker("zeroOrOneEnd", "marker zeroOrOne", true, [A("refX", "30"), A("refY", "9"), A("markerWidth", "30"), A("markerHeight", "18"), A("orient", "auto")],
				E("circle", A("fill", "white"), A("cx", "9"), A("cy", "9"), A("r", "6")),
				E("path", A("d", "M21,0 L21,18")))),
		Set("one_or_more",
			Marker("oneOrMoreStart", "marker oneOrMore", true, [A("refX", "18"), A("refY", "18"), A("markerWidth", "45"), A("markerHeight", "36"), A("orient", "auto")],
				E("path", A("d", "M0,18 Q 18,0 36,18 Q 18,36 0,18 M42,9 L42,27"))),
			Marker("oneOrMoreEnd", "marker oneOrMore", true, [A("refX", "27"), A("refY", "18"), A("markerWidth", "45"), A("markerHeight", "36"), A("orient", "auto")],
				E("path", A("d", "M3,9 L3,27 M9,18 Q27,0 45,18 Q27,36 9,18")))),
		Set("zero_or_more",
			Marker("zeroOrMoreStart", "marker zeroOrMore", true, [A("refX", "18"), A("refY", "18"), A("markerWidth", "57"), A("markerHeight", "36"), A("orient", "auto")],
				E("circle", A("fill", "white"), A("cx", "48"), A("cy", "18"), A("r", "6")),
				E("path", A("d", "M0,18 Q18,0 36,18 Q18,36 0,18"))),
			Marker("zeroOrMoreEnd", "marker zeroOrMore", true, [A("refX", "39"), A("refY", "18"), A("markerWidth", "57"), A("markerHeight", "36"), A("orient", "auto")],
				E("circle", A("fill", "white"), A("cx", "9"), A("cy", "18"), A("r", "6")),
				E("path", A("d", "M21,18 Q39,0 57,18 Q39,36 21,18")))),
		Set("only_one_neo",
			Marker("onlyOneStart", "marker onlyOne", true, [A("refX", "0"), A("refY", "9"), A("markerWidth", "18"), A("markerHeight", "18"), A("orient", "auto"), A("markerUnits", UserSpace)],
				E("path", A("d", "M9,0 L9,18 M15,0 L15,18"), T("stroke-width", MarkerThemeValue.StrokeWidth))),
			Marker("onlyOneEnd", "marker onlyOne", true, [A("refX", "18"), A("refY", "9"), A("markerWidth", "18"), A("markerHeight", "18"), A("orient", "auto"), A("markerUnits", UserSpace)],
				E("path", A("d", "M3,0 L3,18 M9,0 L9,18"), T("stroke-width", MarkerThemeValue.StrokeWidth)))),
		Set("zero_or_one_neo",
			Marker("zeroOrOneStart", "marker zeroOrOne", true, [A("refX", "0"), A("refY", "9"), A("markerWidth", "30"), A("markerHeight", "18"), A("orient", "auto"), A("markerUnits", UserSpace)],
				E("circle", T("fill", MarkerThemeValue.MainBackground), A("cx", "21"), A("cy", "9"), T("stroke-width", MarkerThemeValue.StrokeWidth), A("r", "6")),
				E("path", A("d", "M9,0 L9,18"), T("stroke-width", MarkerThemeValue.StrokeWidth))),
			Marker("zeroOrOneEnd", "marker zeroOrOne", true, [A("refX", "30"), A("refY", "9"), A("markerWidth", "30"), A("markerHeight", "18"), A("markerUnits", UserSpace), A("orient", "auto")],
				E("circle", T("fill", MarkerThemeValue.MainBackground), A("cx", "9"), A("cy", "9"), T("stroke-width", MarkerThemeValue.StrokeWidth), A("r", "6")),
				E("path", A("d", "M21,0 L21,18"), T("stroke-width", MarkerThemeValue.StrokeWidth)))),
		Set("one_or_more_neo",
			Marker("oneOrMoreStart", "marker oneOrMore", true, [A("refX", "18"), A("refY", "18"), A("markerWidth", "45"), A("markerHeight", "36"), A("orient", "auto"), A("markerUnits", UserSpace)],
				E("path", A("d", "M0,18 Q 18,0 36,18 Q 18,36 0,18 M42,9 L42,27"), T("stroke-width", MarkerThemeValue.StrokeWidth))),
			Marker("oneOrMoreEnd", "marker oneOrMore", true, [A("refX", "27"), A("refY", "18"), A("markerWidth", "45"), A("markerHeight", "36"), A("markerUnits", UserSpace), A("orient", "auto")],
				E("path", A("d", "M3,9 L3,27 M9,18 Q27,0 45,18 Q27,36 9,18"), T("stroke-width", MarkerThemeValue.StrokeWidth)))),
		Set("zero_or_more_neo",
			Marker("zeroOrMoreStart", "marker zeroOrMore", true, [A("refX", "18"), A("refY", "18"), A("markerWidth", "57"), A("markerHeight", "36"), A("markerUnits", UserSpace), A("orient", "auto")],
				E("circle", T("fill", MarkerThemeValue.MainBackground), A("cx", "45.5"), A("cy", "18"), A("r", "6"), T("stroke-width", MarkerThemeValue.StrokeWidth)),
				E("path", A("d", "M0,18 Q18,0 36,18 Q18,36 0,18"), T("stroke-width", MarkerThemeValue.StrokeWidth))),
			Marker("zeroOrMoreEnd", "marker zeroOrMore", true, [A("refX", "39"), A("refY", "18"), A("markerWidth", "57"), A("markerHeight", "36"), A("orient", "auto"), A("markerUnits", UserSpace)],
				E("circle", T("fill", MarkerThemeValue.MainBackground), A("cx", "11"), A("cy", "18"), A("r", "6"), T("stroke-width", MarkerThemeValue.StrokeWidth)),
				E("path", A("d", "M21,18 Q39,0 57,18 Q39,36 21,18"), T("stroke-width", MarkerThemeValue.StrokeWidth)))),
		Set("requirement_arrow",
			Marker("requirement_arrowEnd", null, true, [A("refX", "20"), A("refY", "10"), A("markerWidth", "20"), A("markerHeight", "20"), A("orient", "auto")],
				E("path", A("d", RequirementArrow)))),
		Set("requirement_contains",
			Marker("requirement_containsStart", null, true, [A("refX", "0"), A("refY", "10"), A("markerWidth", "20"), A("markerHeight", "20"), A("orient", "auto")],
				new MarkerElement("g", [],
				[
					E("circle", A("cx", "10"), A("cy", "10"), A("r", "9"), A("fill", "none")),
					E("line", A("x1", "1"), A("x2", "19"), A("y1", "10"), A("y2", "10")),
					E("line", A("y1", "1"), A("y2", "19"), A("x1", "10"), A("x2", "10")),
				]))),
		Set("requirement_arrow_neo",
			Marker("requirement_arrowEnd", null, true, [A("refX", "20"), A("refY", "10"), A("markerWidth", "20"), A("markerHeight", "20"), A("orient", "auto"), A("markerUnits", UserSpace), T("stroke-width", MarkerThemeValue.StrokeWidth), A("viewBox", "0 0 25 20")],
				E("path", A("d", RequirementArrow), A("stroke-linejoin", "miter")))),
		Set("requirement_contains_neo",
			Marker("requirement_containsStart", null, true, [A("refX", "0"), A("refY", "10"), A("markerWidth", "20"), A("markerHeight", "20"), A("orient", "auto"), A("markerUnits", UserSpace)],
				new MarkerElement("g", [],
				[
					E("circle", A("cx", "10"), A("cy", "10"), A("r", "9"), A("fill", "none"), T("stroke-width", MarkerThemeValue.StrokeWidth)),
					E("line", A("x1", "1"), A("x2", "19"), A("y1", "10"), A("y2", "10"), T("stroke-width", MarkerThemeValue.StrokeWidth)),
					E("line", A("y1", "1"), A("y2", "19"), A("x1", "10"), A("x2", "10"), T("stroke-width", MarkerThemeValue.StrokeWidth)),
				]))),
		Set("openArrow",
			Marker("openArrowEnd", "marker openArrow", false, [A("viewBox", "0 0 12 12"), A("refX", "11"), A("refY", "6"), A("markerUnits", UserSpace), A("markerWidth", "12"), A("markerHeight", "12"), A("orient", "auto")],
				E("path", A("d", "M 1 1 L 11 6 L 1 11"), A("class", ClassPath), S("stroke-width", "1.4"), S("stroke-linecap", "round"), S("stroke-linejoin", "round"), S("stroke-dasharray", "1,0"), S("fill", "none"))),
			Marker("openArrowStart", "marker openArrow", false, [A("viewBox", "0 0 12 12"), A("refX", "1"), A("refY", "6"), A("markerUnits", UserSpace), A("markerWidth", "12"), A("markerHeight", "12"), A("orient", "auto")],
				E("path", A("d", "M 11 1 L 1 6 L 11 11"), A("class", ClassPath), S("stroke-width", "1.4"), S("stroke-linecap", "round"), S("stroke-linejoin", "round"), S("stroke-dasharray", "1,0"), S("fill", "none")))),
		Set("hollowCircle",
			Marker("hollowCircleStart", "marker hollowCircle", false, [A("viewBox", "0 0 10 10"), A("refX", "0"), A("refY", "5"), A("markerUnits", UserSpace), A("markerWidth", "10"), A("markerHeight", "10"), A("orient", "auto")],
				E("circle", A("cx", "5"), A("cy", "5"), A("r", "3.6"), A("class", ClassPath), S("stroke-width", "1.4"), S("stroke-dasharray", "1,0"), S("fill", "none"))),
			Marker("hollowCircleEnd", "marker hollowCircle", false, [A("viewBox", "0 0 10 10"), A("refX", "10"), A("refY", "5"), A("markerUnits", UserSpace), A("markerWidth", "10"), A("markerHeight", "10"), A("orient", "auto")],
				E("circle", A("cx", "5"), A("cy", "5"), A("r", "3.6"), A("class", ClassPath), S("stroke-width", "1.4"), S("stroke-dasharray", "1,0"), S("fill", "none")))),
	];

	private static MarkerAttribute A(string name, string value) => new(name, value);

	private static MarkerAttribute S(string name, string value) => new(name, value, IsStyle: true);

	// A neo marker's theme variable. Mermaid writes `${variable}`, so a theme without it gives "undefined", except
	// mainBkg, which falls back to white.
	private static MarkerAttribute T(string name, MarkerThemeValue theme) =>
		new(name, theme == MarkerThemeValue.MainBackground ? "white" : "undefined", theme);

	private static MarkerElement E(string tag, params MarkerAttribute[] attributes) => new(tag, [.. attributes], []);

	private static MarkerDefinition Marker(string name, string? @class, bool inDefs, MarkerAttribute[] attributes, params MarkerElement[] children) =>
		new(name, @class, inDefs, [.. attributes], [.. children]);

	private static MarkerSet Set(string name, params MarkerDefinition[] markers) => new(name, [.. markers]);
}
