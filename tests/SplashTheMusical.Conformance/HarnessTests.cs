using AwesomeAssertions;

namespace SplashTheMusical.Conformance;

public class HarnessTests
{
	[Test]
	public void LoadsTheCore() =>
		System.Reflection.Assembly.Load("SplashTheMusical").GetName().Name.Should().Be("SplashTheMusical");
}
