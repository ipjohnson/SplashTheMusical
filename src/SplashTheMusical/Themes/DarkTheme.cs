namespace SplashTheMusical.Themes;

/// <summary>mermaid's <c>packages/mermaid/src/themes/theme-dark.js</c>.</summary>
internal sealed class DarkTheme : Theme
{
	public override string Name => "dark";

	protected override void Construct()
	{
		Set("background", Str("#333"));
		Set("primaryColor", Str("#1f2020"));
		Set("secondaryColor", Lighten(Get("primaryColor"), 16));
		Set("tertiaryColor", Adjust(Get("primaryColor"), new KhromaChannels(H: -160)));
		Set("primaryBorderColor", Invert(Get("background")));
		Set("secondaryBorderColor", MkBorder(Get("secondaryColor"), Truthy(Get("darkMode"))));
		Set("tertiaryBorderColor", MkBorder(Get("tertiaryColor"), Truthy(Get("darkMode"))));
		Set("primaryTextColor", Invert(Get("primaryColor")));
		Set("secondaryTextColor", Invert(Get("secondaryColor")));
		Set("tertiaryTextColor", Invert(Get("tertiaryColor")));
		Set("lineColor", Invert(Get("background")));
		Set("textColor", Invert(Get("background")));
		Set("mainBkg", Str("#1f2020"));
		Set("secondBkg", Str("calculated"));
		Set("mainContrastColor", Str("lightgrey"));
		Set("darkTextColor", Lighten(Invert(Str("#323D47")), 10));
		Set("lineColor", Str("calculated"));
		Set("border1", Str("#ccc"));
		Set("border2", Rgba(255, 255, 255, 0.25));
		Set("arrowheadColor", Str("calculated"));
		Set("fontFamily", Str("\"trebuchet ms\", verdana, arial, sans-serif"));
		Set("fontSize", Str("16px"));
		Set("labelBackground", Str("#181818"));
		Set("textColor", Str("#ccc"));
		Set("THEME_COLOR_LIMIT", Num(12));
		Set("radius", Num(5));
		Set("strokeWidth", Num(1));
		Set("nodeBkg", Str("calculated"));
		Set("nodeBorder", Str("calculated"));
		Set("clusterBkg", Str("calculated"));
		Set("clusterBorder", Str("calculated"));
		Set("defaultLinkColor", Str("calculated"));
		Set("titleColor", Str("#F9FFFE"));
		Set("edgeLabelBackground", Str("calculated"));
		Set("actorBorder", Str("calculated"));
		Set("actorBkg", Str("calculated"));
		Set("actorTextColor", Str("calculated"));
		Set("actorLineColor", Str("calculated"));
		Set("signalColor", Str("calculated"));
		Set("signalTextColor", Str("calculated"));
		Set("labelBoxBkgColor", Str("calculated"));
		Set("labelBoxBorderColor", Str("calculated"));
		Set("labelTextColor", Str("calculated"));
		Set("loopTextColor", Str("calculated"));
		Set("noteBorderColor", Str("calculated"));
		Set("noteBkgColor", Str("#fff5ad"));
		Set("noteTextColor", Str("calculated"));
		Set("activationBorderColor", Str("calculated"));
		Set("activationBkgColor", Str("calculated"));
		Set("sequenceNumberColor", Str("black"));
		Set("clusterBkg", Str("#302F3D"));
		Set("sectionBkgColor", Darken(Str("#EAE8D9"), 30));
		Set("altSectionBkgColor", Str("calculated"));
		Set("sectionBkgColor2", Str("#EAE8D9"));
		Set("excludeBkgColor", Darken(Get("sectionBkgColor"), 10));
		Set("taskBorderColor", Rgba(255, 255, 255, 70));
		Set("taskBkgColor", Str("calculated"));
		Set("taskTextColor", Str("calculated"));
		Set("taskTextLightColor", Str("calculated"));
		Set("taskTextOutsideColor", Str("calculated"));
		Set("taskTextClickableColor", Str("#003163"));
		Set("activeTaskBorderColor", Rgba(255, 255, 255, 50));
		Set("activeTaskBkgColor", Str("#81B1DB"));
		Set("gridColor", Str("calculated"));
		Set("doneTaskBkgColor", Str("calculated"));
		Set("doneTaskBorderColor", Str("grey"));
		Set("critBorderColor", Str("#E83737"));
		Set("critBkgColor", Str("#E83737"));
		Set("taskTextDarkColor", Str("calculated"));
		Set("todayLineColor", Str("#DB5757"));
		Set("vertLineColor", Str("#00BFFF"));
		Set("personBorder", Get("primaryBorderColor"));
		Set("personBkg", Get("mainBkg"));
		Set("archEdgeColor", Str("calculated"));
		Set("archEdgeArrowColor", Str("calculated"));
		Set("archEdgeWidth", Str("3"));
		Set("archGroupBorderColor", Get("primaryBorderColor"));
		Set("archGroupBorderWidth", Str("2px"));
		Set("rowOdd", (Or((Or(Get("rowOdd")) ?? Lighten(Get("mainBkg"), 5))) ?? Str("#ffffff")));
		Set("rowEven", (Or(Get("rowEven")) ?? Darken(Get("mainBkg"), 10)));
		Set("labelColor", Str("calculated"));
		Set("errorBkgColor", Str("#a44141"));
		Set("errorTextColor", Str("#ddd"));
		Set("useGradient", MermaidValue.True);
		Set("gradientStart", Get("primaryBorderColor"));
		Set("gradientStop", Get("secondaryBorderColor"));
		Set("dropShadow", Str("drop-shadow( 1px 2px 2px rgba(185,185,185,1))"));
		Set("noteFontWeight", (Or(Get("noteFontWeight")) ?? Str("normal")));
		Set("fontWeight", (Or(Get("fontWeight")) ?? Str("normal")));
	}

