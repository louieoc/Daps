namespace Dapsman.Domain;

public class HostingProvider
{
	public required ProviderDefinition ConfigDefinition { get; init; }
	public required ProviderOptions Options { get; init; }
	public required string RemoteHost { get; init; }
	public required string RemoteUser { get; init; }
	public required string KeyName { get; init; }
}