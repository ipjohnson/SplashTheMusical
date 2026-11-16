namespace SplashTheMusical.Themes;

/// <summary>mermaid's <c>packages/mermaid/src/themes/theme-forest.js</c>.</summary>
internal sealed class ForestTheme : Theme
{
	public override string Name => "forest";

	protected override void Construct()
	{
		Set("background", Str("#f4f4f4"));
		Set("primaryColor", Str("#cde498"));
		Set("secondaryColor", Str("#cdffb2"));
		Set("background", Str("white"));
		Set("mainBkg", Str("#cde498"));
		Set("secondBkg", Str("#cdffb2"));
		Set("lineColor", Str("green"));
		Set("border1", Str("#13540c"));
		Set("border2", Str("#6eaa49"));
		Set("arrowheadColor", Str("green"));
		Set("fontFamily", Str("\"trebuchet ms\", verdana, arial, sans-serif"));
		Set("fontSize", Str("16px"));
		Set("tertiaryColor", Lighten(Str("#cde498"), 10));
		Set("primaryBorderColor", MkBorder(Get("primaryColor"), Truthy(Get("darkMode"))));
		Set("secondaryBorderColor", MkBorder(Get("secondaryColor"), Truthy(Get("darkMode"))));
		Set("tertiaryBorderColor", MkBorder(Get("tertiaryColor"), Truthy(Get("darkMode"))));
		Set("primaryTextColor", Invert(Get("primaryColor")));
		Set("secondaryTextColor", Invert(Get("secondaryColor")));
		Set("tertiaryTextColor", Invert(Get("primaryColor")));
		Set("lineColor", Invert(Get("background")));
		Set("textColor", Invert(Get("background")));
		Set("THEME_COLOR_LIMIT", Num(12));
		Set("radius", Num(5));
		Set("strokeWidth", Num(1));
		Set("nodeBkg", Str("calculated"));
		Set("nodeBorder", Str("calculated"));
		Set("clusterBkg", Str("calculated"));
		Set("clusterBorder", Str("calculated"));
		Set("defaultLinkColor", Str("calculated"));
		Set("titleColor", Str("#333"));
		Set("edgeLabelBackground", Str("#e8e8e8"));
		Set("actorBorder", Str("calculated"));
		Set("actorBkg", Str("calculated"));
		Set("actorTextColor", Str("black"));
		Set("actorLineColor", Str("calculated"));
		Set("signalColor", Str("#333"));
		Set("signalTextColor", Str("#333"));
		Set("labelBoxBkgColor", Str("calculated"));
		Set("labelBoxBorderColor", Str("#326932"));
		Set("labelTextColor", Str("calculated"));
		Set("loopTextColor", Str("calculated"));
		Set("noteBorderColor", Str("calculated"));
		Set("noteBkgColor", Str("#fff5ad"));
		Set("noteTextColor", Str("calculated"));
		Set("activationBorderColor", Str("#666"));
		Set("activationBkgColor", Str("#f4f4f4"));
		Set("sequenceNumberColor", Str("white"));
		Set("sectionBkgColor", Str("#6eaa49"));
		Set("altSectionBkgColor", Str("white"));
		Set("sectionBkgColor2", Str("#6eaa49"));
		Set("excludeBkgColor", Str("#eeeeee"));
		Set("taskBorderColor", Str("calculated"));
		Set("taskBkgColor", Str("#487e3a"));
		Set("taskTextLightColor", Str("white"));
		Set("taskTextColor", Str("calculated"));
		Set("taskTextDarkColor", Str("black"));
		Set("taskTextOutsideColor", Str("calculated"));
		Set("taskTextClickableColor", Str("#003163"));
		Set("activeTaskBorderColor", Str("calculated"));
		Set("activeTaskBkgColor", Str("calculated"));
		Set("gridColor", Str("lightgrey"));
		Set("doneTaskBkgColor", Str("lightgrey"));
		Set("doneTaskBorderColor", Str("grey"));
		Set("critBorderColor", Str("#ff8888"));
		Set("critBkgColor", Str("red"));
		Set("todayLineColor", Str("red"));
		Set("vertLineColor", Str("#00BFFF"));
		Set("personBorder", Get("primaryBorderColor"));
		Set("personBkg", Get("mainBkg"));
		Set("archEdgeColor", Str("calculated"));
		Set("archEdgeArrowColor", Str("calculated"));
		Set("archEdgeWidth", Str("3"));
		Set("archGroupBorderColor", Get("primaryBorderColor"));
		Set("archGroupBorderWidth", Str("2px"));
		Set("noteFontWeight", Str("normal"));
		Set("fontWeight", Str("normal"));
		Set("labelColor", Str("black"));
		Set("errorBkgColor", Str("#552222"));
		Set("errorTextColor", Str("#552222"));
		Set("useGradient", MermaidValue.True);
		Set("gradientStart", Get("primaryBorderColor"));
		Set("gradientStop", Get("secondaryBorderColor"));
		Set("dropShadow", Str("drop-shadow( 1px 2px 2px rgba(185,185,185,0.5))"));
	}

