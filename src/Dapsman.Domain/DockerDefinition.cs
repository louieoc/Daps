namespace Dapsman.Domain;

public sealed class DockerDefinition : Resolved
{
	public IReadOnlyList<string> LocalDapsComposeFilePaths { get; set; } = [];
	public IReadOnlyList<string> RemoteDapsComposeFilePaths { get; set; } = [];
}
