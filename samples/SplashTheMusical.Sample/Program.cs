using DependencyModules.Runtime;
using Microsoft.Extensions.DependencyInjection;
using SplashTheMusical;
using SplashTheMusical.DependencyModule;

var services = new ServiceCollection();
services.AddModule<SplashTheMusicalModule>();
using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

var svg = provider.GetRequiredService<IMermaid>().RenderSvg("info");
if (!svg.StartsWith("<svg", StringComparison.Ordinal) || !svg.Contains("aria-roledescription=\"info\"", StringComparison.Ordinal))
{
	Console.Error.WriteLine("The info diagram did not render.");
	return 1;
}

Console.WriteLine(svg);
return 0;
