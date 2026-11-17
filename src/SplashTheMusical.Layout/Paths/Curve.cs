using System.Collections.Immutable;

namespace SplashTheMusical.Layout.Paths;

/// <summary>The curves an edge can be drawn with, one per <c>curve</c> value of mermaid 12.3.0's config schema.</summary>
internal enum Curve
{
	Basis,
	BumpX,
	BumpY,
	Cardinal,
	CatmullRom,
	Linear,
	MonotoneX,
	MonotoneY,
	Natural,
	Step,
	StepAfter,
	StepBefore,

	/// <summary>Mermaid's own curve, straight lines with a quadratic corner of radius 5, from <c>generateRoundedPath</c>.</summary>
	Rounded,
}

/// <summary>The config names of the curves.</summary>
internal static class CurveNames
{
	/// <summary>Each curve with its name, in the schema's order.</summary>
	public static ImmutableArray<(string Name, Curve Curve)> All { get; } =
	[
		("basis", Curve.Basis),
		("bumpX", Curve.BumpX),
		("bumpY", Curve.BumpY),
		("cardinal", Curve.Cardinal),
		("catmullRom", Curve.CatmullRom),
		("linear", Curve.Linear),
		("monotoneX", Curve.MonotoneX),
		("monotoneY", Curve.MonotoneY),
		("natural", Curve.Natural),
		("step", Curve.Step),
		("stepAfter", Curve.StepAfter),
		("stepBefore", Curve.StepBefore),
		("rounded", Curve.Rounded),
	];

	public static bool TryParse(string? name, out Curve curve)
	{
		foreach (var (text, value) in All)
		{
			if (string.Equals(text, name, StringComparison.Ordinal))
			{
				curve = value;
				return true;
			}
		}

		curve = Curve.Basis;
		return false;
	}

	/// <summary>The curve <c>insertEdge</c> draws for <paramref name="name"/>, which is basis for a name it does not know.</summary>
	public static Curve Parse(string? name) => TryParse(name, out var curve) ? curve : Curve.Basis;

	public static string Name(Curve curve) => All[(int)curve].Name;
}
