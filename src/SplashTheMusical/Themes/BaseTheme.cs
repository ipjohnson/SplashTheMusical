namespace SplashTheMusical.Themes;

/// <summary>mermaid's <c>packages/mermaid/src/themes/theme-base.js</c>.</summary>
internal sealed class BaseTheme : Theme
{
	public override string Name => "base";

	protected override void Construct()
	{
		Set("background", Str("#f4f4f4"));
		Set("primaryColor", Str("#fff4dd"));
		Set("noteBkgColor", Str("#fff5ad"));
		Set("noteTextColor", Str("#333"));
		Set("THEME_COLOR_LIMIT", Num(12));
		Set("radius", Num(5));
		Set("strokeWidth", Num(1));
		Set("fontFamily", Str("\"trebuchet ms\", verdana, arial, sans-serif"));
		Set("fontSize", Str("16px"));
		Set("useGradient", MermaidValue.True);
		Set("dropShadow", Str("drop-shadow( 1px 2px 2px rgba(185,185,185,1))"));
	}

	protected override void UpdateColors()
	{
		Set("primaryTextColor", (Or(Get("primaryTextColor")) ?? (Truthy(Get("darkMode")) ? Str("#eee") : Str("#333"))));
		Set("secondaryColor", (Or(Get("secondaryColor")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: -120))));
		Set("tertiaryColor", (Or(Get("tertiaryColor")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 180, L: 5))));
		Set("primaryBorderColor", (Or(Get("primaryBorderColor")) ?? MkBorder(Get("primaryColor"), Truthy(Get("darkMode")))));
		Set("secondaryBorderColor", (Or(Get("secondaryBorderColor")) ?? MkBorder(Get("secondaryColor"), Truthy(Get("darkMode")))));
		Set("tertiaryBorderColor", (Or(Get("tertiaryBorderColor")) ?? MkBorder(Get("tertiaryColor"), Truthy(Get("darkMode")))));
		Set("noteBorderColor", (Or(Get("noteBorderColor")) ?? MkBorder(Get("noteBkgColor"), Truthy(Get("darkMode")))));
		Set("noteBkgColor", (Or(Get("noteBkgColor")) ?? Str("#fff5ad")));
		Set("noteTextColor", (Or(Get("noteTextColor")) ?? Str("#333")));
		Set("secondaryTextColor", (Or(Get("secondaryTextColor")) ?? Invert(Get("secondaryColor"))));
		Set("tertiaryTextColor", (Or(Get("tertiaryTextColor")) ?? Invert(Get("tertiaryColor"))));
		Set("lineColor", (Or(Get("lineColor")) ?? Invert(Get("background"))));
		Set("arrowheadColor", (Or(Get("arrowheadColor")) ?? Invert(Get("background"))));
		Set("textColor", (Or(Get("textColor")) ?? Get("primaryTextColor")));
		Set("border2", (Or(Get("border2")) ?? Get("tertiaryBorderColor")));
		Set("nodeBkg", (Or(Get("nodeBkg")) ?? Get("primaryColor")));
		Set("mainBkg", (Or(Get("mainBkg")) ?? Get("primaryColor")));
		Set("nodeBorder", (Or(Get("nodeBorder")) ?? Get("primaryBorderColor")));
		Set("clusterBkg", (Or(Get("clusterBkg")) ?? Get("tertiaryColor")));
		Set("clusterBorder", (Or(Get("clusterBorder")) ?? Get("tertiaryBorderColor")));
		Set("defaultLinkColor", (Or(Get("defaultLinkColor")) ?? Get("lineColor")));
		Set("titleColor", (Or(Get("titleColor")) ?? Get("tertiaryTextColor")));
		Set("edgeLabelBackground", (Or(Get("edgeLabelBackground")) ?? (Truthy(Get("darkMode")) ? Darken(Get("secondaryColor"), 30) : Get("secondaryColor"))));
		Set("flowContainerStroke", (Or(Get("flowContainerStroke")) ?? Get("secondaryBorderColor")));
		Set("nodeTextColor", (Or(Get("nodeTextColor")) ?? Get("primaryTextColor")));
		Set("actorBorder", (Or(Get("actorBorder")) ?? Get("primaryBorderColor")));
		Set("actorBkg", (Or(Get("actorBkg")) ?? Get("mainBkg")));
		Set("actorTextColor", (Or(Get("actorTextColor")) ?? Get("primaryTextColor")));
		Set("actorLineColor", (Or(Get("actorLineColor")) ?? Get("actorBorder")));
		Set("labelBoxBkgColor", (Or(Get("labelBoxBkgColor")) ?? Get("actorBkg")));
		Set("signalColor", (Or(Get("signalColor")) ?? Get("textColor")));
		Set("signalTextColor", (Or(Get("signalTextColor")) ?? Get("textColor")));
		Set("labelBoxBorderColor", (Or(Get("labelBoxBorderColor")) ?? Get("actorBorder")));
		Set("labelTextColor", (Or(Get("labelTextColor")) ?? Get("actorTextColor")));
		Set("loopTextColor", (Or(Get("loopTextColor")) ?? Get("actorTextColor")));
		Set("activationBorderColor", (Or(Get("activationBorderColor")) ?? Darken(Get("secondaryColor"), 10)));
		Set("activationBkgColor", (Or(Get("activationBkgColor")) ?? Get("secondaryColor")));
		Set("sequenceNumberColor", (Or(Get("sequenceNumberColor")) ?? Invert(Get("lineColor"))));
		Set("rectBkgColor", (Or(Get("rectBkgColor")) ?? Get("tertiaryColor")));
		Set("sectionBkgColor", (Or(Get("sectionBkgColor")) ?? Get("tertiaryColor")));
		Set("altSectionBkgColor", (Or(Get("altSectionBkgColor")) ?? Str("white")));
		Set("sectionBkgColor", (Or(Get("sectionBkgColor")) ?? Get("secondaryColor")));
		Set("sectionBkgColor2", (Or(Get("sectionBkgColor2")) ?? Get("primaryColor")));
		Set("excludeBkgColor", (Or(Get("excludeBkgColor")) ?? Str("#eeeeee")));
		Set("taskBorderColor", (Or(Get("taskBorderColor")) ?? Get("primaryBorderColor")));
		Set("taskBkgColor", (Or(Get("taskBkgColor")) ?? Get("primaryColor")));
		Set("activeTaskBorderColor", (Or(Get("activeTaskBorderColor")) ?? Get("primaryColor")));
		Set("activeTaskBkgColor", (Or(Get("activeTaskBkgColor")) ?? Lighten(Get("primaryColor"), 23)));
		Set("gridColor", (Or(Get("gridColor")) ?? Str("lightgrey")));
		Set("doneTaskBkgColor", (Or(Get("doneTaskBkgColor")) ?? Str("lightgrey")));
		Set("doneTaskBorderColor", (Or(Get("doneTaskBorderColor")) ?? Str("grey")));
		Set("critBorderColor", (Or(Get("critBorderColor")) ?? Str("#ff8888")));
		Set("critBkgColor", (Or(Get("critBkgColor")) ?? Str("red")));
		Set("todayLineColor", (Or(Get("todayLineColor")) ?? Str("red")));
		Set("vertLineColor", (Or(Get("vertLineColor")) ?? Str("navy")));
		Set("taskTextColor", (Or(Get("taskTextColor")) ?? Get("textColor")));
		Set("taskTextOutsideColor", (Or(Get("taskTextOutsideColor")) ?? Get("textColor")));
		Set("taskTextLightColor", (Or(Get("taskTextLightColor")) ?? Get("textColor")));
		Set("taskTextColor", (Or(Get("taskTextColor")) ?? Get("primaryTextColor")));
		Set("taskTextDarkColor", (Or(Get("taskTextDarkColor")) ?? Get("textColor")));
		Set("taskTextClickableColor", (Or(Get("taskTextClickableColor")) ?? Str("#003163")));
		Set("noteFontWeight", (Or(Get("noteFontWeight")) ?? Str("normal")));
		Set("fontWeight", (Or(Get("fontWeight")) ?? Str("normal")));
		Set("personBorder", (Or(Get("personBorder")) ?? Get("primaryBorderColor")));
		Set("personBkg", (Or(Get("personBkg")) ?? Get("mainBkg")));
		if (Truthy(Get("darkMode")))
		{
			Set("rowOdd", (Or((Or(Get("rowOdd")) ?? Darken(Get("mainBkg"), 5))) ?? Str("#ffffff")));
			Set("rowEven", (Or(Get("rowEven")) ?? Darken(Get("mainBkg"), 10)));
		}
		else
		{
			Set("rowOdd", (Or((Or(Get("rowOdd")) ?? Lighten(Get("mainBkg"), 75))) ?? Str("#ffffff")));
			Set("rowEven", (Or(Get("rowEven")) ?? Lighten(Get("mainBkg"), 5)));
		}
		Set("transitionColor", (Or(Get("transitionColor")) ?? Get("lineColor")));
		Set("transitionLabelColor", (Or(Get("transitionLabelColor")) ?? Get("textColor")));
		Set("stateLabelColor", (Or((Or(Get("stateLabelColor")) ?? Get("stateBkg"))) ?? Get("primaryTextColor")));
		Set("stateBkg", (Or(Get("stateBkg")) ?? Get("mainBkg")));
		Set("labelBackgroundColor", (Or(Get("labelBackgroundColor")) ?? Get("stateBkg")));
		Set("compositeBackground", (Or((Or(Get("compositeBackground")) ?? Get("background"))) ?? Get("tertiaryColor")));
		Set("altBackground", (Or(Get("altBackground")) ?? Get("tertiaryColor")));
		Set("compositeTitleBackground", (Or(Get("compositeTitleBackground")) ?? Get("mainBkg")));
		Set("compositeBorder", (Or(Get("compositeBorder")) ?? Get("nodeBorder")));
		Set("innerEndBackground", Get("nodeBorder"));
		Set("errorBkgColor", (Or(Get("errorBkgColor")) ?? Get("tertiaryColor")));
		Set("errorTextColor", (Or(Get("errorTextColor")) ?? Get("tertiaryTextColor")));
		Set("transitionColor", (Or(Get("transitionColor")) ?? Get("lineColor")));
		Set("specialStateColor", Get("lineColor"));
		Set("cScale0", (Or(Get("cScale0")) ?? Get("primaryColor")));
		Set("cScale1", (Or(Get("cScale1")) ?? Get("secondaryColor")));
		Set("cScale2", (Or(Get("cScale2")) ?? Get("tertiaryColor")));
		Set("cScale3", (Or(Get("cScale3")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 30))));
		Set("cScale4", (Or(Get("cScale4")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 60))));
		Set("cScale5", (Or(Get("cScale5")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 90))));
		Set("cScale6", (Or(Get("cScale6")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 120))));
		Set("cScale7", (Or(Get("cScale7")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 150))));
		Set("cScale8", (Or(Get("cScale8")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 210, L: 150))));
		Set("cScale9", (Or(Get("cScale9")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 270))));
		Set("cScale10", (Or(Get("cScale10")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 300))));
		Set("cScale11", (Or(Get("cScale11")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 330))));
		if (Truthy(Get("darkMode")))
		{
			for (var i = 0; i < ToNumber(Get("THEME_COLOR_LIMIT")); i++)
			{
				Set(Key("cScale", i), Darken(Get(Key("cScale", i)), 75));
			}
		}
		else
		{
			for (var i = 0; i < ToNumber(Get("THEME_COLOR_LIMIT")); i++)
			{
				Set(Key("cScale", i), Darken(Get(Key("cScale", i)), 25));
			}
		}
		for (var i = 0; i < ToNumber(Get("THEME_COLOR_LIMIT")); i++)
		{
			Set(Key("cScaleInv", i), (Or(Get(Key("cScaleInv", i))) ?? Invert(Get(Key("cScale", i)))));
		}
		for (var i = 0; i < ToNumber(Get("THEME_COLOR_LIMIT")); i++)
		{
			if (Truthy(Get("darkMode")))
			{
				Set(Key("cScalePeer", i), (Or(Get(Key("cScalePeer", i))) ?? Lighten(Get(Key("cScale", i)), 10)));
			}
			else
			{
				Set(Key("cScalePeer", i), (Or(Get(Key("cScalePeer", i))) ?? Darken(Get(Key("cScale", i)), 10)));
			}
		}
		Set("scaleLabelColor", (Or(Get("scaleLabelColor")) ?? Get("labelTextColor")));
		for (var i = 0; i < ToNumber(Get("THEME_COLOR_LIMIT")); i++)
		{
			Set(Key("cScaleLabel", i), (Or(Get(Key("cScaleLabel", i))) ?? Get("scaleLabelColor")));
		}
		var multiplier = (Truthy(Get("darkMode")) ? -4 : -1);
		for (var i = 0; i < 5; i++)
		{
			Set(Key("surface", i), (Or(Get(Key("surface", i))) ?? Adjust(Get("mainBkg"), new KhromaChannels(H: 180, S: -15, L: (multiplier * (5 + (i * 3)))))));
			Set(Key("surfacePeer", i), (Or(Get(Key("surfacePeer", i))) ?? Adjust(Get("mainBkg"), new KhromaChannels(H: 180, S: -15, L: (multiplier * (8 + (i * 3)))))));
		}
		Set("classText", (Or(Get("classText")) ?? Get("textColor")));
		Set("fillType0", (Or(Get("fillType0")) ?? Get("primaryColor")));
		Set("fillType1", (Or(Get("fillType1")) ?? Get("secondaryColor")));
		Set("fillType2", (Or(Get("fillType2")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 64))));
		Set("fillType3", (Or(Get("fillType3")) ?? Adjust(Get("secondaryColor"), new KhromaChannels(H: 64))));
		Set("fillType4", (Or(Get("fillType4")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: -64))));
		Set("fillType5", (Or(Get("fillType5")) ?? Adjust(Get("secondaryColor"), new KhromaChannels(H: -64))));
		Set("fillType6", (Or(Get("fillType6")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 128))));
		Set("fillType7", (Or(Get("fillType7")) ?? Adjust(Get("secondaryColor"), new KhromaChannels(H: 128))));
		Set("pie1", (Or(Get("pie1")) ?? Get("primaryColor")));
		Set("pie2", (Or(Get("pie2")) ?? Get("secondaryColor")));
		Set("pie3", (Or(Get("pie3")) ?? Get("tertiaryColor")));
		Set("pie4", (Or(Get("pie4")) ?? Adjust(Get("primaryColor"), new KhromaChannels(L: -10))));
		Set("pie5", (Or(Get("pie5")) ?? Adjust(Get("secondaryColor"), new KhromaChannels(L: -10))));
		Set("pie6", (Or(Get("pie6")) ?? Adjust(Get("tertiaryColor"), new KhromaChannels(L: -10))));
		Set("pie7", (Or(Get("pie7")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 60, L: -10))));
		Set("pie8", (Or(Get("pie8")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: -60, L: -10))));
		Set("pie9", (Or(Get("pie9")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 120, L: 0))));
		Set("pie10", (Or(Get("pie10")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 60, L: -20))));
		Set("pie11", (Or(Get("pie11")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: -60, L: -20))));
		Set("pie12", (Or(Get("pie12")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 120, L: -10))));
		Set("pieTitleTextSize", (Or(Get("pieTitleTextSize")) ?? Str("25px")));
		Set("pieTitleTextColor", (Or(Get("pieTitleTextColor")) ?? Get("taskTextDarkColor")));
		Set("pieSectionTextSize", (Or(Get("pieSectionTextSize")) ?? Str("17px")));
		Set("pieSectionTextColor", (Or(Get("pieSectionTextColor")) ?? Get("textColor")));
		Set("pieLegendTextSize", (Or(Get("pieLegendTextSize")) ?? Str("17px")));
		Set("pieLegendTextColor", (Or(Get("pieLegendTextColor")) ?? Get("taskTextDarkColor")));
		Set("pieStrokeColor", (Or(Get("pieStrokeColor")) ?? Str("black")));
		Set("pieStrokeWidth", (Or(Get("pieStrokeWidth")) ?? Str("2px")));
		Set("pieOuterStrokeWidth", (Or(Get("pieOuterStrokeWidth")) ?? Str("2px")));
		Set("pieOuterStrokeColor", (Or(Get("pieOuterStrokeColor")) ?? Str("black")));
		Set("pieOpacity", (Or(Get("pieOpacity")) ?? Str("0.7")));
		Set("venn1", (Nullish(Get("venn1")) ?? Adjust(Get("primaryColor"), new KhromaChannels(L: -30))));
		Set("venn2", (Nullish(Get("venn2")) ?? Adjust(Get("secondaryColor"), new KhromaChannels(L: -30))));
		Set("venn3", (Nullish(Get("venn3")) ?? Adjust(Get("tertiaryColor"), new KhromaChannels(L: -30))));
		Set("venn4", (Nullish(Get("venn4")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 60, L: -30))));
		Set("venn5", (Nullish(Get("venn5")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: -60, L: -30))));
		Set("venn6", (Nullish(Get("venn6")) ?? Adjust(Get("secondaryColor"), new KhromaChannels(H: 60, L: -30))));
		Set("venn7", (Nullish(Get("venn7")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 120, L: -30))));
		Set("venn8", (Nullish(Get("venn8")) ?? Adjust(Get("secondaryColor"), new KhromaChannels(H: 120, L: -30))));
		Set("vennTitleTextColor", (Nullish(Get("vennTitleTextColor")) ?? Get("titleColor")));
		Set("vennSetTextColor", (Nullish(Get("vennSetTextColor")) ?? Get("textColor")));
		Set("cynefin", Obj(("domainFontSize", (Or(Member(Get("cynefin"), "domainFontSize")) ?? Num(16))), ("itemFontSize", (Or(Member(Get("cynefin"), "itemFontSize")) ?? Num(12))), ("boundaryColor", (Or(Member(Get("cynefin"), "boundaryColor")) ?? Get("lineColor"))), ("boundaryWidth", (Or(Member(Get("cynefin"), "boundaryWidth")) ?? Num(2))), ("cliffColor", (Or(Member(Get("cynefin"), "cliffColor")) ?? Str("#8B0000"))), ("cliffWidth", (Or(Member(Get("cynefin"), "cliffWidth")) ?? Num(4))), ("arrowColor", (Or(Member(Get("cynefin"), "arrowColor")) ?? Get("lineColor"))), ("arrowWidth", (Or(Member(Get("cynefin"), "arrowWidth")) ?? Num(2))), ("complexBg", (Or(Member(Get("cynefin"), "complexBg")) ?? Str("#E8F5E9"))), ("complicatedBg", (Or(Member(Get("cynefin"), "complicatedBg")) ?? Str("#E3F2FD"))), ("chaoticBg", (Or(Member(Get("cynefin"), "chaoticBg")) ?? Str("#FBE9E7"))), ("clearBg", (Or(Member(Get("cynefin"), "clearBg")) ?? Str("#FFF8E1"))), ("confusionBg", (Or(Member(Get("cynefin"), "confusionBg")) ?? Str("#F3E5F5"))), ("textColor", (Or(Member(Get("cynefin"), "textColor")) ?? Get("textColor"))), ("labelColor", (Or(Member(Get("cynefin"), "labelColor")) ?? Get("primaryTextColor")))));
		Set("radar", Obj(("axisColor", (Or(Member(Get("radar"), "axisColor")) ?? Get("lineColor"))), ("axisStrokeWidth", (Or(Member(Get("radar"), "axisStrokeWidth")) ?? Num(2))), ("axisLabelFontSize", (Or(Member(Get("radar"), "axisLabelFontSize")) ?? Num(12))), ("curveOpacity", (Or(Member(Get("radar"), "curveOpacity")) ?? Num(0.5))), ("curveStrokeWidth", (Or(Member(Get("radar"), "curveStrokeWidth")) ?? Num(2))), ("graticuleColor", (Or(Member(Get("radar"), "graticuleColor")) ?? Str("#DEDEDE"))), ("graticuleStrokeWidth", (Or(Member(Get("radar"), "graticuleStrokeWidth")) ?? Num(1))), ("graticuleOpacity", (Or(Member(Get("radar"), "graticuleOpacity")) ?? Num(0.3))), ("legendBoxSize", (Or(Member(Get("radar"), "legendBoxSize")) ?? Num(12))), ("legendFontSize", (Or(Member(Get("radar"), "legendFontSize")) ?? Num(12)))));
		Set("wardleyEvolutionColor", (Or(Get("wardleyEvolutionColor")) ?? Str("#dc3545")));
		Set("wardley", Obj(("backgroundColor", (Or(Member(Get("wardley"), "backgroundColor")) ?? Get("background"))), ("axisColor", (Or(Member(Get("wardley"), "axisColor")) ?? Get("lineColor"))), ("axisTextColor", (Or(Member(Get("wardley"), "axisTextColor")) ?? Get("primaryTextColor"))), ("gridColor", (Or(Member(Get("wardley"), "gridColor")) ?? Get("gridColor"))), ("componentFill", (Or(Member(Get("wardley"), "componentFill")) ?? Get("background"))), ("componentStroke", (Or(Member(Get("wardley"), "componentStroke")) ?? Get("lineColor"))), ("componentLabelColor", (Or(Member(Get("wardley"), "componentLabelColor")) ?? Get("primaryTextColor"))), ("linkStroke", (Or(Member(Get("wardley"), "linkStroke")) ?? Get("lineColor"))), ("evolutionStroke", (Or(Member(Get("wardley"), "evolutionStroke")) ?? Get("wardleyEvolutionColor"))), ("annotationStroke", (Or(Member(Get("wardley"), "annotationStroke")) ?? Get("lineColor"))), ("annotationTextColor", (Or(Member(Get("wardley"), "annotationTextColor")) ?? Get("primaryTextColor"))), ("annotationFill", (Or(Member(Get("wardley"), "annotationFill")) ?? Get("background")))));
		Set("archEdgeColor", (Or(Get("archEdgeColor")) ?? Str("#777")));
		Set("archEdgeArrowColor", (Or(Get("archEdgeArrowColor")) ?? Str("#777")));
		Set("archEdgeWidth", (Or(Get("archEdgeWidth")) ?? Str("3")));
		Set("archGroupBorderColor", (Or(Get("archGroupBorderColor")) ?? Str("#000")));
		Set("archGroupBorderWidth", (Or(Get("archGroupBorderWidth")) ?? Str("2px")));
		Set("quadrant1Fill", (Or(Get("quadrant1Fill")) ?? Get("primaryColor")));
		Set("quadrant2Fill", (Or(Get("quadrant2Fill")) ?? Adjust(Get("primaryColor"), new KhromaChannels(R: 5, G: 5, B: 5))));
		Set("quadrant3Fill", (Or(Get("quadrant3Fill")) ?? Adjust(Get("primaryColor"), new KhromaChannels(R: 10, G: 10, B: 10))));
		Set("quadrant4Fill", (Or(Get("quadrant4Fill")) ?? Adjust(Get("primaryColor"), new KhromaChannels(R: 15, G: 15, B: 15))));
		Set("quadrant1TextFill", (Or(Get("quadrant1TextFill")) ?? Get("primaryTextColor")));
		Set("quadrant2TextFill", (Or(Get("quadrant2TextFill")) ?? Adjust(Get("primaryTextColor"), new KhromaChannels(R: -5, G: -5, B: -5))));
		Set("quadrant3TextFill", (Or(Get("quadrant3TextFill")) ?? Adjust(Get("primaryTextColor"), new KhromaChannels(R: -10, G: -10, B: -10))));
		Set("quadrant4TextFill", (Or(Get("quadrant4TextFill")) ?? Adjust(Get("primaryTextColor"), new KhromaChannels(R: -15, G: -15, B: -15))));
		Set("quadrantPointFill", ((Truthy(Get("quadrantPointFill")) || IsDark(Get("quadrant1Fill"))) ? Lighten(Get("quadrant1Fill"), double.NaN) : Darken(Get("quadrant1Fill"), double.NaN)));
		Set("quadrantPointTextFill", (Or(Get("quadrantPointTextFill")) ?? Get("primaryTextColor")));
		Set("quadrantXAxisTextFill", (Or(Get("quadrantXAxisTextFill")) ?? Get("primaryTextColor")));
		Set("quadrantYAxisTextFill", (Or(Get("quadrantYAxisTextFill")) ?? Get("primaryTextColor")));
		Set("quadrantInternalBorderStrokeFill", (Or(Get("quadrantInternalBorderStrokeFill")) ?? Get("primaryBorderColor")));
		Set("quadrantExternalBorderStrokeFill", (Or(Get("quadrantExternalBorderStrokeFill")) ?? Get("primaryBorderColor")));
		Set("quadrantTitleFill", (Or(Get("quadrantTitleFill")) ?? Get("primaryTextColor")));
		Set("xyChart", Obj(("backgroundColor", (Or(Member(Get("xyChart"), "backgroundColor")) ?? Get("background"))), ("titleColor", (Or(Member(Get("xyChart"), "titleColor")) ?? Get("primaryTextColor"))), ("dataLabelColor", (Or(Member(Get("xyChart"), "dataLabelColor")) ?? Get("primaryTextColor"))), ("legendTextColor", (Or(Member(Get("xyChart"), "legendTextColor")) ?? Get("primaryTextColor"))), ("xAxisTitleColor", (Or(Member(Get("xyChart"), "xAxisTitleColor")) ?? Get("primaryTextColor"))), ("xAxisLabelColor", (Or(Member(Get("xyChart"), "xAxisLabelColor")) ?? Get("primaryTextColor"))), ("xAxisTickColor", (Or(Member(Get("xyChart"), "xAxisTickColor")) ?? Get("primaryTextColor"))), ("xAxisLineColor", (Or(Member(Get("xyChart"), "xAxisLineColor")) ?? Get("primaryTextColor"))), ("yAxisTitleColor", (Or(Member(Get("xyChart"), "yAxisTitleColor")) ?? Get("primaryTextColor"))), ("yAxisLabelColor", (Or(Member(Get("xyChart"), "yAxisLabelColor")) ?? Get("primaryTextColor"))), ("yAxisTickColor", (Or(Member(Get("xyChart"), "yAxisTickColor")) ?? Get("primaryTextColor"))), ("yAxisLineColor", (Or(Member(Get("xyChart"), "yAxisLineColor")) ?? Get("primaryTextColor"))), ("plotColorPalette", (Or(Member(Get("xyChart"), "plotColorPalette")) ?? Str("#FFF4DD,#FFD8B1,#FFA07A,#ECEFF1,#D6DBDF,#C3E0A8,#FFB6A4,#FFD74D,#738FA7,#FFFFF0")))));
		Set("requirementBackground", (Or(Get("requirementBackground")) ?? Get("primaryColor")));
		Set("requirementBorderColor", (Or(Get("requirementBorderColor")) ?? Get("primaryBorderColor")));
		Set("requirementBorderSize", (Or(Get("requirementBorderSize")) ?? Str("1")));
		Set("requirementTextColor", (Or(Get("requirementTextColor")) ?? Get("primaryTextColor")));
		Set("relationColor", (Or(Get("relationColor")) ?? Get("lineColor")));
		Set("relationLabelBackground", (Or(Get("relationLabelBackground")) ?? (Truthy(Get("darkMode")) ? Darken(Get("secondaryColor"), 30) : Get("secondaryColor"))));
		Set("relationLabelColor", (Or(Get("relationLabelColor")) ?? Get("actorTextColor")));
		Set("git0", (Or(Get("git0")) ?? Get("primaryColor")));
		Set("git1", (Or(Get("git1")) ?? Get("secondaryColor")));
		Set("git2", (Or(Get("git2")) ?? Get("tertiaryColor")));
		Set("git3", (Or(Get("git3")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: -30))));
		Set("git4", (Or(Get("git4")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: -60))));
		Set("git5", (Or(Get("git5")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: -90))));
		Set("git6", (Or(Get("git6")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 60))));
		Set("git7", (Or(Get("git7")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 120))));
		if (Truthy(Get("darkMode")))
		{
			Set("git0", Lighten(Get("git0"), 25));
			Set("git1", Lighten(Get("git1"), 25));
			Set("git2", Lighten(Get("git2"), 25));
			Set("git3", Lighten(Get("git3"), 25));
			Set("git4", Lighten(Get("git4"), 25));
			Set("git5", Lighten(Get("git5"), 25));
			Set("git6", Lighten(Get("git6"), 25));
			Set("git7", Lighten(Get("git7"), 25));
		}
		else
		{
			Set("git0", Darken(Get("git0"), 25));
			Set("git1", Darken(Get("git1"), 25));
			Set("git2", Darken(Get("git2"), 25));
			Set("git3", Darken(Get("git3"), 25));
			Set("git4", Darken(Get("git4"), 25));
			Set("git5", Darken(Get("git5"), 25));
			Set("git6", Darken(Get("git6"), 25));
			Set("git7", Darken(Get("git7"), 25));
		}
		Set("gitInv0", (Or(Get("gitInv0")) ?? Invert(Get("git0"))));
		Set("gitInv1", (Or(Get("gitInv1")) ?? Invert(Get("git1"))));
		Set("gitInv2", (Or(Get("gitInv2")) ?? Invert(Get("git2"))));
		Set("gitInv3", (Or(Get("gitInv3")) ?? Invert(Get("git3"))));
		Set("gitInv4", (Or(Get("gitInv4")) ?? Invert(Get("git4"))));
		Set("gitInv5", (Or(Get("gitInv5")) ?? Invert(Get("git5"))));
		Set("gitInv6", (Or(Get("gitInv6")) ?? Invert(Get("git6"))));
		Set("gitInv7", (Or(Get("gitInv7")) ?? Invert(Get("git7"))));
		Set("branchLabelColor", (Or(Get("branchLabelColor")) ?? (Truthy(Get("darkMode")) ? Str("black") : Get("labelTextColor"))));
		Set("gitBranchLabel0", (Or(Get("gitBranchLabel0")) ?? Get("branchLabelColor")));
		Set("gitBranchLabel1", (Or(Get("gitBranchLabel1")) ?? Get("branchLabelColor")));
		Set("gitBranchLabel2", (Or(Get("gitBranchLabel2")) ?? Get("branchLabelColor")));
		Set("gitBranchLabel3", (Or(Get("gitBranchLabel3")) ?? Get("branchLabelColor")));
		Set("gitBranchLabel4", (Or(Get("gitBranchLabel4")) ?? Get("branchLabelColor")));
		Set("gitBranchLabel5", (Or(Get("gitBranchLabel5")) ?? Get("branchLabelColor")));
		Set("gitBranchLabel6", (Or(Get("gitBranchLabel6")) ?? Get("branchLabelColor")));
		Set("gitBranchLabel7", (Or(Get("gitBranchLabel7")) ?? Get("branchLabelColor")));
		Set("tagLabelColor", (Or(Get("tagLabelColor")) ?? Get("primaryTextColor")));
		Set("tagLabelBackground", (Or(Get("tagLabelBackground")) ?? Get("primaryColor")));
		Set("tagLabelBorder", (Or(Get("tagBorder")) ?? Get("primaryBorderColor")));
		Set("tagLabelFontSize", (Or(Get("tagLabelFontSize")) ?? Str("10px")));
		Set("commitLabelColor", (Or(Get("commitLabelColor")) ?? Get("secondaryTextColor")));
		Set("commitLabelBackground", (Or(Get("commitLabelBackground")) ?? Get("secondaryColor")));
		Set("commitLabelFontSize", (Or(Get("commitLabelFontSize")) ?? Str("10px")));
		Set("emUiFill", (Or(Get("emUiFill")) ?? Str("white")));
		Set("emUiStroke", (Or(Get("emUiStroke")) ?? Str("#dbdada")));
		Set("emProcessorFill", (Or(Get("emProcessorFill")) ?? Str("#edb3f6")));
		Set("emProcessorStroke", (Or(Get("emProcessorStroke")) ?? Str("#b88cbf")));
		Set("emReadModelFill", (Or(Get("emReadModelFill")) ?? Str("#d3f1a2")));
		Set("emReadModelStroke", (Or(Get("emReadModelStroke")) ?? Str("#a3b732")));
		Set("emCommandFill", (Or(Get("emCommandFill")) ?? Str("#bcd6fe")));
		Set("emCommandStroke", (Or(Get("emCommandStroke")) ?? Str("#679ac3")));
		Set("emEventFill", (Or(Get("emEventFill")) ?? Str("#ffb778")));
		Set("emEventStroke", (Or(Get("emEventStroke")) ?? Str("#c19a0f")));
		Set("emSwimlaneBackgroundOdd", (Or(Get("emSwimlaneBackgroundOdd")) ?? Str("rgb(250,250,250)")));
		Set("emSwimlaneBackgroundStroke", (Or(Get("emSwimlaneBackgroundStroke")) ?? Str("rgb(240,240,240)")));
		Set("emArrowhead", (Or(Get("emArrowhead")) ?? Get("lineColor")));
		Set("emRelationStroke", (Or(Get("emRelationStroke")) ?? Get("lineColor")));
		Set("attributeBackgroundColorOdd", (Or(Get("attributeBackgroundColorOdd")) ?? Str("#ffffff")));
		Set("attributeBackgroundColorEven", (Or(Get("attributeBackgroundColorEven")) ?? Str("#f2f2f2")));
		Set("gradientStart", Get("primaryBorderColor"));
		Set("gradientStop", Get("secondaryBorderColor"));
	}

	/// <summary>An override of <c>nodeBorder</c> turns the gradient off, unless <c>useGradient</c> is overridden too.</summary>
	protected override void AfterCalculate(MermaidValue overrides)
	{
		if (overrides.Kind == MermaidValueKind.Mapping && overrides.Entries.ContainsKey("nodeBorder") && !overrides.Entries.ContainsKey("useGradient"))
		{
			Set("useGradient", MermaidValue.False);
		}
	}
}
