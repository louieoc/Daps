using Dapsman.Domain;

namespace Dapsman.Application.Tests;

public sealed class RemoteBuildServiceTests
{
	[Fact]
	public void CreatePlan_DelegatesToDependencies()
	{
		var checker = new FakeChecker();
		var loader = new FakeLoader();
		var planner = new FakePlanner();
		var runner = new FakeBashRunner();
		var service = new RemoteBuildService(checker, loader, planner, runner);

		var plan = service.CreatePlan("daps.yaml", new RemoteBuildOptions { DryRun = true });

		Assert.True(checker.Called);
		Assert.True(loader.Called);
		Assert.True(planner.Called);
		Assert.Equal("toolkit", plan.ToolkitContainerName);
	}

	[Fact]
	public void Execute_DelegatesToToolkitRunner()
	{
		var checker = new FakeChecker();
		var loader = new FakeLoader();
		var planner = new FakePlanner();
		var runner = new FakeBashRunner();
		var service = new RemoteBuildService(checker, loader, planner, runner);

		var plan = planner.BuildRemotePlan(new DapsConfig { DapsRootPath = ".", FullYamlPath = Path.Combine(".", "daps.yaml"), Projects = Array.Empty<ProjectDefinition>() }, new RemoteBuildOptions());
		service.Execute(plan);

		Assert.True(runner.ShellCalls.Any());
	}

	private sealed class FakeChecker : IPrerequisiteChecker
	{
		public bool Called { get; private set; }
		public void EnsureLocalBuildPrerequisites() => Called = true;
	}

	private sealed class FakeLoader : IConfigLoader
	{
		public bool Called { get; private set; }

		public DapsConfig Load(string dapsYamlPath)
		{
			Called = true;
			return new DapsConfig
			{
				DapsRootPath = ".",
				FullYamlPath = Path.Combine(".", "daps.yaml"),
				Providers =
				[
					new OpenstackProviderDefinition { Name = "dream", OpenRcPath = "./hosting/dream_openrc.sh" },
				],
				Projects = Array.Empty<ProjectDefinition>(),
			};
		}
	}

	private sealed class FakePlanner : IRemotePlanBuilder
	{
		public bool Called { get; private set; }

		public RemoteBuildPlan BuildRemotePlan(DapsConfig config, RemoteBuildOptions options)
		{
			Called = true;
			return new RemoteBuildPlan
			{
				ProviderName = "dream",
				DefaultKeyName = "daps-key-dream",
				ToolkitContainerName = "toolkit",
				OpenRcScriptHostPath = "openrc",
				SetVarsScriptHostPath = "setvars",
				CreateScriptHostPath = "create",
				OpenRcScriptContainerPath = "/srv/daps/openrc",
				SetVarsScriptContainerPath = "/srv/daps/setvars",
				CreateScriptContainerPath = "/srv/daps/create",
				ToolkitCommand = "echo ok",
			};
		}
	}
}
