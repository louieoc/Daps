namespace Dapsman.Infrastructure.Tests;

public sealed class WorkstationDockerComposeBuilderTests
{
	[Fact]
	public void BuildDockerComposeDownCommand_GivenRemoveVolumesTrue_IncludesDashV()
	{
		var builder = GetBuilder();
		var removeVolumes = true;
	
		var arguments = builder.BuildDockerComposeDownCommand(removeVolumes: removeVolumes);

		Assert.Equal("compose -f \"my project/compose_mywpsite.yaml\" -f \"my project/compose_mywpsite.dev.yaml\" down -v", arguments);
	}

	[Fact]
	public void BuildDockerComposeDownCommand_GivenRemoveVolumesFalse_DoesNotIncludeDashV()
	{
		var builder = GetBuilder();
		var removeVolumes = false;
	
		var arguments = builder.BuildDockerComposeDownCommand(removeVolumes: removeVolumes);

		Assert.Equal("compose -f \"my project/compose_mywpsite.yaml\" -f \"my project/compose_mywpsite.dev.yaml\" down", arguments);
	}

	[Fact]
	public void BuildDockerComposeUpCommand_GivenBuildImagesTrue_IncludesBuildArg()
	{
		var builder = GetBuilder();
		var buildImages = true;
	
		var arguments = builder.BuildDockerComposeUpCommand(buildImages);

		Assert.Equal("compose -f \"my project/compose_mywpsite.yaml\" -f \"my project/compose_mywpsite.dev.yaml\" up -d --build --renew-anon-volumes", arguments);
	}

	[Fact]
	public void BuildDockerComposeUpCommand_GivenBuildImagesFalse_DoesNotIncludeBuildArg()
	{
		var builder = GetBuilder();
		var buildImages = false;
	
		var arguments = builder.BuildDockerComposeUpCommand(buildImages);

		Assert.Equal("compose -f \"my project/compose_mywpsite.yaml\" -f \"my project/compose_mywpsite.dev.yaml\" up -d", arguments);
	}

	[Fact]
	public void Ctor_GivenEmptyComposeFileList_Throws()
	{
		Assert.Throws<ArgumentException>(() => new WorkstationDockerComposeBuilder(Array.Empty<string>()));
	}

	private static WorkstationDockerComposeBuilder GetBuilder()
	{
		var composeFiles = new[]
		{
			"my project/compose_mywpsite.yaml",
			"my project/compose_mywpsite.dev.yaml",
		};
		var builder = new WorkstationDockerComposeBuilder(composeFiles);
		return builder;
	}
}
