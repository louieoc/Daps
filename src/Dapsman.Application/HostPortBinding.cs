namespace Dapsman.Application;

public sealed record HostPortBinding(
	string ProjectName,
	string FilePath,
	int HostPort,
	int ContainerPort);
