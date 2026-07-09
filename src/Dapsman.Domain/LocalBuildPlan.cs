namespace Dapsman.Domain;

public sealed class LocalBuildPlan
{
	public required LocalCaddySyncPlan CaddySync { get; init; }
	public required IReadOnlyList<string> DapsComposeFiles { get; init; }
	public required IReadOnlyList<LocalProjectComposePlan> ProjectComposePlans { get; init; }
	public required IReadOnlyList<string> Warnings { get; init; }
	public required bool HasProjectsConfigured { get; init; }
}
