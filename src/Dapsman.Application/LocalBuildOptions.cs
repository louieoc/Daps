namespace Dapsman.Application;

public sealed class LocalBuildOptions
{
    public bool BuildImages { get; init; }

    /// <summary>
    /// Removes each selected project's containers and named volumes (`docker compose down -v`)
    /// before bringing them back up. Destroys project data — recovers a project whose volumes
    /// are corrupt, e.g. a MySQL data directory left half-initialized by an interrupted build.
    /// </summary>
    public bool Rebuild { get; init; }

    public bool DryRun { get; init; }
    public IReadOnlyList<string> ProjectFilters { get; init; } = Array.Empty<string>();
}
