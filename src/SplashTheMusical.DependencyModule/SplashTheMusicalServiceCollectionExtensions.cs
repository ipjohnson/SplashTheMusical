using DependencyModules.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace SplashTheMusical.DependencyModule;

/// <summary>Registers STM for hosts that do not use DependencyModules, and configures its options.</summary>
public static class SplashTheMusicalServiceCollectionExtensions
{
	/// <summary>Adds <see cref="SplashTheMusicalModule"/>, then <paramref name="configure"/> when it is given.</summary>
	/// <param name="configure">Returns the options from the options so far, such as <c>options => options with { ... }</c>.</param>
	public static IServiceCollection AddSplashTheMusical(this IServiceCollection services, Func<MermaidOptions, MermaidOptions>? configure = null)
	{
		services.AddModule<SplashTheMusicalModule>();
		return configure is null ? services : services.ConfigureMermaid(configure);
	}

	/// <summary>
	/// Adds a step to the <see cref="MermaidOptions"/> the module registers. The steps run in the order they were added,
	/// from <see cref="MermaidOptions.Default"/>, and may be added before or after the module. A <see cref="MermaidOptions"/>
	/// the host registers itself replaces them.
	/// </summary>
	public static IServiceCollection ConfigureMermaid(this IServiceCollection services, Func<MermaidOptions, MermaidOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);
		return services.AddSingleton(new MermaidOptionsConfiguration(configure));
	}
}

/// <summary>A step <see cref="SplashTheMusicalServiceCollectionExtensions.ConfigureMermaid"/> adds.</summary>
internal sealed class MermaidOptionsConfiguration(Func<MermaidOptions, MermaidOptions> configure)
{
	public MermaidOptions Apply(MermaidOptions options) =>
		configure(options) ?? throw new InvalidOperationException("A ConfigureMermaid step returned null.");
}