	protected override void UpdateColors()
	{
		Set("actorBorder", Darken(Get("mainBkg"), 20));
		Set("actorBkg", Get("mainBkg"));
		Set("labelBoxBkgColor", Get("actorBkg"));
		Set("labelTextColor", Get("actorTextColor"));
		Set("loopTextColor", Get("actorTextColor"));
		Set("noteBorderColor", Get("border2"));
		Set("noteTextColor", Get("actorTextColor"));
		Set("actorLineColor", Get("actorBorder"));
		Set("rectBkgColor", (Or(Get("rectBkgColor")) ?? Get("tertiaryColor")));
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
		Set(Key("cScalePeer", 1), (Or(Get(Key("cScalePeer", 1))) ?? Darken(Get("secondaryColor"), 45)));
		Set(Key("cScalePeer", 2), (Or(Get(Key("cScalePeer", 2))) ?? Darken(Get("tertiaryColor"), 40)));
		for (var i = 0; i < ToNumber(Get("THEME_COLOR_LIMIT")); i++)
		{
			Set(Key("cScale", i), Darken(Get(Key("cScale", i)), 10));
			Set(Key("cScalePeer", i), (Or(Get(Key("cScalePeer", i))) ?? Darken(Get(Key("cScale", i)), 25)));
		}
		for (var i = 0; i < ToNumber(Get("THEME_COLOR_LIMIT")); i++)
		{
			Set(Key("cScaleInv", i), (Or(Get(Key("cScaleInv", i))) ?? Adjust(Get(Key("cScale", i)), new KhromaChannels(H: 180))));
		}
		Set("scaleLabelColor", ((!StrictEquals(Get("scaleLabelColor"), Str("calculated")) && Truthy(Get("scaleLabelColor"))) ? Get("scaleLabelColor") : Get("labelTextColor")));
		for (var i = 0; i < ToNumber(Get("THEME_COLOR_LIMIT")); i++)
		{
			Set(Key("cScaleLabel", i), (Or(Get(Key("cScaleLabel", i))) ?? Get("scaleLabelColor")));
		}
		for (var i = 0; i < 5; i++)
		{
			Set(Key("surface", i), (Or(Get(Key("surface", i))) ?? Adjust(Get("mainBkg"), new KhromaChannels(H: 30, S: -30, L: -(5 + (i * 5))))));
			Set(Key("surfacePeer", i), (Or(Get(Key("surfacePeer", i))) ?? Adjust(Get("mainBkg"), new KhromaChannels(H: 30, S: -30, L: -(8 + (i * 5))))));
		}
		Set("nodeBkg", Get("mainBkg"));
		Set("nodeBorder", Get("border1"));
		Set("clusterBkg", Get("secondBkg"));
		Set("clusterBorder", Get("border2"));
		Set("defaultLinkColor", Get("lineColor"));
		Set("flowContainerStroke", (Or(Get("flowContainerStroke")) ?? Get("secondaryBorderColor")));
		Set("taskBorderColor", Get("border1"));
		Set("taskTextColor", Get("taskTextLightColor"));
		Set("taskTextOutsideColor", Get("taskTextDarkColor"));
		Set("activeTaskBorderColor", Get("taskBorderColor"));
		Set("activeTaskBkgColor", Get("mainBkg"));
		Set("archEdgeColor", Get("lineColor"));
		Set("archEdgeArrowColor", Get("lineColor"));
		Set("rowOdd", (Or((Or(Get("rowOdd")) ?? Lighten(Get("mainBkg"), 75))) ?? Str("#ffffff")));
		Set("rowEven", (Or(Get("rowEven")) ?? Lighten(Get("mainBkg"), 20)));
		Set("transitionColor", (Or(Get("transitionColor")) ?? Get("lineColor")));
		Set("transitionLabelColor", (Or(Get("transitionLabelColor")) ?? Get("textColor")));
		Set("stateLabelColor", (Or((Or(Get("stateLabelColor")) ?? Get("stateBkg"))) ?? Get("primaryTextColor")));
		Set("stateBkg", (Or(Get("stateBkg")) ?? Get("mainBkg")));
		Set("labelBackgroundColor", (Or(Get("labelBackgroundColor")) ?? Get("stateBkg")));
		Set("compositeBackground", (Or((Or(Get("compositeBackground")) ?? Get("background"))) ?? Get("tertiaryColor")));
		Set("altBackground", (Or(Get("altBackground")) ?? Str("#f0f0f0")));
		Set("compositeTitleBackground", (Or(Get("compositeTitleBackground")) ?? Get("mainBkg")));
		Set("compositeBorder", (Or(Get("compositeBorder")) ?? Get("nodeBorder")));
		Set("innerEndBackground", Get("primaryBorderColor"));
		Set("specialStateColor", Get("lineColor"));
		Set("errorBkgColor", (Or(Get("errorBkgColor")) ?? Get("tertiaryColor")));
		Set("errorTextColor", (Or(Get("errorTextColor")) ?? Get("tertiaryTextColor")));
		Set("transitionColor", (Or(Get("transitionColor")) ?? Get("lineColor")));
		Set("classText", Get("primaryTextColor"));
		Set("fillType0", Get("primaryColor"));
		Set("fillType1", Get("secondaryColor"));
		Set("fillType2", Adjust(Get("primaryColor"), new KhromaChannels(H: 64)));
		Set("fillType3", Adjust(Get("secondaryColor"), new KhromaChannels(H: 64)));
		Set("fillType4", Adjust(Get("primaryColor"), new KhromaChannels(H: -64)));
		Set("fillType5", Adjust(Get("secondaryColor"), new KhromaChannels(H: -64)));
		Set("fillType6", Adjust(Get("primaryColor"), new KhromaChannels(H: 128)));
		Set("fillType7", Adjust(Get("secondaryColor"), new KhromaChannels(H: 128)));
		Set("pie1", (Or(Get("pie1")) ?? Get("primaryColor")));
		Set("pie2", (Or(Get("pie2")) ?? Get("secondaryColor")));
		Set("pie3", (Or(Get("pie3")) ?? Get("tertiaryColor")));
		Set("pie4", (Or(Get("pie4")) ?? Adjust(Get("primaryColor"), new KhromaChannels(L: -30))));
		Set("pie5", (Or(Get("pie5")) ?? Adjust(Get("secondaryColor"), new KhromaChannels(L: -30))));
		Set("pie6", (Or(Get("pie6")) ?? Adjust(Get("tertiaryColor"), new KhromaChannels(H: 40, L: -40))));
		Set("pie7", (Or(Get("pie7")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 60, L: -10))));
		Set("pie8", (Or(Get("pie8")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: -60, L: -10))));
		Set("pie9", (Or(Get("pie9")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 120, L: 0))));
		Set("pie10", (Or(Get("pie10")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 60, L: -50))));
		Set("pie11", (Or(Get("pie11")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: -60, L: -50))));
		Set("pie12", (Or(Get("pie12")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 120, L: -50))));
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
		Set("cynefin", Obj(("domainFontSize", (Or(Member(Get("cynefin"), "domainFontSize")) ?? Num(16))), ("itemFontSize", (Or(Member(Get("cynefin"), "itemFontSize")) ?? Num(12))), ("boundaryColor", (Or(Member(Get("cynefin"), "boundaryColor")) ?? Get("lineColor"))), ("boundaryWidth", (Or(Member(Get("cynefin"), "boundaryWidth")) ?? Num(2))), ("cliffColor", (Or(Member(Get("cynefin"), "cliffColor")) ?? Str("#8B4513"))), ("cliffWidth", (Or(Member(Get("cynefin"), "cliffWidth")) ?? Num(4))), ("arrowColor", (Or(Member(Get("cynefin"), "arrowColor")) ?? Get("lineColor"))), ("arrowWidth", (Or(Member(Get("cynefin"), "arrowWidth")) ?? Num(2))), ("complexBg", (Or(Member(Get("cynefin"), "complexBg")) ?? Str("#C8E6C9"))), ("complicatedBg", (Or(Member(Get("cynefin"), "complicatedBg")) ?? Str("#DCEDC8"))), ("chaoticBg", (Or(Member(Get("cynefin"), "chaoticBg")) ?? Str("#FFE0B2"))), ("clearBg", (Or(Member(Get("cynefin"), "clearBg")) ?? Str("#FFF9C4"))), ("confusionBg", (Or(Member(Get("cynefin"), "confusionBg")) ?? Str("#D7CCC8"))), ("textColor", (Or(Member(Get("cynefin"), "textColor")) ?? Get("textColor"))), ("labelColor", (Or(Member(Get("cynefin"), "labelColor")) ?? Get("primaryTextColor")))));
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
		Set("packet", Obj(("startByteColor", Get("primaryTextColor")), ("endByteColor", Get("primaryTextColor")), ("labelColor", Get("primaryTextColor")), ("titleColor", Get("primaryTextColor")), ("blockStrokeColor", Get("primaryTextColor")), ("blockFillColor", Get("mainBkg"))));
		Set("radar", Obj(("axisColor", (Or(Member(Get("radar"), "axisColor")) ?? Get("lineColor"))), ("axisStrokeWidth", (Or(Member(Get("radar"), "axisStrokeWidth")) ?? Num(2))), ("axisLabelFontSize", (Or(Member(Get("radar"), "axisLabelFontSize")) ?? Num(12))), ("curveOpacity", (Or(Member(Get("radar"), "curveOpacity")) ?? Num(0.5))), ("curveStrokeWidth", (Or(Member(Get("radar"), "curveStrokeWidth")) ?? Num(2))), ("graticuleColor", (Or(Member(Get("radar"), "graticuleColor")) ?? Str("#DEDEDE"))), ("graticuleStrokeWidth", (Or(Member(Get("radar"), "graticuleStrokeWidth")) ?? Num(1))), ("graticuleOpacity", (Or(Member(Get("radar"), "graticuleOpacity")) ?? Num(0.3))), ("legendBoxSize", (Or(Member(Get("radar"), "legendBoxSize")) ?? Num(12))), ("legendFontSize", (Or(Member(Get("radar"), "legendFontSize")) ?? Num(12)))));
		Set("wardleyEvolutionColor", (Or(Get("wardleyEvolutionColor")) ?? Str("#dc3545")));
		Set("wardley", Obj(("backgroundColor", (Or(Member(Get("wardley"), "backgroundColor")) ?? Get("background"))), ("axisColor", (Or(Member(Get("wardley"), "axisColor")) ?? Get("lineColor"))), ("axisTextColor", (Or(Member(Get("wardley"), "axisTextColor")) ?? Get("primaryTextColor"))), ("gridColor", (Or(Member(Get("wardley"), "gridColor")) ?? Get("gridColor"))), ("componentFill", (Or(Member(Get("wardley"), "componentFill")) ?? Get("background"))), ("componentStroke", (Or(Member(Get("wardley"), "componentStroke")) ?? Get("lineColor"))), ("componentLabelColor", (Or(Member(Get("wardley"), "componentLabelColor")) ?? Get("primaryTextColor"))), ("linkStroke", (Or(Member(Get("wardley"), "linkStroke")) ?? Get("lineColor"))), ("evolutionStroke", (Or(Member(Get("wardley"), "evolutionStroke")) ?? Get("wardleyEvolutionColor"))), ("annotationStroke", (Or(Member(Get("wardley"), "annotationStroke")) ?? Get("lineColor"))), ("annotationTextColor", (Or(Member(Get("wardley"), "annotationTextColor")) ?? Get("primaryTextColor"))), ("annotationFill", (Or(Member(Get("wardley"), "annotationFill")) ?? Get("background")))));
		Set("xyChart", Obj(("backgroundColor", (Or(Member(Get("xyChart"), "backgroundColor")) ?? Get("background"))), ("titleColor", (Or(Member(Get("xyChart"), "titleColor")) ?? Get("primaryTextColor"))), ("dataLabelColor", (Or(Member(Get("xyChart"), "dataLabelColor")) ?? Get("primaryTextColor"))), ("legendTextColor", (Or(Member(Get("xyChart"), "legendTextColor")) ?? Get("primaryTextColor"))), ("xAxisTitleColor", (Or(Member(Get("xyChart"), "xAxisTitleColor")) ?? Get("primaryTextColor"))), ("xAxisLabelColor", (Or(Member(Get("xyChart"), "xAxisLabelColor")) ?? Get("primaryTextColor"))), ("xAxisTickColor", (Or(Member(Get("xyChart"), "xAxisTickColor")) ?? Get("primaryTextColor"))), ("xAxisLineColor", (Or(Member(Get("xyChart"), "xAxisLineColor")) ?? Get("primaryTextColor"))), ("yAxisTitleColor", (Or(Member(Get("xyChart"), "yAxisTitleColor")) ?? Get("primaryTextColor"))), ("yAxisLabelColor", (Or(Member(Get("xyChart"), "yAxisLabelColor")) ?? Get("primaryTextColor"))), ("yAxisTickColor", (Or(Member(Get("xyChart"), "yAxisTickColor")) ?? Get("primaryTextColor"))), ("yAxisLineColor", (Or(Member(Get("xyChart"), "yAxisLineColor")) ?? Get("primaryTextColor"))), ("plotColorPalette", (Or(Member(Get("xyChart"), "plotColorPalette")) ?? Str("#CDE498,#FF6B6B,#A0D2DB,#D7BDE2,#F0F0F0,#FFC3A0,#7FD8BE,#FF9A8B,#FAF3E0,#FFF176")))));
		Set("requirementBackground", (Or(Get("requirementBackground")) ?? Get("primaryColor")));
		Set("requirementBorderColor", (Or(Get("requirementBorderColor")) ?? Get("primaryBorderColor")));
		Set("requirementBorderSize", (Or(Get("requirementBorderSize")) ?? Str("1")));
		Set("requirementTextColor", (Or(Get("requirementTextColor")) ?? Get("primaryTextColor")));
		Set("relationColor", (Or(Get("relationColor")) ?? Get("lineColor")));
		Set("relationLabelBackground", (Or(Get("relationLabelBackground")) ?? Get("edgeLabelBackground")));
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
