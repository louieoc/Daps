namespace Dapsman.Domain;

public sealed class CaddySyncPlan
{
    public required string RuntimeSitesPath { get; init; }
    public required IReadOnlyList<CaddySiteCopyPlan> FilesToCopy { get; init; }
    public required bool ShouldCreatePlaceholder { get; init; }
    public required string PlaceholderFilePath { get; init; }
}

public sealed class CaddySiteCopyPlan
{
    public required string ProjectName { get; init; }
    public required string SourcePath { get; init; }
    public required string DestinationPath { get; init; }
}
