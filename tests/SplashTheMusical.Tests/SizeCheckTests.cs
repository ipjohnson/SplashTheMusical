using AwesomeAssertions;
using SplashTheMusical.Benchmarks;

namespace SplashTheMusical.Tests;

public class SizeCheckTests
{
	[Test]
	public void FailsWhenABinaryGrowsByMoreThanFivePercentAndPassesOnceAccepted()
	{
		var folder = Directory.CreateTempSubdirectory().FullName;
		try
		{
			var sizes = Path.Combine(folder, "sizes.md");
			var binary = Path.Combine(folder, "stm");
			(string, string, string)[] binaries = [("stm", "osx-arm64", binary)];
			var output = new StringWriter();

			File.WriteAllBytes(binary, new byte[1000]);
			SizeCheck.Run(sizes, binaries, accept: true, output).Should().Be(0);

			File.WriteAllBytes(binary, new byte[1050]);
			SizeCheck.Run(sizes, binaries, accept: false, output).Should().Be(0);

			File.WriteAllBytes(binary, new byte[1051]);
			SizeCheck.Run(sizes, binaries, accept: false, output).Should().Be(1);
			output.ToString().Should().Contain("stm (osx-arm64): 1000 -> 1051 bytes (+5.1%), more than 5% larger");

			SizeCheck.Run(sizes, binaries, accept: true, output).Should().Be(0);
			SizeCheck.Run(sizes, binaries, accept: false, output).Should().Be(0);
		}
		finally
		{
			Directory.Delete(folder, recursive: true);
		}
	}

	[Test]
	public void FailsWhenTheSizesFileHasNoRowForTheBinary()
	{
		var folder = Directory.CreateTempSubdirectory().FullName;
		try
		{
			var binary = Path.Combine(folder, "stm");
			File.WriteAllBytes(binary, new byte[10]);
			var output = new StringWriter();
			SizeCheck.Run(Path.Combine(folder, "sizes.md"), [("stm", "linux-x64", binary)], accept: false, output).Should().Be(1);
			output.ToString().Should().Contain("has no row for it");
		}
		finally
		{
			Directory.Delete(folder, recursive: true);
		}
	}
}
