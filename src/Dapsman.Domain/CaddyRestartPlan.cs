namespace Dapsman.Domain;

public sealed class CaddyRestartPlan
{
	public required string ReloadCommand { get; init; }
	public required string WorkingDirectory { get; init; }

	/// <summary>
	/// For remote plans: the toolkit container to run the command in.
	/// Null for local plans (command runs on the workstation).
	/// TODO: remove this?
	/// </summary>
	public string? ToolkitContainerName { get; init; }
}
