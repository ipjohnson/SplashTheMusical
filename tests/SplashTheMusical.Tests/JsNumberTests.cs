using AwesomeAssertions;

namespace SplashTheMusical.Tests;

public class JsNumberTests
{
	[Test]
	[Arguments(0d, "0")]
	[Arguments(-0d, "0")]
	[Arguments(16d, "16")]
	[Arguments(-8.5, "-8.5")]
	[Arguments(0.1, "0.1")]
	[Arguments(0.000001, "0.000001")]
	[Arguments(0.0000001, "1e-7")]
	[Arguments(1.5e-10, "1.5e-10")]
	[Arguments(1e15, "1000000000000000")]
	[Arguments(123456789012345680000d, "123456789012345680000")]
	[Arguments(1e21, "1e+21")]
	[Arguments(1.25e22, "1.25e+22")]
	[Arguments(double.NaN, "NaN")]
	[Arguments(double.NegativeInfinity, "-Infinity")]
	public void WritesNumbersAsJavaScriptDoes(double value, string text) => JsNumber.ToString(value).Should().Be(text);
}
