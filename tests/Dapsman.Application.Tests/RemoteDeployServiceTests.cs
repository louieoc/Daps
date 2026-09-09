using Dapsman.Domain;

namespace Dapsman.Application.Tests;

public sealed partial class RemoteDeployServiceTests
{
	[Fact]
	public void CreatePlan_DelegatesToDependencies()
	{
		var loader = new FakeLoader();
		var planner = new FakePlanner();
		var executor = new FakeExecutor();
		var service = new RemoteDeployService(loader, planner, executor);

		var plan = service.CreatePlan("daps.yaml", new RemoteDeployOptions { DryRun = true });

		Assert.True(loader.Called);
		Assert.True(planner.Called);
		Assert.Equal("10.0.0.5", plan.RemoteHost);
	}

	[Fact]
	public void ExecuteBuildImageScripts_DelegatesToExecutor()
	{
		var loader = new FakeLoader();
		var planner = new FakePlanner();
		var executor = new FakeExecutor();
		var service = new RemoteDeployService(loader, planner, executor);

		var plan = planner.BuildRemoteDeployPlan(
			new DapsConfig
			{
				DapsRootPath = ".",
				FullYamlPath = Path.Combine(".", "daps.yaml"),
			}, new RemoteDeployOptions());
		service.ExecuteBuildImageScripts(plan.BuildImageCommands, plan.DapsRootPath, "linux/amd64");

		Assert.True(executor.BuildImagesCalled);
	}

	[Fact]
	public void ExecuteBuildImageScripts_PassesTheTargetPlatformThrough()
	{
		var loader = new FakeLoader();
		var planner = new FakePlanner();
		var executor = new FakeExecutor();
		var service = new RemoteDeployService(loader, planner, executor);

		var plan = planner.BuildRemoteDeployPlan(
			new DapsConfig
			{
				DapsRootPath = ".",
				FullYamlPath = Path.Combine(".", "daps.yaml"),
			}, new RemoteDeployOptions());
		service.ExecuteBuildImageScripts(plan.BuildImageCommands, plan.DapsRootPath, "linux/arm64");

		Assert.Equal("linux/arm64", executor.BuildImagesPlatform);
	}

	private sealed class FakeLoader : IDapsConfigLoader
	{
		public bool Called { get; private set; }

		public DapsConfig Load(string dapsYamlPath)
		{
			Called = true;
			return new DapsConfig
			{
				DapsRootPath = ".",
				FullYamlPath = Path.Combine(".", "daps.yaml")
			};
		}
	}

	private sealed class FakePlanner : IRemoteDeployPlanBuilder
	{
		public bool Called { get; private set; }

		public RemoteDeployPlan BuildRemoteDeployPlan(DapsConfig config, RemoteDeployOptions options)
		{
			Called = true;
			var buildImageCommands = new[]
				{
					new BuildImageCommandPlan
					{
						ProjectName = "a",
						ToolkitScriptPath = "/srv/projects/a/_scripts/build-docker-images.toolkit.sh",
						HasExistingExports = false,
					},
					new BuildImageCommandPlan
					{
						ProjectName = "b",
						ToolkitScriptPath = "/srv/projects/b/_scripts/build-docker-images.toolkit.sh",
						HasExistingExports = true,
					},
				};

			return new RemoteDeployPlan
			{
				DapsRootPath = ".",
				ProviderName = "ramnode",
				ToolkitContainerName = "daps-toolkit-1",
				RemoteHost = "10.0.0.5",
				RemoteUser = "root",
				SshKeyName = "daps-key-ramnode",
				IsRoot = true,
				BuildImageCommands = buildImageCommands,
				SelectedBuildImageCommands = buildImageCommands,
				CaddyfileSourcePath = "caddy/Caddyfile",
				CaddySiteFilesToUpload = Array.Empty<CaddyUploadPlan>(),
				ExpectedProdCaddyFileNames = Array.Empty<string>(),
				DapsComposeFilesToUpload = Array.Empty<string>(),
				ProjectPlans = Array.Empty<RemoteProjectDeployPlan>(),
				Warnings = Array.Empty<string>(),
			};
		}
	}

	private sealed class FakeExecutor : IRemoteDeployExecutor
	{
		public bool BuildImagesCalled { get; private set; }
		public bool Called { get; private set; }

		public void ExecuteBuildImages(IReadOnlyList<BuildImageCommandPlan> commands, string dapsRootPath, string? targetPlatform)
		{
			BuildImagesCalled = true;
			BuildImagesPlatform = targetPlatform;
		}

		public string? BuildImagesPlatform { get; private set; }

		public void Execute(RemoteDeployPlan plan)
		{
			Called = true;
		}
	}
}
