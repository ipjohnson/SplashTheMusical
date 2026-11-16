using System.Runtime.CompilerServices;

namespace SplashTheMusical.Cli;

/// <summary>
/// A reflection call ILC cannot resolve. It is compiled only with <c>-p:StmReflectionProbe=true</c>, and CI fails when the
/// native publish with it succeeds, which shows that ILC's warnings fail the build.
/// </summary>
internal static class ReflectionProbe
{
	[ModuleInitializer]
	internal static void Probe() => _ = Type.GetType(Environment.GetEnvironmentVariable("STM_PROBE_TYPE") ?? "")?.GetMethods();
}
