using Dapsman.Domain;

namespace Dapsman.Infrastructure.Tests;

public sealed class ProjectResolver_IntegrationTests
{
	[Fact]
	[Trait("Category", "Integration")]
	public void Resolve_GivenWordpressTemplate_ReturnsExpected()
	{
		var dapsPath = "../../../../..";
		var wordpressPath = Path.Combine(dapsPath, "templates/wordpress");
		var projects = new List<ProjectDefinition>
		{
			new ProjectDefinition
			{
				Name = "mywpsite",
				Path = wordpressPath
			}
		};

		var config = new DapsConfig
		{
			DapsRootPath = dapsPath,
			FullYamlPath = Path.Combine(dapsPath, "daps.yaml"),
			Projects = projects
		};

		var resolver = new ProjectResolver(config);
		var wordpressProject = resolver.Resolve("mywpsite");

		Assert.NotNull(wordpressProject);
		Assert.Equal("mywpsite", wordpressProject.Definition.Name);
		Assert.Equal("/srv/projects/mywpsite/_scripts", wordpressProject.ToolkitScriptsPath);
	}
}
