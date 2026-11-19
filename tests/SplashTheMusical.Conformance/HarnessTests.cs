namespace SplashTheMusical.Conformance;

public class HarnessTests
{
	[Test]
	public void LoadsTheCore() =>
		Verify(() => System.Reflection.Assembly.Load("SplashTheMusical").GetName().Name == "SplashTheMusical");
}
