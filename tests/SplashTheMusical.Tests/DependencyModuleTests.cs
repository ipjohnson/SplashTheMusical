using AwesomeAssertions;
using DependencyModules.Runtime;
using Microsoft.Extensions.DependencyInjection;
using SplashTheMusical.DependencyModule;
using SplashTheMusical.Diagrams;
using SplashTheMusical.Icons;
using SplashTheMusical.Tests.Rendering;
using SplashTheMusical.Text;
using SplashTheMusical.Themes;
using static SplashTheMusical.Tests.MermaidTests;

namespace SplashTheMusical.Tests;

public class DependencyModuleTests
{
	[Test]
	public void AddModuleRegistersAMermaidThatRendersADiagram()
	{
		using var provider = Build(services => services.AddModule<SplashTheMusicalModule>());

		var mermaid = provider.GetRequiredService<IMermaid>();

		SvgDocuments.Checked(mermaid.RenderSvg("info")).Should().Be(Mermaid.Default.RenderSvg("info"));
		provider.GetRequiredService<IMermaid>().Should().BeSameAs(mermaid);
		provider.GetRequiredService<IMermaidParser>().Should().BeSameAs(mermaid);
		provider.GetRequiredService<IMermaidRenderer>().Should().BeSameAs(mermaid);
		provider.GetRequiredService<IMermaidWriter>().Should().BeSameAs(mermaid);
		provider.GetRequiredService<ITextMeasurer>().Should().BeSameAs(TextMeasurer.Default);
		provider.GetRequiredService<IIconProvider>().Should().BeSameAs(IconRegistry.Default);
		provider.GetRequiredService<IThemeProvider>().Should().BeSameAs(MermaidThemes.Default);
		provider.GetServices<IDiagramModule>().Should().Equal(Mermaid.CoreModules);
		provider.GetRequiredService<MermaidOptions>().Should().Be(MermaidOptions.Default);
	}

	[Test]
	public void AddSplashTheMusicalRegistersAMermaidThatRendersADiagram()
	{
		using var provider = Build(services => services.AddSplashTheMusical());

		SvgDocuments.Checked(provider.GetRequiredService<IMermaid>().RenderSvg("info")).Should().Be(Mermaid.Default.RenderSvg("info"));
	}

	[Test]
	public void LoadingTheModuleAgainAddsNoRegistrations()
	{
		var services = new ServiceCollection();
		services.AddModule<SplashTheMusicalModule>();
		var count = services.Count;

		services.AddModule<SplashTheMusicalModule>();
		services.AddSplashTheMusical();

		services.Count.Should().Be(count);
	}

	[Test]
	public void AMeasurerRegisteredBeforeTheModuleReplacesTheDefault()
	{
		var measurer = new FixedMeasurer(123);
		using var provider = Build(services => services
			.AddSingleton<ITextMeasurer>(measurer)
			.AddSingleton<IDiagramModule>(new BoxModule())
			.AddModule<SplashTheMusicalModule>());

		provider.GetServices<ITextMeasurer>().Should().ContainSingle().Which.Should().BeSameAs(measurer);
		AssertBoxIsMeasuredBy(provider, measurer);
	}

	[Test]
	public void AMeasurerRegisteredAfterTheModuleReplacesTheDefault()
	{
		var measurer = new FixedMeasurer(123);
		using var provider = Build(services => services
			.AddModule<SplashTheMusicalModule>()
			.AddSingleton<IDiagramModule>(new BoxModule())
			.AddSingleton<ITextMeasurer>(measurer));

		AssertBoxIsMeasuredBy(provider, measurer);
	}

	[Test]
	public void AHostDiagramModuleIsAddedToTheCoreModules()
	{
		var box = new BoxModule();
		using var provider = Build(services => services.AddSplashTheMusical().AddSingleton<IDiagramModule>(box));

		var mermaid = provider.GetRequiredService<IMermaid>();

		provider.GetServices<IDiagramModule>().Should().Equal([.. Mermaid.CoreModules, box]);
		mermaid.Parse("box Hello").Should().BeOfType<BoxDocument>().Which.Label.Should().Be("Hello");
		SvgDocuments.Checked(mermaid.RenderSvg("box Hello")).Should().Contain("aria-roledescription=\"box\"");
		mermaid.RenderSvg("info").Should().Be(Mermaid.Default.RenderSvg("info"));
	}

	[Test]
	public void OptionsStepsRunInOrderBeforeAndAfterTheModule()
	{
		using var provider = Build(services => services
			.ConfigureMermaid(options => options with { LockedKeys = ["theme"] })
			.AddSplashTheMusical(options => options with { Limits = options.Limits with { MaxInputLength = 4 } })
			.ConfigureMermaid(options => options with { LockedKeys = [.. options.LockedKeys!, "fontSize"] }));

		var options = provider.GetRequiredService<MermaidOptions>();
		options.LockedKeys.Should().Equal("theme", "fontSize");
		options.Limits.MaxInputLength.Should().Be(4);
		provider.GetRequiredService<IMermaid>().Invoking(mermaid => mermaid.RenderSvg("info\n")).Should().Throw<MermaidResourceLimitException>();
	}

	[Test]
	public void AHostMermaidOptionsReplacesTheSteps()
	{
		var host = new MermaidOptions { LockedKeys = ["theme"] };
		using var provider = Build(services => services
			.AddSplashTheMusical(options => options with { LockedKeys = [] })
			.AddSingleton(host));

		provider.GetRequiredService<MermaidOptions>().Should().BeSameAs(host);
	}

	[Test]
	public void TheCoreProjectHasNoPackageReference()
	{
		var project = File.ReadAllText(Path.Combine(Repository(), "src", "SplashTheMusical", "SplashTheMusical.csproj"));

		project.Should().NotContain("PackageReference");
	}

	private static void AssertBoxIsMeasuredBy(ServiceProvider provider, FixedMeasurer measurer)
	{
		var svg = SvgDocuments.Checked(provider.GetRequiredService<IMermaid>().RenderSvg("box Hello"));

		svg.Should().Contain("<rect width=\"123\" height=\"20\"/>");
		measurer.Calls.Should().Be(1);
	}

	private static ServiceProvider Build(Action<IServiceCollection> register)
	{
		var services = new ServiceCollection();
		register(services);
		return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
	}

	private static string Repository()
	{
		for (var folder = AppContext.BaseDirectory; folder is not null; folder = Path.GetDirectoryName(folder))
		{
			if (File.Exists(Path.Combine(folder, "SplashTheMusical.slnx")))
			{
				return folder;
			}
		}

		throw new InvalidOperationException("The tests run outside the repository.");
	}
}
