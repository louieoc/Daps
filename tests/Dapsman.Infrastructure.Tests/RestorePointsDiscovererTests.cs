using Dapsman.Domain;

namespace Dapsman.Infrastructure.Tests;

public sealed class RestorePointsDiscovererTests
{
	[Fact]
	public void Discover_NoListScript_ReturnsNothing()
	{
		var project = CreateProject(sourceDirectories: [BackupSources.FromProd], withListScript: false);

		var points = new RestorePointsDiscoverer(new FakeBashRunner()).Discover(project);

		Assert.Empty(points);
	}

	[Fact]
	public void Discover_NoBackupDirectories_ReturnsNothing()
	{
		var project = CreateProject(sourceDirectories: []);

		var points = new RestorePointsDiscoverer(new FakeBashRunner()).Discover(project);

		Assert.Empty(points);
	}

	[Fact]
	public void Discover_LocalBackupsOnly_TagsThemWithTheirSourceDirectory()
	{
		var project = CreateProject(sourceDirectories: [BackupSources.FromLocal]);
		var runner = RunnerReturning(project, (BackupSources.FromLocal, [("local", "20260820_101500")]));

		var points = new RestorePointsDiscoverer(runner).Discover(project);

		Assert.Equal(BackupSources.FromLocal, Assert.Single(points).SourceDirectory);
		Assert.False(points[0].ReplacesUrls);
	}

	[Fact]
	public void Discover_BothDirectories_OrdersMostRecentFirstAcrossSources()
	{
		var project = CreateProject(sourceDirectories: [BackupSources.FromProd, BackupSources.FromLocal]);
		var runner = RunnerReturning(project,
			(BackupSources.FromProd, [("prod", "20260101_120000"), ("prod", "20260815_090000")]),
			(BackupSources.FromLocal, [("local", "20260820_101500")]));

		var points = new RestorePointsDiscoverer(runner).Discover(project);

		Assert.Equal(
			new[] { "20260820_101500", "20260815_090000", "20260101_120000" },
			points.Select(p => p.Timestamp));
	}

	[Fact]
	public void Discover_BothDirectories_RenumbersTheMergedList()
	{
		var project = CreateProject(sourceDirectories: [BackupSources.FromProd, BackupSources.FromLocal]);
		var runner = RunnerReturning(project,
			(BackupSources.FromProd, [("prod", "20260101_120000"), ("prod", "20260815_090000")]),
			(BackupSources.FromLocal, [("local", "20260820_101500")]));

		var points = new RestorePointsDiscoverer(runner).Discover(project);

		Assert.Equal(new[] { 1, 2, 3 }, points.Select(p => p.Index));
	}

	[Fact]
	public void Discover_BothDirectories_KeepsEachPointPairedWithItsOwnSource()
	{
		var project = CreateProject(sourceDirectories: [BackupSources.FromProd, BackupSources.FromLocal]);
		var runner = RunnerReturning(project,
			(BackupSources.FromProd, [("prod", "20260815_090000")]),
			(BackupSources.FromLocal, [("local", "20260820_101500")]));

		var points = new RestorePointsDiscoverer(runner).Discover(project);

		Assert.Equal(
			new[] { BackupSources.FromLocal, BackupSources.FromProd },
			points.Select(p => p.SourceDirectory));
	}
	
	[Fact]
	public void Discover_BothDirectories_SetsReplacesUrlPropertyAccurately()
	{
		var project = CreateProject(sourceDirectories: [BackupSources.FromProd, BackupSources.FromLocal]);
		var runner = RunnerReturning(project,
			(BackupSources.FromProd, [("prod", "20260103_120000"), ("prod", "20260102_090000")]),
			(BackupSources.FromLocal, [("local", "20260101_101500")])); // reverse temporal order

		var points = new RestorePointsDiscoverer(runner).Discover(project);

		Assert.Equal(
			new[] { true, true, false },
			points.Select(p => p.ReplacesUrls));
	}

	private static FakeBashRunner RunnerReturning(
		DapsProject project,
		params (string SourceDirectory, (string Env, string Timestamp)[] Points)[] bySource)
	{
		var output = bySource.ToDictionary(
			s => Path.Combine(project.WorkstationBackupsPath, s.SourceDirectory).Replace('\\', '/'),
			s => ToJson(s.Points));

		return new FakeBashRunner(output);
	}

	private static string ToJson((string Env, string Timestamp)[] points)
	{
		var entries = points.Select((p, i) =>
			$$"""{"index": {{i + 1}}, "env": "{{p.Env}}", "timestamp": "{{p.Timestamp}}", "complete": true}""");

		return $"[{string.Join(",", entries)}]";
	}

	private static DapsProject CreateProject(
		IReadOnlyList<string> sourceDirectories,
		bool withListScript = true)
	{
		var root = Path.Combine(Path.GetTempPath(), "dapsman-tests", Guid.NewGuid().ToString("N"));
		var projectRoot = Path.Combine(root, "alpha-project");
		var dockerRoot = Path.Combine(projectRoot, "_docker");
		Directory.CreateDirectory(dockerRoot);
		File.WriteAllText(Path.Combine(dockerRoot, "compose_daps_alpha.dev.yaml"), """
services:
  toolkit:
    volumes:
      - ../../alpha-project:/srv/projects/alpha
""");

		var scriptsRoot = Path.Combine(projectRoot, "_scripts");
		Directory.CreateDirectory(scriptsRoot);
		if (withListScript)
		{
			File.WriteAllText(
				Path.Combine(scriptsRoot, RestorePointsDiscoverer.ListRestorePointsScript),
				"#!/usr/bin/env bash\n");
		}

		foreach (var sourceDirectory in sourceDirectories)
		{
			Directory.CreateDirectory(Path.Combine(projectRoot, "_backups", sourceDirectory));
		}

		var config = new DapsConfig
		{
			DapsRootPath = root,
			FullYamlPath = Path.Combine(root, "daps.yaml"),
			Projects = new[] { new ProjectDefinition { Name = "alpha", Path = projectRoot } },
		};

		return new ProjectResolver(config).Resolve("alpha");
	}
}
