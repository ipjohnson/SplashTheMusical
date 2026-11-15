namespace SplashTheMusical.Conformance;

public class HarnessTests
{
	[Test]
	public async Task LoadsTheCore() =>
		await Assert.That(System.Reflection.Assembly.Load("SplashTheMusical").GetName().Name).IsEqualTo("SplashTheMusical");
}