	protected override void UpdateColors()
	{
		Set("secondBkg", Lighten(Get("mainBkg"), 16));
		Set("lineColor", Get("mainContrastColor"));
		Set("arrowheadColor", Get("mainContrastColor"));
		Set("nodeBkg", Get("mainBkg"));
		Set("nodeBorder", Get("border1"));
		Set("clusterBkg", Get("secondBkg"));
		Set("clusterBorder", Get("border2"));
		Set("defaultLinkColor", Get("lineColor"));
		Set("edgeLabelBackground", Lighten(Get("labelBackground"), 25));
		Set("flowContainerStroke", (Or(Get("flowContainerStroke")) ?? Get("secondaryBorderColor")));
		Set("actorBorder", Get("border1"));
		Set("actorBkg", Get("mainBkg"));
		Set("actorTextColor", Get("mainContrastColor"));
		Set("actorLineColor", Get("actorBorder"));
		Set("signalColor", Get("mainContrastColor"));
		Set("signalTextColor", Get("mainContrastColor"));
		Set("labelBoxBkgColor", Get("actorBkg"));
		Set("labelBoxBorderColor", Get("actorBorder"));
		Set("labelTextColor", Get("mainContrastColor"));
		Set("loopTextColor", Get("mainContrastColor"));
		Set("noteBorderColor", Get("secondaryBorderColor"));
		Set("noteBkgColor", Get("secondBkg"));
		Set("noteTextColor", Get("secondaryTextColor"));
		Set("activationBorderColor", Get("border1"));
		Set("activationBkgColor", Get("secondBkg"));
		Set("rectBkgColor", (Or(Get("rectBkgColor")) ?? Get("tertiaryColor")));
		Set("altSectionBkgColor", Get("background"));
		Set("taskBkgColor", Lighten(Get("mainBkg"), 23));
		Set("taskTextColor", Get("darkTextColor"));
		Set("taskTextLightColor", Get("mainContrastColor"));
		Set("taskTextOutsideColor", Get("taskTextLightColor"));
		Set("gridColor", Get("mainContrastColor"));
		Set("doneTaskBkgColor", Get("mainContrastColor"));
		Set("taskTextDarkColor", Invert(Get("doneTaskBkgColor")));
		Set("archEdgeColor", Get("lineColor"));
		Set("archEdgeArrowColor", Get("lineColor"));
		Set("transitionColor", (Or(Get("transitionColor")) ?? Get("lineColor")));
		Set("transitionLabelColor", (Or(Get("transitionLabelColor")) ?? Get("textColor")));
		Set("stateLabelColor", (Or((Or(Get("stateLabelColor")) ?? Get("stateBkg"))) ?? Get("primaryTextColor")));
		Set("stateBkg", (Or(Get("stateBkg")) ?? Get("mainBkg")));
		Set("labelBackgroundColor", (Or(Get("labelBackgroundColor")) ?? Get("stateBkg")));
		Set("compositeBackground", (Or((Or(Get("compositeBackground")) ?? Get("background"))) ?? Get("tertiaryColor")));
		Set("altBackground", (Or(Get("altBackground")) ?? Str("#555")));
		Set("compositeTitleBackground", (Or(Get("compositeTitleBackground")) ?? Get("mainBkg")));
		Set("compositeBorder", (Or(Get("compositeBorder")) ?? Get("nodeBorder")));
		Set("innerEndBackground", Get("primaryBorderColor"));
		Set("specialStateColor", Str("#f4f4f4"));
		Set("errorBkgColor", (Or(Get("errorBkgColor")) ?? Get("tertiaryColor")));
		Set("errorTextColor", (Or(Get("errorTextColor")) ?? Get("tertiaryTextColor")));
		Set("fillType0", Get("primaryColor"));
		Set("fillType1", Get("secondaryColor"));
		Set("fillType2", Adjust(Get("primaryColor"), new KhromaChannels(H: 64)));
		Set("fillType3", Adjust(Get("secondaryColor"), new KhromaChannels(H: 64)));
		Set("fillType4", Adjust(Get("primaryColor"), new KhromaChannels(H: -64)));
		Set("fillType5", Adjust(Get("secondaryColor"), new KhromaChannels(H: -64)));
		Set("fillType6", Adjust(Get("primaryColor"), new KhromaChannels(H: 128)));
		Set("fillType7", Adjust(Get("secondaryColor"), new KhromaChannels(H: 128)));
		Set("cScale1", (Or(Get("cScale1")) ?? Str("#0b0000")));
		Set("cScale2", (Or(Get("cScale2")) ?? Str("#4d1037")));
		Set("cScale3", (Or(Get("cScale3")) ?? Str("#3f5258")));
		Set("cScale4", (Or(Get("cScale4")) ?? Str("#4f2f1b")));
		Set("cScale5", (Or(Get("cScale5")) ?? Str("#6e0a0a")));
		Set("cScale6", (Or(Get("cScale6")) ?? Str("#3b0048")));
		Set("cScale7", (Or(Get("cScale7")) ?? Str("#995a01")));
		Set("cScale8", (Or(Get("cScale8")) ?? Str("#154706")));
		Set("cScale9", (Or(Get("cScale9")) ?? Str("#161722")));
		Set("cScale10", (Or(Get("cScale10")) ?? Str("#00296f")));
		Set("cScale11", (Or(Get("cScale11")) ?? Str("#01629c")));
		Set("cScale12", (Or(Get("cScale12")) ?? Str("#010029")));
		Set("cScale0", (Or(Get("cScale0")) ?? Get("primaryColor")));
		Set("cScale1", (Or(Get("cScale1")) ?? Get("secondaryColor")));
		Set("cScale2", (Or(Get("cScale2")) ?? Get("tertiaryColor")));
		Set("cScale3", (Or(Get("cScale3")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 30))));
		Set("cScale4", (Or(Get("cScale4")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 60))));
		Set("cScale5", (Or(Get("cScale5")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 90))));
		Set("cScale6", (Or(Get("cScale6")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 120))));
		Set("cScale7", (Or(Get("cScale7")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 150))));
		Set("cScale8", (Or(Get("cScale8")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 210))));
		Set("cScale9", (Or(Get("cScale9")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 270))));
		Set("cScale10", (Or(Get("cScale10")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 300))));
		Set("cScale11", (Or(Get("cScale11")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 330))));
		for (var i = 0; i < ToNumber(Get("THEME_COLOR_LIMIT")); i++)
		{
			Set(Key("cScaleInv", i), (Or(Get(Key("cScaleInv", i))) ?? Invert(Get(Key("cScale", i)))));
		}
		for (var i = 0; i < ToNumber(Get("THEME_COLOR_LIMIT")); i++)
		{
			Set(Key("cScalePeer", i), (Or(Get(Key("cScalePeer", i))) ?? Lighten(Get(Key("cScale", i)), 10)));
		}
		for (var i = 0; i < 5; i++)
		{
			Set(Key("surface", i), (Or(Get(Key("surface", i))) ?? Adjust(Get("mainBkg"), new KhromaChannels(H: 30, S: -30, L: -(-10 + (i * 4))))));
			Set(Key("surfacePeer", i), (Or(Get(Key("surfacePeer", i))) ?? Adjust(Get("mainBkg"), new KhromaChannels(H: 30, S: -30, L: -(-7 + (i * 4))))));
		}
		Set("scaleLabelColor", (Or(Get("scaleLabelColor")) ?? (Truthy(Get("darkMode")) ? Str("black") : Get("labelTextColor"))));
		for (var i = 0; i < ToNumber(Get("THEME_COLOR_LIMIT")); i++)
		{
			Set(Key("cScaleLabel", i), (Or(Get(Key("cScaleLabel", i))) ?? Get("scaleLabelColor")));
		}
		for (var i = 0; i < ToNumber(Get("THEME_COLOR_LIMIT")); i++)
		{
			Set(Key("pie", i), Get(Key("cScale", i)));
		}
		Set("pieTitleTextSize", (Or(Get("pieTitleTextSize")) ?? Str("25px")));
		Set("pieTitleTextColor", (Or(Get("pieTitleTextColor")) ?? Get("mainContrastColor")));
		Set("pieSectionTextSize", (Or(Get("pieSectionTextSize")) ?? Str("17px")));
		Set("pieSectionTextColor", (Or(Get("pieSectionTextColor")) ?? Get("textColor")));
		Set("pieLegendTextSize", (Or(Get("pieLegendTextSize")) ?? Str("17px")));
		Set("pieLegendTextColor", (Or(Get("pieLegendTextColor")) ?? Get("mainContrastColor")));
		Set("pieStrokeColor", (Or(Get("pieStrokeColor")) ?? Str("black")));
		Set("pieStrokeWidth", (Or(Get("pieStrokeWidth")) ?? Str("2px")));
		Set("pieOuterStrokeWidth", (Or(Get("pieOuterStrokeWidth")) ?? Str("2px")));
		Set("pieOuterStrokeColor", (Or(Get("pieOuterStrokeColor")) ?? Str("black")));
		Set("pieOpacity", (Or(Get("pieOpacity")) ?? Str("0.7")));
		for (var i = 0; i < 8; i++)
		{
			Set(Key("venn", (i + 1)), (Nullish(Get(Key("venn", (i + 1)))) ?? Lighten(Get(Key("cScale", i)), 30)));
		}
		Set("vennTitleTextColor", (Nullish(Get("vennTitleTextColor")) ?? Get("titleColor")));
		Set("vennSetTextColor", (Nullish(Get("vennSetTextColor")) ?? Get("textColor")));
		Set("cynefin", Obj(("domainFontSize", (Or(Member(Get("cynefin"), "domainFontSize")) ?? Num(16))), ("itemFontSize", (Or(Member(Get("cynefin"), "itemFontSize")) ?? Num(12))), ("boundaryColor", (Or(Member(Get("cynefin"), "boundaryColor")) ?? Get("lineColor"))), ("boundaryWidth", (Or(Member(Get("cynefin"), "boundaryWidth")) ?? Num(2))), ("cliffColor", (Or(Member(Get("cynefin"), "cliffColor")) ?? Str("#FF6B6B"))), ("cliffWidth", (Or(Member(Get("cynefin"), "cliffWidth")) ?? Num(4))), ("arrowColor", (Or(Member(Get("cynefin"), "arrowColor")) ?? Get("lineColor"))), ("arrowWidth", (Or(Member(Get("cynefin"), "arrowWidth")) ?? Num(2))), ("complexBg", (Or(Member(Get("cynefin"), "complexBg")) ?? Str("#1B5E20"))), ("complicatedBg", (Or(Member(Get("cynefin"), "complicatedBg")) ?? Str("#0D47A1"))), ("chaoticBg", (Or(Member(Get("cynefin"), "chaoticBg")) ?? Str("#BF360C"))), ("clearBg", (Or(Member(Get("cynefin"), "clearBg")) ?? Str("#F57F17"))), ("confusionBg", (Or(Member(Get("cynefin"), "confusionBg")) ?? Str("#4A148C"))), ("textColor", (Or(Member(Get("cynefin"), "textColor")) ?? Get("textColor"))), ("labelColor", (Or(Member(Get("cynefin"), "labelColor")) ?? Get("primaryTextColor")))));
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
		Set("xyChart", Obj(("backgroundColor", (Or(Member(Get("xyChart"), "backgroundColor")) ?? Get("background"))), ("titleColor", (Or(Member(Get("xyChart"), "titleColor")) ?? Get("primaryTextColor"))), ("dataLabelColor", (Or(Member(Get("xyChart"), "dataLabelColor")) ?? Get("primaryTextColor"))), ("legendTextColor", (Or(Member(Get("xyChart"), "legendTextColor")) ?? Get("primaryTextColor"))), ("xAxisTitleColor", (Or(Member(Get("xyChart"), "xAxisTitleColor")) ?? Get("primaryTextColor"))), ("xAxisLabelColor", (Or(Member(Get("xyChart"), "xAxisLabelColor")) ?? Get("primaryTextColor"))), ("xAxisTickColor", (Or(Member(Get("xyChart"), "xAxisTickColor")) ?? Get("primaryTextColor"))), ("xAxisLineColor", (Or(Member(Get("xyChart"), "xAxisLineColor")) ?? Get("primaryTextColor"))), ("yAxisTitleColor", (Or(Member(Get("xyChart"), "yAxisTitleColor")) ?? Get("primaryTextColor"))), ("yAxisLabelColor", (Or(Member(Get("xyChart"), "yAxisLabelColor")) ?? Get("primaryTextColor"))), ("yAxisTickColor", (Or(Member(Get("xyChart"), "yAxisTickColor")) ?? Get("primaryTextColor"))), ("yAxisLineColor", (Or(Member(Get("xyChart"), "yAxisLineColor")) ?? Get("primaryTextColor"))), ("plotColorPalette", (Or(Member(Get("xyChart"), "plotColorPalette")) ?? Str("#3498db,#2ecc71,#e74c3c,#f1c40f,#bdc3c7,#ffffff,#34495e,#9b59b6,#1abc9c,#e67e22")))));
		Set("packet", Obj(("startByteColor", Get("primaryTextColor")), ("endByteColor", Get("primaryTextColor")), ("labelColor", Get("primaryTextColor")), ("titleColor", Get("primaryTextColor")), ("blockStrokeColor", Get("primaryTextColor")), ("blockFillColor", Get("background"))));
		Set("radar", Obj(("axisColor", (Or(Member(Get("radar"), "axisColor")) ?? Get("lineColor"))), ("axisStrokeWidth", (Or(Member(Get("radar"), "axisStrokeWidth")) ?? Num(2))), ("axisLabelFontSize", (Or(Member(Get("radar"), "axisLabelFontSize")) ?? Num(12))), ("curveOpacity", (Or(Member(Get("radar"), "curveOpacity")) ?? Num(0.5))), ("curveStrokeWidth", (Or(Member(Get("radar"), "curveStrokeWidth")) ?? Num(2))), ("graticuleColor", (Or(Member(Get("radar"), "graticuleColor")) ?? Str("#DEDEDE"))), ("graticuleStrokeWidth", (Or(Member(Get("radar"), "graticuleStrokeWidth")) ?? Num(1))), ("graticuleOpacity", (Or(Member(Get("radar"), "graticuleOpacity")) ?? Num(0.3))), ("legendBoxSize", (Or(Member(Get("radar"), "legendBoxSize")) ?? Num(12))), ("legendFontSize", (Or(Member(Get("radar"), "legendFontSize")) ?? Num(12)))));
		Set("wardleyEvolutionColor", (Or(Get("wardleyEvolutionColor")) ?? Str("#ff6b6b")));
		Set("wardley", Obj(("backgroundColor", (Or(Member(Get("wardley"), "backgroundColor")) ?? Get("background"))), ("axisColor", (Or(Member(Get("wardley"), "axisColor")) ?? Get("lineColor"))), ("axisTextColor", (Or(Member(Get("wardley"), "axisTextColor")) ?? Get("primaryTextColor"))), ("gridColor", (Or(Member(Get("wardley"), "gridColor")) ?? Get("gridColor"))), ("componentFill", (Or(Member(Get("wardley"), "componentFill")) ?? Get("mainBkg"))), ("componentStroke", (Or(Member(Get("wardley"), "componentStroke")) ?? Get("lineColor"))), ("componentLabelColor", (Or(Member(Get("wardley"), "componentLabelColor")) ?? Get("primaryTextColor"))), ("linkStroke", (Or(Member(Get("wardley"), "linkStroke")) ?? Get("lineColor"))), ("evolutionStroke", (Or(Member(Get("wardley"), "evolutionStroke")) ?? Get("wardleyEvolutionColor"))), ("annotationStroke", (Or(Member(Get("wardley"), "annotationStroke")) ?? Get("lineColor"))), ("annotationTextColor", (Or(Member(Get("wardley"), "annotationTextColor")) ?? Get("primaryTextColor"))), ("annotationFill", (Or(Member(Get("wardley"), "annotationFill")) ?? Get("mainBkg")))));
		Set("classText", Get("primaryTextColor"));
		Set("requirementBackground", (Or(Get("requirementBackground")) ?? Get("primaryColor")));
		Set("requirementBorderColor", (Or(Get("requirementBorderColor")) ?? Get("primaryBorderColor")));
		Set("requirementBorderSize", (Or(Get("requirementBorderSize")) ?? Str("1")));
		Set("requirementTextColor", (Or(Get("requirementTextColor")) ?? Get("primaryTextColor")));
		Set("relationColor", (Or(Get("relationColor")) ?? Get("lineColor")));
		Set("relationLabelBackground", (Or(Get("relationLabelBackground")) ?? (Truthy(Get("darkMode")) ? Darken(Get("secondaryColor"), 30) : Get("secondaryColor"))));
		Set("relationLabelColor", (Or(Get("relationLabelColor")) ?? Get("actorTextColor")));
		Set("git0", Lighten(Get("secondaryColor"), 20));
		Set("git1", Lighten((Or(Get("pie2")) ?? Get("secondaryColor")), 20));
		Set("git2", Lighten((Or(Get("pie3")) ?? Get("tertiaryColor")), 20));
		Set("git3", Lighten((Or(Get("pie4")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: -30))), 20));
		Set("git4", Lighten((Or(Get("pie5")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: -60))), 20));
		Set("git5", Lighten((Or(Get("pie6")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: -90))), 10));
		Set("git6", Lighten((Or(Get("pie7")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 60))), 10));
		Set("git7", Lighten((Or(Get("pie8")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 120))), 20));
		Set("gitInv0", (Or(Get("gitInv0")) ?? Invert(Get("git0"))));
		Set("gitInv1", (Or(Get("gitInv1")) ?? Invert(Get("git1"))));
		Set("gitInv2", (Or(Get("gitInv2")) ?? Invert(Get("git2"))));
		Set("gitInv3", (Or(Get("gitInv3")) ?? Invert(Get("git3"))));
		Set("gitInv4", (Or(Get("gitInv4")) ?? Invert(Get("git4"))));
		Set("gitInv5", (Or(Get("gitInv5")) ?? Invert(Get("git5"))));
		Set("gitInv6", (Or(Get("gitInv6")) ?? Invert(Get("git6"))));
		Set("gitInv7", (Or(Get("gitInv7")) ?? Invert(Get("git7"))));
		Set("gitBranchLabel0", (Or(Get("gitBranchLabel0")) ?? Invert(Get("labelTextColor"))));
		Set("gitBranchLabel1", (Or(Get("gitBranchLabel1")) ?? Get("labelTextColor")));
		Set("gitBranchLabel2", (Or(Get("gitBranchLabel2")) ?? Get("labelTextColor")));
		Set("gitBranchLabel3", (Or(Get("gitBranchLabel3")) ?? Invert(Get("labelTextColor"))));
		Set("gitBranchLabel4", (Or(Get("gitBranchLabel4")) ?? Get("labelTextColor")));
		Set("gitBranchLabel5", (Or(Get("gitBranchLabel5")) ?? Get("labelTextColor")));
		Set("gitBranchLabel6", (Or(Get("gitBranchLabel6")) ?? Get("labelTextColor")));
		Set("gitBranchLabel7", (Or(Get("gitBranchLabel7")) ?? Get("labelTextColor")));
		Set("tagLabelColor", (Or(Get("tagLabelColor")) ?? Get("primaryTextColor")));
		Set("tagLabelBackground", (Or(Get("tagLabelBackground")) ?? Get("primaryColor")));
		Set("tagLabelBorder", (Or(Get("tagBorder")) ?? Get("primaryBorderColor")));
		Set("tagLabelFontSize", (Or(Get("tagLabelFontSize")) ?? Str("10px")));
		Set("commitLabelColor", (Or(Get("commitLabelColor")) ?? Get("secondaryTextColor")));
		Set("commitLabelBackground", (Or(Get("commitLabelBackground")) ?? Get("secondaryColor")));
		Set("commitLabelFontSize", (Or(Get("commitLabelFontSize")) ?? Str("10px")));
		Set("emUiFill", (Or(Get("emUiFill")) ?? Str("#2d2d2d")));
		Set("emUiStroke", (Or(Get("emUiStroke")) ?? Str("#555")));
		Set("emProcessorFill", (Or(Get("emProcessorFill")) ?? Lighten(Str("#5a3d5c"), 10)));
		Set("emProcessorStroke", (Or(Get("emProcessorStroke")) ?? Str("#8a6d8c")));
		Set("emReadModelFill", (Or(Get("emReadModelFill")) ?? Lighten(Str("#3d5a2d"), 10)));
		Set("emReadModelStroke", (Or(Get("emReadModelStroke")) ?? Str("#6d8c5c")));
		Set("emCommandFill", (Or(Get("emCommandFill")) ?? Lighten(Str("#2d3d5a"), 10)));
		Set("emCommandStroke", (Or(Get("emCommandStroke")) ?? Str("#5c6d8c")));
		Set("emEventFill", (Or(Get("emEventFill")) ?? Lighten(Str("#5a452d"), 10)));
		Set("emEventStroke", (Or(Get("emEventStroke")) ?? Str("#8c755c")));
		Set("emSwimlaneBackgroundOdd", (Or(Get("emSwimlaneBackgroundOdd")) ?? Lighten(Get("background"), 5)));
		Set("emSwimlaneBackgroundStroke", (Or(Get("emSwimlaneBackgroundStroke")) ?? Lighten(Get("background"), 12)));
		Set("emArrowhead", (Or(Get("emArrowhead")) ?? Get("lineColor")));
		Set("emRelationStroke", (Or(Get("emRelationStroke")) ?? Get("lineColor")));
		Set("attributeBackgroundColorOdd", (Or(Get("attributeBackgroundColorOdd")) ?? Lighten(Get("background"), 12)));
		Set("attributeBackgroundColorEven", (Or(Get("attributeBackgroundColorEven")) ?? Lighten(Get("background"), 2)));
		Set("nodeBorder", (Or(Get("nodeBorder")) ?? Str("#999")));
	}
}
