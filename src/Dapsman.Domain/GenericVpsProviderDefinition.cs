namespace Dapsman.Domain;

public sealed class GenericVpsProviderDefinition : ProviderDefinition
{
	public required string Hostname { get; init; }
	public required string User { get; init; }
}
