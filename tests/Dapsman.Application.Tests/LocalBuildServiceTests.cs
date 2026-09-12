using Dapsman.Domain;

namespace Dapsman.Application.Tests;

public sealed class LocalBuildServiceTests
{
	[Fact]
	public void CreatePlan_DelegatesToDependencies()
	{
		var loader = new FakeLoader();
		var builder = new FakeBuilder();
		var caddySync = new FakeCaddySync();
		var compose = new FakeDockerExecutor();
		var runner = new FakeBashRunner();
		var service = new LocalBuildService(loader, builder, caddySync, compose, runner);

		var plan = service.CreatePlan("daps.yaml", new LocalBuildOptions { DryRun = true });

		Assert.True(loader.Called);
		Assert.True(builder.Called);
		Assert.Single(plan.DapsComposeFiles);
	}

	[Fact]
	public void SyncCaddySites_DelegatesToCaddySync()
	{
		var loader = new FakeLoader();
		var builder = new FakeBuilder();
		var caddySync = new FakeCaddySync();
		var compose = new FakeDockerExecutor();
		var runner = new FakeBashRunner();
		var service = new LocalBuildService(loader, builder, caddySync, compose, runner);

		var plan = builder.BuildLocalPlan(new LocalBuildOptions());

		service.SyncCaddySites(plan);

		Assert.True(caddySync.Called);
	}

	[Fact]
	public void ExecuteDapsCompose_DelegatesToComposeExecutor()
	{
		var loader = new FakeLoader();
		var builder = new FakeBuilder();
		var caddySync = new FakeCaddySync();
		var compose = new FakeDockerExecutor();
		var runner = new FakeBashRunner();
		var service = new LocalBuildService(loader, builder, caddySync, compose, runner);

		var plan = builder.BuildLocalPlan(new LocalBuildOptions());

		service.ExecuteDapsCompose(plan);

		Assert.True(compose.Called);
	}

	[Fact]
	public void EnsureSharedNetwork_DelegatesToComposeExecutor()
	{
		var loader = new FakeLoader();
		var builder = new FakeBuilder();
		var caddySync = new FakeCaddySync();
		var compose = new FakeDockerExecutor();
		var runner = new FakeBashRunner();
		var service = new LocalBuildService(loader, builder, caddySync, compose, runner);

		var plan = builder.BuildLocalPlan(new LocalBuildOptions());

		service.EnsureSharedNetwork(plan, ".");

		Assert.Equal("daps_net", compose.EnsuredNetworkName);
	}

	[Fact]
	public void ExecuteProjectComposeDown_ExecutesTheDownCommand()
	{
		var loader = new FakeLoader();
		var builder = new FakeBuilder();
		var caddySync = new FakeCaddySync();
		var compose = new FakeDockerExecutor();
		var runner = new FakeBashRunner();
		var service = new LocalBuildService(loader, builder, caddySync, compose, runner);

		var projectPlan = new LocalProjectComposePlan
		{
			ProjectName = "myproject",
			ProjectPath = "..\\myproject",
			ComposeFiles = new[] { Path.Combine("_docker", "compose_myproject.yaml") },
			PrerequisiteScripts = Array.Empty<string>(),
			ComposeUpCommand = "I'm the up command",
			ComposeDownCommand = "I'm the down command"
		};

		service.ExecuteProjectComposeDown(projectPlan);

		Assert.Equal(projectPlan.ComposeDownCommand, compose.LastArguments);
	}

	[Fact]
	public void ExecutePrerequisites_PassesProjectNameAsEnvVar()
	{
		var loader = new FakeLoader();
		var builder = new FakeBuilder();
		var caddySync = new FakeCaddySync();
		var compose = new FakeDockerExecutor();
		var runner = new FakeBashRunner();
		var service = new LocalBuildService(loader, builder, caddySync, compose, runner);

		var projectPlan = new LocalProjectComposePlan
		{
			ProjectName = "myproject",
			ProjectPath = "..\\myproject",
			ComposeFiles = new[] { Path.Combine("_docker", "compose_myproject.yaml") },
			PrerequisiteScripts = new[] { Path.Combine("_scripts", "prerequisites.dev.sh") },
			PrerequisiteScriptEnvVars = new Dictionary<string, string> { ["DAPS_PROJECT"] = "myproject" },
			ComposeUpCommand = "I'm the up command"
		};

		service.ExecutePrerequisites(projectPlan);

		var call = Assert.Single(runner.ScriptCalls);
		Assert.NotNull(call.Env);
		Assert.Equal("myproject", call.Env!["DAPS_PROJECT"]);
	}

	private sealed class FakeLoader : IDapsConfigLoader
	{
		public bool Called { get; private set; }

		public DapsConfig Load(string dapsYamlPath)
		{
			Called = true;
			return new DapsConfig { DapsRootPath = ".", FullYamlPath = ".\\daps.yaml", Projects = Array.Empty<ProjectDefinition>() };
		}
	}

	private sealed class FakeBuilder : ILocalBuildPlanBuilder
	{
		public bool Called { get; private set; }

		public LocalBuildPlan BuildLocalPlan(LocalBuildOptions options)
		{
			Called = true;
			var composeFiles = new[] { Path.Combine("docker", "compose_daps.yaml") };
			return new LocalBuildPlan
			{
				DapsComposeFiles = composeFiles,
				ProjectComposePlans = Array.Empty<LocalProjectComposePlan>(),
				CaddySync = new LocalCaddySyncPlan
				{
					RuntimeSitesPath = "caddy_sites",
					FilesToCopy = Array.Empty<LocalCaddySiteCopyPlan>(),
					ShouldCreatePlaceholder = true,
					PlaceholderFilePath = Path.Combine("caddy_sites", "000-empty.dev.caddy"),
				},
				SharedNetworkName = "daps_net",
				Warnings = Array.Empty<string>(),
				HasProjectsConfigured = false,
				DapsComposeCommand = "compose -f \"caddy_sites\\compose_daps.yaml\" down -v"
			};
		}
	}

	private sealed class FakeCaddySync : ICaddySiteSync
	{
		public bool Called { get; private set; }

		public void SyncLocalSites(LocalCaddySyncPlan plan)
		{
			Called = true;
		}
	}
}
