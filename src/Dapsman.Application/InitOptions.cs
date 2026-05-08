namespace Dapsman.Application;

public sealed class InitOptions
{
    public required string TemplateName { get; init; }
    public required string ProjectName { get; init; }
    public string? DestinationPath { get; init; }
    public bool DryRun { get; init; }
}
