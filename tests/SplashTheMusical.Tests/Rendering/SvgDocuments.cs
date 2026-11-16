using System.Buffers;
using System.Text;
using SplashTheMusical.Rendering;

namespace SplashTheMusical.Tests.Rendering;

/// <summary>Writes documents with <see cref="SvgWriter"/>, and fails unless <see cref="SvgSanitizer"/> returns each unchanged.</summary>
internal static class SvgDocuments
{
	public static string Write(Action<SvgWriter> write) => Checked(SvgWriter.Write(write, static (writer, write) => write(writer)));

	public static byte[] WriteBytes(Action<SvgWriter> write)
	{
		var buffer = new ArrayBufferWriter<byte>();
		write(new SvgWriter(buffer));
		Checked(Encoding.UTF8.GetString(buffer.WrittenSpan));
		return buffer.WrittenSpan.ToArray();
	}

	public static string Checked(string svg)
	{
		var result = SvgSanitizer.SanitizeRendererOutput(svg);
		if (result.HasViolations || !ReferenceEquals(result.Svg, svg))
		{
			throw new InvalidOperationException($"The sanitizer changed SvgWriter's output ({string.Join(", ", result.Violations)}):\n{svg}");
		}

		return svg;
	}
}
