using CsCheck;
using SplashTheMusical.Diagrams.Info;

namespace SplashTheMusical.Tests.Generation;

/// <summary>
/// Random <c>info</c> documents: <c>showInfo</c>, a title and the accessibility statements, with their text as the
/// langium grammar leaves it, <see cref="GenText.Folded"/>.
/// </summary>
public static class InfoGenerator
{
	private static readonly Gen<string> Line = GenText.Line.Select(GenText.Folded);

	public static Gen<InfoDocument> Document { get; } =
		Gen.Select(
			Gen.Bool,
			Gen.Frequency((1, Gen.Const("")), (3, Line)).OrNull(),
			Line.OrNull(),
			Gen.OneOf<string>(Line, GenText.Lines.Select(GenText.Folded)).OrNull(),
			(showInfo, title, accTitle, accDescr) => new InfoDocument
			{
				ShowInfo = showInfo,
				DiagramTitle = title,
				AccessibilityTitle = accTitle,
				AccessibilityDescription = accDescr,
			});
}
