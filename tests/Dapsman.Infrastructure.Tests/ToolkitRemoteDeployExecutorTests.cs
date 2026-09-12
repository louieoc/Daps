using Dapsman.Domain;

namespace Dapsman.Infrastructure.Tests;

public sealed class ToolkitRemoteDeployExecutorTests
{
	[Fact]
	public void ExecuteBuildImages_PassesTheProjectNameToEachScript()
	{
		var runner = new FakeBashRunner();
		var executor = new ToolkitRemoteDeployExecutor(runner);

		executor.ExecuteBuildImages(
			[
				Command("alpha"),
				Command("beta"),
			],
			dapsRootPath: "/srv/daps",
			targetPlatform: null);

		Assert.Collection(
			runner.ScriptRuns,
			run => Assert.Equal("alpha", run.Env!["DAPS_PROJECT"]),
			run => Assert.Equal("beta", run.Env!["DAPS_PROJECT"]));

		// Absent, not empty: a script that reads it should see it unset for a local-native build.
		Assert.DoesNotContain("DAPS_TARGET_PLATFORM", runner.ScriptRuns[0].Env!.Keys);
	}

	[Fact]
	public void ExecuteBuildImages_PassesTheTargetPlatformAlongsideTheProjectName()
	{
		var runner = new FakeBashRunner();
		var executor = new ToolkitRemoteDeployExecutor(runner);

		executor.ExecuteBuildImages([Command("alpha")], "/srv/daps", "linux/amd64");

		var env = Assert.Single(runner.ScriptRuns).Env!;
		Assert.Equal("alpha", env["DAPS_PROJECT"]);
		Assert.Equal("linux/amd64", env["DAPS_TARGET_PLATFORM"]);
	}

	private static BuildImageCommandPlan Command(string projectName) => new()
	{
		ProjectName = projectName,
		ToolkitScriptPath = $"/srv/projects/{projectName}/_scripts/build-docker-images.toolkit.sh",
		HasExistingExports = false,
	};
}
