namespace Dapsman.Domain;

public class ToolkitDefinition
{
	public required string ContainerName { get; init; }
	public required string SshPath { get; init; }

	/// <summary>
	/// False when no toolkit container is currently running; ContainerName is then the
	/// conventional name the container will have once it is built.
	/// </summary>
	public bool IsRunning { get; init; }
}
