using AwesomeAssertions;

namespace SplashTheMusical.Tests;

public class AssemblyReferenceTests
{
	[Test]
	public void CoreReferencesOnlyTheRuntime() =>
		ReferencedAssemblies("SplashTheMusical").Should().OnlyContain(name => IsRuntimeAssembly(name));

	[Test]
	public void LayoutReferencesOnlyTheRuntime() =>
		ReferencedAssemblies("SplashTheMusical.Layout").Should().OnlyContain(name => IsRuntimeAssembly(name));

	private static string[] ReferencedAssemblies(string assemblyName)
	{
		var assembly = System.Reflection.Assembly.Load(assemblyName);
		return [.. assembly.GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty)];
	}

	private static bool IsRuntimeAssembly(string name) =>
		name is "netstandard" or "mscorlib" || name.StartsWith("System.", StringComparison.Ordinal) || name == "System";
}
