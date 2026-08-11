namespace Dapsman.Infrastructure.Tests;

public sealed class ConfigUtilsCopyFileTests
{
	[Fact]
	public void CopyFileAndReplaceLineEndingsForLinux_RewritesCrlfAsLf()
	{
		var dir = CreateTempDirectory();
		var source = Path.Combine(dir, "prerequisites.prod.sh");
		var destination = Path.Combine(dir, "staged", "prerequisites.prod.sh");

		// What a Windows checkout can produce, and what fails on Linux with
		// "set: pipefail: invalid option name".
		File.WriteAllText(source, "#!/usr/bin/env bash\r\nset -euo pipefail\r\necho hi\r\n");

		ConfigUtils.CopyFileAndReplaceLineEndingsForLinux(source, destination);

		var copied = File.ReadAllText(destination);
		Assert.DoesNotContain("\r", copied, StringComparison.Ordinal);
		Assert.Equal("#!/usr/bin/env bash\nset -euo pipefail\necho hi\n", copied);
	}

	[Fact]
	public void CopyFileAndReplaceLineEndingsForLinux_LeavesLfFileUnchanged()
	{
		var dir = CreateTempDirectory();
		var source = Path.Combine(dir, "generate-secrets.sh");
		var destination = Path.Combine(dir, "staged", "generate-secrets.sh");
		const string content = "#!/usr/bin/env bash\nset -euo pipefail\n";
		File.WriteAllText(source, content);

		ConfigUtils.CopyFileAndReplaceLineEndingsForLinux(source, destination);

		Assert.Equal(content, File.ReadAllText(destination));
	}

	[Fact]
	public void CopyFileAndReplaceLineEndingsForLinux_CreatesDestinationDirectory()
	{
		var dir = CreateTempDirectory();
		var source = Path.Combine(dir, "script.sh");
		var destination = Path.Combine(dir, "a", "b", "script.sh");
		File.WriteAllText(source, "echo hi\n");

		ConfigUtils.CopyFileAndReplaceLineEndingsForLinux(source, destination);

		Assert.True(File.Exists(destination));
	}

	private static string CreateTempDirectory()
	{
		var path = Path.Combine(Path.GetTempPath(), "dapsman-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(path);
		return path;
	}
}
