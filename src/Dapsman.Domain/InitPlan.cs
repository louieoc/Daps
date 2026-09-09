namespace Dapsman.Domain;

public sealed class InitPlan
{
	public required string TemplateName { get; init; }
	public required string TemplatePath { get; init; }
	public required string ProjectName { get; init; }
	public required string DestinationPath { get; init; }
	public required string DapsYamlPath { get; init; }
	public required string DapsYamlProjectRelativePath { get; init; }
	public string? ProdUrl { get; init; }
	public bool Overlay { get; init; }
	public IReadOnlyList<PortAssignment> DevPortAssignments { get; init; } = [];
	public IReadOnlyList<string> ExcludedFromTemplateTransform { get; init; } = [];
	public string? PlaceholderText { get; init; }
}
