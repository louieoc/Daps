using Dapsman.Domain;

namespace Dapsman.Application.Tests;

public sealed class TemplateManagerTests
{
    [Fact]
    public void ApplyDockerDevPortChanges_WorksAsExpected()
    {
        var content = @"
services:
  db:
    ports:
      - ""127.0.0.1:3306:3306""

  phpmyadmin:
    ports:
      - ""127.0.0.1:8082:80""
    networks:
      - mywpsite_net

  wordpress:
    build:
      context: ..
    ports:
      - ""127.0.0.1:8080:80""
    volumes:
      - ../wp-content:/var/www/html/wp-content
        ";

        var changes = new List<PortAssignment>
        {
            new PortAssignment(3306, 3307, "already taken by another project"),
            new PortAssignment(8080, 8082, "already taken by another project")
        };

        var expected = @"
services:
  db:
    ports:
      - ""127.0.0.1:3307:3306""

  phpmyadmin:
    ports:
      - ""127.0.0.1:8082:80""
    networks:
      - mywpsite_net

  wordpress:
    build:
      context: ..
    ports:
      - ""127.0.0.1:8082:80""
    volumes:
      - ../wp-content:/var/www/html/wp-content
        ";

        var actual = TemplateManager.ApplyDockerDevPortChanges(content, changes);

        Assert.Equal(expected, actual);
    }

	[Fact]
	public void CreateProjectFromTemplate_ReplacesPlaceholderInFileContents()
	{
		var templateDir = TestHelper.CreateTempDirectory();
		var destDir = NewDestinationPath();
		File.WriteAllText(Path.Combine(templateDir, "notes.md"), "the mywpsite project, see mywpsite_net");

		new TemplateManager(CreatePlan(templateDir, destDir), new FakeDapsYamlEditor()).CreateProjectFromTemplate();

		Assert.Equal("the mysite project, see mysite_net", File.ReadAllText(Path.Combine(destDir, "notes.md")));
	}

	[Fact]
	public void CreateProjectFromTemplate_ReplacesPlaceholderInFilename()
	{
		var templateDir = TestHelper.CreateTempDirectory();
		var destDir = NewDestinationPath();
		var dockerDir = Path.Combine(templateDir, "_docker");
		Directory.CreateDirectory(dockerDir);
		File.WriteAllText(Path.Combine(dockerDir, "compose_mywpsite.dev.yaml"), "name: mywpsite\n");

		new TemplateManager(CreatePlan(templateDir, destDir), new FakeDapsYamlEditor()).CreateProjectFromTemplate();

		Assert.True(File.Exists(Path.Combine(destDir, "_docker", "compose_mysite.dev.yaml")));
		Assert.False(File.Exists(Path.Combine(destDir, "_docker", "compose_mywpsite.dev.yaml")));
	}

	[Fact]
	public void CreateProjectFromTemplate_LeavesContentsOfExcludedDirectoriesUntouched()
	{
		var templateDir = TestHelper.CreateTempDirectory();
		var destDir = NewDestinationPath();
		var uploads = Path.Combine(templateDir, "wp-content", "uploads");
		Directory.CreateDirectory(uploads);
		File.WriteAllText(Path.Combine(uploads, "caption.txt"), "a photo of mywpsite");

		var plan = CreatePlan(templateDir, destDir, excluded: ["wp-content/uploads"]);
		new TemplateManager(plan, new FakeDapsYamlEditor()).CreateProjectFromTemplate();

		var copied = Path.Combine(destDir, "wp-content", "uploads", "caption.txt");
		Assert.True(File.Exists(copied));
		Assert.Equal("a photo of mywpsite", File.ReadAllText(copied));
	}

