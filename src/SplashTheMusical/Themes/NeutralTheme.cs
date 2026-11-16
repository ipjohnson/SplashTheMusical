namespace SplashTheMusical.Themes;

/// <summary>mermaid's <c>packages/mermaid/src/themes/theme-neutral.js</c>.</summary>
internal sealed class NeutralTheme : Theme
{
	public override string Name => "neutral";

	protected override void Construct()
	{
		Set("primaryColor", Str("#eee"));
		Set("contrast", Str("#707070"));
		Set("secondaryColor", Lighten(Get("contrast"), 55));
		Set("background", Str("#ffffff"));
		Set("tertiaryColor", Adjust(Get("primaryColor"), new KhromaChannels(H: -160)));
		Set("primaryBorderColor", MkBorder(Get("primaryColor"), Truthy(Get("darkMode"))));
		Set("secondaryBorderColor", MkBorder(Get("secondaryColor"), Truthy(Get("darkMode"))));
		Set("tertiaryBorderColor", MkBorder(Get("tertiaryColor"), Truthy(Get("darkMode"))));
		Set("primaryTextColor", Invert(Get("primaryColor")));
		Set("secondaryTextColor", Invert(Get("secondaryColor")));
		Set("tertiaryTextColor", Invert(Get("tertiaryColor")));
		Set("lineColor", Invert(Get("background")));
		Set("textColor", Invert(Get("background")));
		Set("mainBkg", Str("#eee"));
		Set("secondBkg", Str("calculated"));
		Set("lineColor", Str("#666"));
		Set("border1", Str("#999"));
		Set("border2", Str("calculated"));
		Set("note", Str("#ffa"));
		Set("text", Str("#333"));
		Set("critical", Str("#d42"));
		Set("done", Str("#bbb"));
		Set("arrowheadColor", Str("#333333"));
		Set("fontFamily", Str("\"trebuchet ms\", verdana, arial, sans-serif"));
		Set("fontSize", Str("16px"));
		Set("THEME_COLOR_LIMIT", Num(12));
		Set("radius", Num(5));
		Set("strokeWidth", Num(1));
		Set("nodeBkg", Str("calculated"));
		Set("nodeBorder", Str("calculated"));
		Set("clusterBkg", Str("calculated"));
		Set("clusterBorder", Str("calculated"));
		Set("defaultLinkColor", Str("calculated"));
		Set("titleColor", Str("calculated"));
		Set("edgeLabelBackground", Str("white"));
		Set("actorBorder", Str("calculated"));
		Set("actorBkg", Str("calculated"));
		Set("actorTextColor", Str("calculated"));
		Set("actorLineColor", Get("actorBorder"));
		Set("signalColor", Str("calculated"));
		Set("signalTextColor", Str("calculated"));
		Set("labelBoxBkgColor", Str("calculated"));
		Set("labelBoxBorderColor", Str("calculated"));
		Set("labelTextColor", Str("calculated"));
		Set("loopTextColor", Str("calculated"));
		Set("noteBorderColor", Str("calculated"));
		Set("noteBkgColor", Str("calculated"));
		Set("noteTextColor", Str("calculated"));
		Set("activationBorderColor", Str("#666"));
		Set("activationBkgColor", Str("#f4f4f4"));
		Set("sequenceNumberColor", Str("white"));
		Set("sectionBkgColor", Str("calculated"));
		Set("altSectionBkgColor", Str("white"));
		Set("sectionBkgColor2", Str("calculated"));
		Set("excludeBkgColor", Str("#eeeeee"));
		Set("taskBorderColor", Str("calculated"));
		Set("taskBkgColor", Str("calculated"));
		Set("taskTextLightColor", Str("white"));
		Set("taskTextColor", Str("calculated"));
		Set("taskTextDarkColor", Str("calculated"));
		Set("taskTextOutsideColor", Str("calculated"));
		Set("taskTextClickableColor", Str("#003163"));
		Set("activeTaskBorderColor", Str("calculated"));
		Set("activeTaskBkgColor", Str("calculated"));
		Set("gridColor", Str("calculated"));
		Set("doneTaskBkgColor", Str("calculated"));
		Set("doneTaskBorderColor", Str("calculated"));
		Set("critBkgColor", Str("calculated"));
		Set("critBorderColor", Str("calculated"));
		Set("todayLineColor", Str("calculated"));
		Set("vertLineColor", Str("calculated"));
		Set("personBorder", Get("primaryBorderColor"));
		Set("personBkg", Get("mainBkg"));
		Set("archEdgeColor", Str("calculated"));
		Set("archEdgeArrowColor", Str("calculated"));
		Set("archEdgeWidth", Str("3"));
		Set("archGroupBorderColor", Get("primaryBorderColor"));
		Set("archGroupBorderWidth", Str("2px"));
		Set("noteFontWeight", Str("normal"));
		Set("fontWeight", Str("normal"));
		Set("rowOdd", (Or((Or(Get("rowOdd")) ?? Lighten(Get("mainBkg"), 75))) ?? Str("#ffffff")));
		Set("rowEven", (Or(Get("rowEven")) ?? Str("#f4f4f4")));
		Set("labelColor", Str("black"));
		Set("errorBkgColor", Str("#552222"));
		Set("errorTextColor", Str("#552222"));
		Set("useGradient", MermaidValue.True);
		Set("gradientStart", Get("primaryBorderColor"));
		Set("gradientStop", Get("secondaryBorderColor"));
		Set("dropShadow", Str("drop-shadow( 1px 2px 2px rgba(185,185,185,1))"));
	}

