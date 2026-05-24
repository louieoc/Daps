namespace Dapsman.Domain;

public sealed class UnprovisionPlan
{
	public required string DapsRootPath { get; init; }
	public required string ToolkitContainerName { get; init; }

	/// <summary>Path to the OpenRC script inside the toolkit container.</summary>
	public required string OpenRcContainerPath { get; init; }

	/// <summary>Path to the OpenStack instance vars script inside the toolkit container.</summary>
	public required string InstanceVarsContainerPath { get; init; }

	/// <summary>Default OpenStack instance name, overridable by OS_INSTANCE_NAME in instance vars.</summary>
	public required string DefaultInstanceName { get; init; }

	/// <summary>Default SSH key name, overridable by OS_KEY_NAME in instance vars.</summary>
	public required string DefaultKeyName { get; init; }

	/// <summary>Absolute path to the SSH key inside the toolkit, e.g. /root/.ssh/daps-key-ramnode.</summary>
	public required string SshKeyToolkitPath { get; init; }

	/// <summary>Workstation path to the instance vars file to delete after unprovision.</summary>
	public required string InstanceVarsFilePath { get; init; }
}
