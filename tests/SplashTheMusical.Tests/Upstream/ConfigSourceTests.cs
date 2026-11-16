using AwesomeAssertions;
using SplashTheMusical.Upstream;

namespace SplashTheMusical.Tests.Upstream;

public class ConfigSourceTests
{
	private const string Schema = """
		$defs:
		  BaseDiagramConfig:
		    type: object
		    properties:
		      useMaxWidth:
		        type: boolean
		        default: true
		  PieDiagramConfig:
		    type: object
		    allOf:
		      - $ref: '#/$defs/BaseDiagramConfig'
		    properties:
		      textPosition:
		        type: number
		        default: 0.75
		type: object
		properties:
		  pie:
		    $ref: '#/$defs/PieDiagramConfig'
		  gantt:
		    $ref: '#/$defs/BaseDiagramConfig'

		""";

	[Test]
	public void AMermaid12CheckoutReadsTheSchemaAndTheBuildsDiagramKeys()
	{
		using var fixture = new UpstreamFixture();
		fixture.Write(ConfigSource.V12SchemaPath, Schema);
		fixture.Write(ConfigSource.V12BuildPath, "export const MERMAID_CONFIG_DIAGRAM_KEYS = ['pie', 'gantt'] as const;\n");

		var source = ConfigSource.Read(fixture.Root);

		source.SchemaPath.Should().Be(ConfigSource.V12SchemaPath);
		source.DiagramKeys.Should().Equal("pie", "gantt");
	}

	[Test]
	public void AMermaid13CheckoutReadsCoresSchemaAndTheSectionsItsDiagramPackagesOwn()
	{
		using var fixture = new UpstreamFixture();
		fixture.Write(ConfigSource.V13SchemaPath, Schema);
		WritePackage(fixture, "pie", configDefaults: true, """{ "plugin": "diagram", "id": "pie" }""");
		WritePackage(fixture, "gantt", configDefaults: false, """{ "plugin": "diagram", "id": "gantt" }""");
		WritePackage(fixture, "wardley", configDefaults: true, """{ "plugin": "diagram", "id": "wardley", "section": "wardley-beta" }""");

		var source = ConfigSource.Read(fixture.Root);

		source.SchemaPath.Should().Be(ConfigSource.V13SchemaPath);
		source.DiagramKeys.Should().Equal(["pie"], "gantt owns no defaults, and the schema has no wardley-beta or swimlane");
		var pie = source.Schema.DefaultConfig(source.DiagramKeys, v12: false)["pie"]!;
		pie["textPosition"]!.GetValue<double>().Should().Be(0.75);
		pie["useMaxWidth"]!.GetValue<bool>().Should().BeTrue();
	}

	[Test]
	public void DiagramKeysReadsTheManifestSectionBeforeItsId()
	{
		using var fixture = new UpstreamFixture();
		WritePackage(fixture, "wardley", configDefaults: true, """{ "plugin": "diagram", "id": "wardley", "section": "wardley-beta" }""");
		WritePackage(fixture, "xychart", configDefaults: true, """{ "plugin": "diagram", "id": "xychart", "section": "xyChart" }""");
		WritePackage(fixture, "info", configDefaults: true, """{ "plugin": "renderer", "id": "info" }""");

		DiagramKeys.ReadPackages(fixture.Root).Should().Equal("wardley-beta", "xyChart", "swimlane");
	}

	[Test]
	public void ACheckoutWithoutASchemaFails()
	{
		using var fixture = new UpstreamFixture();

		var read = () => ConfigSource.Read(fixture.Root);

		read.Should().Throw<FileNotFoundException>().WithMessage("*has neither*");
	}

	private static void WritePackage(UpstreamFixture fixture, string name, bool configDefaults, string manifest)
	{
		fixture.Write($"packages/diagram-{name}/workspace.yaml", $"# wiring\nconfigDefaults: {(configDefaults ? "true" : "false")}\n");
		fixture.Write($"packages/diagram-{name}/package.json", $$"""{ "name": "@mermaid-js/diagram-{{name}}", "mermaid": {{manifest}} }""");
	}
}
