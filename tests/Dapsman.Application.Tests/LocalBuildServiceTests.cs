using Dapsman.Domain;

namespace Dapsman.Application.Tests;

public sealed class LocalBuildServiceTests
{
    [Fact]
    public void CreatePlan_DelegatesToDependencies()
    {
        var checker = new FakeChecker();
        var loader = new FakeLoader();
        var builder = new FakeBuilder();
        var caddySync = new FakeCaddySync();
        var compose = new FakeComposeExecutor();
        var runner = new FakeBashRunner();
        var service = new LocalBuildService(checker, loader, builder, caddySync, compose, runner);

        var plan = service.CreatePlan("daps.yaml", new LocalBuildOptions { DryRun = true });

        Assert.True(checker.Called);
        Assert.True(loader.Called);
        Assert.True(builder.Called);
        Assert.Single(plan.DapsComposeFiles);
    }

    [Fact]
    public void SyncCaddySites_DelegatesToCaddySync()
    {
        var checker = new FakeChecker();
        var loader = new FakeLoader();
        var builder = new FakeBuilder();
        var caddySync = new FakeCaddySync();
        var compose = new FakeComposeExecutor();
        var runner = new FakeBashRunner();
        var service = new LocalBuildService(checker, loader, builder, caddySync, compose, runner);

        var plan = builder.BuildLocalPlan(new LocalBuildOptions());

        service.SyncCaddySites(plan);

        Assert.True(caddySync.Called);
    }

    [Fact]
    public void ExecuteDapsCompose_DelegatesToComposeExecutor()
    {
        var checker = new FakeChecker();
        var loader = new FakeLoader();
        var builder = new FakeBuilder();
        var caddySync = new FakeCaddySync();
        var compose = new FakeComposeExecutor();
        var runner = new FakeBashRunner();
        var service = new LocalBuildService(checker, loader, builder, caddySync, compose, runner);

        var plan = builder.BuildLocalPlan(new LocalBuildOptions());

        service.ExecuteDapsCompose(plan, new LocalBuildOptions());

        Assert.True(compose.Called);
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
            return new DapsConfig { DapsRootPath = ".", FullYamlPath = ".\\daps.yaml", Projects = Array.Empty<ProjectDefinition>() };
        }
    }

    private sealed class FakeBuilder : ILocalPlanBuilder
    {
        public bool Called { get; private set; }

        public LocalBuildPlan BuildLocalPlan(LocalBuildOptions options)
        {
            Called = true;
            return new LocalBuildPlan
            {
                DapsComposeFiles = new[] { Path.Combine("docker", "compose_daps.yaml") },
                ProjectComposePlans = Array.Empty<ProjectComposePlan>(),
                CaddySync = new CaddySyncPlan
                {
                    RuntimeSitesPath = "caddy_sites",
                    FilesToCopy = Array.Empty<CaddySiteCopyPlan>(),
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

        public void SyncDevSites(CaddySyncPlan plan)
        {
            Called = true;
        }
    }

    private sealed class FakeComposeExecutor : IComposeExecutor
    {
        public bool Called { get; private set; }

        public void RunComposeUp(IReadOnlyList<string> composeFiles, bool buildImages, string workingDirectory)
        {
            Called = true;
        }
    }
}
