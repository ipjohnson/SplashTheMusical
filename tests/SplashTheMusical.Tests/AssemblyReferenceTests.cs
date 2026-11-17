using AwesomeAssertions;

namespace SplashTheMusical.Tests;

public class AssemblyReferenceTests
{
	/// <summary>The core draws flowcharts with the dagre port, curves and markers of the layout project, and nothing else.</summary>
	[Test]
	public void CoreReferencesOnlyTheRuntimeAndTheLayoutProject() =>
		ReferencedAssemblies("SplashTheMusical").Should().OnlyContain(name => IsRuntimeAssembly(name) || name == "SplashTheMusical.Layout");

	[Test]
	public void LayoutReferencesOnlyTheRuntime() =>
		ReferencedAssemblies("SplashTheMusical.Layout").Should().OnlyContain(name => IsRuntimeAssembly(name));

	[Test]
	public void CoreDoesNotReferenceRegularExpressions() =>
		ReferencedAssemblies("SplashTheMusical").Should().NotContain("System.Text.RegularExpressions");

	private static string[] ReferencedAssemblies(string assemblyName)
	{
		var assembly = System.Reflection.Assembly.Load(assemblyName);
		return [.. assembly.GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty)];
	}

	private static bool IsRuntimeAssembly(string name) =>
		name is "netstandard" or "mscorlib" || name.StartsWith("System.", StringComparison.Ordinal) || name == "System";
}
