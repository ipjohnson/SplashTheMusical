namespace SplashTheMusical.Themes;

/// <summary>mermaid's <c>packages/mermaid/src/themes/theme-redux-dark.js</c>.</summary>
internal sealed class ReduxDarkTheme : Theme
{
	public override string Name => "redux-dark";

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
		Set("mainBkg", Str("#111113"));
		Set("secondBkg", Str("calculated"));
		Set("mainContrastColor", Str("lightgrey"));
		Set("darkTextColor", Lighten(Invert(Str("#323D47")), 10));
		Set("border1", Str("#ccc"));
		Set("border2", Rgba(255, 255, 255, 0.25));
		Set("arrowheadColor", Invert(Get("background")));
		Set("fontFamily", Str("\"Recursive Variable\", arial, sans-serif"));
		Set("fontSize", Str("14px"));
		Set("labelBackground", Str("#111113"));
		Set("textColor", Str("#ccc"));
		Set("THEME_COLOR_LIMIT", Num(12));
		Set("radius", Num(12));
		Set("strokeWidth", Num(2));
		Set("noteBkgColor", (Nullish(Get("noteBkgColor")) ?? Str("#FEF9C3")));
		Set("noteTextColor", (Nullish(Get("noteTextColor")) ?? Str("#28253D")));
		Set("THEME_COLOR_LIMIT", Num(12));
		Set("fontFamily", Str("\"Recursive Variable\", arial, sans-serif"));
		Set("fontSize", Str("14px"));
		Set("nodeBorder", Str("#FFFFFF"));
		Set("stateBorder", Str("#FFFFFF"));
		Set("useGradient", MermaidValue.False);
		Set("gradientStart", Str("#0042eb"));
		Set("gradientStop", Str("#eb0042"));
		Set("dropShadow", Str("url(#drop-shadow)"));
		Set("nodeShadow", MermaidValue.True);
		Set("archEdgeColor", Str("calculated"));
		Set("archEdgeArrowColor", Str("calculated"));
		Set("archEdgeWidth", Str("3"));
		Set("archGroupBorderColor", Get("primaryBorderColor"));
		Set("archGroupBorderWidth", Str("2px"));
		Set("clusterBkg", Str("#1E1A2E"));
		Set("clusterBorder", Str("#BDBCCC"));
		Set("noteBorderColor", Str("#FACC15"));
		Set("noteFontWeight", Num(600));
		Set("filterColor", Str("#FFFFFF"));
	}

	protected override void UpdateColors()
	{
		Set("primaryTextColor", (Or(Get("primaryTextColor")) ?? (Truthy(Get("darkMode")) ? Str("#eee") : Str("#FFFFFF"))));
		Set("secondaryColor", (Or(Get("secondaryColor")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: -120))));
		Set("tertiaryColor", (Or(Get("tertiaryColor")) ?? Adjust(Get("primaryColor"), new KhromaChannels(H: 180, L: 5))));
		Set("primaryBorderColor", (Or(Get("primaryBorderColor")) ?? MkBorder(Get("primaryColor"), Truthy(Get("darkMode")))));
		Set("secondaryBorderColor", (Or(Get("secondaryBorderColor")) ?? MkBorder(Get("secondaryColor"), Truthy(Get("darkMode")))));
		Set("tertiaryBorderColor", (Or(Get("tertiaryBorderColor")) ?? MkBorder(Get("tertiaryColor"), Truthy(Get("darkMode")))));
		Set("noteBorderColor", (Or(Get("noteBorderColor")) ?? MkBorder(Get("noteBkgColor"), Truthy(Get("darkMode")))));
		Set("noteBkgColor", (Or(Get("noteBkgColor")) ?? Str("#fff5ad")));
		Set("noteTextColor", (Or(Get("noteTextColor")) ?? Str("#FFFFFF")));
		Set("secondaryTextColor", (Or(Get("secondaryTextColor")) ?? Invert(Get("secondaryColor"))));
		Set("tertiaryTextColor", (Or(Get("tertiaryTextColor")) ?? Invert(Get("tertiaryColor"))));
		Set("lineColor", (Or(Get("lineColor")) ?? Invert(Get("background"))));
		Set("arrowheadColor", (Or(Get("arrowheadColor")) ?? Invert(Get("background"))));
		Set("textColor", (Or(Get("textColor")) ?? Get("primaryTextColor")));
		Set("border2", (Or(Get("border2")) ?? Get("tertiaryBorderColor")));
		Set("nodeBkg", (Or(Get("nodeBkg")) ?? Get("primaryColor")));
		Set("mainBkg", (Or(Get("mainBkg")) ?? Get("primaryColor")));
		Set("secondBkg", (StrictEquals(Get("secondBkg"), Str("calculated")) ? Lighten(Get("mainBkg"), 16) : Get("secondBkg")));
		Set("nodeBorder", (Or(Get("nodeBorder")) ?? Get("border1")));
		Set("clusterBkg", (Or(Get("clusterBkg")) ?? Get("tertiaryColor")));
		Set("clusterBorder", (Or(Get("clusterBorder")) ?? Get("tertiaryBorderColor")));
		Set("defaultLinkColor", (Or(Get("defaultLinkColor")) ?? Get("lineColor")));
		Set("titleColor", (Or(Get("titleColor")) ?? Get("tertiaryTextColor")));
		Set("edgeLabelBackground", (Or(Get("edgeLabelBackground")) ?? (Truthy(Get("darkMode")) ? Darken(Get("secondaryColor"), 30) : Get("secondaryColor"))));
		Set("nodeTextColor", (Or(Get("nodeTextColor")) ?? Get("primaryTextColor")));
		Set("flowContainerStroke", (Or(Get("flowContainerStroke")) ?? Get("secondaryBorderColor")));
		Set("actorBorder", Str("#FFFFFF"));
		Set("signalColor", Str("#FFFFFF"));
		Set("labelBoxBorderColor", Str("#BDBCCC"));
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
		Set("doneTaskBkgColor", (Or(Get("doneTaskBkgColor")) ?? Get("secondBkg")));
		Set("doneTaskBorderColor", (Or(Get("doneTaskBorderColor")) ?? Str("grey")));
		Set("critBorderColor", (Or(Get("critBorderColor")) ?? Str("#ff8888")));
		Set("critBkgColor", (Or(Get("critBkgColor")) ?? Str("red")));
		Set("todayLineColor", (Or(Get("todayLineColor")) ?? Str("red")));
		Set("taskTextColor", (Or(Get("taskTextColor")) ?? Get("textColor")));
		Set("taskTextOutsideColor", (Or(Get("taskTextOutsideColor")) ?? Get("textColor")));
		Set("taskTextLightColor", (Or(Get("taskTextLightColor")) ?? Get("textColor")));
		Set("taskTextColor", (Or(Get("taskTextColor")) ?? Get("primaryTextColor")));
		Set("taskTextDarkColor", (Or(Get("taskTextDarkColor")) ?? Get("textColor")));
		Set("taskTextClickableColor", (Or(Get("taskTextClickableColor")) ?? Str("#003163")));
		Set("archEdgeColor", Get("lineColor"));
		Set("archEdgeArrowColor", Get("lineColor"));
		Set("personBorder", (Or(Get("personBorder")) ?? Get("primaryBorderColor")));
		Set("personBkg", (Or(Get("personBkg")) ?? Get("mainBkg")));
		Set("transitionColor", (Or(Get("transitionColor")) ?? Get("lineColor")));
		Set("transitionLabelColor", (Or(Get("transitionLabelColor")) ?? Get("textColor")));
		Set("stateLabelColor", (Or((Or(Get("stateLabelColor")) ?? Get("stateBkg"))) ?? Get("primaryTextColor")));
		Set("vertLineColor", (Or(Get("vertLineColor")) ?? Get("primaryBorderColor")));
		Set("compositeBackground", Str("#16141F"));
		Set("altBackground", Str("#16141F"));
		Set("compositeTitleBackground", Str("#16141F"));
		Set("stateEdgeLabelBackground", Str("#16141F"));
		Set("fontWeight", Num(600));
		Set("stateBkg", (Or(Get("stateBkg")) ?? Get("mainBkg")));
		Set("labelBackgroundColor", (Or(Get("labelBackgroundColor")) ?? Get("stateBkg")));
		Set("compositeBackground", (Or((Or(Get("compositeBackground")) ?? Get("background"))) ?? Get("tertiaryColor")));
		Set("altBackground", (Or(Get("altBackground")) ?? Str("#f0f0f0")));
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
		Set("vennTitleTextColor", (Nullish(Get("vennTitleTextColor")) ?? Get("titleColor")));
		Set("vennSetTextColor", (Nullish(Get("vennSetTextColor")) ?? Get("textColor")));
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
		Set("xyChart", Obj(("backgroundColor", (Or(Member(Get("xyChart"), "backgroundColor")) ?? Get("background"))), ("titleColor", (Or(Member(Get("xyChart"), "titleColor")) ?? Get("primaryTextColor"))), ("legendTextColor", (Or(Member(Get("xyChart"), "legendTextColor")) ?? Get("primaryTextColor"))), ("xAxisTitleColor", (Or(Member(Get("xyChart"), "xAxisTitleColor")) ?? Get("primaryTextColor"))), ("xAxisLabelColor", (Or(Member(Get("xyChart"), "xAxisLabelColor")) ?? Get("primaryTextColor"))), ("xAxisTickColor", (Or(Member(Get("xyChart"), "xAxisTickColor")) ?? Get("primaryTextColor"))), ("xAxisLineColor", (Or(Member(Get("xyChart"), "xAxisLineColor")) ?? Get("primaryTextColor"))), ("yAxisTitleColor", (Or(Member(Get("xyChart"), "yAxisTitleColor")) ?? Get("primaryTextColor"))), ("yAxisLabelColor", (Or(Member(Get("xyChart"), "yAxisLabelColor")) ?? Get("primaryTextColor"))), ("yAxisTickColor", (Or(Member(Get("xyChart"), "yAxisTickColor")) ?? Get("primaryTextColor"))), ("yAxisLineColor", (Or(Member(Get("xyChart"), "yAxisLineColor")) ?? Get("primaryTextColor"))), ("plotColorPalette", (Or(Member(Get("xyChart"), "plotColorPalette")) ?? Str("#FFF4DD,#FFD8B1,#FFA07A,#ECEFF1,#D6DBDF,#C3E0A8,#FFB6A4,#FFD74D,#738FA7,#FFFFF0")))));
		Set("requirementBackground", (Or(Get("requirementBackground")) ?? Get("primaryColor")));
		Set("requirementBorderColor", (Or(Get("requirementBorderColor")) ?? Get("primaryBorderColor")));
		Set("requirementBorderSize", (Or(Get("requirementBorderSize")) ?? Str("1")));
		Set("requirementTextColor", (Or(Get("requirementTextColor")) ?? Get("primaryTextColor")));
		Set("relationColor", (Or(Get("relationColor")) ?? Get("lineColor")));
		Set("relationLabelBackground", (Or(Get("relationLabelBackground")) ?? (Truthy(Get("darkMode")) ? Darken(Get("secondaryColor"), 30) : Get("secondaryColor"))));
		Set("relationLabelColor", (Or(Get("relationLabelColor")) ?? Get("actorTextColor")));
		Set("requirementEdgeLabelBackground", Str("#16141F"));
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
		Set("commitLineColor", (Nullish(Get("commitLineColor")) ?? Str("#BDBCCC")));
		Set("erEdgeLabelBackground", Str("#16141F"));
		Set("attributeBackgroundColorOdd", (Or(Get("attributeBackgroundColorOdd")) ?? Str("#ffffff")));
		Set("attributeBackgroundColorEven", (Or(Get("attributeBackgroundColorEven")) ?? Str("#f2f2f2")));
	}
}
