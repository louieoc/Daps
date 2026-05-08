namespace Dapsman.Application;

public sealed class LocalBuildOptions
{
    public bool BuildImages { get; init; }
    public bool DryRun { get; init; }
    public IReadOnlyList<string> ProjectFilters { get; init; } = Array.Empty<string>();
}
