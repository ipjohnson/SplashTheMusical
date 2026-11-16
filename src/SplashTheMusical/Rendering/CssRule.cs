namespace SplashTheMusical.Rendering;

/// <summary>A rule of the stylesheet <see cref="SvgWriter.WriteStyle"/> writes.</summary>
internal abstract class CssRule;

/// <summary>A style rule. Declarations that a factory refused, given as <c>null</c>, are left out.</summary>
internal sealed class CssStyleRule : CssRule
{
	public CssStyleRule(IReadOnlyList<CssSelector> selectors, params ReadOnlySpan<CssDeclaration?> declarations)
	{
		ArgumentNullException.ThrowIfNull(selectors);
		ArgumentOutOfRangeException.ThrowIfZero(selectors.Count);
		Selectors = selectors;
		var kept = new List<CssDeclaration>(declarations.Length);
		foreach (var declaration in declarations)
		{
			if (declaration is not null)
			{
				kept.Add(declaration);
			}
		}

		Declarations = kept;
	}

	public CssStyleRule(CssSelector selector, params ReadOnlySpan<CssDeclaration?> declarations)
		: this([selector], declarations)
	{
	}

	public IReadOnlyList<CssSelector> Selectors { get; }

	public IReadOnlyList<CssDeclaration> Declarations { get; }
}

/// <summary>An <c>@keyframes</c> rule. mermaid's <c>compileCSS</c> leaves its name and frames unscoped.</summary>
internal sealed class CssKeyframesRule : CssRule
{
	/// <exception cref="ArgumentException">The name is not a CSS identifier, or a frame is not <c>from</c>, <c>to</c> or a percentage.</exception>
	public CssKeyframesRule(string name, params IReadOnlyList<(string Frame, CssDeclaration?[] Declarations)> frames)
	{
		ArgumentNullException.ThrowIfNull(name);
		ArgumentNullException.ThrowIfNull(frames);
		if (!CssText.IsIdentifier(name))
		{
			throw new ArgumentException($"'{name}' is not a CSS identifier.", nameof(name));
		}

		foreach (var (frame, _) in frames)
		{
			if (frame is not ("from" or "to") && !(frame.EndsWith('%') && double.TryParse(frame.AsSpan(0, frame.Length - 1), System.Globalization.CultureInfo.InvariantCulture, out _)))
			{
				throw new ArgumentException($"'{frame}' is not a keyframe selector.", nameof(frames));
			}
		}

		Name = name;
		Frames = [.. frames.Select(f => (f.Frame, (IReadOnlyList<CssDeclaration>)[.. f.Declarations.OfType<CssDeclaration>()]))];
	}

	public string Name { get; }

	public IReadOnlyList<(string Frame, IReadOnlyList<CssDeclaration> Declarations)> Frames { get; }
}
