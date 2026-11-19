using DependencyModules.Runtime.Attributes;
using DependencyModules.Runtime.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SplashTheMusical.Diagrams;
using SplashTheMusical.Icons;
using SplashTheMusical.Text;
using SplashTheMusical.Themes;

namespace SplashTheMusical.DependencyModule;

/// <summary>
/// Registers <see cref="IMermaid"/>, its three services and its extension points as singletons. Every registration is
/// a <c>TryAdd</c>, so a host's own registration wins whether it comes before or after the module.
/// </summary>
/// <remarks>
/// The <see cref="Mermaid"/> instance takes <see cref="MermaidOptions"/>, <see cref="ITextMeasurer"/>,
/// <see cref="IIconProvider"/> and <see cref="IThemeProvider"/> from the container, every <see cref="ILayoutEngine"/>,
/// and every <see cref="IDiagramModule"/> besides <see cref="Mermaid.CoreModules"/>.
/// </remarks>
[DependencyModule]
public partial class SplashTheMusicalModule : IServiceCollectionConfiguration
{
	public void ConfigureServices(IServiceCollection services)
	{
		services.TryAddSingleton<MermaidOptions>(CreateOptions);
		services.TryAddSingleton<ITextMeasurer>(TextMeasurer.Default);
		services.TryAddSingleton<IIconProvider>(IconRegistry.Default);
		services.TryAddSingleton<IThemeProvider>(MermaidThemes.Default);
		// TryAddEnumerable keeps one registration per implementation type, and two core modules share FlowchartModule.
		foreach (var module in Mermaid.CoreModules)
		{
			if (!services.Any(descriptor => descriptor.ServiceType == typeof(IDiagramModule) && ReferenceEquals(descriptor.ImplementationInstance, module)))
			{
				services.Add(ServiceDescriptor.Singleton<IDiagramModule>(module));
			}
		}

		services.TryAddSingleton<IMermaid>(CreateMermaid);
		services.TryAddSingleton<IMermaidParser>(provider => provider.GetRequiredService<IMermaid>());
		services.TryAddSingleton<IMermaidRenderer>(provider => provider.GetRequiredService<IMermaid>());
		services.TryAddSingleton<IMermaidWriter>(provider => provider.GetRequiredService<IMermaid>());
	}

	private static MermaidOptions CreateOptions(IServiceProvider provider) =>
		provider.GetServices<MermaidOptionsConfiguration>().Aggregate(MermaidOptions.Default, (options, configuration) => configuration.Apply(options));

	private static Mermaid CreateMermaid(IServiceProvider provider) =>
		new(
			provider.GetRequiredService<MermaidOptions>(),
			provider.GetServices<IDiagramModule>().Where(module => !Mermaid.CoreModules.Contains(module)),
			provider.GetRequiredService<ITextMeasurer>(),
			provider.GetRequiredService<IIconProvider>(),
			provider.GetServices<ILayoutEngine>(),
			provider.GetRequiredService<IThemeProvider>());
}
