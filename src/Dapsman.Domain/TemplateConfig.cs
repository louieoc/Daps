namespace Dapsman.Domain;

public sealed class TemplateConfig
{
	public string? PlaceholderText { get; init; }
	public string[] ExcludedFromTransformation { get; init; } = [];
}