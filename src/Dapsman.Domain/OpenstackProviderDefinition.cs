namespace Dapsman.Domain;

public sealed class OpenstackProviderDefinition : ProviderDefinition
{
	public required string OpenRcPath { get; init; }
}