	[Fact]
	public void CreateProjectFromTemplate_CopiesScriptsVerbatimWithoutTemplateYamlSayingSo()
	{
		var templateDir = TestHelper.CreateTempDirectory();
		var destDir = NewDestinationPath();
		var scriptsDir = Path.Combine(templateDir, "_scripts");
		var dockerDir = Path.Combine(templateDir, "_docker");
		Directory.CreateDirectory(scriptsDir);
		Directory.CreateDirectory(dockerDir);
		File.WriteAllText(Path.Combine(scriptsDir, "prerequisites.dev.sh"), "SECRETS=/srv/projects/mywpsite\n");
		File.WriteAllText(Path.Combine(dockerDir, "compose.yaml"), "image: mywpsite-wordpress\n");

		// Note: no excluded directories are configured -- excluding _scripts is a framework convention.
		new TemplateManager(CreatePlan(templateDir, destDir), new FakeDapsYamlEditor()).CreateProjectFromTemplate();

		Assert.Equal(
			"SECRETS=/srv/projects/mywpsite\n",
			File.ReadAllText(Path.Combine(destDir, "_scripts", "prerequisites.dev.sh")));
		Assert.Equal(
			"image: mysite-wordpress\n",
			File.ReadAllText(Path.Combine(destDir, "_docker", "compose.yaml")));
	}

	[Fact]
	public void CreateProjectFromTemplate_CopiesBinaryFilesByteForByte()
	{
		var templateDir = TestHelper.CreateTempDirectory();
		var destDir = NewDestinationPath();
		// A NUL byte marks the file as binary; the CRLF pair would be rewritten to LF if it were
		// treated as text, and the placeholder bytes would be substituted.
		var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x00, 0x0D, 0x0A, 0x6D, 0x79, 0x77, 0x70, 0x73, 0x69, 0x74, 0x65 };
		File.WriteAllBytes(Path.Combine(templateDir, "logo.png"), bytes);

		new TemplateManager(CreatePlan(templateDir, destDir), new FakeDapsYamlEditor()).CreateProjectFromTemplate();

		Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(destDir, "logo.png")));
	}

	[Fact]
	public void CreateProjectFromTemplate_ContinuesCopyingAfterABinaryFile()
	{
		var templateDir = TestHelper.CreateTempDirectory();
		var destDir = NewDestinationPath();
		// "a" sorts before "z", so the binary file is enumerated first.
		File.WriteAllBytes(Path.Combine(templateDir, "a-logo.png"), new byte[] { 0x89, 0x00, 0x0A });
		File.WriteAllText(Path.Combine(templateDir, "z-notes.md"), "mywpsite");

		new TemplateManager(CreatePlan(templateDir, destDir), new FakeDapsYamlEditor()).CreateProjectFromTemplate();

		Assert.True(File.Exists(Path.Combine(destDir, "z-notes.md")));
	}

	[Fact]
	public void CreateProjectFromTemplate_InOverlayMode_LeavesPreExistingFilesUntouched()
	{
		var templateDir = TestHelper.CreateTempDirectory();
		var destDir = TestHelper.CreateTempDirectory();
		File.WriteAllText(Path.Combine(templateDir, "README.md"), "template says mywpsite");
		File.WriteAllText(Path.Combine(templateDir, "new.md"), "template says mywpsite");
		File.WriteAllText(Path.Combine(destDir, "README.md"), "the user wrote this");

		var plan = CreatePlan(templateDir, destDir, overlay: true);
		new TemplateManager(plan, new FakeDapsYamlEditor()).CreateProjectFromTemplate();

		Assert.Equal("the user wrote this", File.ReadAllText(Path.Combine(destDir, "README.md")));
		Assert.Equal("template says mysite", File.ReadAllText(Path.Combine(destDir, "new.md")));
	}

	private static string NewDestinationPath()
		=> Path.Combine(Path.GetTempPath(), "dapsman-tests", Guid.NewGuid().ToString("N"));

	private static InitPlan CreatePlan(
		string templateDir,
		string destinationDir,
		bool overlay = false,
		IReadOnlyList<string>? excluded = null)
		=> new()
		{
			TemplateName = "wordpress",
			TemplatePath = templateDir,
			ProjectName = "mysite",
			DestinationPath = destinationDir,
			DapsYamlPath = Path.Combine(templateDir, "daps.yaml"),
			DapsYamlProjectRelativePath = "../mysite",
			PlaceholderText = "mywpsite",
			Overlay = overlay,
			ExcludedFromTemplateTransform = excluded ?? [],
		};
}