	protected override void UpdateColors()
	{
		Set("secondBkg", Lighten(Get("contrast"), 55));
		Set("border2", Get("contrast"));
		Set("actorBorder", Lighten(Get("border1"), 23));
		Set("actorBkg", Get("mainBkg"));
		Set("actorTextColor", Get("text"));
		Set("actorLineColor", Get("actorBorder"));
		Set("rectBkgColor", (Or(Get("rectBkgColor")) ?? Get("tertiaryColor")));
		Set("signalColor", Get("text"));
		Set("signalTextColor", Get("text"));
		Set("labelBoxBkgColor", Get("actorBkg"));
		Set("labelBoxBorderColor", Get("actorBorder"));
		Set("labelTextColor", Get("text"));
		Set("loopTextColor", Get("text"));
		Set("noteBorderColor", Str("#999"));
		Set("noteBkgColor", Str("#666"));
		Set("noteTextColor", Str("#fff"));
		Set("cScale0", (Or(Get("cScale0")) ?? Str("#555")));
		Set("cScale1", (Or(Get("cScale1")) ?? Str("#F4F4F4")));
		Set("cScale2", (Or(Get("cScale2")) ?? Str("#555")));
		Set("cScale3", (Or(Get("cScale3")) ?? Str("#BBB")));
		Set("cScale4", (Or(Get("cScale4")) ?? Str("#777")));
		Set("cScale5", (Or(Get("cScale5")) ?? Str("#999")));
		Set("cScale6", (Or(Get("cScale6")) ?? Str("#DDD")));
		Set("cScale7", (Or(Get("cScale7")) ?? Str("#FFF")));
		Set("cScale8", (Or(Get("cScale8")) ?? Str("#DDD")));
		Set("cScale9", (Or(Get("cScale9")) ?? Str("#BBB")));
		Set("cScale10", (Or(Get("cScale10")) ?? Str("#999")));
		Set("cScale11", (Or(Get("cScale11")) ?? Str("#777")));
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
		Set("scaleLabelColor", (Or(Get("scaleLabelColor")) ?? (Truthy(Get("darkMode")) ? Str("black") : Get("labelTextColor"))));
		Set("cScaleLabel0", (Or(Get("cScaleLabel0")) ?? Get("cScale1")));
		Set("cScaleLabel2", (Or(Get("cScaleLabel2")) ?? Get("cScale1")));
		for (var i = 0; i < ToNumber(Get("THEME_COLOR_LIMIT")); i++)
		{
			Set(Key("cScaleLabel", i), (Or(Get(Key("cScaleLabel", i))) ?? Get("scaleLabelColor")));
		}
		for (var i = 0; i < 5; i++)
		{
			Set(Key("surface", i), (Or(Get(Key("surface", i))) ?? Adjust(Get("mainBkg"), new KhromaChannels(L: -(5 + (i * 5))))));
			Set(Key("surfacePeer", i), (Or(Get(Key("surfacePeer", i))) ?? Adjust(Get("mainBkg"), new KhromaChannels(L: -(8 + (i * 5))))));
		}
		Set("nodeBkg", Get("mainBkg"));
		Set("nodeBorder", Get("border1"));
		Set("clusterBkg", Get("secondBkg"));
		Set("clusterBorder", Get("border2"));
		Set("defaultLinkColor", Get("lineColor"));
		Set("titleColor", Get("text"));
		Set("flowContainerStroke", (Or(Get("flowContainerStroke")) ?? Get("secondaryBorderColor")));
		Set("sectionBkgColor", Lighten(Get("contrast"), 30));
		Set("sectionBkgColor2", Lighten(Get("contrast"), 30));
		Set("taskBorderColor", Darken(Get("contrast"), 10));
		Set("taskBkgColor", Get("contrast"));
		Set("taskTextColor", Get("taskTextLightColor"));
		Set("taskTextDarkColor", Get("text"));
		Set("taskTextOutsideColor", Get("taskTextDarkColor"));
		Set("activeTaskBorderColor", Get("taskBorderColor"));
		Set("activeTaskBkgColor", Get("mainBkg"));
		Set("gridColor", Lighten(Get("border1"), 30));
		Set("doneTaskBkgColor", Get("done"));
		Set("doneTaskBorderColor", Get("lineColor"));
		Set("critBkgColor", Get("critical"));
		Set("critBorderColor", Darken(Get("critBkgColor"), 10));
		Set("todayLineColor", Get("critBkgColor"));
		Set("vertLineColor", Get("critBkgColor"));
		Set("archEdgeColor", Get("lineColor"));
		Set("archEdgeArrowColor", Get("lineColor"));
		Set("transitionColor", (Or(Get("transitionColor")) ?? Str("#000")));
		Set("transitionLabelColor", (Or(Get("transitionLabelColor")) ?? Get("textColor")));
		Set("stateLabelColor", (Or((Or(Get("stateLabelColor")) ?? Get("stateBkg"))) ?? Get("primaryTextColor")));
		Set("stateBkg", (Or(Get("stateBkg")) ?? Get("mainBkg")));
		Set("labelBackgroundColor", (Or(Get("labelBackgroundColor")) ?? Get("stateBkg")));
		Set("compositeBackground", (Or((Or(Get("compositeBackground")) ?? Get("background"))) ?? Get("tertiaryColor")));
		Set("altBackground", (Or(Get("altBackground")) ?? Str("#f4f4f4")));
		Set("compositeTitleBackground", (Or(Get("compositeTitleBackground")) ?? Get("mainBkg")));
		Set("stateBorder", (Or(Get("stateBorder")) ?? Str("#000")));
		Set("innerEndBackground", Get("primaryBorderColor"));
		Set("specialStateColor", Str("#222"));
		Set("errorBkgColor", (Or(Get("errorBkgColor")) ?? Get("tertiaryColor")));
		Set("errorTextColor", (Or(Get("errorTextColor")) ?? Get("tertiaryTextColor")));
		Set("classText", Get("primaryTextColor"));
		Set("fillType0", Get("primaryColor"));
		Set("fillType1", Get("secondaryColor"));
		Set("fillType2", Adjust(Get("primaryColor"), new KhromaChannels(H: 64)));
		Set("fillType3", Adjust(Get("secondaryColor"), new KhromaChannels(H: 64)));
		Set("fillType4", Adjust(Get("primaryColor"), new KhromaChannels(H: -64)));
		Set("fillType5", Adjust(Get("secondaryColor"), new KhromaChannels(H: -64)));
		Set("fillType6", Adjust(Get("primaryColor"), new KhromaChannels(H: 128)));
		Set("fillType7", Adjust(Get("secondaryColor"), new KhromaChannels(H: 128)));
		for (var i = 0; i < ToNumber(Get("THEME_COLOR_LIMIT")); i++)
		{
			Set(Key("pie", i), Get(Key("cScale", i)));
		}
		Set("pie12", Get("pie0"));
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
		for (var i = 0; i < 8; i++)
		{
			Set(Key("venn", (i + 1)), (Nullish(Get(Key("venn", (i + 1)))) ?? Get(Key("cScale", i))));
		}
		Set("vennTitleTextColor", (Nullish(Get("vennTitleTextColor")) ?? Get("titleColor")));
		Set("vennSetTextColor", (Nullish(Get("vennSetTextColor")) ?? Get("textColor")));
		Set("cynefin", Obj(("domainFontSize", (Or(Member(Get("cynefin"), "domainFontSize")) ?? Num(16))), ("itemFontSize", (Or(Member(Get("cynefin"), "itemFontSize")) ?? Num(12))), ("boundaryColor", (Or(Member(Get("cynefin"), "boundaryColor")) ?? Get("lineColor"))), ("boundaryWidth", (Or(Member(Get("cynefin"), "boundaryWidth")) ?? Num(2))), ("cliffColor", (Or(Member(Get("cynefin"), "cliffColor")) ?? Str("#8B0000"))), ("cliffWidth", (Or(Member(Get("cynefin"), "cliffWidth")) ?? Num(4))), ("arrowColor", (Or(Member(Get("cynefin"), "arrowColor")) ?? Get("lineColor"))), ("arrowWidth", (Or(Member(Get("cynefin"), "arrowWidth")) ?? Num(2))), ("complexBg", (Or(Member(Get("cynefin"), "complexBg")) ?? Str("#E8F5E9"))), ("complicatedBg", (Or(Member(Get("cynefin"), "complicatedBg")) ?? Str("#E3F2FD"))), ("chaoticBg", (Or(Member(Get("cynefin"), "chaoticBg")) ?? Str("#FBE9E7"))), ("clearBg", (Or(Member(Get("cynefin"), "clearBg")) ?? Str("#FFF8E1"))), ("confusionBg", (Or(Member(Get("cynefin"), "confusionBg")) ?? Str("#F3E5F5"))), ("textColor", (Or(Member(Get("cynefin"), "textColor")) ?? Get("textColor"))), ("labelColor", (Or(Member(Get("cynefin"), "labelColor")) ?? Get("primaryTextColor")))));
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
		Set("xyChart", Obj(("backgroundColor", (Or(Member(Get("xyChart"), "backgroundColor")) ?? Get("background"))), ("titleColor", (Or(Member(Get("xyChart"), "titleColor")) ?? Get("primaryTextColor"))), ("dataLabelColor", (Or(Member(Get("xyChart"), "dataLabelColor")) ?? Get("primaryTextColor"))), ("legendTextColor", (Or(Member(Get("xyChart"), "legendTextColor")) ?? Get("primaryTextColor"))), ("xAxisTitleColor", (Or(Member(Get("xyChart"), "xAxisTitleColor")) ?? Get("primaryTextColor"))), ("xAxisLabelColor", (Or(Member(Get("xyChart"), "xAxisLabelColor")) ?? Get("primaryTextColor"))), ("xAxisTickColor", (Or(Member(Get("xyChart"), "xAxisTickColor")) ?? Get("primaryTextColor"))), ("xAxisLineColor", (Or(Member(Get("xyChart"), "xAxisLineColor")) ?? Get("primaryTextColor"))), ("yAxisTitleColor", (Or(Member(Get("xyChart"), "yAxisTitleColor")) ?? Get("primaryTextColor"))), ("yAxisLabelColor", (Or(Member(Get("xyChart"), "yAxisLabelColor")) ?? Get("primaryTextColor"))), ("yAxisTickColor", (Or(Member(Get("xyChart"), "yAxisTickColor")) ?? Get("primaryTextColor"))), ("yAxisLineColor", (Or(Member(Get("xyChart"), "yAxisLineColor")) ?? Get("primaryTextColor"))), ("plotColorPalette", (Or(Member(Get("xyChart"), "plotColorPalette")) ?? Str("#EEE,#6BB8E4,#8ACB88,#C7ACD6,#E8DCC2,#FFB2A8,#FFF380,#7E8D91,#FFD8B1,#FAF3E0")))));
		Set("radar", Obj(("axisColor", (Or(Member(Get("radar"), "axisColor")) ?? Get("lineColor"))), ("axisStrokeWidth", (Or(Member(Get("radar"), "axisStrokeWidth")) ?? Num(2))), ("axisLabelFontSize", (Or(Member(Get("radar"), "axisLabelFontSize")) ?? Num(12))), ("curveOpacity", (Or(Member(Get("radar"), "curveOpacity")) ?? Num(0.5))), ("curveStrokeWidth", (Or(Member(Get("radar"), "curveStrokeWidth")) ?? Num(2))), ("graticuleColor", (Or(Member(Get("radar"), "graticuleColor")) ?? Str("#DEDEDE"))), ("graticuleStrokeWidth", (Or(Member(Get("radar"), "graticuleStrokeWidth")) ?? Num(1))), ("graticuleOpacity", (Or(Member(Get("radar"), "graticuleOpacity")) ?? Num(0.3))), ("legendBoxSize", (Or(Member(Get("radar"), "legendBoxSize")) ?? Num(12))), ("legendFontSize", (Or(Member(Get("radar"), "legendFontSize")) ?? Num(12)))));
		Set("wardleyEvolutionColor", (Or(Get("wardleyEvolutionColor")) ?? Str("#dc3545")));
		Set("wardley", Obj(("backgroundColor", (Or(Member(Get("wardley"), "backgroundColor")) ?? Get("background"))), ("axisColor", (Or(Member(Get("wardley"), "axisColor")) ?? Get("lineColor"))), ("axisTextColor", (Or(Member(Get("wardley"), "axisTextColor")) ?? Get("primaryTextColor"))), ("gridColor", (Or(Member(Get("wardley"), "gridColor")) ?? Get("gridColor"))), ("componentFill", (Or(Member(Get("wardley"), "componentFill")) ?? Get("background"))), ("componentStroke", (Or(Member(Get("wardley"), "componentStroke")) ?? Get("lineColor"))), ("componentLabelColor", (Or(Member(Get("wardley"), "componentLabelColor")) ?? Get("primaryTextColor"))), ("linkStroke", (Or(Member(Get("wardley"), "linkStroke")) ?? Get("lineColor"))), ("evolutionStroke", (Or(Member(Get("wardley"), "evolutionStroke")) ?? Get("wardleyEvolutionColor"))), ("annotationStroke", (Or(Member(Get("wardley"), "annotationStroke")) ?? Get("lineColor"))), ("annotationTextColor", (Or(Member(Get("wardley"), "annotationTextColor")) ?? Get("primaryTextColor"))), ("annotationFill", (Or(Member(Get("wardley"), "annotationFill")) ?? Get("background")))));
		Set("requirementBackground", (Or(Get("requirementBackground")) ?? Get("primaryColor")));
		Set("requirementBorderColor", (Or(Get("requirementBorderColor")) ?? Get("primaryBorderColor")));
		Set("requirementBorderSize", (Or(Get("requirementBorderSize")) ?? Str("1")));
		Set("requirementTextColor", (Or(Get("requirementTextColor")) ?? Get("primaryTextColor")));
		Set("relationColor", (Or(Get("relationColor")) ?? Get("lineColor")));
		Set("relationLabelBackground", (Or(Get("relationLabelBackground")) ?? Get("edgeLabelBackground")));
		Set("relationLabelColor", (Or(Get("relationLabelColor")) ?? Get("actorTextColor")));
		Set("git0", (Or(Darken(Get("pie1"), 25)) ?? Get("primaryColor")));
		Set("git1", (Or(Get("pie2")) ?? Get("secondaryColor")));
		Set("git2", (Or(Get("pie3")) ?? Get("tertiaryColor")));
		Set("git3", (Or(Get("pie4")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: -30))));
		Set("git4", (Or(Get("pie5")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: -60))));
		Set("git5", (Or(Get("pie6")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: -90))));
		Set("git6", (Or(Get("pie7")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 60))));
		Set("git7", (Or(Get("pie8")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 120))));
		Set("gitInv0", (Or(Get("gitInv0")) ?? Invert(Get("git0"))));
		Set("gitInv1", (Or(Get("gitInv1")) ?? Invert(Get("git1"))));
		Set("gitInv2", (Or(Get("gitInv2")) ?? Invert(Get("git2"))));
		Set("gitInv3", (Or(Get("gitInv3")) ?? Invert(Get("git3"))));
		Set("gitInv4", (Or(Get("gitInv4")) ?? Invert(Get("git4"))));
		Set("gitInv5", (Or(Get("gitInv5")) ?? Invert(Get("git5"))));
		Set("gitInv6", (Or(Get("gitInv6")) ?? Invert(Get("git6"))));
		Set("gitInv7", (Or(Get("gitInv7")) ?? Invert(Get("git7"))));
		Set("branchLabelColor", (Or(Get("branchLabelColor")) ?? Get("labelTextColor")));
		Set("gitBranchLabel0", Get("branchLabelColor"));
		Set("gitBranchLabel1", Str("white"));
		Set("gitBranchLabel2", Get("branchLabelColor"));
		Set("gitBranchLabel3", Str("white"));
		Set("gitBranchLabel4", Get("branchLabelColor"));
		Set("gitBranchLabel5", Get("branchLabelColor"));
		Set("gitBranchLabel6", Get("branchLabelColor"));
		Set("gitBranchLabel7", Get("branchLabelColor"));
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
	}
}
