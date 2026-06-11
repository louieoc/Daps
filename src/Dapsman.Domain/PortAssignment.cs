namespace Dapsman.Domain;

public sealed record PortAssignment(int OriginalPort, int AssignedPort, string Reason);
