using System.Collections.Immutable;

namespace SplashTheMusical.Icons;

/// <summary>
/// The icons a diagram can name, such as <c>logos:aws</c>, as mermaid's <c>registerIconPacks</c> provides them.
/// <see cref="IconRegistry"/> is the provider of registered <see cref="IconPack"/>s.
/// </summary>
/// <remarks>An implementation is stateless and thread safe.</remarks>
public interface IIconProvider
{
	/// <summary>The icon named <paramref name="name"/>, or <c>null</c> when there is none.</summary>
	/// <param name="name">The pack's prefix, a colon and the icon's name, as the diagram writes it.</param>
	MermaidIcon? GetIcon(string name);
}

/// <summary>
/// An icon in the form of an Iconify icon: its SVG content and its view box. The content is sanitized once, when the
/// icon is made, so a renderer draws only what <see cref="Rendering.SvgWriter"/> writes.
/// </summary>
/// <remarks>
/// Sanitizing applies each <c>style</c> declaration of a presentation attribute as that attribute, then removes every
/// element and attribute outside the writer's allowlists, such as <c>script</c>, <c>foreignObject</c>, event handlers,
/// links and images. <see cref="Removed"/> lists what was removed.
/// </remarks>
public sealed class MermaidIcon
{
	/// <summary>Makes an icon from an Iconify icon's fields, sanitizing <paramref name="body"/>.</summary>
	/// <param name="body">The SVG content of the icon, without the <c>svg</c> element.</param>
	/// <param name="width">The width of the view box. Iconify's default is 16.</param>
	/// <param name="height">The height of the view box.</param>
	/// <param name="left">The left of the view box.</param>
	/// <param name="top">The top of the view box.</param>
	/// <param name="rotate">Quarter turns clockwise, 0 to 3.</param>
	/// <param name="horizontalFlip">Whether the icon is mirrored left to right.</param>
	/// <param name="verticalFlip">Whether the icon is mirrored top to bottom.</param>
	/// <exception cref="ArgumentOutOfRangeException">The width or height is not a positive finite number, or the left or top is not finite.</exception>
	public MermaidIcon(string body, double width = 16, double height = 16, double left = 0, double top = 0, int rotate = 0, bool horizontalFlip = false, bool verticalFlip = false)
	{
		ArgumentNullException.ThrowIfNull(body);
		if (!double.IsFinite(width) || width <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(width), width, "The width is a positive number.");
		}

		if (!double.IsFinite(height) || height <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(height), height, "The height is a positive number.");
		}

		if (!double.IsFinite(left) || !double.IsFinite(top))
		{
			throw new ArgumentOutOfRangeException(double.IsFinite(left) ? nameof(top) : nameof(left), "The view box's corner is finite.");
		}

		(Content, Body, Removed) = IconContent.Sanitize(body);
		Width = width;
		Height = height;
		Left = left;
		Top = top;
		Rotate = ((rotate % 4) + 4) % 4;
		HorizontalFlip = horizontalFlip;
		VerticalFlip = verticalFlip;
	}

	private MermaidIcon(MermaidIcon content, double width, double height, double left, double top, int rotate, bool horizontalFlip, bool verticalFlip)
	{
		(Content, Body, Removed) = (content.Content, content.Body, content.Removed);
		Width = width;
		Height = height;
		Left = left;
		Top = top;
		Rotate = ((rotate % 4) + 4) % 4;
		HorizontalFlip = horizontalFlip;
		VerticalFlip = verticalFlip;
	}

	/// <summary>The sanitized SVG content.</summary>
	public string Body { get; }

	/// <summary>What sanitizing removed from the body the icon was made from.</summary>
	public IReadOnlyList<SvgViolation> Removed { get; }

	public double Width { get; }

	public double Height { get; }

	public double Left { get; }

	public double Top { get; }

	/// <summary>Quarter turns clockwise, 0 to 3.</summary>
	public int Rotate { get; }

	public bool HorizontalFlip { get; }

	public bool VerticalFlip { get; }

	internal ImmutableArray<IconElement> Content { get; }

	/// <summary>
	/// The view box and the transform of the content, as Iconify's <c>iconToSVG</c> gives them: a flip is a translate
	/// and a scale, a turn a rotate about the box's centre, and a quarter turn swaps the box's sides.
	/// </summary>
	internal (double Left, double Top, double Width, double Height, string? Transform) Drawing()
	{
		var (left, top, width, height) = (Left, Top, Width, Height);
		var transforms = new List<string>();
		var rotation = Rotate;
		if (HorizontalFlip)
		{
			if (VerticalFlip)
			{
				rotation += 2;
			}
			else
			{
				transforms.Add("translate(" + N(width + left) + " " + N(0 - top) + ")");
				transforms.Add("scale(-1 1)");
				top = left = 0;
			}
		}
		else if (VerticalFlip)
		{
			transforms.Add("translate(" + N(0 - left) + " " + N(height + top) + ")");
			transforms.Add("scale(1 -1)");
			top = left = 0;
		}

		rotation %= 4;
		switch (rotation)
		{
			case 1:
				var quarter = (height / 2) + top;
				transforms.Insert(0, "rotate(90 " + N(quarter) + " " + N(quarter) + ")");
				break;
			case 2:
				transforms.Insert(0, "rotate(180 " + N((width / 2) + left) + " " + N((height / 2) + top) + ")");
				break;
			case 3:
				var threeQuarters = (width / 2) + left;
				transforms.Insert(0, "rotate(-90 " + N(threeQuarters) + " " + N(threeQuarters) + ")");
				break;
		}

		if (rotation % 2 == 1)
		{
			(left, top) = (top, left);
			(width, height) = (height, width);
		}

		return (left, top, width, height, transforms.Count == 0 ? null : string.Join(' ', transforms));
	}

	/// <summary>This icon's content with an alias's fields, as Iconify's <c>mergeIconData</c> merges them.</summary>
	internal MermaidIcon WithAlias(double? width, double? height, double? left, double? top, int rotate, bool horizontalFlip, bool verticalFlip) =>
		new(this, width ?? Width, height ?? Height, left ?? Left, top ?? Top, Rotate + rotate, HorizontalFlip != horizontalFlip, VerticalFlip != verticalFlip);

	private static string N(double value) => JsNumber.ToString(value);
}

/// <summary>The provider that has no icons, so every icon a diagram names draws mermaid's unknown icon.</summary>
public sealed class NoIcons : IIconProvider
{
	private NoIcons()
	{
	}

	public static NoIcons Instance { get; } = new();

	public MermaidIcon? GetIcon(string name) => null;
}
