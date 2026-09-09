namespace Dapsman.Infrastructure.Tests;

public sealed class TestHelper
{
    public static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "dapsman-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}