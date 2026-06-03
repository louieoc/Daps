namespace Dapsman.Infrastructure.Tests;

public sealed class DapsYamlEditorTests
{
    private static string TempYaml(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), "dapsman-tests", Guid.NewGuid().ToString("N"), "daps.yaml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void AddProject_AppendsToExistingProjectsSection()
    {
        var path = TempYaml("projects:\n  existing:\n    path: ../existing\n");
        new DapsYamlEditor().AddProject(path, "newsite", "../newsite");
        var yaml = File.ReadAllText(path);
        Assert.Contains("  newsite:", yaml);
        Assert.Contains("    path: ../newsite", yaml);
        Assert.Contains("  existing:", yaml);
    }


	[Fact]
	public void AddProject_WithWindowsLineEndings_StillAppendsToExistingProjectsSection()
	{
		var path = TempYaml("projects:\r\n  existing:\r\n    path: ../existing\r\n");
		new DapsYamlEditor().AddProject(path, "newsite", "../newsite");
		var yaml = File.ReadAllText(path);
		Assert.Contains("  newsite:", yaml);
		Assert.Contains("    path: ../newsite\r\n", yaml);
		Assert.Contains("  existing:", yaml);
	}

	[Fact]
    public void AddProject_CreatesProjectsSectionIfMissing()
    {
        var path = TempYaml("providers:\n  ramnode: openstack\n");
        new DapsYamlEditor().AddProject(path, "mysite", "../mysite");
        var yaml = File.ReadAllText(path);
        Assert.Contains("projects:", yaml);
        Assert.Contains("  mysite:", yaml);
    }

    [Fact]
    public void AddProject_ThrowsIfProjectAlreadyRegistered()
    {
        var path = TempYaml("projects:\n  mysite:\n    path: ../mysite\n");
        var ex = Assert.Throws<InvalidOperationException>(
            () => new DapsYamlEditor().AddProject(path, "mysite", "../mysite"));
        Assert.Contains("already registered", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RemoveProject_RemovesKeyAndSubProperties()
    {
        var path = TempYaml("projects:\n  alpha:\n    path: ../alpha\n  beta:\n    path: ../beta\n  gamma:\n    path: ../gamma\n");
        new DapsYamlEditor().RemoveProject(path, "beta");
        var yaml = File.ReadAllText(path);
        Assert.DoesNotContain("  beta:", yaml);
        Assert.DoesNotContain("../beta", yaml);
        Assert.Contains("  alpha:", yaml);
        Assert.Contains("  gamma:", yaml);
    }

    [Fact]
    public void RemoveProject_PreservesBlankLineSeparators()
    {
        var path = TempYaml("projects:\n  alpha:\n    path: ../alpha\n\n  beta:\n    path: ../beta\n\n  gamma:\n    path: ../gamma\n");
        new DapsYamlEditor().RemoveProject(path, "beta");
        var yaml = File.ReadAllText(path);
        Assert.DoesNotContain("  beta:", yaml);
        Assert.Contains("  alpha:", yaml);
        Assert.Contains("  gamma:", yaml);
    }

    [Fact]
    public void RemoveProject_WorksForLastEntry()
    {
        var path = TempYaml("projects:\n  alpha:\n    path: ../alpha\n  beta:\n    path: ../beta\n");
        new DapsYamlEditor().RemoveProject(path, "beta");
        var yaml = File.ReadAllText(path);
        Assert.DoesNotContain("beta", yaml);
        Assert.Contains("  alpha:", yaml);
    }

    [Fact]
    public void RemoveProject_ThrowsIfProjectNotFound()
    {
        var path = TempYaml("projects:\n  alpha:\n    path: ../alpha\n");
        Assert.Throws<InvalidOperationException>(
            () => new DapsYamlEditor().RemoveProject(path, "missing"));
    }
}
