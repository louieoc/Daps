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
        var compose = new FakeComposeExecutor();
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
        var compose = new FakeComposeExecutor();
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
        var compose = new FakeComposeExecutor();
        var runner = new FakeBashRunner();
        var service = new LocalBuildService(loader, builder, caddySync, compose, runner);

        var plan = builder.BuildLocalPlan(new LocalBuildOptions());

        service.ExecuteDapsCompose(plan, new LocalBuildOptions());

        Assert.True(compose.Called);
    }

    [Fact]
    public void ExecuteProjectComposeDown_RemovesVolumes()
    {
        var loader = new FakeLoader();
        var builder = new FakeBuilder();
        var caddySync = new FakeCaddySync();
        var compose = new FakeComposeExecutor();
        var runner = new FakeBashRunner();
        var service = new LocalBuildService(loader, builder, caddySync, compose, runner);

        var projectPlan = new LocalProjectComposePlan
        {
            ProjectName = "myproject",
            ProjectPath = "..\\myproject",
            ComposeFiles = new[] { Path.Combine("_docker", "compose_myproject.yaml") },
            PrerequisiteScripts = Array.Empty<string>(),
        };

        service.ExecuteProjectComposeDown(projectPlan);

        Assert.True(compose.DownCalled);
        Assert.True(compose.DownRemovedVolumes);
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
            return new LocalBuildPlan
            {
                DapsComposeFiles = new[] { Path.Combine("docker", "compose_daps.yaml") },
                ProjectComposePlans = Array.Empty<LocalProjectComposePlan>(),
                CaddySync = new LocalCaddySyncPlan
                {
                    RuntimeSitesPath = "caddy_sites",
                    FilesToCopy = Array.Empty<LocalCaddySiteCopyPlan>(),
                    ShouldCreatePlaceholder = true,
                    PlaceholderFilePath = Path.Combine("caddy_sites", "000-empty.dev.caddy"),
                },
                Warnings = Array.Empty<string>(),
                HasProjectsConfigured = false,
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

    private sealed class FakeComposeExecutor : IDockerComposeExecutor
    {
        public bool Called { get; private set; }
        public bool DownCalled { get; private set; }
        public bool DownRemovedVolumes { get; private set; }

        public void RunComposeUp(IReadOnlyList<string> composeFiles, bool buildImages, string workingDirectory)
        {
            Called = true;
        }

        public void RunComposeDown(IReadOnlyList<string> composeFiles, bool removeVolumes, string workingDirectory)
        {
            DownCalled = true;
            DownRemovedVolumes = removeVolumes;
        }
    }
}
